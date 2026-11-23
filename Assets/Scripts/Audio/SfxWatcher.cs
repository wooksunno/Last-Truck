using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace LastTruck.Audio
{
    /// <summary>
    /// 게임 코드를 여기저기 고치지 않고 "상태 변화"를 지켜보다가 효과음을 낸다 (모든 컴퓨터에서 각자).
    ///  - 버튼 클릭 (UI Toolkit 버튼 + 메뉴의 UGUI 버튼)
    ///  - 트럭/배낭/제작 창 열기·닫기
    ///  - 내 가방에 아이템이 늘어남 (줍기 / 창 안에서는 제작·꺼내기)
    ///  - 핫바 칸 바꾸기 (칼 → 칼 뽑는 소리, 도구 → 허리띠 소리, 그 외 → 딸깍)
    ///  - 트럭이 맞음 / 수리됨
    ///  - 낮↔밤 바뀜 (종소리)
    ///  - 모든 플레이어 발소리 (움직인 거리로 판단, 트럭에 탄 동안은 안 남)
    /// 전투 타격·채집·휘두르기·임무 카드 소리는 해당 코드에서 직접 부른다 (Damageable / WeaponController / MissionHud).
    /// </summary>
    public class SfxWatcher : MonoBehaviour
    {
        private static SfxWatcher _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("SfxWatcher");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SfxWatcher>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private void Update()
        {
            WatchUguiClicks();
            if (Time.unscaledTime >= _nextDocScan) { _nextDocScan = Time.unscaledTime + 1f; HookUiDocuments(); }
            WatchLocalPlayer();
            WatchPhase();
            WatchTruck();
            WatchFootsteps();
        }

        // ───────────────────────── 버튼 클릭 ─────────────────────────

        private float _nextDocScan;
        private readonly HashSet<VisualElement> _hookedRoots = new HashSet<VisualElement>();
        private static readonly List<RaycastResult> _hits = new List<RaycastResult>();

        private void HookUiDocuments()
        {
            _hookedRoots.RemoveWhere(r => r == null || r.panel == null);
            foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                VisualElement root = doc != null ? doc.rootVisualElement : null;
                if (root == null || _hookedRoots.Contains(root)) continue;
                root.RegisterCallback<ClickEvent>(OnToolkitClick, TrickleDown.TrickleDown);
                _hookedRoots.Add(root);
            }
        }

        private static void OnToolkitClick(ClickEvent evt)
        {
            for (VisualElement e = evt.target as VisualElement; e != null; e = e.parent)
            {
                if (e is Button || e is Toggle || e is Foldout) { Sfx.Play("ui_click"); return; }
            }
        }

        // 메뉴(UGUI): 누른 곳 아래에 켜진 버튼이 있으면 딸깍
        private static void WatchUguiClicks()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            EventSystem es = EventSystem.current;
            if (es == null) return;
            var data = new PointerEventData(es) { position = Input.mousePosition };
            _hits.Clear();
            es.RaycastAll(data, _hits);
            foreach (RaycastResult r in _hits)
            {
                if (r.gameObject == null) continue;
                var sel = r.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>();
                if (sel != null && sel.IsInteractable()) { Sfx.Play("ui_click"); return; }
                break;   // 가장 위에 있는 것만 본다
            }
        }

        // ───────────────────────── 내 캐릭터 ─────────────────────────

        private PlayerInventory _inv;
        private PlayerMove _move;
        private bool _uiWasOpen;
        private int _lastCount = -1;
        private int _lastSlot = -1;
        private float _ignoreCountUntil;

        private void WatchLocalPlayer()
        {
            PlayerInventory inv = LastTruck.Networking.TruckInventorySync.LocalPlayerInventory;
            if (inv != _inv)
            {
                _inv = inv;
                _move = inv != null ? inv.GetComponent<PlayerMove>() : null;
                _lastCount = -1;
                _lastSlot = inv != null ? inv.SelectedSlotIndex : -1;
                _ignoreCountUntil = Time.unscaledTime + 2f;   // 처음 받은 시작 아이템에는 소리 X
            }
            if (_inv == null) return;

            // 창 열기/닫기
            bool open = _move != null && _move.IsUIOpen;
            if (open != _uiWasOpen)
            {
                _uiWasOpen = open;
                Sfx.Play(open ? "ui_open" : "ui_close");
            }

            // 가방 개수가 늘었다
            int count = 0;
            foreach (InventorySlot s in _inv.Slots)
                if (s != null && s.item != null) count += s.count;
            if (_lastCount >= 0 && count > _lastCount && Time.unscaledTime >= _ignoreCountUntil)
                Sfx.Play(open ? "craft" : "pickup", 1f, 1f, 0.08f);
            _lastCount = count;

            // 핫바 칸 바꾸기
            int slot = _inv.SelectedSlotIndex;
            if (slot != _lastSlot)
            {
                _lastSlot = slot;
                ItemData item = _inv.SelectedItem;
                if (item == null) Sfx.Play("ui_switch", 0.8f);
                else if (TruckCodexView.IsWeaponItem(item) || item.isWeapon) Sfx.Play(IsGun(item) ? "equip_tool" : "equip_blade");
                else if (item.toolTier > 0) Sfx.Play("equip_tool");
                else Sfx.Play("ui_switch");
            }
        }

        private static bool IsGun(ItemData item)
        {
            switch (item.itemID)
            {
                case ItemIds.Pistol: case ItemIds.Ak47: case ItemIds.Shotgun: case ItemIds.PlatinumSniperRifle:
                case ItemIds.Flamethrower: case ItemIds.ChemicalSprayer: case ItemIds.ShredderDrillLauncher:
                case ItemIds.IronFieldCannon: case ItemIds.PlatinumRailCannon:
                    return true;
            }
            return false;
        }

        // ───────────────────────── 낮/밤 ─────────────────────────

        private GameManager _gm;
        private int _lastPhase = -1;

        private void WatchPhase()
        {
            GameManager gm = GameManager.Instance;
            if (gm != _gm) { _gm = gm; _lastPhase = -1; }
            if (gm == null || gm.IsWaitingForNetworkStart) return;
            int phase = (int)gm.CurrentPhase;
            if (_lastPhase >= 0 && phase != _lastPhase)
            {
                // 밤: 낮고 무거운 종 / 아침: 높고 맑은 종
                if (gm.CurrentPhase == GameManager.GamePhase.Night) Sfx.Play("bell", 1f, 0.72f, 0.5f);
                else Sfx.Play("bell", 0.7f, 1.35f, 0.5f);
            }
            _lastPhase = phase;
        }

        // ───────────────────────── 트럭 ─────────────────────────

        private TruckInformation _truck;
        private float _truckHp = -1f;
        private float _nextTruckFind;

        private void WatchTruck()
        {
            if (_truck == null)
            {
                _truckHp = -1f;
                if (Time.unscaledTime < _nextTruckFind) return;
                _nextTruckFind = Time.unscaledTime + 2f;
                _truck = FindFirstObjectByType<TruckInformation>();
                if (_truck == null) return;
            }
            float hp = _truck.CurrentDurability;
            if (_truckHp >= 0f)
            {
                // 밤에 여러 마리가 동시에 때리면 쉴 새 없이 울려서, 0.45초에 한 번까지만
                if (hp < _truckHp - 0.01f) Sfx.PlayAt("truck_hit", _truck.transform.position, Mathf.Clamp(0.55f + (_truckHp - hp) / 20f, 0.55f, 1f), 1f, 0.45f);
                else if (hp > _truckHp + 0.5f) Sfx.PlayAt("truck_repair", _truck.transform.position, 1f, 1f, 0.2f);
            }
            _truckHp = hp;
        }

        // ───────────────────────── 발소리 ─────────────────────────

        private class Walker { public Vector3 last; public float dist; public bool init; }
        private readonly Dictionary<Transform, Walker> _walkers = new Dictionary<Transform, Walker>();
        private readonly List<Transform> _gone = new List<Transform>();

        private void WatchFootsteps()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            foreach (LastTruck.Networking.NetworkPlayer p in LastTruck.Networking.NetworkPlayer.All)
            {
                if (p == null) continue;
                Transform t = p.transform;
                if (!_walkers.TryGetValue(t, out Walker w)) { w = new Walker(); _walkers[t] = w; }
                Vector3 pos = t.position;
                if (!w.init) { w.last = pos; w.init = true; continue; }

                Vector3 d = pos - w.last;
                w.last = pos;
                float flat = new Vector2(d.x, d.z).magnitude;
                float speed = flat / dt;
                // 트럭에 타서 실려 가거나 순간이동한 건 걸음이 아니다
                if (speed < 1.2f || speed > 11.5f || Mathf.Abs(d.y) / dt > 6f) { w.dist = Mathf.Min(w.dist, 0.4f); continue; }

                w.dist += flat;
                float stride = speed > 8f ? 1.9f : 1.45f;   // 달리면 보폭이 길다
                if (w.dist >= stride)
                {
                    w.dist = 0f;
                    Sfx.PlayAt("footstep", pos, speed > 8f ? 1f : 0.8f, 1f, 0f);
                }
            }

            _gone.Clear();
            foreach (var kv in _walkers) if (kv.Key == null) _gone.Add(kv.Key);
            foreach (Transform t in _gone) _walkers.Remove(t);
        }
    }
}
