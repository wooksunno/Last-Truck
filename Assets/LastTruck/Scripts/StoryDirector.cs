using CraftingSystem;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 탈출 서사. "이 구역을 벗어날 탈출 게이트는 7일째 낮에 열린다. 그때까지 부품 3종(엔진 코어/연료 탱크/신호기)을 만들어 트럭에 싣고 버텨라."
    ///
    ///  - 낮/밤이 바뀔 때마다 무전·일지 대사를 HUD 배너에 띄운다 (BeatFor) + 목표 줄(GoalLine: 부품 n/3, 게이트 개방까지, 방향/거리).
    ///  - 탈출 조건: 게이트가 열렸고(EscapeDay일째 낮 이후) + 트럭이 멀쩡하고 + 부품 3종이 트럭 보관함에 있고 + 트럭이 게이트 구역에 도착.
    ///  - 싱글플레이는 여기서 바로 엔딩을 띄우고, 멀티플레이는 호스트의 NetworkGameState가 이 조건(EscapeConditionMet)을 보고
    ///    모두에게 같은 "탈출 성공"을 알린다.
    /// 맵에 EscapeGate 오브젝트가 없으면 탈출 목표 없이 대사만 나온다 (기존처럼 무한 반복).
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        public const int EscapeDay = 7;   // 이 날 낮에 게이트가 열린다

        private static readonly string[] PartIds = { ItemIds.EscapeEngineCore, ItemIds.EscapeFuelTank, ItemIds.EscapeBeacon };
        private static readonly string[] PartNames = { "엔진 코어", "연료 탱크", "신호기" };

        private static StoryDirector _instance;
        private TruckInformation _truck;
        private TruckInventory _storage;
        private float _nextCheck;
        private bool _escapedShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("StoryDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<StoryDirector>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // 멀티플레이는 호스트의 NetworkGameState가 판정한다. 싱글플레이만 여기서 바로 엔딩.
            if (_escapedShown || Networking.GameLauncher.IsOnlineSession) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.4f;

            if (EscapeConditionMet())
            {
                _escapedShown = true;
                ShowEnding(true);
            }
        }

        // ---------------------------------------------------------------- 탈출 조건

        /// <summary>탈출 게이트가 열렸는가 (EscapeDay일째 낮부터).</summary>
        public static bool GateIsOpen
        {
            get
            {
                GameManager gm = GameManager.Instance;
                return gm != null && !gm.IsWaitingForNetworkStart && gm.CurrentCycle >= EscapeDay;
            }
        }

        /// <summary>탈출 작전을 보여 줄 때인가: 모든 생존 조건을 돌파한 뒤(EscapeDay일째 낮부터). 그전에는 임무 UI에 생존 조건만 나온다.</summary>
        public static bool EscapeVisible
        {
            get { return EscapeGate.Instance != null && GateIsOpen; }
        }

        /// <summary>트럭 보관함에 있는 탈출 부품 종류 수 (0~3).</summary>
        public static int PartsHave()
        {
            if (_instance == null) return 0;
            TruckInventory storage = _instance.Storage();
            if (storage == null) return 0;

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            int have = 0;
            foreach (string id in PartIds)
            {
                ItemData item = catalog != null ? catalog.GetItem(id) : null;
                if (item != null && storage.GetItemCount(item) > 0) have++;
            }
            return have;
        }

        // ---------------------------------------------------------------- 임무 UI(MissionHud)가 쓰는 조회

        public static int PartCount => PartIds.Length;
        public static string PartName(int i) => PartNames[i];

        /// <summary>i번째 부품이 트럭 보관함에 있는가.</summary>
        public static bool PartOwned(int i)
        {
            if (_instance == null) return false;
            TruckInventory storage = _instance.Storage();
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            ItemData item = catalog != null ? catalog.GetItem(PartIds[i]) : null;
            return storage != null && item != null && storage.GetItemCount(item) > 0;
        }

        public static Sprite PartIcon(int i)
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            ItemData item = catalog != null ? catalog.GetItem(PartIds[i]) : null;
            return item != null ? item.icon : null;
        }

        /// <summary>트럭(없으면 카메라)에서 게이트까지의 거리(m)와 방위(도, +Z=북, 시계방향). 게이트가 없으면 false.</summary>
        public static bool TryGateBearing(out float distance, out float bearingDegrees)
        {
            distance = 0f; bearingDegrees = 0f;
            EscapeGate gate = EscapeGate.Instance;
            if (gate == null || _instance == null) return false;

            TruckInformation truck = _instance.Truck();
            Vector3 from = truck != null ? truck.transform.position : (Camera.main != null ? Camera.main.transform.position : gate.transform.position);
            Vector3 d = gate.transform.position - from;
            distance = new Vector2(d.x, d.z).magnitude;
            bearingDegrees = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>어떤 부품이 모자란지 (예: "연료 탱크, 신호기"). 다 있으면 빈 문자열.</summary>
        public static string MissingParts()
        {
            if (_instance == null) return "";
            TruckInventory storage = _instance.Storage();
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            var missing = new System.Collections.Generic.List<string>();
            for (int i = 0; i < PartIds.Length; i++)
            {
                ItemData item = catalog != null ? catalog.GetItem(PartIds[i]) : null;
                if (item == null || storage == null || storage.GetItemCount(item) <= 0) missing.Add(PartNames[i]);
            }
            return string.Join(", ", missing);
        }

        /// <summary>탈출 성공 조건: 게이트 열림 + 트럭 멀쩡 + 부품 3종 + 트럭이 게이트 구역 안. (싱글/호스트가 부른다)</summary>
        public static bool EscapeConditionMet()
        {
            if (_instance == null || EscapeGate.Instance == null || !GateIsOpen) return false;
            TruckInformation truck = _instance.Truck();
            if (truck == null || truck.IsBroken) return false;
            if (HorizontalDistance(truck.transform.position, EscapeGate.Instance.transform.position) > EscapeGate.Radius) return false;
            return PartsHave() >= PartIds.Length;
        }

        private TruckInformation Truck()
        {
            if (_truck == null) _truck = FindFirstObjectByType<TruckInformation>();
            return _truck;
        }

        private TruckInventory Storage()
        {
            if (_storage == null) _storage = FindFirstObjectByType<TruckInventory>();
            return _storage;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ---------------------------------------------------------------- 엔딩

        /// <summary>탈출 성공 화면. 싱글플레이면 Enter로 이어서 플레이할 수 있다.</summary>
        public static void ShowEnding(bool singlePlayer)
        {
            GameManager gm = GameManager.Instance;
            string body = EpilogueText(gm != null ? gm.CurrentCycle : EscapeDay);
            if (singlePlayer)
                LastTruckHud.ShowEnding("탈출 성공!", body, "Enter 키를 눌러 계속 플레이");
            else
                Networking.SystemUI.Alert("탈출 성공!", body, () =>
                {
                    Networking.GameLauncher current = Networking.GameLauncher.Instance;
                    if (current != null) current.LeaveAfterGameOver();
                }, "방 목록으로");
        }

        public static string EpilogueText(int cycle)
        {
            return cycle + "일째, 마지막 트럭이 빛의 문을 넘었습니다.\n\n"
                 + "[무전] …치직… 관제소다. 신호 확인. 살아 있었구나.\n"
                 + "[무전] 부품을 싣고 이 지옥을 뚫고 온 건 너희가 처음이다. 환영한다, 생존자들.";
        }

        // ---------------------------------------------------------------- 대사 / 목표

        /// <summary>낮/밤이 시작될 때 띄우는 무전·일지 대사. 없으면 null.</summary>
        public static string BeatFor(GameManager gm)
        {
            if (gm == null) return null;
            bool night = gm.CurrentPhase == GameManager.GamePhase.Night;
            int c = gm.CurrentCycle;
            bool hasGate = EscapeGate.Instance != null;

            // 탈출 작전은 모든 생존 조건을 돌파한 뒤(EscapeDay일째)에야 알려 준다. 그전에는 밤마다 주어지는 생존 조건만 이야기한다.
            SurvivalKind kind = SurvivalDirector.KindFor(c);
            string boss = SurvivalDirector.HasBossWave(c) ? " 중간보스까지 섞여 나온다!" : "";

            if (!night)
            {
                if (hasGate && c == EscapeDay)
                    return PartsHave() >= PartIds.Length
                        ? "[무전] 모든 밤을 버텼구나! 탈출 게이트가 열렸다. 부품은 다 실었지? 트럭을 몰아 게이트로 달려라!"
                        : "[무전] 모든 밤을 버텼구나! 탈출 게이트가 열렸다. 하지만 부품이 모자라… 서둘러 만들어라! (부족: " + MissingParts() + ")";
                if (hasGate && c > EscapeDay)
                    return "[무전] 게이트는 아직 열려 있다. 부품을 싣고 달려라!";

                if (c == 1)
                    return "[무전] …치직… 생존자 있나? 날이 저물면 놈들이 몰려온다. 오늘 밤 임무는 '" + SurvivalDirector.Title(kind) + "'. " + SurvivalDirector.Tip(kind);
                return "[일지] " + c + "일째 낮. 오늘 밤 생존 조건은 '" + SurvivalDirector.Title(kind) + "'. " + SurvivalDirector.Tip(kind) + boss;
            }

            return "[무전] 조건 확인: " + SurvivalDirector.Title(kind) + ". " + SurvivalDirector.Detail(kind) + boss;
        }

        /// <summary>탈출 목표 한 줄 (부품 n/3 + 게이트 개방/방향/거리). 게이트가 없으면 null.</summary>
        public static string GoalLine(GameManager gm)
        {
            if (gm == null || EscapeGate.Instance == null) return null;

            int have = PartsHave();
            string parts = "탈출 부품 " + have + "/" + PartIds.Length;
            if (have < PartIds.Length)
            {
                string missing = MissingParts();
                if (!string.IsNullOrEmpty(missing)) parts += " (부족: " + missing + ")";
            }

            if (!GateIsOpen)
            {
                int daysLeft = EscapeDay - gm.CurrentCycle;
                bool night = gm.CurrentPhase == GameManager.GamePhase.Night;
                string when = night && daysLeft <= 1 ? "이 밤이 지나면 게이트가 열려요" : "게이트 개방까지 " + (night ? daysLeft : daysLeft) + "일";
                return when + "  ·  " + parts;
            }

            string where = "";
            if (_instance != null)
            {
                Transform from = _instance.Truck() != null ? _instance.Truck().transform : null;
                if (from != null)
                    where = "  ·  트럭에서 " + Compass(from.position, EscapeGate.Instance.transform.position) + " "
                          + Mathf.RoundToInt(HorizontalDistance(from.position, EscapeGate.Instance.transform.position)) + "m";
            }
            return "게이트 개방! 트럭을 게이트로" + where + "  ·  " + parts;
        }

        private static readonly string[] Dirs = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };

        private static string Compass(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;   // +Z가 북, +X가 동
            int idx = Mathf.RoundToInt(angle / 45f);
            idx = ((idx % 8) + 8) % 8;
            return Dirs[idx] + "쪽";
        }
    }
}
