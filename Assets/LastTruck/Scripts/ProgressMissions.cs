using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 성장 임무: "임무를 하나씩 깨면서 장비를 점점 좋게 만든다"는 흐름을 잡아 주는 순서 있는 임무 줄.
    ///
    ///  - 밤마다 주어지는 생존 조건(SurvivalDirector)과 별개로, 낮 동안 무엇을 해야 할지 알려 준다
    ///    (나무 모으기 → 곡괭이 → 무기 → 모닥불 → 구리 → 갑옷 → 철 → 포탑 → 총 → 탈출 부품).
    ///  - 임무를 달성하면 보상이 전부 트럭 보관함에 들어간다 (트럭이 꽉 차면 넘친 만큼만 내 가방/발밑). 보상에는 소모품과 "무기 강화 키트"가 섞여 있다.
    ///  - 화면에는 도전과제처럼 보여 준다 (UI.AchievementHud): 달성 순간 위에서 내려오는 알림, 트럭 창의 도전과제 탭(목록).
    ///    HUD 스킨이 없을 때만 아래 OnGUI의 간단한 표시로 대신한다.
    ///
    /// 조건은 0.5초마다 훑어 본다. "아이템 가지기"는 가방 + 트럭 보관함을 합쳐 세고, 한 번 채운 뒤 써 버려도 달성으로 남는다.
    /// 진행은 각자 화면에서 따로 센다 (멀티플레이에서 동기화하지 않는다).
    /// </summary>
    public class ProgressMissions : MonoBehaviour
    {
        private struct Reward { public string id; public int count; }

        private class Mission
        {
            public string title, hint;
            public Func<ProgressMissions, int> current;
            public int goal;
            public Reward[] rewards;
        }

        private static Reward R(string id, int count) => new Reward { id = id, count = count };

        private static Mission Have(string title, string hint, string itemId, int goal, params Reward[] rewards) =>
            new Mission { title = title, hint = hint, goal = goal, rewards = rewards, current = p => p.Seen(itemId) };

        private static Mission HaveAny(string title, string hint, string[] itemIds, params Reward[] rewards) =>
            new Mission
            {
                title = title, hint = hint, goal = 1, rewards = rewards,
                current = p => { foreach (string id in itemIds) if (p.Seen(id) > 0) return 1; return 0; },
            };

        private static readonly Mission[] Missions =
        {
            // 초반 길잡이: 처음 하는 사람도 조합식을 몰라도 따라갈 수 있게 재료를 안내에 그대로 적는다 (트럭 옆 Tab → 제작/가공)
            Have("나무 10개 모으기", "나무에 다가가 E를 꾹 누르세요", ItemIds.Wood, 10, R(ItemIds.Stone, 6)),
            Have("나무 곡괭이 만들기", "트럭 옆에서 Tab → 제작 → 도구: 나무 6개", ItemIds.WoodPickaxe, 1, R(ItemIds.HerbBandage, 2)),
            Have("돌 8개 캐기", "곡괭이를 손에 들고 바위 앞에서 E 꾹", ItemIds.Stone, 8, R(ItemIds.ThrowingStone, 4)),
            HaveAny("첫 무기 만들기", "Tab → 제작 → 무기: 나무창 / 돌도끼 등 (만들면 가방으로 들어와요)",
                new[] { ItemIds.WoodSpear, ItemIds.StoneAxe, ItemIds.CopperDagger, ItemIds.Machete, ItemIds.HuntingBow },
                R(ItemIds.ThrowingStone, 6)),
            new Mission
            {
                title = "모닥불 설치하기", hint = "Tab → 제작 → 설비: 나무 15 + 돌 3. 굽기/제련을 할 수 있어요", goal = 1,
                current = p => p.FacilityBuilt(FacilityType.Campfire) ? 1 : 0,
                rewards = new[] { R(ItemIds.Fish, 2), R(ItemIds.Wood, 10) },
            },
            Have("돌 곡괭이 만들기", "Tab → 제작 → 도구: 나무 6 + 돌 5 (구리 광맥을 캘 수 있어요)", ItemIds.StonePickaxe, 1, R(ItemIds.WoodSpikeTrap, 2)),
            Have("구리 원석 6개 캐기", "붉은 결정이 구리 광맥이에요 (돌 곡괭이로 E 꾹)", ItemIds.CopperOre, 6, R(ItemIds.Charcoal, 4)),
            Have("순수 구리 3개 제련", "Tab → 가공 → 모닥불: 구리 원석을 누르면 칸에 들어가요", ItemIds.Copper, 3, R(ItemIds.WeaponUpgradeKit, 1)),
            Have("구리 곡괭이 만들기", "숫돌 연마대(돌 10 + 나무 4) → 가공에서 구리 절삭날 → 제작에서 구리 곡괭이", ItemIds.CopperPickaxe, 1, R(ItemIds.ToolRepairKit, 2)),
            new Mission
            {
                title = "첫 번째 밤 버티기", hint = "밤의 생존 조건을 달성하면 아침이 와요 (K: 생존 임무)", goal = 1,
                current = p => p.Cycle() >= 2 ? 1 : 0,
                rewards = new[] { R(ItemIds.WeaponUpgradeKit, 1), R(ItemIds.Steak, 2) },
            },
            // (QA: 낮에 진행할 수 있는 임무를 먼저. "좀비 20마리"가 앞에 있으면 낮 내내 다음 안내가 막혔다)
            Have("철 원석 6개 캐기", "푸른빛 결정이 철 광맥이에요 (구리 곡괭이로 E 꾹)", ItemIds.IronOre, 6, R(ItemIds.WeaponUpgradeKit, 1)),
            HaveAny("철제 무기 만들기", "철 제련(모닥불) → 강화 철 날(숫돌) + 나무 손잡이(정밀 절단기) → 마체테/철도끼",
                new[] { ItemIds.Machete, ItemIds.IronAxe, ItemIds.BattleAxe, ItemIds.FlameMachete, ItemIds.VibrationBlade },
                R(ItemIds.Molotov, 3)),
            HaveAny("갑옷 입기", "가죽 갑옷: 무두질 건조대에서 경화 가죽 4 + 젖은 가죽 2 (젖은 가죽은 좀비가 떨어뜨려요)",
                new[] { ItemIds.LeatherArmor, ItemIds.IronPlateArmor, ItemIds.AlloyPlateArmor },
                R(ItemIds.Steak, 3)),
            new Mission
            {
                title = "좀비 20마리 처치", hint = "밤낮 가리지 않고 누적으로 세요", goal = 20,
                current = p => p._kills, rewards = new[] { R(ItemIds.HerbBandage, 3) },
            },
            new Mission
            {
                title = "트럭 강화 3번 하기", hint = "트럭 옆에서 Tab → 강화", goal = 3,
                current = p => p.TruckLevels(), rewards = new[] { R(ItemIds.EmergencyPatchBoard, 3) },
            },
            new Mission
            {
                title = "트럭에 포탑 달기", hint = "미니 포탑을 조립해서 지붕에 끼우세요", goal = 1,
                current = p => p.HasTurret() ? 1 : 0, rewards = new[] { R(ItemIds.IronRound, 20) },
            },
            new Mission
            {
                title = "좀비 100마리 처치", hint = "누적 처치 수", goal = 100,
                current = p => p._kills, rewards = new[] { R(ItemIds.WeaponUpgradeKit, 2) },
            },
            HaveAny("총 만들기", "권총 / 산탄총 / 석궁 / AK-47 중 하나",
                new[] { ItemIds.Pistol, ItemIds.Shotgun, ItemIds.Crossbow, ItemIds.Ak47, ItemIds.PlatinumSniperRifle },
                R(ItemIds.WeaponUpgradeKit, 1), R(ItemIds.FragGrenade, 3)),
            Have("금 원석 3개 캐기", "노란 결정이 금 광맥이에요 (철제 곡괭이 필요)", ItemIds.PlatinumOre, 3, R(ItemIds.WeaponUpgradeKit, 2)),
            new Mission
            {
                title = "다섯 번째 밤 버티기", hint = "밤이 깊어질수록 좀비가 강해져요", goal = 5,
                current = p => Mathf.Max(0, p.Cycle() - 1), rewards = new[] { R(ItemIds.VitalityTonic, 2), R(ItemIds.WeaponUpgradeKit, 1) },
            },
            new Mission
            {
                title = "탈출 부품 3종 싣기", hint = "엔진 코어 / 연료 탱크 / 신호기를 트럭 보관함에", goal = 3,
                current = p => StoryDirector.PartsHave(), rewards = new[] { R(ItemIds.WeaponUpgradeKit, 3) },
            },
        };

        // ---------------------------------------------------------------- 생명주기

        private static ProgressMissions _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("ProgressMissions");
            DontDestroyOnLoad(go);
            go.AddComponent<ProgressMissions>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; Completed = null; }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>몬스터 한 마리가 쓰러졌다 (MonsterLoot가 화면마다 한 번씩 부른다).</summary>
        public static void NotifyMonsterKilled()
        {
            if (_instance != null) _instance._kills++;
        }

        public static int CurrentIndex => _instance != null ? _instance._index : 0;
        public static int Count => Missions.Length;

        // ---------------------------------------------------------------- 화면(AchievementHud)이 쓰는 조회

        public struct RewardInfo { public ItemData item; public int count; }

        public struct Info
        {
            public string title, hint;
            public int goal, current;
            public bool done, isCurrent;
            public RewardInfo[] rewards;
        }

        /// <summary>게임 씬에서 진행 중인가 (로비/메뉴에서는 false).</summary>
        public static bool Active => _instance != null && _instance._started;

        /// <summary>도전과제를 달성한 순간 (번호). 화면 알림용.</summary>
        public static event Action<int> Completed;

        public static Info GetInfo(int index)
        {
            var info = new Info();
            if (index < 0 || index >= Missions.Length) return info;
            Mission m = Missions[index];
            int cur = _instance != null ? _instance._index : 0;
            info.title = m.title;
            info.hint = m.hint;
            info.goal = m.goal;
            info.done = index < cur;
            info.isCurrent = index == cur;
            info.current = info.done ? m.goal : (info.isCurrent && _instance != null ? _instance._shownCurrent : 0);

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            var list = new List<RewardInfo>();
            if (m.rewards != null && catalog != null)
                foreach (Reward r in m.rewards)
                {
                    ItemData item = catalog.GetItem(r.id);
                    if (item != null) list.Add(new RewardInfo { item = item, count = r.count });
                }
            info.rewards = list.ToArray();
            return info;
        }

        // ---------------------------------------------------------------- 상태

        private int _index;
        private GameManager _gameOf;   // 지금 진행 중인 게임 (바뀌면 처음부터)
        private int _kills;
        private readonly Dictionary<string, int> _seen = new Dictionary<string, int>();   // 아이템별로 지금까지 가져 본 최대 수
        private float _nextCheck;
        private int _shownCurrent;
        private float _flashUntil;
        private string _flashTitle, _flashReward;
        private bool _collapsed;
        private TruckInventory _truck;
        private bool _started;

        private void Update()
        {
            if (!LastTruckHud.IsReady && Input.GetKeyDown(KeyCode.L) && !PopupOpen()) _collapsed = !_collapsed;

            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;

            // 게임 씬(플레이어가 있는 곳)에서만 돈다. 로비/메뉴에서는 쉰다.
            GameObject player = GatherRules.Player;
            GameManager gm = GameManager.Instance;
            if (player == null || gm == null || gm.IsWaitingForNetworkStart) { _started = false; return; }
            _started = true;

            // 새 게임(게임 매니저가 바뀜)이면 도전과제를 처음부터 다시 (이 오브젝트는 게임이 끝나도 남는다)
            if (gm != _gameOf)
            {
                _gameOf = gm;
                _index = 0; _kills = 0; _shownCurrent = 0;
                _seen.Clear();
                _truck = null;
                _flashUntil = 0f;
            }

            if (_index >= Missions.Length) return;

            RefreshSeen(player);
            Mission m = Missions[_index];
            _shownCurrent = Mathf.Min(m.goal, m.current(this));
            if (_shownCurrent >= m.goal) Complete(m, player);
        }

        private void Complete(Mission m, GameObject player)
        {
            string reward = GiveRewards(m.rewards, player);
            _flashTitle = m.title;
            _flashReward = reward;
            _flashUntil = Time.unscaledTime + 4f;
            int done = _index;
            LastTruck.Audio.Sfx.Play("notify_done", 1f, 1f, 0.3f);     // 달성 알림
            LastTruck.Audio.Sfx.Play("quest_flip", 0.7f, 1.1f, 0.3f);  // + 다음 임무로 책장 넘김
            // HUD가 있으면 도전과제 알림이 뜬다. 없을 때만 아래쪽 토스트로 알린다
            if (!LastTruckHud.IsReady)
                HudToast.Show("도전과제 달성: " + m.title + (string.IsNullOrEmpty(reward) ? "" : "  →  보상 " + reward + " (트럭 보관함)"));
            _index++;
            Completed?.Invoke(done);
            _shownCurrent = 0;
            _nextCheck = 0f;   // 이미 조건을 채운 다음 임무는 바로 이어서 넘어간다
        }

        // ---------------------------------------------------------------- 조건 조회

        private TruckInventory Truck()
        {
            if (_truck == null) _truck = FindFirstObjectByType<TruckInventory>();
            return _truck;
        }

        /// <summary>지금 임무와 이후 임무가 보는 아이템들의 "가져 본 최대 수"를 갱신한다 (가방 + 트럭 보관함).</summary>
        private void RefreshSeen(GameObject player)
        {
            PlayerInventory bag = player.GetComponent<PlayerInventory>();
            TruckInventory truck = Truck();
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            if (catalog == null) return;

            foreach (string id in TrackedIds())
            {
                ItemData item = catalog.GetItem(id);
                if (item == null) continue;
                int have = (bag != null ? bag.GetItemCount(item) : 0) + (truck != null ? truck.GetItemCount(item) : 0)
                         + (bag != null && bag.Pouch != null ? bag.Pouch.GetItemCount(item) : 0);   // 배낭에 든 것도 센다
                if (!_seen.TryGetValue(id, out int best) || have > best) _seen[id] = have;
            }
        }

        private static string[] _tracked;

        private static string[] TrackedIds()
        {
            if (_tracked != null) return _tracked;
            _tracked = new[]
            {
                ItemIds.Wood, ItemIds.Stone, ItemIds.CopperOre, ItemIds.IronOre, ItemIds.PlatinumOre, ItemIds.Copper,
                ItemIds.WoodPickaxe, ItemIds.StonePickaxe, ItemIds.CopperPickaxe,
                ItemIds.WoodSpear, ItemIds.StoneAxe, ItemIds.CopperDagger, ItemIds.Machete, ItemIds.HuntingBow,
                ItemIds.IronAxe, ItemIds.BattleAxe, ItemIds.FlameMachete, ItemIds.VibrationBlade,
                ItemIds.LeatherArmor, ItemIds.IronPlateArmor, ItemIds.AlloyPlateArmor,
                ItemIds.Pistol, ItemIds.Shotgun, ItemIds.Crossbow, ItemIds.Ak47, ItemIds.PlatinumSniperRifle,
            };
            return _tracked;
        }

        private int Seen(string itemId) => _seen.TryGetValue(itemId, out int n) ? n : 0;

        private int Cycle() { GameManager gm = GameManager.Instance; return gm != null ? gm.CurrentCycle : 1; }

        private bool FacilityBuilt(FacilityType type)
        {
            TruckInventory truck = Truck();
            return truck != null && TruckFacilities.IsBuilt(truck, type);
        }

        private int TruckLevels()
        {
            TruckUpgradeState state = TruckUpgradeState.Instance;
            if (state == null) return 0;
            int sum = 0;
            foreach (int level in state.LevelsCopy()) sum += level;
            return sum;
        }

        private bool HasTurret()
        {
            TruckUpgradeState state = TruckUpgradeState.Instance;
            return state != null && state.AnyTurret;
        }

        // ---------------------------------------------------------------- 보상

        /// <summary>보상을 트럭 보관함에 넣는다 (멀티플레이는 호스트에게 요청). 트럭에 못 넣은 만큼은 내 가방, 넘치면 발밑.</summary>
        private string GiveRewards(Reward[] rewards, GameObject player)
        {
            if (rewards == null) return "";
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            PlayerInventory bag = player != null ? player.GetComponent<PlayerInventory>() : null;
            TruckInventory truck = Truck();
            var names = new List<string>();

            foreach (Reward r in rewards)
            {
                ItemData item = catalog != null ? catalog.GetItem(r.id) : null;
                if (item == null || r.count <= 0) continue;
                names.Add(item.itemName + " x" + r.count);

                // 보상은 종류와 상관없이 전부 트럭 보관함으로 (트럭이 꽉 차서 못 넣은 만큼만 내 가방/발밑)
                if (truck != null || Networking.GameLauncher.IsOnlineSession)
                {
                    Networking.TruckInventorySync.GrantItem(truck, item, r.count, bag);
                    continue;
                }
                if (bag != null) bag.AddOrDrop(item, r.count);
                else if (player != null) DroppedItem.SpawnAt(player.transform.position + player.transform.forward * 0.8f, item, r.count);
            }
            return string.Join(", ", names);
        }

        private static string RewardText(Reward[] rewards)
        {
            if (rewards == null) return "";
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            var names = new List<string>();
            foreach (Reward r in rewards)
            {
                ItemData item = catalog != null ? catalog.GetItem(r.id) : null;
                if (item != null) names.Add(item.itemName + " x" + r.count);
            }
            return string.Join(", ", names);
        }

        // ---------------------------------------------------------------- 화면 (왼쪽 임무 표시)

        private static Texture2D _px;
        private GUIStyle _small, _title, _body;
        private int _styleFont;

        private static bool PopupOpen() => GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;

        private static void Fill(Rect r, Color c)
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = prev;
        }

        private void EnsureStyles(float k)
        {
            int font = Mathf.Max(10, Mathf.RoundToInt(15f * k));
            if (_small != null && _styleFont == font) return;
            _styleFont = font;
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(font * 0.85f), wordWrap = true, richText = true };
            _body = new GUIStyle(GUI.skin.label) { fontSize = font, wordWrap = true, richText = true };
            _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(font * 1.25f), fontStyle = FontStyle.Bold, wordWrap = true };
        }

        private void OnGUI()
        {
            if (LastTruckHud.IsReady) return;   // 도전과제 HUD(UI Toolkit)가 대신 그린다
            if (!_started || Event.current.type != EventType.Repaint) return;
            if (PopupOpen() || Networking.NetworkGameState.IsGameOver) return;

            float k = Screen.height / 1080f;
            EnsureStyles(k);

            float w = 330f * k, pad = 12f * k;
            // 왼쪽 아래 (왼쪽 위에는 생존 임무 카드가 열린다). 아래쪽을 기준으로 맞춰서 높이가 달라도 바닥선이 같다
            float x = 24f * k, bottom = Screen.height - 36f * k;
            bool flashNow = Time.unscaledTime < _flashUntil;
            float panelH = (_collapsed && !flashNow ? 30f : flashNow ? 86f : _index >= Missions.Length ? 62f : 150f) * k;
            float y = bottom - panelH;
            Color gold = new Color(1f, 0.8f, 0.34f), cream = new Color(0.97f, 0.93f, 0.82f), green = new Color(0.4f, 1f, 0.62f);
            Color panel = new Color(0.09f, 0.11f, 0.18f, 0.9f);   // 남색 테마

            bool allDone = _index >= Missions.Length;
            bool flashing = Time.unscaledTime < _flashUntil;

            // 접힌 상태: 한 줄만
            if (_collapsed && !flashing)
            {
                var bar = new Rect(x, y, w, 30f * k);
                Fill(bar, panel);
                Fill(new Rect(x, y, 4f * k, bar.height), gold);
                _small.normal.textColor = cream;
                string line = allDone ? "성장 임무 완료" : "성장 임무 " + (_index + 1) + "/" + Missions.Length + "  " + Missions[_index].title;
                GUI.Label(new Rect(x + pad, y + 5f * k, w - pad * 2f, 22f * k), line + "   <color=#9a8f7c>[L]</color>", _small);
                return;
            }

            if (flashing)
            {
                // 방금 달성: 초록 띠 + 보상
                float h = 86f * k;
                var box = new Rect(x, y, w, h);
                Fill(box, new Color(0.1f, 0.22f, 0.13f, 0.92f));
                Fill(new Rect(x, y, 4f * k, h), green);
                _small.normal.textColor = green;
                GUI.Label(new Rect(x + pad, y + 8f * k, w - pad * 2f, 20f * k), "임무 완료!", _small);
                _title.normal.textColor = cream;
                GUI.Label(new Rect(x + pad, y + 26f * k, w - pad * 2f, 26f * k), _flashTitle, _title);
                _small.normal.textColor = gold;
                GUI.Label(new Rect(x + pad, y + 56f * k, w - pad * 2f, 24f * k), "보상: " + _flashReward, _small);
                return;
            }

            if (allDone)
            {
                var box = new Rect(x, y, w, 62f * k);
                Fill(box, panel);
                Fill(new Rect(x, y, 4f * k, box.height), green);
                _body.normal.textColor = cream;
                GUI.Label(new Rect(x + pad, y + 8f * k, w - pad * 2f, 24f * k), "성장 임무를 모두 끝냈어요!", _body);
                _small.normal.textColor = gold;
                GUI.Label(new Rect(x + pad, y + 34f * k, w - pad * 2f, 22f * k), PlayerGear.WeaponSummary, _small);
                return;
            }

            Mission m = Missions[_index];
            float height = 150f * k;
            var panelRect = new Rect(x, y, w, height);
            Fill(panelRect, panel);
            Fill(new Rect(x, y, 4f * k, height), gold);

            float cy = y + 8f * k, cw = w - pad * 2f;
            _small.normal.textColor = gold;
            GUI.Label(new Rect(x + pad, cy, cw, 20f * k), "성장 임무 " + (_index + 1) + " / " + Missions.Length + "   <color=#9a8f7c>[L] 접기</color>", _small);
            cy += 20f * k;

            _title.normal.textColor = cream;
            GUI.Label(new Rect(x + pad, cy, cw, 26f * k), m.title, _title);
            cy += 28f * k;

            _small.normal.textColor = new Color(0.8f, 0.76f, 0.68f);
            GUI.Label(new Rect(x + pad, cy, cw, 36f * k), m.hint, _small);
            cy += 36f * k;

            // 진행 막대
            float barH = 14f * k;
            var back = new Rect(x + pad, cy, cw, barH);
            Fill(back, new Color(1f, 1f, 1f, 0.14f));
            float t = m.goal > 0 ? Mathf.Clamp01((float)_shownCurrent / m.goal) : 0f;
            Fill(new Rect(back.x, back.y, back.width * t, barH), gold);
            _small.normal.textColor = Color.white;
            _small.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(back.x, back.y - 3f * k, back.width, barH + 6f * k), _shownCurrent + " / " + m.goal, _small);
            _small.alignment = TextAnchor.UpperLeft;
            cy += barH + 6f * k;

            _small.normal.textColor = gold;
            GUI.Label(new Rect(x + pad, cy, cw, 20f * k), "보상: " + RewardText(m.rewards), _small);
            cy += 19f * k;

            _small.normal.textColor = new Color(0.72f, 0.86f, 1f);
            GUI.Label(new Rect(x + pad, cy, cw, 20f * k), PlayerGear.WeaponSummary, _small);
        }
    }
}
