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
        private void OnDisable() { if (_state != null) _state.Changed -= Refresh; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

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
            if (has && _root == null) Build();
            if (_root != null) _root.gameObject.SetActive(has);
            if (!has) LocalGunning = false;
        }

        private void Build()
        {
            // 트럭 몸체(바퀴/트리거 제외) 위쪽 가운데에 올린다
            Bounds b = new Bounds(transform.position, Vector3.zero); bool any = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            Vector3 top = any ? new Vector3(b.center.x, b.max.y, b.center.z) : transform.position + Vector3.up * 2.5f;

            _root = new GameObject("TruckTurret_Model").transform;
            _root.SetParent(transform, false);
            _root.position = top;
            _root.localRotation = Quaternion.identity;

            var dark = MakeMat(new Color(0.22f, 0.24f, 0.26f));
            var olive = MakeMat(new Color(0.36f, 0.42f, 0.28f));

            Prim(PrimitiveType.Cylinder, _root, new Vector3(0f, 0.12f, 0f), new Vector3(1.1f, 0.12f, 1.1f), dark);
            _yawPivot = new GameObject("Yaw").transform;
            _yawPivot.SetParent(_root, false);
            _yawPivot.localPosition = new Vector3(0f, 0.3f, 0f);
            Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 1.0f), olive);
            Prim(PrimitiveType.Cube, _yawPivot, new Vector3(0f, 0.25f, -0.55f), new Vector3(0.7f, 0.4f, 0.2f), dark);
            var barrel = Prim(PrimitiveType.Cylinder, _yawPivot, new Vector3(0f, 0.3f, 0.95f), new Vector3(0.18f, 0.55f, 0.18f), dark);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_yawPivot, false);
            _muzzle.localPosition = new Vector3(0f, 0.3f, 1.55f);
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
                _yaw = Mathf.MoveTowardsAngle(_yaw, LastTruck.Networking.NetworkTruck.Instance.TurretYaw, 540f * Time.deltaTime);
            _yawPivot.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        // ───── 사수 (이 컴퓨터) ─────

        /// <summary>사수가 아닐 때 부른다 (안내 숨김).</summary>
        public void StopLocalGunner() => LocalGunning = false;

        /// <summary>사수인 동안 매 프레임 부른다: 마우스 조준, 좌클릭 발사, R 탄환 만들기.</summary>
        public void LocalGunnerUpdate()
        {
            if (_state == null || !_state.HasTurret) { LocalGunning = false; return; }
            LocalGunning = true;

            bool popup = LastTruck.Networking.SystemUI.Instance != null && LastTruck.Networking.SystemUI.Instance.IsPopupVisible;
            Camera cam = Camera.main;
            if (cam != null && !popup)
            {
                _lastAim = AimPoint(cam);
                Vector3 d = _lastAim - MuzzlePosition; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }

            if (Online && Time.time >= _nextAimSend)
            {
                _nextAimSend = Time.time + 0.1f;
                LastTruck.Networking.NetworkTruck.SendTurretAim(_yaw);
            }

            if (popup) return;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (Input.GetMouseButton(0) && !overUI && Time.time >= _nextLocalShot)
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

        private Vector3 AimPoint(Camera cam)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float range = _state.StatValue(TruckStat.TurretRange);
            float best = float.MaxValue; Vector3 point = Vector3.zero; bool hit = false;
            foreach (var h in Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < best) { best = h.distance; point = h.point; hit = true; }
            }
            if (!hit)
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
                point = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : MuzzlePosition + transform.forward * range;
            }
            return point;
        }

        public float CurrentRate()
        {
            float rate = _state != null ? _state.StatValue(TruckStat.TurretRate) : 1.5f;
            if (_info != null && _info.MaxDurability > 0f && _info.CurrentDurability / _info.MaxDurability < DamagedThreshold) rate *= DamagedRatePenalty;
            return Mathf.Max(0.1f, rate);
        }

        public int AmmoCount()
        {
            var inv = Inventory; if (inv == null) return 0;
            var catalog = ItemCatalog.GetOrCreate();
            int n = 0;
            foreach (var a in TruckUpgradeRules.AmmoTypes) { var it = catalog.GetItem(a.id); if (it != null) n += inv.GetItemCount(it); }
            return n;
        }

        // ───── 판정 (싱글/호스트) ─────

        /// <summary>한 발 쏜다. 탄환을 쓰고 피해를 준다. 실패하면 이유를 message로.</summary>
        public bool HostFire(Vector3 aimPoint, out string message)
        {
            message = null;
            if (_state == null || !_state.HasTurret) { message = "미니 포탑이 없어요."; return false; }
            // 요청이 너무 잦으면 무시 (네트워크 지연을 감안해 20% 여유)
            if (Time.time - _lastHostShot < 0.8f / CurrentRate()) return false;

            var inv = Inventory; var catalog = ItemCatalog.GetOrCreate();
            int ammoDamage = -1;
            if (inv != null)
            {
                foreach (var a in TruckUpgradeRules.AmmoTypes)
                {
                    var it = catalog.GetItem(a.id);
                    if (it != null && inv.GetItemCount(it) > 0 && inv.RemoveItem(it, 1)) { ammoDamage = a.damage; break; }
                }
            }
            if (ammoDamage < 0) { message = "탄환이 없어요. R: 트럭 보관함의 나무/돌/광석으로 탄환 만들기"; return false; }
            _lastHostShot = Time.time;

            Vector3 from = MuzzlePosition;
            Vector3 dir = aimPoint - from;
            if (dir.sqrMagnitude < 0.01f) dir = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            dir.Normalize();
            float range = _state.StatValue(TruckStat.TurretRange);
            Vector3 to = from + dir * Mathf.Min(range, Vector3.Distance(from, aimPoint) + 0.5f);

            float bestDist = float.MaxValue;
            foreach (var h in Physics.SphereCastAll(from, 0.25f, dir, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || IsPlayer(h.collider)) continue;
                if (h.distance < bestDist) { bestDist = h.distance; to = h.distance > 0f ? h.point : h.collider.ClosestPoint(from); }
            }

            int damage = Mathf.RoundToInt(_state.StatValue(TruckStat.TurretAttack) + ammoDamage);
            float area = _state.StatValue(TruckStat.TurretArea);
            var done = new System.Collections.Generic.HashSet<Combat.Damageable>();
            foreach (var col in Physics.OverlapSphere(to, area, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(transform) || IsPlayer(col)) continue;
                var target = col.GetComponentInParent<Combat.Damageable>();
                if (target == null || !done.Add(target)) continue;
                target.TakeDamage(damage, col.ClosestPoint(to), null);
            }

            if (Online) LastTruck.Networking.NetworkTruck.BroadcastTurretShot(from, to);
            else ShowShot(from, to);
            return true;
        }

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

        public void ShowShot(Vector3 from, Vector3 to)
        {
            if (_tracerMat == null)
            {
                var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _tracerMat = new Material(sh);
            }
            var go = new GameObject("TurretTracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _tracerMat;
            lr.positionCount = 2;
            lr.SetPosition(0, from); lr.SetPosition(1, to);
            lr.startWidth = 0.12f; lr.endWidth = 0.05f;
            lr.startColor = new Color(1f, 0.85f, 0.4f); lr.endColor = new Color(1f, 0.5f, 0.1f, 0.6f);
            Destroy(go, 0.07f);

            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flash.GetComponent<Collider>());
            flash.transform.position = to;
            float area = _state != null ? _state.StatValue(TruckStat.TurretArea) : 3f;
            flash.transform.localScale = Vector3.one * Mathf.Min(area, 2.5f);
            flash.GetComponent<Renderer>().sharedMaterial = _tracerMat;
            Destroy(flash, 0.08f);
        }

        private void OnGUI()
        {
            if (!LocalGunning) return;
            string text = $"포탑 사수   마우스: 조준   좌클릭: 발사   R: 탄환 만들기   탄환 {AmmoCount()}발   공격 {_state.StatValue(TruckStat.TurretAttack):0} · {CurrentRate():0.0}/초 · 사거리 {_state.StatValue(TruckStat.TurretRange):0}m"
                          + (Online ? "" : "   T: 운전석으로   E: 내리기");
            var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(14, Screen.height / 45), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            Vector2 size = style.CalcSize(new GUIContent(text)) + new Vector2(24f, 12f);
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, Screen.height - size.y * 2f - 32f, size.x, size.y), text, style);
        }
    }
}
