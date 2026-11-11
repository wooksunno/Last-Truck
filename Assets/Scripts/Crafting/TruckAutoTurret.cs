using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 적재함의 자동 포탑. 사수(플레이어) 없이 가까운 적을 알아서 조준하고 쏜다.
    ///
    ///  - 적재함(TruckPartSlot.Bed)에 자동 포탑 파츠를 끼우면 Mount_Bed 자리에 포탑 모델(수동 포탑과 같은 모양 + 파란 센서 눈)이 생긴다.
    ///  - 사거리 안에서 가장 가까운 적(몬스터)을 골라 포신이 돌아가고, 조준이 맞으면 쏜다. 적이 없으면 천천히 주변을 훑는다.
    ///  - 탄환은 수동 포탑과 같은 트럭 보관함의 탄환을 쓴다. 레벨 강화(포탑 공격력/속도/사거리/범위)도 같이 적용된다.
    ///    사격 속도는 같은 종류의 수동 포탑보다 15% 느리다 (TruckUpgradeRules.AutoRateMultiplier).
    ///  - 피해 판정/탄환 소모는 싱글 또는 호스트에서만 한다. 멀티플레이 클라이언트는 포신 방향을 자기 화면에서 따로 맞추고
    ///    (몬스터 위치는 모두 같다) 탄도 연출만 호스트가 보낸 신호로 본다.
    /// TruckInformation이 자동으로 붙인다.
    /// </summary>
    public class TruckAutoTurret : MonoBehaviour
    {
        private const float TurnSpeed = 320f;       // 도/초
        private const float FireArcDegrees = 9f;    // 이 각도 안으로 맞으면 쏜다
        private const float RetargetInterval = 0.2f;
        private const float IdleScanSpeed = 28f;    // 적이 없을 때 훑는 속도 (도/초)
        private const float NoAmmoToastInterval = 25f;

        public static TruckAutoTurret Instance { get; private set; }

        private TruckUpgradeState _state;
        private LastTruck.TruckInformation _info;
        private TruckTurret _manual;
        private TruckInventory _inventory;

        private Transform _root, _yawPivot, _muzzle;
        private string _builtId;
        private float _yawAngle;
        private Combat.Damageable _target;
        private float _nextRetarget, _nextShot, _nextToast;

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

        private TruckInventory Inventory
        {
            get
            {
                if (_inventory == null) _inventory = GetComponentInChildren<TruckInventory>();
                if (_inventory == null) _inventory = FindFirstObjectByType<TruckInventory>();
                return _inventory;
            }
        }

        private bool _subscribed;

        private void Awake()
        {
            Instance = this;
            _yawAngle = transform.eulerAngles.y + 180f;   // 처음엔 뒤쪽을 본다
        }

        /// <summary>파츠 상태(TruckUpgradeState)는 TruckInformation이 Awake에서 붙이므로, 모든 Awake가 끝난 뒤에 찾는다.</summary>
        private bool Acquire()
        {
            if (_state == null) _state = GetComponent<TruckUpgradeState>();
            if (_info == null) _info = GetComponent<LastTruck.TruckInformation>();
            if (_manual == null) _manual = GetComponent<TruckTurret>();
            if (_state != null && !_subscribed) { _state.Changed += Refresh; _subscribed = true; }
            return _state != null;
        }

        private void OnEnable() { if (_state != null && !_subscribed) { _state.Changed += Refresh; _subscribed = true; } }
        private void OnDisable() { if (_state != null && _subscribed) { _state.Changed -= Refresh; _subscribed = false; } }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Start() { if (Acquire()) Refresh(); }

        // ───── 모델 ─────

        private void Refresh()
        {
            bool has = _state != null && _state.HasAutoTurret;
            string id = has ? _state.PartAt(TruckPartSlot.Bed) : null;

            if (has && _root != null && id != _builtId)
            {
                Destroy(_root.gameObject);
                _root = null; _yawPivot = null; _muzzle = null;
            }
            if (has && _root == null) Build(id);
            if (_root != null) _root.gameObject.SetActive(has);
            if (!has) _target = null;
        }

        private void Build(string autoId)
        {
            _builtId = autoId;
            string baseId = autoId.StartsWith(TruckUpgradeRules.AutoPrefix) ? autoId.Substring(TruckUpgradeRules.AutoPrefix.Length) : autoId;

            Transform mount = transform.Find("Mount_Bed");
            Vector3 position = mount != null ? mount.position : transform.TransformPoint(new Vector3(0f, 1.2f, -1.7f));

            _root = new GameObject("TruckAutoTurret_Model").transform;
            _root.SetParent(transform, false);
            _root.position = position;
            _root.localRotation = Quaternion.identity;

            TruckTurret.BuildModel(baseId, _root, out _yawPivot, out _muzzle);

            // 적재함 바닥에서 포탑 받침까지 올라오는 기둥 (포신이 적재함 난간 위로 나오도록 높였다)
            var steel = Lit(new Color(0.3f, 0.32f, 0.36f));
            Prim(PrimitiveType.Cylinder, _root, new Vector3(0f, -0.2f, 0f), new Vector3(0.32f, 0.2f, 0.32f), steel);

            // 자동 포탑 표시: 파란 센서 눈 + 안테나
            var eye = Glow(new Color(0.3f, 0.85f, 1f));
            Prim(PrimitiveType.Sphere, _yawPivot, new Vector3(0f, 0.72f, -0.15f), new Vector3(0.22f, 0.22f, 0.22f), eye);
            Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0.28f, 0.8f, -0.3f), new Vector3(0.03f, 0.35f, 0.03f), steel);
            Prim(PrimitiveType.Sphere, _yawPivot, new Vector3(0.28f, 1.17f, -0.3f), new Vector3(0.08f, 0.08f, 0.08f), eye);
        }

        // ───── 조준 / 사격 ─────

        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf || _state == null || !_state.HasAutoTurret) return;

            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + RetargetInterval;
                _target = FindTarget();
            }

            bool aimed = false;
            if (_target != null)
            {
                Vector3 d = _target.transform.position - _muzzle.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                {
                    float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    _yawAngle = Mathf.MoveTowardsAngle(_yawAngle, want, TurnSpeed * Time.deltaTime);
                    aimed = Mathf.Abs(Mathf.DeltaAngle(_yawAngle, want)) <= FireArcDegrees;
                }
            }
            else
            {
                _yawAngle += IdleScanSpeed * Time.deltaTime;   // 적이 없을 땐 천천히 주변을 훑는다
            }
            _yawPivot.rotation = Quaternion.Euler(0f, _yawAngle, 0f);

            if (aimed && HasAuthority && Time.time >= _nextShot)
            {
                if (HasClearShot(_target)) TryFire();
                else _nextShot = Time.time + 0.25f;   // 나무/바위에 가로막히면 탄환을 낭비하지 않고 기다린다
            }
        }

        /// <summary>총구에서 목표까지 사이에 몬스터가 아닌 지형/구조물이 끼어 있지 않은가.</summary>
        private bool HasClearShot(Combat.Damageable target)
        {
            if (IsMortar) return true;   // 박격포는 포물선으로 나무/바위를 넘어간다
            Vector3 from = _muzzle.position;
            Vector3 aim = target.transform.position + Vector3.up * 0.9f;
            Vector3 dir = aim - from;
            float dist = dir.magnitude;
            if (dist < 0.1f) return true;

            var hits = Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<LastTruck.PlayerMove>() != null || h.collider.GetComponentInParent<LastTruck.Networking.NetworkPlayer>() != null) continue;
                return h.collider.GetComponentInParent<Combat.Damageable>() != null;   // 처음 맞는 것이 몬스터(피해를 받는 대상)여야 한다
            }
            return true;
        }

        private Combat.Damageable FindTarget()
        {
            TruckUpgradeRules.Turret kind = _state.AutoTurret;
            float range = _state.StatValueFor(TruckStat.TurretRange, kind);
            float rangeSqr = range * range;
            Vector3 origin = _muzzle.position;

            // 주변 콜라이더(나무/바위/지형)가 너무 많아서 물리 검색 대신 몬스터 목록에서 직접 고른다 (싱글: MonsterAI, 멀티: NetworkMonster)
            _candidates.Clear();
            foreach (MonsterAI m in FindObjectsByType<MonsterAI>(FindObjectsSortMode.None))
                Consider(m.GetComponent<Combat.Damageable>(), origin, rangeSqr);
            foreach (var m in FindObjectsByType<LastTruck.Networking.NetworkMonster>(FindObjectsSortMode.None))
                Consider(m.GetComponent<Combat.Damageable>(), origin, rangeSqr);
            if (_candidates.Count == 0) return null;

            // 가까운 순서로, 가로막힌 적은 건너뛰고 쏠 수 있는 적을 먼저 고른다 (전부 막혔으면 가장 가까운 적을 향해 돌기만 한다)
            _candidates.Sort((a, b) => a.sqrDist.CompareTo(b.sqrDist));
            foreach (var c in _candidates)
                if (HasClearShot(c.target)) return c.target;
            return _candidates[0].target;
        }

        private struct Candidate { public Combat.Damageable target; public float sqrDist; }
        private readonly System.Collections.Generic.List<Candidate> _candidates = new System.Collections.Generic.List<Candidate>();

        private void Consider(Combat.Damageable d, Vector3 origin, float rangeSqr)
        {
            if (d == null || d.IsDeadOffline || d.CurrentHealth <= 0) return;
            float dist = (d.transform.position - origin).sqrMagnitude;
            if (dist <= rangeSqr) _candidates.Add(new Candidate { target = d, sqrDist = dist });
        }

        private float CurrentRate(TruckUpgradeRules.Turret kind)
        {
            float rate = _state.StatValueFor(TruckStat.TurretRate, kind);
            if (_info != null && _info.MaxDurability > 0f && _info.CurrentDurability / _info.MaxDurability < TruckTurret.DamagedThreshold)
                rate *= TruckTurret.DamagedRatePenalty;
            return Mathf.Max(0.1f, rate);
        }

        private void TryFire()
        {
            TruckUpgradeRules.Turret kind = _state.AutoTurret;
            if (!TruckTurret.TryConsumeAmmoFrom(Inventory, kind.ammoPerShot, out int ammoDamage))
            {
                _nextShot = Time.time + 1f;
                if (Time.time >= _nextToast && !TruckInventory.IsHandlingRemoteRequest)
                {
                    _nextToast = Time.time + NoAmmoToastInterval;
                    LastTruck.HudToast.Show("자동 포탑: 탄환이 없어요 (트럭 보관함의 나무/돌/광석으로 탄환을 만들어 두세요)");
                }
                return;
            }

            _nextShot = Time.time + 1f / CurrentRate(kind);

            Vector3 from = _muzzle.position;
            int damage = Mathf.RoundToInt(_state.StatValueFor(TruckStat.TurretAttack, kind) + ammoDamage);

            if (IsMortar && _manual != null)
            {
                // 박격포: 적이 포탄이 떨어질 때쯤 있을 자리(발밑)를 노려 포물선으로 쏘고, 비행 시간 뒤에 그 자리에 범위 피해
                Vector3 land = PredictFeet(_target, from);
                float area = _state.StatValueFor(TruckStat.TurretArea, kind);
                _manual.StartMortarImpact(land, damage, area, MortarArc.FlightTime(from, land));
                if (Online) LastTruck.Networking.NetworkTruck.BroadcastAutoTurretShot(from, land);
                else ShowShot(from, land);
                return;
            }

            Vector3 aim = _target.transform.position + Vector3.up * 0.9f;
            TruckTurret.ApplyShot(transform, from, aim, _yawAngle, kind,
                _state.StatValueFor(TruckStat.TurretRange, kind), damage, _state.StatValueFor(TruckStat.TurretArea, kind), out Vector3 to);

            if (Online) LastTruck.Networking.NetworkTruck.BroadcastAutoTurretShot(from, to);
            else ShowShot(from, to);
        }

        private bool IsMortar
        {
            get
            {
                string autoId = _state != null ? _state.PartAt(TruckPartSlot.Bed) : null;
                return autoId != null && autoId == TruckUpgradeRules.AutoPrefix + ItemIds.MortarTurret;
            }
        }

        /// <summary>포탄이 떨어질 때 목표가 있을 발밑 위치 (움직이는 속도만큼 앞을 노린다).</summary>
        private static Vector3 PredictFeet(Combat.Damageable target, Vector3 from)
        {
            Vector3 at = target.transform.position;
            Vector3 vel = Vector3.zero;
            var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) vel = agent.velocity;
            else if (target.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) vel = rb.linearVelocity;
            vel.y = 0f;
            // 비행 시간은 착탄 지점 거리로 정해지므로 두 번 맞춰 본다
            Vector3 land = at + vel * MortarArc.FlightTime(from, at);
            land = at + vel * MortarArc.FlightTime(from, land);
            return land;
        }

        /// <summary>탄도/폭발 연출 (모든 컴퓨터). 수동 포탑과 같은 종류별 연출을 쓴다.</summary>
        public void ShowShot(Vector3 from, Vector3 to)
        {
            if (_manual == null || _state == null || !_state.HasAutoTurret) return;
            string autoId = _state.PartAt(TruckPartSlot.Bed);
            string baseId = autoId.StartsWith(TruckUpgradeRules.AutoPrefix) ? autoId.Substring(TruckUpgradeRules.AutoPrefix.Length) : autoId;
            _manual.ShowShotFor(baseId, _state.StatValueFor(TruckStat.TurretArea, _state.AutoTurret), from, to);
        }

        // ───── 도형 / 재질 ─────

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

        private static Material Lit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        private static Material Glow(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }
    }
}
