using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 지붕의 미니 포탑 (기획서 6-1).
    ///  - 미니 포탑 파츠를 끼우면 지붕에 포탑 모델이 생긴다.
    ///  - 사수(멀티: 동승석 1번 / 싱글: 트럭 안에서 T로 전환)가 마우스로 조준하고 좌클릭(누르고 있기)으로 쏜다. R = 탄환 만들기.
    ///  - 탄환은 트럭 보관함에서 좋은 것부터 쓴다. 피해 = 포탑 공격력 + 탄환 데미지, 맞은 곳 주변(피해 범위)에 모두 들어간다.
    ///  - 트럭 내구도 65% 미만이면 공격 속도 -30% (기획서: 트럭 손상 패널티).
    /// 판정(탄환 소모/피해)은 싱글 또는 호스트에서만 한다. 멀티 클라이언트는 NetworkTruck을 통해 호스트에 요청한다.
    /// TruckInformation이 자동으로 붙인다.
    /// </summary>
    public class TruckTurret : MonoBehaviour
    {
        public const float DamagedRatePenalty = 0.7f;
        public const float DamagedThreshold = 0.65f;

        private TruckUpgradeState _state;
        private LastTruck.TruckInformation _info;
        private TruckInventory _inventory;

        private Transform _root, _yawPivot, _muzzle;
        private Material _tracerMat;
        private float _yaw;                 // 월드 기준 포탑 방향
        private float _pitch;               // 위아래 (아래가 +, 1인칭 조준)
        private float _lastHostShot = -10f;
        private float _nextLocalShot;
        private float _nextAimSend;
        private Vector3 _lastAim;

        /// <summary>이 컴퓨터의 플레이어가 지금 포탑을 잡고 있는가 (안내 표시용).</summary>
        public bool LocalGunning { get; private set; }

        public static TruckTurret Instance { get; private set; }

        public Vector3 MuzzlePosition => _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 3f;

        private void Awake()
        {
            Instance = this;
            _state = GetComponent<TruckUpgradeState>();
            _info = GetComponent<LastTruck.TruckInformation>();
            _yaw = transform.eulerAngles.y;
        }

        private void OnEnable() { if (_state != null) _state.Changed += Refresh; }
        private void OnDisable()
        {
            if (_state != null) _state.Changed -= Refresh;
            ExitFirstPerson();
        }

        private void OnDestroy()
        {
            ExitFirstPerson();
            if (_mortarPreview != null) Destroy(_mortarPreview.gameObject);
            if (Instance == this) Instance = null;
        }

        private void Start() => Refresh();

        private TruckInventory Inventory
        {
            get
            {
                if (_inventory == null) _inventory = GetComponentInChildren<TruckInventory>();
                if (_inventory == null) _inventory = FindFirstObjectByType<TruckInventory>();
                return _inventory;
            }
        }

        private bool Online => LastTruck.Networking.GameLauncher.IsOnlineSession;

        private bool HasAuthority
        {
            get
            {
                if (!Online) return true;
                var net = LastTruck.Networking.NetworkTruck.Instance;
                return net != null && net.Object != null && net.Object.IsValid && net.HasStateAuthority;
            }
        }

        // ───── 모델 ─────

        private void Refresh()
        {
            bool has = _state != null && _state.HasTurret;
            string id = has ? _state.PartAt(TruckPartSlot.Roof) : null;

            // 다른 종류의 포탑으로 갈아 끼웠으면 모델을 새로 만든다
            if (has && _root != null && id != _builtId)
            {
                Destroy(_root.gameObject);
                _root = null; _yawPivot = null; _muzzle = null;
            }
            if (has && _root == null) Build(id);
            if (_root != null) _root.gameObject.SetActive(has);
            if (!has) LocalGunning = false;
        }

        private string _builtId;

        private void Build(string turretId)
        {
            _builtId = turretId;
            // 트럭의 "Mount_Roof" 자리(운전석 지붕)에 올린다. 자리가 없는 트럭이면 몸체 위쪽 가운데.
            Vector3 top;
            Transform mount = transform.Find("Mount_Roof");
            if (mount != null) top = mount.position;
            else
            {
                Bounds b = new Bounds(transform.position, Vector3.zero); bool any = false;
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                top = any ? new Vector3(b.center.x, b.max.y, b.center.z) : transform.position + Vector3.up * 2.5f;
            }

            _root = new GameObject("TruckTurret_Model").transform;
            _root.SetParent(transform, false);
            _root.position = top;
            _root.localRotation = Quaternion.identity;
            BuildModel(turretId, _root, out _yawPivot, out _muzzle);
        }

        /// <summary>
        /// 포탑 모델을 만든다: root 아래에 받침대 + 회전하는 포신(yawPivot) + 총구(muzzle). 자동 포탑(TruckAutoTurret)도 같은 모델을 쓴다.
        /// turretId는 수동 포탑 id (자동은 "auto_"를 뗀 id를 넘긴다).
        /// </summary>
        public static void BuildModel(string turretId, Transform _root, out Transform _yawPivot, out Transform _muzzle)
        {
            var dark = MakeMat(new Color(0.22f, 0.24f, 0.26f));
            var olive = MakeMat(new Color(0.36f, 0.42f, 0.28f));

            Prim(PrimitiveType.Cylinder, _root, new Vector3(0f, 0.12f, 0f), new Vector3(1.1f, 0.12f, 1.1f), dark);
            _yawPivot = new GameObject("Yaw").transform;
            _yawPivot.SetParent(_root, false);
            _yawPivot.localPosition = new Vector3(0f, 0.3f, 0f);
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_yawPivot, false);

            Vector3 muzzle = new Vector3(0f, 0.3f, 1.55f);
            switch (turretId)
            {
                case ItemIds.GatlingTurret:
                {
                    // 넓은 몸통 + 굵은 총신 묶음 3개
                    var steel = MakeMat(new Color(0.55f, 0.58f, 0.62f));
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.5f, 1.0f), olive);
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.55f, -0.1f), new Vector3(0.5f, 0.25f, 0.6f), dark);   // 탄창
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.3f, 0.75f), new Vector3(0.42f, 0.2f, 0.42f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.PI * 2f / 3f;
                        Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(Mathf.Cos(a) * 0.13f, 0.3f + Mathf.Sin(a) * 0.13f, 1.2f),
                             new Vector3(0.1f, 0.55f, 0.1f), steel).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                    muzzle = new Vector3(0f, 0.3f, 1.8f);
                    break;
                }
                case ItemIds.SniperTurret:
                {
                    // 길고 가는 총신 + 위에 얹은 조준경
                    var steel = MakeMat(new Color(0.30f, 0.34f, 0.40f));
                    var lens = MakeMat(new Color(0.25f, 0.65f, 0.9f));
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.22f, -0.1f), new Vector3(0.6f, 0.44f, 1.0f), steel);
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.3f, 1.35f), new Vector3(0.12f, 0.95f, 0.12f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.58f, 0.35f), new Vector3(0.16f, 0.45f, 0.16f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Sphere, _yawPivot, new Vector3(0f, 0.58f, 0.8f), new Vector3(0.17f, 0.17f, 0.12f), lens);
                    muzzle = new Vector3(0f, 0.3f, 2.35f);
                    break;
                }
                case ItemIds.MortarTurret:
                {
                    // 굵고 짧은 관을 위로 세운 박격포
                    var steel = MakeMat(new Color(0.40f, 0.40f, 0.34f));
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.18f, 0f), new Vector3(1.0f, 0.18f, 1.0f), olive);
                    var tube = Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.8f, 0.3f), new Vector3(0.42f, 0.55f, 0.42f), steel);
                    tube.localRotation = Quaternion.Euler(35f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 1.32f, 0.62f), new Vector3(0.5f, 0.06f, 0.5f), dark).localRotation = Quaternion.Euler(35f, 0f, 0f);
                    muzzle = new Vector3(0f, 1.36f, 0.64f);
                    break;
                }
                case ItemIds.FlameTurret:
                {
                    // 주황 노즐 + 양옆 붉은 연료 탱크
                    var tank = MakeMat(new Color(0.72f, 0.16f, 0.12f));
                    var nozzle = MakeMat(new Color(0.95f, 0.5f, 0.12f));
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.25f, 0.05f), new Vector3(0.8f, 0.5f, 0.9f), olive);
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(-0.5f, 0.3f, -0.2f), new Vector3(0.38f, 0.45f, 0.38f), tank).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0.5f, 0.3f, -0.2f), new Vector3(0.38f, 0.45f, 0.38f), tank).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.3f, 0.85f), new Vector3(0.3f, 0.4f, 0.3f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.3f, 1.3f), new Vector3(0.42f, 0.1f, 0.42f), nozzle).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    muzzle = new Vector3(0f, 0.3f, 1.45f);
                    break;
                }
                case ItemIds.RailTurret:
                {
                    // 길게 뻗은 두 가닥 레일 + 청록 에너지 띠
                    var steel = MakeMat(new Color(0.78f, 0.82f, 0.88f));
                    var glow = MakeUnlit(new Color(0.25f, 0.95f, 1f));
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.22f, -0.1f), new Vector3(0.8f, 0.44f, 0.9f), dark);
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(-0.18f, 0.4f, 1.0f), new Vector3(0.1f, 0.12f, 2.0f), steel);
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0.18f, 0.4f, 1.0f), new Vector3(0.1f, 0.12f, 2.0f), steel);
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.4f, 1.0f), new Vector3(0.12f, 0.06f, 2.0f), glow);
                    Prim(PrimitiveType.Sphere, _yawPivot, new Vector3(0f, 0.62f, 0f), new Vector3(0.3f, 0.3f, 0.3f), glow);
                    muzzle = new Vector3(0f, 0.4f, 2.1f);
                    break;
                }
                default:
                {
                    // 미니 포탑 (기본형)
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 1.0f), olive);
                    Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.25f, -0.55f), new Vector3(0.7f, 0.4f, 0.2f), dark);
                    var barrel = Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.3f, 0.95f), new Vector3(0.18f, 0.55f, 0.18f), dark);
                    barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                }
            }
            _muzzle.localPosition = muzzle;
        }

        private static Material MakeUnlit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        private static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material MakeMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        private void LateUpdate()
        {
            if (_yawPivot == null || !_root.gameObject.activeSelf) return;
            // 내가 쏘는 중이 아니면 네트워크 방향을 따라간다
            if (!LocalGunning && Online && LastTruck.Networking.NetworkTruck.IsReady)
            {
                _yaw = Mathf.MoveTowardsAngle(_yaw, LastTruck.Networking.NetworkTruck.Instance.TurretYaw, 540f * Time.deltaTime);
                _pitch = Mathf.MoveTowards(_pitch, LastTruck.Networking.NetworkTruck.Instance.TurretPitch, 180f * Time.deltaTime);
            }
            _yawPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            if (_fp) UpdateFirstPersonCamera();
        }

        // ───── 사수 (이 컴퓨터) ─────

        /// <summary>사수가 아닐 때 부른다 (안내 숨김, 1인칭 끝).</summary>
        public void StopLocalGunner()
        {
            LocalGunning = false;
            ExitFirstPerson();
            HideMortarPreview();
        }

        /// <summary>사수인 동안 매 프레임 부른다: 마우스 조준, 좌클릭 발사, R 탄환 만들기.</summary>
        public void LocalGunnerUpdate()
        {
            if (_state == null || !_state.HasTurret || _root == null) { StopLocalGunner(); return; }
            LocalGunning = true;
            if (!_fp) EnterFirstPerson();

            // 창(Tab 트럭 창, 시스템 팝업, 휠 메뉴)이 열려 있으면 조준/발사를 멈추고 커서를 풀어 준다
            bool popup = (LastTruck.Networking.SystemUI.Instance != null && LastTruck.Networking.SystemUI.Instance.IsPopupVisible)
                         || (GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen)
                         || LastTruck.TruckRadialMenu.IsOpen;
            if (!LastTruck.TruckRadialMenu.IsOpen) SetCursorLocked(!popup);

            if (!popup)
            {
                // 1인칭 마우스 시점: 포탑이 시선을 따라 돈다
                _yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * MouseSensitivity, MinPitch, CurrentMaxPitch);
                _yawPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                _lastAim = AimPoint();
            }

            // 박격포: 어디에 떨어지는지 점선 포물선 + 범위 원으로 미리 보여 준다
            if (!popup && IsMortar) UpdateMortarPreview(MuzzlePosition, MortarLanding(MuzzlePosition, _lastAim), _state.StatValue(TruckStat.TurretArea));
            else HideMortarPreview();

            if (Online && Time.time >= _nextAimSend)
            {
                _nextAimSend = Time.time + 0.08f;
                LastTruck.Networking.NetworkTruck.SendTurretAim(_yaw, _pitch);
            }

            if (popup) return;

            if (Input.GetMouseButton(0) && Time.time >= _nextLocalShot)
            {
                _nextLocalShot = Time.time + 1f / CurrentRate();
                if (Online && !HasAuthority) LastTruck.Networking.NetworkTruck.SendTurretFire(_lastAim);
                else if (!HostFire(_lastAim, out string msg) && msg != null) LastTruck.HudToast.Show(msg);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (Online && !HasAuthority) LastTruck.Networking.NetworkTruck.SendTurretCraftAmmo();
                else { HostCraftAmmo(out string msg); LastTruck.HudToast.Show(msg); }
            }
        }

        // ───── 박격포 (포물선 + 착탄 범위 피해) ─────

        private bool IsMortar => _state != null && _state.PartAt(TruckPartSlot.Roof) == ItemIds.MortarTurret;

        private const float MortarMinDistance = 5f;
        private const int MortarDots = 26;
        private Transform _mortarPreview;
        private Transform[] _mortarDots;
        private LineRenderer _mortarRing;
        private Material _previewMat;

        /// <summary>
        /// 박격포가 떨어질 땅 위의 점: 조준 방향으로 (최소 5m ~ 사거리) 만큼 간 곳의 지면.
        /// 하늘을 보고 있어도 사거리 끝 땅에 떨어진다.
        /// </summary>
        public Vector3 MortarLanding(Vector3 from, Vector3 aimPoint)
        {
            float range = _state != null ? _state.StatValue(TruckStat.TurretRange) : 30f;
            Vector3 d = aimPoint - from; d.y = 0f;
            float dist = d.magnitude;
            Vector3 dir = dist > 0.01f ? d / dist : Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            dist = Mathf.Clamp(dist, MortarMinDistance, Mathf.Max(MortarMinDistance, range));
            Vector3 p = from + dir * dist;
            p.y = aimPoint.y;

            // 그 위치의 지면을 찾는다 (트럭/플레이어/트리거 제외)
            float bestY = float.MinValue; bool found = false;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, Mathf.Max(from.y, aimPoint.y) + 40f, p.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || IsPlayer(h.collider)) continue;
                if (h.point.y > bestY) { bestY = h.point.y; found = true; }
            }
            if (found) p.y = bestY;
            return p;
        }

        /// <summary>delay초 뒤 at에 박격포 범위 피해 (자동 박격포도 이걸 쓴다).</summary>
        public void StartMortarImpact(Vector3 at, int damage, float area, float delay) =>
            StartCoroutine(MortarImpactRoutine(at, damage, area, delay));

        private System.Collections.IEnumerator MortarImpactRoutine(Vector3 at, int damage, float area, float delay)
        {
            yield return new WaitForSeconds(delay);
            var done = new System.Collections.Generic.HashSet<Combat.Damageable>();
            foreach (var col in Physics.OverlapSphere(at, area, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(transform) || IsPlayer(col)) continue;
                var target = col.GetComponentInParent<Combat.Damageable>();
                if (target == null || !done.Add(target)) continue;
                target.TakeDamage(damage, col.ClosestPoint(at), null);
            }
        }

        private void EnsureMortarPreview()
        {
            if (_mortarPreview != null) return;
            _previewMat = new Material(Shader.Find("Sprites/Default"));
            _mortarPreview = new GameObject("MortarPreview").transform;
            _mortarDots = new Transform[MortarDots];
            for (int i = 0; i < MortarDots; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(_mortarPreview, false);
                var r = g.GetComponent<Renderer>();
                r.sharedMaterial = _previewMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _mortarDots[i] = g.transform;
            }
            var ringGo = new GameObject("LandingRing");
            ringGo.transform.SetParent(_mortarPreview, false);
            _mortarRing = ringGo.AddComponent<LineRenderer>();
            _mortarRing.sharedMaterial = _previewMat;
            _mortarRing.loop = true;
            _mortarRing.positionCount = 48;
            _mortarRing.widthMultiplier = 0.12f;
            _mortarRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mortarRing.startColor = _mortarRing.endColor = new Color(1f, 0.45f, 0.2f, 0.9f);
        }

        /// <summary>점선 포물선(앞으로 흘러가는 점들) + 떨어질 자리의 피해 범위 원.</summary>
        private void UpdateMortarPreview(Vector3 from, Vector3 to, float area)
        {
            EnsureMortarPreview();
            _mortarPreview.gameObject.SetActive(true);

            // 점이 출발점에서 떨어지는 곳 쪽으로 천천히 흘러간다 (점선이 움직이는 느낌)
            float flow = Mathf.Repeat(Time.time * 1.5f, 1f) / MortarDots;
            for (int i = 0; i < MortarDots; i++)
            {
                float t = Mathf.Clamp01((i + 0.5f) / MortarDots + flow);
                _mortarDots[i].position = MortarArc.Evaluate(from, to, t);
                // 멀수록 조금 크게 (화면에서 비슷한 크기로 보이게), 끝으로 갈수록 진하게
                float size = Mathf.Lerp(0.12f, 0.22f, t);
                _mortarDots[i].localScale = Vector3.one * size;
                var r = _mortarDots[i].GetComponent<Renderer>();
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", new Color(1f, Mathf.Lerp(0.95f, 0.5f, t), Mathf.Lerp(0.8f, 0.25f, t), Mathf.Lerp(0.55f, 0.95f, t)));
                r.SetPropertyBlock(mpb);
            }

            // 범위 원: 바닥에 살짝 띄우고, 숨쉬듯 두께가 바뀐다
            Vector3 c = to + Vector3.up * 0.12f;
            for (int i = 0; i < _mortarRing.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / _mortarRing.positionCount;
                _mortarRing.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * area);
            }
            _mortarRing.widthMultiplier = 0.1f + Mathf.PingPong(Time.time * 0.3f, 0.06f);
        }

        private void HideMortarPreview()
        {
            if (_mortarPreview != null) _mortarPreview.gameObject.SetActive(false);
        }

        /// <summary>1인칭 화면 가운데(조준점)가 가리키는 곳. 트럭과 플레이어는 무시한다.</summary>
        private Vector3 AimPoint()
        {
            GetEyePose(out Vector3 eye, out Quaternion look);
            var ray = new Ray(eye, look * Vector3.forward);
            float range = _state.StatValue(TruckStat.TurretRange);
            float best = float.MaxValue; Vector3 point = Vector3.zero; bool hit = false;
            foreach (var h in Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || IsPlayer(h.collider)) continue;
                if (h.distance < best) { best = h.distance; point = h.point; hit = true; }
            }
            if (!hit) point = ray.GetPoint(Mathf.Max(range, 30f));
            return point;
        }

        // ───── 1인칭 (포탑 위에 선 사수) ─────

        private const float MouseSensitivity = 2.2f;
        private const float MinPitch = -35f, MaxPitch = 30f, MortarMaxPitch = 60f;
        // 박격포는 가까운 곳도 노릴 수 있게 더 아래까지 내려다본다
        private float CurrentMaxPitch => IsMortar ? MortarMaxPitch : MaxPitch;
        private const float FirstPersonFov = 62f;
        // 포탑 받침 위, 포신 뒤에 선다 (포탑 기준: 앞 = 포신 방향)
        private static readonly Vector3 StandOffset = new Vector3(0f, 0.24f, -0.95f);
        // 시선: 포신을 내려다보는 높이 (화면 아래에 포신이 보인다)
        private static readonly Vector3 EyeOffset = new Vector3(0f, 1.6f, -1.15f);
        // 박격포: 포신이 위로 솟아 시야를 가리고 포물선이 높이 뜨므로, 더 높고 뒤에서 내려다본다 (궤적 전체가 보이게)
        private static readonly Vector3 MortarEyeOffset = new Vector3(1.5f, 3.4f, -3.6f);   // 오른쪽 어깨 너머: 포물선이 휘어 보이게

        private bool _fp;
        private Camera _fpCam;
        private float _fpSavedFov, _fpSavedNear;
        private readonly System.Collections.Generic.List<Behaviour> _fpDisabledRigs = new System.Collections.Generic.List<Behaviour>();
        private CursorLockMode _fpSavedLock;
        private bool _fpSavedVisible;

        public bool IsFirstPerson => _fp;

        /// <summary>
        /// 사수가 서 있을 자리 (다른 사람 화면에서 포탑 위에 서 있는 모습). 포탑 방향을 따라 받침 위를 돈다.
        /// 포탑이 없으면 false.
        /// </summary>
        public bool TryGetStandPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            if (_root == null || !_root.gameObject.activeInHierarchy) return false;
            Quaternion yaw = Quaternion.Euler(0f, _yaw, 0f);
            position = _root.position + _root.up * StandOffset.y + yaw * new Vector3(StandOffset.x, 0f, StandOffset.z);
            rotation = yaw;
            return true;
        }

        private void GetEyePose(out Vector3 eye, out Quaternion look)
        {
            Quaternion yaw = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 basePos = _root != null ? _root.position : transform.position + Vector3.up * 2.5f;
            Vector3 o = IsMortar ? MortarEyeOffset : EyeOffset;
            eye = basePos + Vector3.up * o.y + yaw * new Vector3(o.x, 0f, o.z);
            look = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void EnterFirstPerson()
        {
            _fpCam = Camera.main;
            if (_fpCam == null) return;
            _fp = true;
            _pitch = Mathf.Clamp(_pitch, MinPitch, CurrentMaxPitch);

            // 3인칭 카메라 장치(CameraFollow, Cinemachine)를 잠시 끄고 카메라를 직접 움직인다
            _fpDisabledRigs.Clear();
            foreach (var b in _fpCam.GetComponents<Behaviour>())
            {
                if (b == null || b is Camera || !b.enabled) continue;
                string n = b.GetType().Name;
                if (n == "CameraFollow" || n == "CinemachineBrain") { b.enabled = false; _fpDisabledRigs.Add(b); }
            }
            _fpSavedFov = _fpCam.fieldOfView;
            _fpSavedNear = _fpCam.nearClipPlane;
            _fpCam.fieldOfView = FirstPersonFov;
            _fpCam.nearClipPlane = 0.05f;

            _fpSavedLock = Cursor.lockState;
            _fpSavedVisible = Cursor.visible;
            SetCursorLocked(true);
            UpdateFirstPersonCamera();
        }

        private void ExitFirstPerson()
        {
            if (!_fp) return;
            _fp = false;
            if (_fpCam != null)
            {
                _fpCam.fieldOfView = _fpSavedFov;
                _fpCam.nearClipPlane = _fpSavedNear;
            }
            foreach (var b in _fpDisabledRigs) if (b != null) b.enabled = true;
            _fpDisabledRigs.Clear();
            Cursor.lockState = _fpSavedLock;
            Cursor.visible = _fpSavedVisible;
            _fpCam = null;
        }

        private void UpdateFirstPersonCamera()
        {
            if (_fpCam == null) { _fp = false; return; }
            GetEyePose(out Vector3 eye, out Quaternion look);
            _fpCam.transform.SetPositionAndRotation(eye, look);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public float CurrentRate()
        {
            float rate = _state != null ? _state.StatValue(TruckStat.TurretRate) : 1.5f;
            if (_info != null && _info.MaxDurability > 0f && _info.CurrentDurability / _info.MaxDurability < DamagedThreshold) rate *= DamagedRatePenalty;
            return Mathf.Max(0.1f, rate);
        }

        public int AmmoCount() => AmmoCountIn(Inventory);

        // ───── 판정 (싱글/호스트) ─────

        /// <summary>한 발 쏜다. 탄환을 쓰고 피해를 준다. 실패하면 이유를 message로.</summary>
        public bool HostFire(Vector3 aimPoint, out string message)
        {
            message = null;
            if (_state == null || !_state.HasTurret) { message = "포탑이 없어요."; return false; }
            // 요청이 너무 잦으면 무시 (네트워크 지연을 감안해 20% 여유)
            if (Time.time - _lastHostShot < 0.8f / CurrentRate()) return false;

            TruckUpgradeRules.Turret kind = _state.Turret;
            if (!TryConsumeAmmo(kind.ammoPerShot, out int ammoDamage))
            {
                message = kind.ammoPerShot > 1
                    ? $"탄환이 모자라요 (한 발에 {kind.ammoPerShot}개). R: 트럭 보관함의 나무/돌/광석으로 탄환 만들기"
                    : "탄환이 없어요. R: 트럭 보관함의 나무/돌/광석으로 탄환 만들기";
                return false;
            }
            _lastHostShot = Time.time;

            Vector3 from = MuzzlePosition;
            int damage = Mathf.RoundToInt(_state.StatValue(TruckStat.TurretAttack) + ammoDamage);
            Vector3 to;
            if (IsMortar)
            {
                // 박격포: 포탄이 포물선으로 날아가 떨어진 뒤에 그 자리에 범위 피해 (보이는 포탄과 같은 비행 시간)
                to = MortarLanding(from, aimPoint);
                StartCoroutine(MortarImpactRoutine(to, damage, _state.StatValue(TruckStat.TurretArea), MortarArc.FlightTime(from, to)));
            }
            else
            {
                ApplyShot(transform, from, aimPoint, _yaw, kind, _state.StatValue(TruckStat.TurretRange), damage, _state.StatValue(TruckStat.TurretArea), out to);
            }

            if (Online) LastTruck.Networking.NetworkTruck.BroadcastTurretShot(from, to);
            else ShowShot(from, to);
            return true;
        }

        /// <summary>
        /// 한 발의 피해 판정 (수동/자동 포탑 공통). truck은 맞히지 않을 트럭, fallbackYaw는 조준점이 너무 가까울 때 쓸 방향.
        /// to에 탄도가 끝나는 지점을 돌려준다.
        /// </summary>
        public static void ApplyShot(Transform truck, Vector3 from, Vector3 aimPoint, float fallbackYaw, TruckUpgradeRules.Turret kind,
                                     float range, int damage, float area, out Vector3 to)
        {
            Vector3 dir = aimPoint - from;
            if (dir.sqrMagnitude < 0.01f) dir = Quaternion.Euler(0f, fallbackYaw, 0f) * Vector3.forward;
            dir.Normalize();
            var done = new System.Collections.Generic.HashSet<Combat.Damageable>();

            if (kind.pierce)
            {
                // 관통: 일직선에 있는 모든 적을 맞히고, 단단한 지형/구조물에서 멈춘다
                to = from + dir * range;
                var hits = Physics.SphereCastAll(from, 0.35f, dir, range, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(truck) || IsPlayer(h.collider)) continue;
                    Vector3 p = h.distance > 0f ? h.point : h.collider.ClosestPoint(from);
                    var target = h.collider.GetComponentInParent<Combat.Damageable>();
                    if (target == null) { to = p; break; }
                    if (done.Add(target)) target.TakeDamage(damage, p, null);
                }
            }
            else
            {
                to = from + dir * Mathf.Min(range, Vector3.Distance(from, aimPoint) + 0.5f);

                float bestDist = float.MaxValue;
                foreach (var h in Physics.SphereCastAll(from, 0.25f, dir, range, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.transform.IsChildOf(truck) || IsPlayer(h.collider)) continue;
                    if (h.distance < bestDist) { bestDist = h.distance; to = h.distance > 0f ? h.point : h.collider.ClosestPoint(from); }
                }

                foreach (var col in Physics.OverlapSphere(to, area, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col.transform.IsChildOf(truck) || IsPlayer(col)) continue;
                    var target = col.GetComponentInParent<Combat.Damageable>();
                    if (target == null || !done.Add(target)) continue;
                    target.TakeDamage(damage, col.ClosestPoint(to), null);
                }
            }
        }

        /// <summary>트럭 보관함에 있는 탄환 수 (좋은 것부터 전부 합친 값).</summary>
        public static int AmmoCountIn(TruckInventory inv)
        {
            if (inv == null) return 0;
            var catalog = ItemCatalog.GetOrCreate();
            int n = 0;
            foreach (var a in TruckUpgradeRules.AmmoTypes) { var it = catalog.GetItem(a.id); if (it != null) n += inv.GetItemCount(it); }
            return n;
        }

        /// <summary>보관함에서 탄환 need개를 좋은 것부터 꺼낸다. 모자라면 아무것도 꺼내지 않고 false. damage는 가장 먼저 쓴 탄환의 데미지.</summary>
        public static bool TryConsumeAmmoFrom(TruckInventory inv, int need, out int damage)
        {
            damage = -1;
            if (inv == null || need <= 0 || AmmoCountIn(inv) < need) return false;

            var catalog = ItemCatalog.GetOrCreate();
            int left = need;
            foreach (var a in TruckUpgradeRules.AmmoTypes)
            {
                var it = catalog.GetItem(a.id);
                if (it == null) continue;
                int take = Mathf.Min(inv.GetItemCount(it), left);
                if (take <= 0 || !inv.RemoveItem(it, take)) continue;
                if (damage < 0) damage = a.damage;
                left -= take;
                if (left <= 0) break;
            }
            return left <= 0;
        }

        /// <summary>트럭 보관함에서 탄환 need개를 좋은 것부터 꺼낸다. 모자라면 아무것도 꺼내지 않고 false. damage는 가장 먼저 쓴 탄환의 데미지.</summary>
        private bool TryConsumeAmmo(int need, out int damage) => TryConsumeAmmoFrom(Inventory, need, out damage);

        private static bool IsPlayer(Collider c) =>
            c.GetComponentInParent<LastTruck.PlayerMove>() != null || c.GetComponentInParent<LastTruck.Networking.NetworkPlayer>() != null;

        /// <summary>트럭 보관함의 나무/돌/구리 광석/철 광석 중 가장 많은 것 2개를 탄환 10발로 만든다.</summary>
        public bool HostCraftAmmo(out string message)
        {
            var inv = Inventory; var catalog = ItemCatalog.GetOrCreate();
            if (inv == null) { message = "트럭 보관함을 찾을 수 없어요."; return false; }
            TruckUpgradeRules.Ammo best = default; ItemData bestRes = null; int bestCount = 0;
            foreach (var a in TruckUpgradeRules.AmmoTypes)
            {
                var res = catalog.GetItem(a.resourceId);
                if (res == null || catalog.GetItem(a.id) == null) continue;
                int c = inv.GetItemCount(res);
                if (c > bestCount) { bestCount = c; best = a; bestRes = res; }
            }
            if (bestRes == null) { message = "탄환 재료(나무/돌/구리 광석/철 광석)가 트럭 보관함에 없어요."; return false; }
            int use = Mathf.Min(2, bestCount);
            var round = catalog.GetItem(best.id);
            if (!inv.RemoveItem(bestRes, use)) { message = "재료를 꺼내지 못했어요."; return false; }
            if (!inv.AddItem(round, use * TruckUpgradeRules.RoundsPerResource))
            {
                inv.AddItem(bestRes, use);
                message = "트럭 보관함에 자리가 없어요.";
                return false;
            }
            message = $"{bestRes.itemName} {use}개 → {round.itemName} {use * TruckUpgradeRules.RoundsPerResource}발";
            return true;
        }

        // ───── 연출 (모든 컴퓨터) ─────

        /// <summary>사수가 쏜 한 발: 레이저 빔 + 맞은 곳 섬광 (모든 컴퓨터).</summary>
        public void ShowShot(Vector3 from, Vector3 to)
        {
            string id = _state != null ? _state.PartAt(TruckPartSlot.Roof) : null;
            float area = _state != null ? _state.StatValue(TruckStat.TurretArea) : 3f;
            if (id == ItemIds.MortarTurret)
            {
                TurretMortarShell.Launch(from, to, area);   // 돌 포탄이 포물선을 그리며 날아가 터진다
                return;
            }
            if (_laserMat == null)
            {
                var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _laserMat = new Material(sh);
            }

            // 포탑 종류마다 레이저 색/굵기/지속시간이 다르다
            Color glow = new Color(1f, 0.25f, 0.15f), core = new Color(1f, 0.92f, 0.85f);
            float width = 0.18f, life = 0.16f;
            switch (id)
            {
                case ItemIds.GatlingTurret: glow = new Color(1f, 0.75f, 0.15f); width = 0.11f; life = 0.08f; break;
                case ItemIds.SniperTurret: glow = new Color(0.35f, 0.75f, 1f); core = Color.white; width = 0.13f; life = 0.26f; break;
                case ItemIds.MortarTurret: glow = new Color(1f, 0.45f, 0.1f); width = 0.3f; life = 0.18f; break;
                case ItemIds.FlameTurret: glow = new Color(1f, 0.4f, 0.05f); width = 0.42f; life = 0.12f; break;
                case ItemIds.RailTurret: glow = new Color(0.2f, 1f, 0.95f); core = Color.white; width = 0.28f; life = 0.3f; break;
            }

            new GameObject("TurretLaser").AddComponent<TurretLaserFx>()
                .Init(_laserMat, from, to, glow, core, width, life, Mathf.Clamp(area * 0.6f, 0.6f, 2.2f));
        }

        private static Material _laserMat;

        /// <summary>포탑 종류(수동 포탑 id)에 맞는 탄도/폭발 연출. 자동 포탑도 이것을 쓴다.</summary>
        public void ShowShotFor(string id, float area, Vector3 from, Vector3 to)
        {
            if (id == ItemIds.MortarTurret)
            {
                TurretMortarShell.Launch(from, to, area);   // 자동 박격포도 돌 포탄이 포물선으로 날아간다
                return;
            }
            if (_tracerMat == null)
            {
                var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _tracerMat = new Material(sh);
            }
            // 포탑 종류마다 탄도 색/굵기/지속시간이 다르다
            float width = 0.12f, life = 0.07f, flashMax = 2.5f;
            Color c0 = new Color(1f, 0.85f, 0.4f), c1 = new Color(1f, 0.5f, 0.1f, 0.6f);
            switch (id)
            {
                case ItemIds.GatlingTurret: width = 0.07f; life = 0.05f; c0 = new Color(1f, 0.95f, 0.55f); break;
                case ItemIds.SniperTurret: width = 0.06f; life = 0.14f; c0 = Color.white; c1 = new Color(0.6f, 0.85f, 1f, 0.6f); break;
                case ItemIds.MortarTurret: width = 0.2f; life = 0.1f; flashMax = 4.5f; c0 = new Color(1f, 0.6f, 0.2f); c1 = new Color(0.9f, 0.25f, 0.1f, 0.6f); break;
                case ItemIds.FlameTurret: width = 0.45f; life = 0.09f; flashMax = 3.2f; c0 = new Color(1f, 0.75f, 0.2f); c1 = new Color(1f, 0.25f, 0.05f, 0.4f); break;
                case ItemIds.RailTurret: width = 0.22f; life = 0.16f; c0 = new Color(0.7f, 1f, 1f); c1 = new Color(0.2f, 0.7f, 1f, 0.5f); break;
            }

            var go = new GameObject("TurretTracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _tracerMat;
            lr.positionCount = 2;
            lr.SetPosition(0, from); lr.SetPosition(1, to);
            lr.startWidth = width; lr.endWidth = width * 0.4f;
            lr.startColor = c0; lr.endColor = c1;
            Destroy(go, life);

            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flash.GetComponent<Collider>());
            flash.transform.position = to;
            flash.transform.localScale = Vector3.one * Mathf.Min(area, flashMax);
            flash.GetComponent<Renderer>().sharedMaterial = _tracerMat;
            Destroy(flash, life + 0.01f);
        }

        private void OnGUI()
        {
            if (!LocalGunning) return;
            var turretItem = ItemCatalog.GetOrCreate().GetItem(_state.PartAt(TruckPartSlot.Roof));
            string turretName = turretItem != null ? turretItem.itemName : "포탑";
            if (_fp) DrawCrosshair();
            string text = $"{turretName} 사수   마우스: 조준   좌클릭: {(IsMortar ? "포탄 발사 (점선 = 떨어질 곳)" : "레이저 발사")}   R: 탄환 만들기   탄환 {AmmoCount()}발   공격 {_state.StatValue(TruckStat.TurretAttack):0} · {CurrentRate():0.0}/초 · 사거리 {_state.StatValue(TruckStat.TurretRange):0}m"
                          + (Online ? "   T: 좌석으로   E: 내리기" : "   T: 운전석으로   E: 내리기");
            var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(14, Screen.height / 45), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            Vector2 size = style.CalcSize(new GUIContent(text)) + new Vector2(24f, 12f);
            // 화면 위쪽 가운데 (아래쪽은 인벤토리/포신과 겹친다)
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.75f, size.x, size.y), text, style);
        }

        /// <summary>1인칭 조준점: 가운데 점 + 네 갈래 선. 발사 직후엔 잠깐 벌어진다.</summary>
        private void DrawCrosshair()
        {
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float s = Mathf.Max(1f, Screen.height / 1080f);
            float kick = Mathf.Clamp01((_nextLocalShot - Time.time) * CurrentRate()) * 6f * s;
            float gap = 7f * s + kick, len = 12f * s, th = 2f * s;
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            DrawCross(cx, cy, gap, len, th + 2f * s);
            GUI.color = new Color(1f, 0.35f, 0.25f, 0.95f);
            DrawCross(cx, cy, gap, len, th);
            GUI.color = old;
        }

        private static void DrawCross(float cx, float cy, float gap, float len, float th)
        {
            Texture2D t = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy - th * 0.5f, th, th), t);
            GUI.DrawTexture(new Rect(cx - gap - len, cy - th * 0.5f, len, th), t);
            GUI.DrawTexture(new Rect(cx + gap, cy - th * 0.5f, len, th), t);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy - gap - len, th, len), t);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy + gap, th, len), t);
        }
    }
}
