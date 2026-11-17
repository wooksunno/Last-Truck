using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>생존 조건 종류 (기획서 "3. 생존 조건 종류").</summary>
    public enum SurvivalKind
    {
        Kill30 = 1,       // 좀비 30마리 처치 (1일차 고정)
        Survive90 = 2,    // 1분 30초 동안 생존
        Kill60 = 3,       // 좀비 60마리 처치
        TruckGuard = 4,   // 트럭 누적 피해 20% 이하로 2분 생존 (실패 시 트럭 내구도 30%)
        MidBosses = 5,    // 순차 등장하는 중간보스 3마리 처치
        Waves = 6,        // 좀비 웨이브 3번 방어
        NoTurret = 7,     // 트럭 포탑을 쓰지 않고 2분 생존
        NoTruck = 8,      // 트럭에 타지 않고 2분 생존
    }

    public enum SurvivalResult { Active = 0, Success = 1, Failed = 2 }

    /// <summary>
    /// 밤마다 하나씩 주어지는 "생존 조건"을 진행하고 판정한다.
    ///
    ///  - 1일차 밤은 고정(좀비 30마리 처치), 2일차부터는 나머지 7가지가 겹치지 않게 무작위 순서로 나온다 (모든 화면이 같은 순서).
    ///  - 밤이 깊어질수록(회차) 좀비의 체력/피해/수가 늘고, 4일차마다 중간보스가 섞여 나온다.
    ///  - 밤은 쿼터가 아니라 이 조건을 달성하면 끝난다 (GameManager.ForceEndNight). 남은 몬스터는 치운다.
    ///  - 실패는 "트럭 누적 피해 20% 초과"뿐이다: 트럭 내구도가 30%로 떨어지고(이미 낮으면 유지) 아침이 온다.
    ///
    /// 호스트(싱글플레이)만 진행/판정/스폰하고, 클라이언트는 NetworkGameState가 보내 준 값(ApplyNetworkState)으로 표시만 한다.
    /// 조건 종류/규칙(KindFor, Title ...)은 회차만으로 정해지므로 동기화가 필요 없다.
    /// </summary>
    public class SurvivalDirector : MonoBehaviour
    {
        // ---- 조정 값
        public const float DamageLimit = 0.20f;            // 트럭 누적 피해 한도 (최대 내구도 대비)
        public const float PenaltyTruckFraction = 0.30f;   // 실패 시 트럭 내구도
        private const float BossHp = 8f, BossDamage = 1.8f, BossSize = 2.0f;
        private const float NormalSize = 1.18f;            // 일반 좀비 몸집 (프리팹 원본이 캐릭터보다 작아 보여서 키운다)
        private const float BossWaveDelay = 15f;           // 4일차마다: 밤이 시작되고 이 시간 뒤 중간보스 1마리가 섞여 나온다
        private const float WaveForceSeconds = 40f;        // 웨이브: 이 시간이 지나면 전멸 못 시켜도 다음 웨이브
        private const float WaveCount = 3f;
        private const int BossCount = 3;

        public static SurvivalDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("SurvivalDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<SurvivalDirector>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_truck != null) _truck.OnDurabilityChanged -= OnTruckDurabilityChanged;
            if (Instance == this) Instance = null;
        }

        // ================================================================ 규칙 (모든 화면 동일)

        private static SurvivalKind[] _order;

        /// <summary>회차(밤)별 생존 조건. 1일차는 고정, 이후는 2~8번이 한 번씩 겹치지 않게 섞여 나온다.</summary>
        public static SurvivalKind KindFor(int cycle)
        {
            if (cycle <= 1) return SurvivalKind.Kill30;
            if (_order == null)
            {
                var bag = new List<SurvivalKind>();
                for (int i = 2; i <= 8; i++) bag.Add((SurvivalKind)i);
                var rng = new System.Random(7351);   // 고정 씨앗: 모든 화면/매 판 같은 순서 (호스트와 클라이언트가 일치)
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    SurvivalKind tmp = bag[i]; bag[i] = bag[j]; bag[j] = tmp;
                }
                _order = bag.ToArray();
            }
            return _order[(cycle - 2) % _order.Length];
        }

        /// <summary>4일차마다 일반 조건에 중간보스가 섞여 나온다 (중간보스 조건 자체인 밤은 제외).</summary>
        public static bool HasBossWave(int cycle) { return cycle >= 4 && cycle % 4 == 0 && KindFor(cycle) != SurvivalKind.MidBosses; }

        public static float HpScale(int cycle) { return 1f + 0.30f * Mathf.Max(0, cycle - 1); }
        /// <summary>
        /// 혼자 플레이할 때의 초반 완화: 1일차 밤 좀비 체력 50%, 2일차 65%, 3일차 80%, 4일차부터 100%.
        /// (혼자서 30마리를 잡아야 하는 첫 밤이 너무 버거워서. 여럿이 하면 그대로다.)
        /// </summary>
        public static float SoloHpEase(int cycle)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.PlayerCount > 1) return 1f;
            if (cycle <= 1) return 0.5f;
            if (cycle == 2) return 0.65f;
            if (cycle == 3) return 0.8f;
            return 1f;
        }

        /// <summary>
        /// 처치 조건의 목표 수. 첫 밤(좀비 30마리)은 혼자 플레이하면 5마리로 줄어든다 (혼자서 첫날부터 30마리는 너무 버겁다).
        /// </summary>
        public static int KillGoal(SurvivalKind k)
        {
            if (k == SurvivalKind.Kill60) return 60;
            GameManager gm = GameManager.Instance;
            bool solo = gm == null || gm.PlayerCount <= 1;
            return solo ? 5 : 30;
        }

        public static float DamageScale(int cycle) { return 1f + 0.12f * Mathf.Max(0, cycle - 1); }
        public static float CountScale(int cycle) { return 1f + 0.15f * Mathf.Max(0, cycle - 1); }

        public static string Title(SurvivalKind k)
        {
            switch (k)
            {
                case SurvivalKind.Kill30: return "좀비 " + KillGoal(k) + "마리 처치";
                case SurvivalKind.Survive90: return "1분 30초 동안 생존";
                case SurvivalKind.Kill60: return "좀비 60마리 처치";
                case SurvivalKind.TruckGuard: return "트럭 피해 20% 이하로 2분 생존";
                case SurvivalKind.MidBosses: return "중간보스 3마리 처치";
                case SurvivalKind.Waves: return "좀비 웨이브 3번 방어";
                case SurvivalKind.NoTurret: return "포탑 없이 2분 생존";
                default: return "트럭 없이 2분 생존";
            }
        }

        public static string Detail(SurvivalKind k)
        {
            switch (k)
            {
                case SurvivalKind.Kill30: return "밤이 되면 몰려오는 좀비를 " + KillGoal(k) + "마리 처치하세요.";
                case SurvivalKind.Survive90: return "해가 뜰 때까지 90초만 버티세요.";
                case SurvivalKind.Kill60: return "쏟아지는 좀비를 60마리 처치하세요.";
                case SurvivalKind.TruckGuard: return "트럭 누적 피해가 20%를 넘기면 실패! 실패하면 트럭 내구도가 30%로 떨어져요.";
                case SurvivalKind.MidBosses: return "보스를 쓰러뜨리거나 30~40초가 지나면 다음 보스가 나타나요.";
                case SurvivalKind.Waves: return "웨이브를 전멸시키거나 40초가 지나면 다음 웨이브가 시작돼요.";
                case SurvivalKind.NoTurret: return "이번 밤은 트럭 포탑에 오를 수 없어요. 2분만 버티세요.";
                default: return "이번 밤은 트럭에 탈 수 없어요. 2분만 버티세요.";
            }
        }

        /// <summary>낮에 미리 알려 주는 준비 조언 (무전/일지 대사에 쓴다).</summary>
        public static string Tip(SurvivalKind k)
        {
            switch (k)
            {
                case SurvivalKind.Kill30: return "무기를 챙기고 트럭 곁을 떠나지 마라.";
                case SurvivalKind.Survive90: return "덫과 미끼로 시간을 벌어라.";
                case SurvivalKind.Kill60: return "탄약과 무기를 넉넉히 챙겨 둬라.";
                case SurvivalKind.TruckGuard: return "트럭 장갑을 보강하고 바리케이드로 길을 막아라.";
                case SurvivalKind.MidBosses: return "보스는 단단하다. 가장 센 무기를 준비해라.";
                case SurvivalKind.Waves: return "파도 사이 틈이 짧다. 포탑과 덫을 미리 깔아 둬라.";
                case SurvivalKind.NoTurret: return "포탑을 못 쓴다. 덫과 방벽, 개인 무기로 버텨라.";
                default: return "트럭에 못 탄다. 트럭에서 내려 버틸 준비를 해라.";
            }
        }

        private static bool NightOf(SurvivalKind k)
        {
            GameManager gm = GameManager.Instance;
            return gm != null && !gm.IsWaitingForNetworkStart && gm.CurrentPhase == GameManager.GamePhase.Night && KindFor(gm.CurrentCycle) == k;
        }

        /// <summary>이번 밤은 트럭 포탑에 오를 수 없다.</summary>
        public static bool TurretBlocked { get { return NightOf(SurvivalKind.NoTurret); } }

        /// <summary>이번 밤은 트럭에 탈 수 없다.</summary>
        public static bool TruckBlocked { get { return NightOf(SurvivalKind.NoTruck); } }

        /// <summary>이번 밤은 몬스터가 (플레이어가 멀면) 트럭을 노린다 (트럭 방어 밤).</summary>
        public static bool TruckTargeting { get { return NightOf(SurvivalKind.TruckGuard); } }

        /// <summary>생존 조건이 이번 밤을 진행 중인가 (GameManager의 쿼터 종료와 MonsterSpawner의 기본 스폰은 쉰다).</summary>
        public static bool ControlsNight { get { return Instance != null && Instance._active; } }

        // ================================================================ 현재 밤 상태

        private bool _active;
        private SurvivalKind _kind;
        private int _cycle;
        private float _elapsed;
        private float _spawnTimer;
        private int _pending;                 // 아직 스폰 중인 몬스터 수
        private MonsterSpawner _spawner;
        private TruckInformation _truck;
        private float _lastDurability, _damageAcc;

        private SurvivalResult _state;
        private int _count;                   // 보스: 처치 수 / 웨이브: 시작한 웨이브 수
        private float _next = -1f;            // 다음 웨이브/보스까지 남은 초 (-1 = 없음)

        // 중간보스
        private readonly List<GameObject> _bosses = new List<GameObject>();
        private readonly List<bool> _bossCounted = new List<bool>();
        private int _bossRequested;
        private bool _bossSpawning;
        private float _nextBossAt;

        // 웨이브
        private int _waveStarted, _waveToSpawn;
        private float _waveStartAt, _waveSpawnTimer, _waveAllClearAt = -1f;

        // 4일차마다 섞이는 중간보스
        private GameObject _extraBoss;
        private bool _extraBossRequested;

        // 클라이언트(멀티플레이): 호스트 값
        private SurvivalResult _mState;
        private float _mElapsed, _mDamage, _mNext = -1f;
        private int _mCount, _mBoss;

        private bool IsHost { get { GameManager gm = GameManager.Instance; return gm != null && !gm.IsNetworkMirror; } }

        // ---- 읽기 (UI / 네트워크 동기화)
        public SurvivalResult State { get { return IsHost ? _state : _mState; } }
        public float Elapsed { get { return IsHost ? _elapsed : _mElapsed; } }
        public int Count { get { return IsHost ? _count : _mCount; } }
        public float Damage { get { return IsHost ? (_truck != null ? _damageAcc / Mathf.Max(1f, _truck.MaxDurability) : 0f) : _mDamage; } }
        public float NextIn { get { return IsHost ? _next : _mNext; } }
        /// <summary>중간보스가 섞인 밤의 보스 상태: 0 = 없음, 1 = 아직, 2 = 처치.</summary>
        public int BossState
        {
            get
            {
                if (!IsHost) return _mBoss;
                if (!_active || !HasBossWave(_cycle)) return 0;
                return _extraBossRequested && IsDead(_extraBoss) ? 2 : 1;
            }
        }

        /// <summary>클라이언트: 호스트가 보낸 진행 값을 반영한다 (NetworkGameState.Render).</summary>
        public void ApplyNetworkState(int state, float elapsed, int count, float damage, float next, int boss)
        {
            SurvivalResult s = (SurvivalResult)state;
            if (s != _mState && s != SurvivalResult.Active) AnnounceResult(s);
            _mState = s;
            _mElapsed = elapsed; _mCount = count; _mDamage = damage; _mNext = next; _mBoss = boss;
        }

        // ================================================================ 밤 시작 / 종료 (호스트)

        /// <summary>GameManager가 밤을 시작할 때 부른다 (OnNightStarted보다 먼저). 이번 밤의 조건을 정하고 쿼터를 맞춘다.</summary>
        public static void NightIsStarting(GameManager gm)
        {
            if (Instance == null || gm == null || gm.IsNetworkMirror) return;
            Instance.BeginNight(gm);
        }

        private void BeginNight(GameManager gm)
        {
            _cycle = gm.CurrentCycle;
            _kind = KindFor(_cycle);
            _elapsed = 0f; _spawnTimer = 3.5f; _pending = 0;
            _count = 0; _next = -1f; _state = SurvivalResult.Active;
            _bosses.Clear(); _bossCounted.Clear(); _bossRequested = 0; _bossSpawning = false; _nextBossAt = 3f;
            _waveStarted = 0; _waveToSpawn = 0; _waveStartAt = 0f; _waveSpawnTimer = 0f; _waveAllClearAt = -1f;
            _extraBoss = null; _extraBossRequested = false;

            if (_truck != null) _truck.OnDurabilityChanged -= OnTruckDurabilityChanged;
            _truck = FindFirstObjectByType<TruckInformation>();
            _lastDurability = _truck != null ? _truck.CurrentDurability : 0f;
            _damageAcc = 0f;
            if (_truck != null) _truck.OnDurabilityChanged += OnTruckDurabilityChanged;

            _spawner = FindFirstObjectByType<MonsterSpawner>();
            if (_spawner == null)
            {
                _active = false;   // 몬스터 스포너가 없는 씬: 예전 쿼터 방식 그대로
                return;
            }
            _active = true;

            switch (_kind)
            {
                case SurvivalKind.Kill30: gm.OverrideNightQuota(KillGoal(_kind)); break;
                case SurvivalKind.Kill60: gm.OverrideNightQuota(60); break;
                case SurvivalKind.Waves: gm.OverrideNightQuota(Mathf.RoundToInt(WaveCount) * WaveSize(gm)); break;
                default: gm.OverrideNightQuota(99999); break;
            }
        }

        private int WaveSize(GameManager gm)
        {
            return Mathf.Max(4, Mathf.RoundToInt(6f * CountScale(_cycle) * gm.PlayerCountMultiplier));
        }

        // ================================================================ 진행 (호스트)

        private void Update()
        {
            if (!_active) return;
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.IsNetworkMirror || gm.CurrentPhase != GameManager.GamePhase.Night) return;
            if (Networking.NetworkGameState.IsGameOver) return;
            if (_spawner == null) { _spawner = FindFirstObjectByType<MonsterSpawner>(); if (_spawner == null) return; }

            float dt = Time.deltaTime;
            _elapsed += dt;
            TrackTruckDamage();

            bool mainDone = false;
            switch (_kind)
            {
                case SurvivalKind.Kill30:
                case SurvivalKind.Kill60:
                {
                    int goal = KillGoal(_kind);
                    float interval = (_kind == SurvivalKind.Kill30 ? 2.2f : 1.5f) / Mathf.Min(1.6f, CountScale(_cycle));
                    int cap = _kind == SurvivalKind.Kill30 ? 6 : 11;
                    Trickle(dt, gm, interval, cap, gm.NightMonstersKilled + AliveTotal() < goal);
                    mainDone = gm.NightMonstersKilled >= goal;
                    break;
                }
                case SurvivalKind.Survive90:
                    Trickle(dt, gm, 4.0f / CountScale(_cycle), 8 + _cycle, true);
                    mainDone = _elapsed >= 90f;
                    break;
                case SurvivalKind.TruckGuard:
                    Trickle(dt, gm, 4.0f / CountScale(_cycle), 9 + _cycle, true);
                    if (Damage > DamageLimit) { FinishNight(false); return; }
                    mainDone = _elapsed >= 120f;
                    break;
                case SurvivalKind.NoTurret:
                case SurvivalKind.NoTruck:
                    Trickle(dt, gm, 3.2f / CountScale(_cycle), 10 + _cycle, true);
                    mainDone = _elapsed >= 120f;
                    break;
                case SurvivalKind.MidBosses:
                    mainDone = TickMidBosses(dt, gm);
                    break;
                case SurvivalKind.Waves:
                    mainDone = TickWaves(dt, gm);
                    break;
            }

            // 시간 기반 조건에서 다음 보스까지 시간을 보여 줄 필요는 없다 (보스 밤만 NextIn 사용)
            bool bossOk = TickExtraBoss(gm);
            if (mainDone && bossOk) FinishNight(true);
        }

        private int AliveTotal() { return (_spawner != null ? _spawner.AliveCount : 0) + _pending; }

        /// <summary>트럭 내구도가 바뀔 때마다 줄어든 만큼 누적 피해로 센다 (수리로 다시 차올라도 누적 피해는 줄지 않는다).</summary>
        private void OnTruckDurabilityChanged(float current, float max)
        {
            if (_active && current < _lastDurability) _damageAcc += _lastDurability - current;
            _lastDurability = current;
        }

        private void TrackTruckDamage()
        {
            if (_truck == null)
            {
                _truck = FindFirstObjectByType<TruckInformation>();
                if (_truck == null) return;
                _lastDurability = _truck.CurrentDurability;
                _truck.OnDurabilityChanged += OnTruckDurabilityChanged;
            }
        }

        private void SpawnNormal(GameManager gm)
        {
            _pending++;
            _spawner.SpawnTiered(HpScale(_cycle) * SoloHpEase(_cycle), DamageScale(_cycle), NormalSize, false, go => { _pending = Mathf.Max(0, _pending - 1); });
        }

        private void Trickle(float dt, GameManager gm, float interval, int cap, bool allow)
        {
            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;
            if (!allow || AliveTotal() >= cap) { _spawnTimer = 0.4f; return; }
            SpawnNormal(gm);
            _spawnTimer = interval * Random.Range(0.8f, 1.25f);
        }

        // ---- 중간보스 3마리 (보스를 쓰러뜨리거나 30~40초가 지나면 다음 보스)

        private bool TickMidBosses(float dt, GameManager gm)
        {
            Trickle(dt, gm, 7f / CountScale(_cycle), 4 + _cycle / 2, true);

            // 처치 수 갱신
            for (int i = 0; i < _bosses.Count; i++)
            {
                if (!_bossCounted[i] && IsDead(_bosses[i]))
                {
                    _bossCounted[i] = true;
                    _count++;
                    _nextBossAt = Mathf.Min(_nextBossAt, _elapsed + 4f);   // 쓰러뜨리면 곧 다음 보스
                }
            }

            if (!_bossSpawning && _bossRequested < BossCount && _elapsed >= _nextBossAt)
            {
                _bossRequested++;
                _bossSpawning = true;   // 스폰이 끝날 때까지 다음 보스를 또 부르지 않는다
                _pending++;
                _spawner.SpawnTiered(HpScale(_cycle) * SoloHpEase(_cycle) * BossHp, DamageScale(_cycle) * BossDamage, BossSize, true, go =>
                {
                    _pending = Mathf.Max(0, _pending - 1);
                    _bossSpawning = false;
                    if (go == null) { _bossRequested--; return; }   // 자리를 못 찾았다: 다시 시도
                    _bosses.Add(go);
                    _bossCounted.Add(false);
                    _nextBossAt = _elapsed + Random.Range(30f, 40f);
                    HudToast.Show("중간보스가 나타났다! (" + _bosses.Count + "/" + BossCount + ")");
                });
            }

            _next = _bossRequested < BossCount ? Mathf.Max(0f, _nextBossAt - _elapsed) : -1f;
            return _count >= BossCount;
        }

        // ---- 좀비 웨이브 3번 (전멸하거나 40초가 지나면 다음 웨이브)

        private bool TickWaves(float dt, GameManager gm)
        {
            int size = WaveSize(gm);

            // 현재 웨이브를 풀어 놓는다
            if (_waveToSpawn > 0)
            {
                _waveSpawnTimer -= dt;
                if (_waveSpawnTimer <= 0f)
                {
                    SpawnNormal(gm);
                    _waveToSpawn--;
                    _waveSpawnTimer = 0.45f;
                }
            }

            bool waveFullySpawned = _waveToSpawn == 0 && _waveStarted > 0;
            bool cleared = waveFullySpawned && AliveTotal() == 0;
            if (cleared && _waveAllClearAt < 0f) _waveAllClearAt = _elapsed;

            // 다음 웨이브: 첫 웨이브는 4초 뒤, 이후는 전멸 3초 뒤 또는 시작 40초 뒤
            bool startNext = false;
            if (_waveStarted < Mathf.RoundToInt(WaveCount))
            {
                if (_waveStarted == 0) startNext = _elapsed >= 4f;
                else if (_waveAllClearAt >= 0f && _elapsed >= _waveAllClearAt + 3f) startNext = true;
                else if (_elapsed >= _waveStartAt + WaveForceSeconds) startNext = true;
            }
            if (startNext)
            {
                _waveStarted++;
                _count = _waveStarted;
                _waveStartAt = _elapsed;
                _waveToSpawn = size;
                _waveSpawnTimer = 0f;
                _waveAllClearAt = -1f;
                HudToast.Show("웨이브 " + _waveStarted + " / " + Mathf.RoundToInt(WaveCount) + " 시작!");
            }

            if (_waveStarted == 0) _next = Mathf.Max(0f, 4f - _elapsed);
            else if (_waveStarted < Mathf.RoundToInt(WaveCount)) _next = Mathf.Max(0f, _waveStartAt + WaveForceSeconds - _elapsed);
            else _next = -1f;

            return _waveStarted >= Mathf.RoundToInt(WaveCount) && waveFullySpawned && AliveTotal() == 0;
        }

        // ---- 4일차마다 섞이는 중간보스 1마리 (그 보스도 쓰러뜨려야 아침이 온다)

        private bool TickExtraBoss(GameManager gm)
        {
            if (!HasBossWave(_cycle)) return true;

            if (!_extraBossRequested && _elapsed >= BossWaveDelay)
            {
                _extraBossRequested = true;
                _pending++;
                _spawner.SpawnTiered(HpScale(_cycle) * SoloHpEase(_cycle) * BossHp, DamageScale(_cycle) * BossDamage, BossSize, true, go =>
                {
                    _pending = Mathf.Max(0, _pending - 1);
                    if (go == null) { _extraBossRequested = false; return; }
                    _extraBoss = go;
                    HudToast.Show("중간보스가 섞여 있다!");
                });
            }

            if (_kind != SurvivalKind.Waves && _kind != SurvivalKind.MidBosses)
                _next = !_extraBossRequested ? Mathf.Max(0f, BossWaveDelay - _elapsed) : -1f;

            return _extraBossRequested && _extraBoss != null && IsDead(_extraBoss);
        }

        private static bool IsDead(GameObject go)
        {
            if (go == null) return true;
            var damageable = go.GetComponent<Combat.Damageable>();
            if (damageable != null && damageable.IsDeadOffline) return true;
            var health = go.GetComponent<Networking.NetworkHealth>();
            if (health != null && health.Object != null && health.Object.IsValid && health.IsDead) return true;
            return false;
        }

        // ---- 끝내기

        private void FinishNight(bool success)
        {
            GameManager gm = GameManager.Instance;
            _active = false;
            _state = success ? SurvivalResult.Success : SurvivalResult.Failed;
            _next = -1f;

            if (_spawner != null) _spawner.ClearAllMonsters();

            if (!success && _truck != null)
            {
                float limit = _truck.MaxDurability * PenaltyTruckFraction;
                if (_truck.CurrentDurability > limit) _truck.SetDurability(limit);
            }

            AnnounceResult(_state);
            if (gm != null) gm.ForceEndNight();
        }

        private static void AnnounceResult(SurvivalResult result)
        {
            if (result == SurvivalResult.Success) HudToast.Show("생존 조건 달성! 아침이 밝았어요");
            else if (result == SurvivalResult.Failed) HudToast.Show("생존 조건 실패… 트럭이 크게 부서졌어요");
        }

        // ================================================================ 임무 UI가 쓰는 조회

        public struct View
        {
            public SurvivalKind kind;
            public bool night;
            public string title, detail;
            public float progress;              // 0..1
            public string progressText;
            public string shortText;            // 시계(다이얼) 안에 들어갈 짧은 글
            public bool showDamage;
            public float damage01;              // 한도(20%) 대비 0..1
            public string damageText;
            public bool showNext;
            public float next01;                // 0..1 (다음 웨이브/보스까지 차오르는 막대)
            public string nextText;
            public string bossText;             // 4일차마다 섞이는 중간보스 (없으면 null)
            public bool bossDone;
            public SurvivalResult state;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60).ToString("0") + ":" + (s % 60).ToString("00");
        }

        /// <summary>지금(낮=오늘 밤 예고 / 밤=진행 상황) 임무 카드에 보여 줄 내용.</summary>
        public static View GetView(GameManager gm)
        {
            var v = new View();
            if (gm == null) return v;

            v.night = gm.CurrentPhase == GameManager.GamePhase.Night;
            v.kind = KindFor(gm.CurrentCycle);
            v.title = Title(v.kind);
            v.detail = Detail(v.kind);
            SurvivalDirector d = Instance;
            v.state = d != null ? d.State : SurvivalResult.Active;

            if (HasBossWave(gm.CurrentCycle))
                v.detail += "  (중간보스가 섞여 나와요)";

            if (!v.night)
            {
                float total = Mathf.Max(1f, gm.DayDurationSeconds);
                v.progress = 1f - Mathf.Clamp01(gm.DayTimeRemaining / total);
                v.progressText = "해질 때까지 " + Clock(gm.DayTimeRemaining);
                v.shortText = Clock(gm.DayTimeRemaining);
                return v;
            }

            if (d == null) return v;
            switch (v.kind)
            {
                case SurvivalKind.Kill30:
                case SurvivalKind.Kill60:
                {
                    int goal = KillGoal(v.kind);
                    v.progress = Mathf.Clamp01((float)gm.NightMonstersKilled / goal);
                    v.progressText = "처치 " + Mathf.Min(goal, gm.NightMonstersKilled) + " / " + goal;
                    v.shortText = Mathf.Min(goal, gm.NightMonstersKilled) + "/" + goal;
                    break;
                }
                case SurvivalKind.Survive90:
                    v.progress = Mathf.Clamp01(d.Elapsed / 90f);
                    v.progressText = "생존 " + Clock(d.Elapsed) + " / " + Clock(90f);
                    v.shortText = Clock(90f - d.Elapsed);
                    break;
                case SurvivalKind.TruckGuard:
                    v.progress = Mathf.Clamp01(d.Elapsed / 120f);
                    v.progressText = "생존 " + Clock(d.Elapsed) + " / " + Clock(120f);
                    v.shortText = Clock(120f - d.Elapsed);
                    v.showDamage = true;
                    v.damage01 = Mathf.Clamp01(d.Damage / DamageLimit);
                    v.damageText = "트럭 누적 피해 " + Mathf.RoundToInt(d.Damage * 100f) + "% / " + Mathf.RoundToInt(DamageLimit * 100f) + "%";
                    break;
                case SurvivalKind.NoTurret:
                case SurvivalKind.NoTruck:
                    v.progress = Mathf.Clamp01(d.Elapsed / 120f);
                    v.progressText = "생존 " + Clock(d.Elapsed) + " / " + Clock(120f);
                    v.shortText = Clock(120f - d.Elapsed);
                    break;
                case SurvivalKind.MidBosses:
                    v.progress = Mathf.Clamp01(d.Count / (float)BossCount);
                    v.progressText = "중간보스 " + d.Count + " / " + BossCount;
                    v.shortText = d.Count + "/" + BossCount;
                    if (d.NextIn >= 0f) { v.showNext = true; v.next01 = 1f - Mathf.Clamp01(d.NextIn / 40f); v.nextText = "다음 중간보스까지 " + Mathf.CeilToInt(d.NextIn) + "초"; }
                    break;
                case SurvivalKind.Waves:
                    v.progress = Mathf.Clamp01(d.Count / WaveCount);
                    v.progressText = "웨이브 " + d.Count + " / " + Mathf.RoundToInt(WaveCount);
                    v.shortText = d.Count + "/" + Mathf.RoundToInt(WaveCount);
                    if (d.NextIn >= 0f) { v.showNext = true; v.next01 = 1f - Mathf.Clamp01(d.NextIn / WaveForceSeconds); v.nextText = "다음 웨이브까지 " + Mathf.CeilToInt(d.NextIn) + "초"; }
                    break;
            }

            // 4일차마다 섞이는 중간보스
            int boss = d.BossState;
            if (boss != 0)
            {
                v.bossText = "중간보스 처치";
                v.bossDone = boss == 2;
                if (!v.showNext && d.NextIn >= 0f && v.kind != SurvivalKind.MidBosses && v.kind != SurvivalKind.Waves)
                { v.showNext = true; v.next01 = 1f - Mathf.Clamp01(d.NextIn / BossWaveDelay); v.nextText = "중간보스 등장까지 " + Mathf.CeilToInt(d.NextIn) + "초"; }
            }
            return v;
        }
    }
}
