using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 행동 연출용 공용 이펙트: 작은 조각(큐브)이 튀는 파티클을 코드로 만든다 (프리팹/에셋 없이 동작, 게임 셰이더 ToonWorld 색으로 그려져 로우폴리 느낌이 난다).
    /// 풀잎 튀김, 돌 파편, 아이템 획득 반짝임, 회복 빛, 불씨 등.
    /// </summary>
    public static class ActionFx
    {
        private static Mesh _cube;
        private static Material _mat;

        private static Mesh Cube()
        {
            if (_cube != null) return _cube;
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return _cube;
        }

        private static Material Mat()
        {
            if (_mat != null) return _mat;
            Shader sh = Shader.Find("LastTruck/ToonWorld");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            _mat = new Material(sh) { name = "ActionFx" };
            return _mat;
        }

        /// <summary>조각 여러 개가 한 번에 튀어 오르다 떨어지며 사라진다.</summary>
        public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float life,
                                 float gravity = 1.2f, float up = 0.6f, bool glow = false, float radius = 0.2f)
        {
            var go = new GameObject("ActionFx");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.65f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(8, count + 4);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 200)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            // 위로 솟구치는 성분을 준다 (속도 배율은 시작 방향 + 위쪽)
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(up * speed * 0.6f, up * speed);
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);   // 세 축이 같은 방식(두 상수)이어야 한다
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var size3 = ps.sizeOverLifetime;
            size3.enabled = true;
            size3.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.12f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f)));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Cube();
            r.sharedMaterial = Mat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", color);
            mpb.SetColor("_Color", color);
            if (glow) mpb.SetColor("_EmissionColor", color * 1.6f);
            r.SetPropertyBlock(mpb);

            ps.Play();
            Object.Destroy(go, life + 0.6f);
        }

        /// <summary>채집이 끝난 프롭이 사라질 때: 풀/선인장은 잎조각, 돌은 파편이 튄다 (나무는 쓰러지는 연출이 있어 제외).</summary>
        public static void PropPuff(Vector3 center, string itemId)
        {
            if (itemId == ItemIds.Wood) return;
            if (itemId == ItemIds.Stone)
            {
                Burst(center, new Color(0.62f, 0.60f, 0.58f), 9, 3.2f, 0.12f, 0.8f, 2.4f, 0.5f);
                return;
            }
            Leaves(center, itemId, 9);
        }

        /// <summary>풀잎 조각 (식물 종류별 색).</summary>
        public static void Leaves(Vector3 pos, string itemId, int count)
        {
            Color c = new Color(0.40f, 0.82f, 0.38f);
            if (itemId == ItemIds.WildGrass) c = new Color(0.66f, 0.86f, 0.34f);
            else if (itemId == ItemIds.Cactus) c = new Color(0.28f, 0.74f, 0.48f);
            else if (itemId == ItemIds.PoisonHerb) c = new Color(0.62f, 0.36f, 0.82f);
            Burst(pos, c, count, 2.4f, 0.09f, 0.9f, 1.5f, 0.55f, false, 0.12f);
        }

        /// <summary>아이템을 얻었을 때의 반짝임.</summary>
        public static void Pickup(Vector3 pos)
        {
            Burst(pos, new Color(1f, 0.86f, 0.42f), 8, 1.8f, 0.07f, 0.9f, -0.15f, 1.0f, true, 0.3f);
        }

        /// <summary>손에 든 물건을 바꿀 때 손 위에 번지는 작은 빛.</summary>
        public static void Equip(Vector3 pos)
        {
            Burst(pos, new Color(1f, 0.95f, 0.75f), 5, 1.1f, 0.05f, 0.55f, -0.1f, 0.7f, true, 0.12f);
        }

        /// <summary>먹거나 마셔서 효과를 얻었을 때: 체력(초록) / 스태미나(노랑) / 수리·강화(주황).</summary>
        public static void Consume(Vector3 pos, bool heal, bool stamina, bool repair)
        {
            if (heal) Burst(pos, new Color(0.45f, 1f, 0.55f), 12, 1.9f, 0.09f, 1.1f, -0.12f, 1.0f, true, 0.35f);
            if (stamina) Burst(pos, new Color(1f, 0.9f, 0.35f), 10, 2.1f, 0.08f, 1.0f, -0.1f, 1.0f, true, 0.4f);
            if (repair) Burst(pos, new Color(1f, 0.62f, 0.25f), 12, 3.0f, 0.07f, 0.8f, 1.2f, 0.4f, true, 0.2f);
        }

        /// <summary>불씨 (조금씩 올라가며 꺼진다).</summary>
        public static void Ember(Vector3 pos)
        {
            Burst(pos, new Color(1f, 0.55f, 0.18f), 1, 0.6f, 0.05f, 0.7f, -0.25f, 1.0f, true, 0.04f);
        }
    }

    /// <summary>
    /// 코드로 만드는 몸짓 연출(애니메이션 클립 없이, 휴머노이드 뼈대를 매 프레임 덧회전한다).
    ///  - Pluck: 풀/선인장 같은 땅의 식물을 캘 때 몸을 숙여 뽑는 동작 (곡괭이 모션이 없는 채집용)
    ///  - Equip: 손에 든 물건을 바꿀 때 오른팔을 살짝 들었다 내리는 동작
    ///  - Eating: 먹고 마시는 동안 팔을 입으로 올리고 고개를 까딱이는 동작
    ///  - Hold: 물건을 앞으로 늘어뜨려 드는 자세 (지금은 쓰는 곳 없음: 랜턴은 허리에 찬다)
    /// 회전은 세계 좌표 축(캐릭터의 오른쪽 축) 기준이라 뼈대의 로컬 축 방향에 상관없이 동작한다.
    /// PlayerInventory가 자동으로 붙이고, 다른 사람의 채집 신호를 받는 캐릭터에는 필요할 때 붙는다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ActionPose : MonoBehaviour
    {
        public enum HoldKind { None, Lantern }

        public const float PluckSeconds = 0.65f;
        private const float EquipSeconds = 0.4f;

        public static ActionPose For(GameObject player)
        {
            ActionPose p = player.GetComponent<ActionPose>();
            if (p == null) p = player.AddComponent<ActionPose>();
            return p;
        }

        private Animator _anim;
        private Transform _spine, _chest, _head, _rUpper, _rLower, _lUpper;
        private PlayerInventory _inv;
        private float _pluckStart = -10f;
        private float _equipStart = -10f;
        private bool _eating;
        private HoldKind _hold;
        private float _eatW, _holdW;
        private HoldKind _poseKind;
        private int _lastCount = -1;

        /// <summary>오른손 뼈 (없으면 null). 손에 든 물건이 손을 따라가도록 쓴다.</summary>
        public Transform RightHand { get; private set; }

        public void Pluck() { _pluckStart = Time.time; }
        public void Equip() { _equipStart = Time.time; }
        public void SetEating(bool on) { _eating = on; }
        public void SetHold(HoldKind kind) { _hold = kind; if (kind != HoldKind.None) _poseKind = kind; }

        private void Start()
        {
            _inv = GetComponent<PlayerInventory>();
            if (_inv != null)
            {
                _inv.SelectedSlotChanged += OnSelectedChanged;
                _inv.Changed += OnInventoryChanged;
                _lastCount = TotalCount();
            }
        }

        private void OnDestroy()
        {
            if (_inv != null)
            {
                _inv.SelectedSlotChanged -= OnSelectedChanged;
                _inv.Changed -= OnInventoryChanged;
            }
        }

        private int TotalCount()
        {
            int n = 0;
            if (_inv == null) return 0;
            foreach (InventorySlot s in _inv.Slots)
                if (s != null && !s.IsEmpty) n += s.count;
            return n;
        }

        private void OnSelectedChanged()
        {
            Equip();
            ResolveBones();
            if (_inv != null && _inv.SelectedItem != null)
                ActionFx.Equip(HandPosition());
        }

        private void OnInventoryChanged()
        {
            int now = TotalCount();
            // 늘어났을 때만 (획득). 먹거나 버려서 줄어드는 것은 반짝이지 않는다.
            if (_lastCount >= 0 && now > _lastCount)
                ActionFx.Pickup(transform.position + Vector3.up * 1.0f);
            _lastCount = now;
        }

        private Vector3 HandPosition()
        {
            return RightHand != null ? RightHand.position : transform.position + Vector3.up * 1.1f + transform.forward * 0.3f;
        }

        private void ResolveBones()
        {
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null || !_anim.isHuman) return;
            if (_spine != null && _rUpper != null) return;
            _spine = _anim.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
            _head = _anim.GetBoneTransform(HumanBodyBones.Head);
            _rUpper = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rLower = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _lUpper = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            RightHand = _anim.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private void Rot(Transform bone, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < 0.01f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, transform.right) * bone.rotation;
        }

        private void LateUpdate()
        {
            ResolveBones();
            if (_spine == null && _rUpper == null) return;

            float dt = Time.deltaTime;
            _eatW = Mathf.MoveTowards(_eatW, _eating ? 1f : 0f, dt * 6f);
            _holdW = Mathf.MoveTowards(_holdW, _hold != HoldKind.None ? 1f : 0f, dt * 5f);

            // 땅의 식물 뽑기: 숙이고(0~0.35) 쑥 당기고(0.35~0.6) 일어난다
            float pluck = 0f;
            float pt = (Time.time - _pluckStart) / PluckSeconds;
            if (pt >= 0f && pt < 1f)
            {
                pluck = Smooth(0f, 0.35f, pt) - Smooth(0.6f, 1f, pt);
                if (pt > 0.35f && pt < 0.6f)
                    pluck += Mathf.Sin((pt - 0.35f) / 0.25f * Mathf.PI * 2f) * 0.12f;
            }

            // 물건 바꿀 때: 오른팔을 살짝 들었다 내린다
            float equip = 0f;
            float et = (Time.time - _equipStart) / EquipSeconds;
            if (et >= 0f && et < 1f) equip = Mathf.Sin(et * Mathf.PI);

            // 먹기: 팔을 입 쪽으로 올리고 고개를 까딱
            float chew = Mathf.Sin(Time.time * 7f);
            float eat = _eatW;

            float hold = _holdW;
            float sway = Mathf.Sin(Time.time * 1.7f);

            Rot(_spine, pluck * 18f + eat * 4f + equip * 3f);
            Rot(_chest, pluck * 12f);
            Rot(_head, -pluck * 10f + eat * (5f + 4f * Mathf.Max(0f, chew)) - equip * 4f);
            Rot(_rUpper, -(pluck * 35f + equip * 32f + eat * 72f + hold * (18f + 2f * sway)));
            Rot(_rLower, -(pluck * 20f + equip * 38f + eat * (95f + 5f * chew) + hold * 30f));
            Rot(_lUpper, -(pluck * 10f + eat * 6f));
        }
    }

    /// <summary>
    /// 풀/약초/선인장을 캘 때 그 식물이 뽑히기 전에 꾸벅 흔들리는 연출 (잔가지가 아닌 프롭 전체를 밑동 기준으로 기울이고 살짝 눌렀다 편다).
    /// 끝나면 정확히 원래 자세로 돌려 놓는다.
    /// </summary>
    public class PlantWobble : MonoBehaviour
    {
        private const float Duration = 0.6f;

        private Quaternion _baseLocalRot;
        private Quaternion _baseWorldRot;
        private Vector3 _baseScale;
        private Vector3 _axis;
        private float _t0;
        private bool _active;

        public static void Wobble(GameObject root, Vector3 from)
        {
            if (root == null) return;
            PlantWobble w = root.GetComponent<PlantWobble>();
            if (w == null) w = root.AddComponent<PlantWobble>();
            w.Begin(from);
        }

        private void Begin(Vector3 from)
        {
            if (!_active)
            {
                _baseLocalRot = transform.localRotation;
                _baseWorldRot = transform.rotation;
                _baseScale = transform.localScale;
            }
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = transform.forward;
            _axis = Vector3.Cross(Vector3.up, away.normalized);
            _t0 = Time.time;
            _active = true;
            enabled = true;
        }

        /// <summary>바로 원래 모습으로 (프롭이 곧 사라질 때).</summary>
        public void Cancel() { Restore(); }

        private void Update()
        {
            if (!_active) { enabled = false; return; }
            float t = Time.time - _t0;
            if (t >= Duration) { Restore(); return; }

            float decay = Mathf.Exp(-5f * t);
            float angle = 10f * decay * Mathf.Sin(t * 14f);
            transform.rotation = Quaternion.AngleAxis(angle, _axis) * _baseWorldRot;
            float k = decay * (0.5f + 0.5f * Mathf.Sin(t * 28f));
            transform.localScale = Vector3.Scale(_baseScale, new Vector3(1f + 0.05f * k, 1f - 0.08f * k, 1f + 0.05f * k));
        }

        private void Restore()
        {
            if (_active)
            {
                transform.localRotation = _baseLocalRot;
                transform.localScale = _baseScale;
            }
            _active = false;
            enabled = false;
        }

        private void OnDisable()
        {
            if (_active) Restore();
        }
    }
}
