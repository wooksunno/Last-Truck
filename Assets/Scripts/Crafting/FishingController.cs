using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 낚시. 낚싯대를 손에 들고(1/2/3/4 슬롯) 강·호수·바다를 바라보며 왼쪽 클릭하면 찌를 던진다.
    ///  1) 찌가 물에 떠서 기다린다 (몇 초).
    ///  2) 입질: 찌가 쑥 잠기고 낚싯대가 부르르 떨린다. 이때 왼쪽 클릭하면 미니게임 시작 (놓치면 다시 기다린다).
    ///  3) 미니게임(LastTruck.FishingMinigameView): 왼쪽 클릭을 누르고 있으면 초록 막대가 올라가고, 떼면 내려온다.
    ///     물고기를 막대 안에 두면 오른쪽 게이지가 차고, 벗어나면 줄어든다. 가득 차면 낚고, 바닥나면 놓친다.
    ///  - 오른쪽 클릭 / 다른 아이템 들기 / 멀리 걸어가면 줄을 감아 그만둔다.
    ///  - 보이기만 하는 연출(낚싯대/찌/줄)은 내 화면에만 있다. 물고기는 내 인벤토리에 들어온다.
    /// </summary>
    public class FishingController : MonoBehaviour
    {
        private enum State { Idle, Casting, Waiting, Bite, Minigame }

        [Header("던지기")]
        [SerializeField] private float minCastDistance = 2.5f;
        [SerializeField] private float maxCastDistance = 9f;
        [SerializeField] private float castSeconds = 0.55f;
        [Tooltip("던진 자리에서 이만큼(m) 멀어지면 줄을 감는다.")]
        [SerializeField] private float leaveDistance = 2.5f;

        [Header("입질")]
        [SerializeField] private float minWaitSeconds = 3f;
        [SerializeField] private float maxWaitSeconds = 9f;
        [Tooltip("입질이 온 뒤 클릭할 수 있는 시간(초).")]
        [SerializeField] private float biteWindowSeconds = 1.1f;

        [Header("대물")]
        [Range(0f, 1f)] [SerializeField] private float bigFishChance = 0.15f;

        private PlayerInventory _inventory;
        private LastTruck.PlayerMove _move;
        private Camera _camera;

        private State _state = State.Idle;
        private Transform _rod, _rodTip, _bobber;
        private LineRenderer _line;
        private Vector3 _castOrigin, _bobberRest;
        private float _timer;
        private float _rodShake;
        private bool _bigFish;
        private float _difficulty;
        private LastTruck.FishingMinigameView _minigame;

        private static readonly HashSet<Collider> WaterColliders = new HashSet<Collider>();
        private static bool _waterScanned;

        public bool IsFishing => _state != State.Idle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            WaterColliders.Clear();
            _waterScanned = false;
        }

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _move = GetComponent<LastTruck.PlayerMove>();
        }

        private void OnDisable()
        {
            StopFishing(false);
            ShowRod(false);
        }

        // ------------------------------------------------------------------ 물 찾기

        /// <summary>물 표면 콜라이더를 한 번 모은다 (이름/재질에 Water가 들어간 것). 콜라이더가 없는 물에는 얇은 트리거를 붙인다.</summary>
        private static void ScanWater()
        {
            if (_waterScanned) return;
            _waterScanned = true;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer) continue;
                string n = r.name;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                bool water = n.Contains("Water") || mat.Contains("Water");
                if (!water) continue;
                Collider c = r.GetComponent<Collider>();
                if (c == null)
                {
                    var box = r.gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    Vector3 size = r.transform.InverseTransformVector(r.bounds.size);
                    box.size = new Vector3(Mathf.Abs(size.x), Mathf.Max(0.05f, Mathf.Abs(size.y)), Mathf.Abs(size.z));
                    box.center = r.transform.InverseTransformPoint(r.bounds.center);
                    c = box;
                }
                WaterColliders.Add(c);
            }
        }

        /// <summary>xz 위치의 맨 위 표면이 물이면 그 점을 돌려준다.</summary>
        private bool WaterAt(Vector3 xz, float refY, out Vector3 point)
        {
            point = default;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(xz.x, refY + 15f, xz.z), Vector3.down, 45f, ~0, QueryTriggerInteraction.Collide);
            float solid = float.MaxValue, water = float.MaxValue;
            Vector3 waterPoint = default;
            foreach (RaycastHit h in hits)
            {
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                if (WaterColliders.Contains(h.collider))
                {
                    if (h.distance < water) { water = h.distance; waterPoint = h.point; }
                }
                else if (!h.collider.isTrigger && h.distance < solid)
                {
                    solid = h.distance;
                }
            }
            if (water == float.MaxValue || water > solid + 0.05f) return false;   // 물이 맨 위 표면이어야 한다 (물 아래 땅이 살짝 높아도 땅으로 본다)
            point = waterPoint;
            return true;
        }

        private bool FindCastPoint(out Vector3 point)
        {
            ScanWater();
            Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            for (float d = minCastDistance; d <= maxCastDistance; d += 0.5f)
                if (WaterAt(transform.position + fwd * d, transform.position.y, out point)) return true;
            point = default;
            return false;
        }

        // ------------------------------------------------------------------ 갱신

        private bool HoldingRod
        {
            get
            {
                InventorySlot hand = _inventory != null ? _inventory.SelectedSlot : null;
                return hand != null && !hand.IsEmpty && hand.item != null && hand.item.itemID == ItemIds.FishingRod;
            }
        }

        private void Update()
        {
            bool holding = HoldingRod;
            ShowRod(holding);
            if (!holding)
            {
                if (_state != State.Idle) StopFishing(false);
                return;
            }

            bool popup = GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;
            if (popup && _state != State.Idle) { StopFishing(false); return; }

            switch (_state)
            {
                case State.Idle:
                    if (!popup && Input.GetMouseButtonDown(0) && !PointerOverUi()) TryCast();
                    break;

                case State.Waiting:
                    if (CancelRequested()) { StopFishing(false); break; }
                    _timer -= Time.deltaTime;
                    FloatBobber(0.03f);
                    if (_timer <= 0f) StartBite();
                    break;

                case State.Bite:
                    if (CancelRequested()) { StopFishing(false); break; }
                    _timer -= Time.deltaTime;
                    _rodShake = 1f;
                    // 찌가 쑥쑥 잠긴다
                    if (_bobber != null)
                        _bobber.position = _bobberRest + Vector3.down * (0.12f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 18f)));
                    if (Input.GetMouseButtonDown(0) && !PointerOverUi())
                    {
                        StartMinigame();
                        break;
                    }
                    if (_timer <= 0f)
                    {
                        LastTruck.HudToast.Show("물고기가 미끼만 먹고 도망갔어요");
                        _rodShake = 0f;
                        BeginWaiting();
                    }
                    break;

                case State.Minigame:
                    FloatBobber(0.06f);
                    _rodShake = 0.35f;
                    if (Input.GetMouseButtonDown(1)) { StopFishing(false); break; }
                    if (_minigame != null)
                    {
                        _minigame.Tick(Time.deltaTime, Input.GetMouseButton(0));
                        if (_minigame.Result != 0) FinishMinigame(_minigame.Result > 0);
                    }
                    break;
            }
        }

        private void LateUpdate()
        {
            // 낚싯대 떨림
            if (_rod != null)
            {
                _rodShake = Mathf.MoveTowards(_rodShake, 0f, Time.deltaTime * 2f);
                float wobble = _rodShake * 9f * Mathf.Sin(Time.time * 42f);
                // 손 뼈 방향과 상관없이 캐릭터 앞쪽 위로 비스듬히 든다 (애니메이션 뒤에 맞춘다)
                Vector3 dir = (transform.forward * 0.62f + Vector3.up * 0.78f + transform.right * 0.08f).normalized;
                _rod.rotation = Quaternion.AngleAxis(wobble, transform.right) * Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }
            UpdateLine();
        }

        private bool CancelRequested()
        {
            if (Input.GetMouseButtonDown(1)) return true;
            Vector3 d = transform.position - _castOrigin; d.y = 0f;
            return d.magnitude > leaveDistance;
        }

        private static bool PointerOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------ 단계

        private void TryCast()
        {
            if (!FindCastPoint(out Vector3 target))
            {
                LastTruck.HudToast.Show("물(강·호수·바다)을 바라보고 던져 주세요");
                return;
            }
            _castOrigin = transform.position;
            StartCoroutine(CastRoutine(target));
        }

        private IEnumerator CastRoutine(Vector3 target)
        {
            _state = State.Casting;
            EnsureBobber();
            Vector3 from = _rodTip != null ? _rodTip.position : transform.position + Vector3.up * 1.5f;
            _bobberRest = target + Vector3.up * 0.04f;
            float t = 0f;
            while (t < castSeconds)
            {
                if (!HoldingRod || _state != State.Casting) yield break;
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / castSeconds);
                Vector3 p = Vector3.Lerp(from, _bobberRest, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 2.2f);
                _bobber.position = p;
                yield return null;
            }
            _bobber.position = _bobberRest;
            SpawnSplash(_bobberRest, 0.5f);
            BeginWaiting();
        }

        private void BeginWaiting()
        {
            _state = State.Waiting;
            _timer = Random.Range(minWaitSeconds, maxWaitSeconds);
        }

        private void StartBite()
        {
            _state = State.Bite;
            _timer = biteWindowSeconds;
            _bigFish = Random.value < bigFishChance;
            _difficulty = _bigFish ? Random.Range(0.65f, 0.9f) : Random.Range(0.25f, 0.6f);
            SpawnSplash(_bobberRest, 0.35f);
        }

        private void StartMinigame()
        {
            if (_minigame == null) _minigame = LastTruck.FishingMinigameView.Create();
            if (_minigame == null)
            {
                // 화면을 만들 수 없으면 그냥 낚은 것으로 친다
                FinishMinigame(true);
                return;
            }
            ItemData fish = ItemCatalog.GetOrCreate().GetItem(ItemIds.Fish);
            _minigame.Begin(_difficulty, fish != null ? fish.icon : null, _bigFish);
            _state = State.Minigame;
            if (_move != null) _move.SetMovementLocked(true);
        }

        private void FinishMinigame(bool caught)
        {
            if (_minigame != null) _minigame.End();
            if (_move != null) _move.SetMovementLocked(false);

            if (caught)
            {
                ItemData fish = ItemCatalog.GetOrCreate().GetItem(ItemIds.Fish);
                int n = _bigFish ? 2 : 1;
                if (fish != null && _inventory != null)
                    _inventory.AddOrDrop(fish, n);
                LastTruck.HudToast.Show(_bigFish ? "대물이다! 물고기 ×2" : "물고기를 낚았어요!");
            }
            else
            {
                LastTruck.HudToast.Show("물고기를 놓쳤어요...");
            }

            // 한 번 끌어올릴 때마다 낚싯대가 조금 닳는다
            if (_inventory != null) _inventory.WearSelectedTool();
            StopFishing(true);
        }

        private void StopFishing(bool reeledIn)
        {
            StopAllCoroutines();
            if (_state == State.Minigame)
            {
                if (_minigame != null) _minigame.End();
                if (_move != null) _move.SetMovementLocked(false);
            }
            _state = State.Idle;
            _rodShake = 0f;
            if (_bobber != null) _bobber.gameObject.SetActive(false);
            if (_line != null) _line.enabled = false;
        }

        // ------------------------------------------------------------------ 연출 (낚싯대 / 찌 / 줄)

        private static readonly Quaternion RodBaseRotation = Quaternion.Euler(-35f, 0f, 0f);

        private void ShowRod(bool show)
        {
            if (show && _rod == null) BuildRod();
            if (_rod != null && _rod.gameObject.activeSelf != show) _rod.gameObject.SetActive(show);
        }

        private Transform FindHandSlot()
        {
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    Transform slot = hand.Find("handslot.r");
                    return slot != null ? slot : hand;
                }
            }
            foreach (Transform t in GetComponentsInChildren<Transform>())
                if (t.name == "handslot.r") return t;
            return null;
        }

        private void BuildRod()
        {
            Transform slot = FindHandSlot();
            var root = new GameObject("FishingRod").transform;
            if (slot != null) root.SetParent(slot, false);
            else { root.SetParent(transform, false); root.localPosition = new Vector3(0.35f, 1.1f, 0.3f); }
            root.localRotation = RodBaseRotation;
            _rod = root;

            Material wood = MakeMat(new Color(0.62f, 0.40f, 0.20f));
            Material grip = MakeMat(new Color(0.30f, 0.19f, 0.10f));
            Material metal = MakeMat(new Color(0.62f, 0.64f, 0.70f));

            // 손잡이 → 대 (위로 길게, 끝으로 갈수록 가늘게)
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.16f, 0.05f), grip);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.55f, 0f), new Vector3(0.032f, 0.36f, 0.032f), wood);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.15f, 0f), new Vector3(0.02f, 0.26f, 0.02f), wood);
            Part(PrimitiveType.Cylinder, root, new Vector3(0.045f, 0.24f, 0f), new Vector3(0.07f, 0.02f, 0.07f), metal).localRotation = Quaternion.Euler(0f, 0f, 90f);

            _rodTip = new GameObject("Tip").transform;
            _rodTip.SetParent(root, false);
            _rodTip.localPosition = new Vector3(0f, 1.42f, 0f);
        }

        private void EnsureBobber()
        {
            if (_bobber == null)
            {
                var b = new GameObject("FishingBobber").transform;
                _bobber = b;
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.05f, 0f), Vector3.one * 0.16f, MakeMat(new Color(0.92f, 0.22f, 0.18f)));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, -0.02f, 0f), Vector3.one * 0.15f, MakeMat(new Color(0.97f, 0.97f, 0.95f)));
                Part(PrimitiveType.Cylinder, b, new Vector3(0f, 0.15f, 0f), new Vector3(0.025f, 0.06f, 0.025f), MakeMat(new Color(0.2f, 0.15f, 0.1f)));

                var lineGo = new GameObject("FishingLine");
                _line = lineGo.AddComponent<LineRenderer>();
                _line.positionCount = 14;
                _line.widthMultiplier = 0.018f;
                _line.numCapVertices = 2;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.sharedMaterial = MakeMat(new Color(0.95f, 0.95f, 0.98f));
            }
            _bobber.gameObject.SetActive(true);
            _line.enabled = true;
        }

        private void FloatBobber(float amount)
        {
            if (_bobber == null) return;
            _bobber.position = _bobberRest + Vector3.up * (Mathf.Sin(Time.time * 2.4f) * amount);
        }

        private void UpdateLine()
        {
            if (_line == null || !_line.enabled || _bobber == null || _rodTip == null) return;
            Vector3 a = _rodTip.position, b = _bobber.position + Vector3.up * 0.18f;
            float sag = _state == State.Minigame || _state == State.Bite ? 0.1f : 0.6f;   // 물고기가 물면 줄이 팽팽해진다
            for (int i = 0; i < _line.positionCount; i++)
            {
                float t = i / (float)(_line.positionCount - 1);
                _line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t)));
            }
        }

        private static void SpawnSplash(Vector3 at, float size)
        {
            var go = new GameObject("FishingSplash");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f; main.loop = false;
            main.startLifetime = 0.55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * size * 2f, 2.6f * size * 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.9f);
            main.gravityModifier = 1.2f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(18 * size + 6)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MakeMat(new Color(0.85f, 0.95f, 1f, 0.9f), true);
            ps.Play();
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        private static readonly Dictionary<Color, Material> Mats = new Dictionary<Color, Material>();

        private static Material MakeMat(Color c, bool particle = false)
        {
            if (!particle && Mats.TryGetValue(c, out Material m) && m != null) return m;
            Shader sh = particle ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (!particle) Mats[c] = m;
            return m;
        }
    }
}
