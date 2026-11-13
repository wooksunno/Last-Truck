using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastTruck.Networking
{
    #region 종류 / 데이터

    /// <summary>몬스터 종류. 번호는 네트워크로 보내는 값이라 순서를 바꾸지 말 것 (새 종류는 끝에 추가).</summary>
    public enum MonsterKind
    {
        Walker = 0,     // 워커: 기본형
        Tank = 1,       // 탱커: 체력 높음, 반원 공격
        Bomber = 2,     // 붐버: 빠름, 발견 후 6초 뒤 폭발
        Berserker = 3,  // 버서커: 강화형
        BigBoy = 4,     // 빅 보이: 중간 보스
        Sniper = 5,     // 스나이퍼: 원거리 기본형
        Targeter = 6,   // 타겟터: 트럭 우선 원거리
        Polluter = 7,   // 오염체: 바닥에 오염 지대
    }

    /// <summary>공격이 맞는 모양.</summary>
    public enum MonsterHitShape
    {
        Single = 0,    // 목표 하나
        FrontArc = 1,  // 앞쪽 반원 (범위 안 모두)
        Circle = 2,    // 주변 원 (폭발 등)
    }

    /// <summary>몬스터 한 종류의 수치. 기획표(체력/공격력/공격속도/이동속도/피해 범위)를 그대로 옮긴 값.</summary>
    [Serializable]
    public class MonsterTypeData
    {
        public MonsterKind kind;
        public string displayName = "워커";

        [Header("기본 수치 (기획표)")]
        public float maxHealth = 50f;
        public float damage = 8f;
        [Tooltip("초당 공격 횟수 (0.7 = 약 1.4초마다 1번).")]
        public float attacksPerSecond = 0.7f;
        [Tooltip("기획표의 이동 속도 값. 실제 m/s = 이 값 × MonsterCatalog.speedScale.")]
        public float moveSpeed = 10f;

        [Header("공격 방식")]
        public MonsterHitShape hitShape = MonsterHitShape.Single;
        [Tooltip("근접: 공격 사거리(m), 원거리: 쏘기 시작하는 거리(m).")]
        public float attackRange = 1.5f;
        [Tooltip("반원/원 공격의 피해 반경(m). 단일이면 무시.")]
        public float hitRadius = 0f;
        [Tooltip("트럭에 주는 피해 배율 (1 = 같은 피해).")]
        public float truckDamageMultiplier = 1f;

        [Header("원거리")]
        public bool ranged;
        [Tooltip("투사체 속도(m/s).")]
        public float projectileSpeed = 16f;
        [Tooltip("투사체가 떨어진 곳에서 이 반경 안이면 맞는다(m).")]
        public float projectileHitRadius = 1.1f;
        [Tooltip("플레이어보다 트럭을 먼저 노린다 (타겟터).")]
        public bool prefersTruck;
        [Tooltip("플레이어를 알아보는 거리(m).")]
        public float detectionRange = 12f;

        [Header("오염 지대 (오염체)")]
        public bool leavesPool;
        public float poolDuration = 3f;
        public float poolRadius = 2.2f;
        [Tooltip("오염 지대 안에 있으면 1초마다 받는 피해.")]
        public float poolDamagePerSecond = 5f;

        [Header("자폭 (붐버)")]
        public bool selfDestruct;
        [Tooltip("플레이어를 처음 본 뒤 터질 때까지(초).")]
        public float fuseSeconds = 6f;

        [Header("모습")]
        public float scale = 1f;
        public Color tint = Color.white;
        [Tooltip("Synty 좀비 몸 메시 이름. 종류별 프리팹을 만들 때 첫 번째 이름의 메시만 켠다 (여러 개면 실행 중 그중에서 고른다).")]
        public string[] bodies = new string[0];
        [Tooltip("애니메이션 재생 속도 배율.")]
        public float animationSpeed = 1f;

        public MonsterTypeData Clone() => (MonsterTypeData)MemberwiseClone();
    }

    /// <summary>밤(웨이브) 하나의 구성. fromNight 이상인 밤 중 가장 큰 항목을 쓴다.</summary>
    [Serializable]
    public class MonsterWaveData
    {
        [Tooltip("몇 번째 밤부터 이 구성을 쓰는가 (1부터).")]
        public int fromNight = 1;
        public string title = "첫 번째 밤";
        [TextArea] public string description = "좀비 무리가 몰려옵니다.";
        [Tooltip("종류별 등장 비율 (MonsterKind 순서: 워커, 탱커, 붐버, 버서커, 빅보이(무시), 스나이퍼, 타겟터, 오염체).")]
        public float[] weights = new float[8];
        [Tooltip("이 밤에 처음 등장하는 빅 보이 수.")]
        public int bossCount;
    }

    #endregion

    /// <summary>
    /// 몬스터 종류 표 + 웨이브 구성 (Resources/MonsterCatalog.asset).
    /// 에셋이 없으면 아래 기본값(기획표)을 쓴다. 메뉴 "LastTruck > Multiplayer > 몬스터 종류/웨이브 표 만들기"로 에셋을 만들어 인스펙터에서 고칠 수 있다.
    /// 호스트와 클라이언트가 같은 표를 쓰므로(같은 빌드) 종류 번호만 네트워크로 보낸다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastTruck/Monster Catalog", fileName = "MonsterCatalog")]
    public class MonsterCatalog : ScriptableObject
    {
        #region 인스펙터

        [Tooltip("기획표 이동 속도 → 실제 m/s 배율 (10 × 0.2 = 2m/s).")]
        public float speedScale = 0.2f;

        [Header("밤이 지날수록 강해짐 (밤 1은 그대로)")]
        [Tooltip("밤마다 체력 증가 비율 (0.12 = 밤마다 +12%).")]
        public float healthPerNight = 0.12f;
        [Tooltip("밤마다 공격력 증가 비율.")]
        public float damagePerNight = 0.08f;

        public List<MonsterTypeData> types = new List<MonsterTypeData>();
        public List<MonsterWaveData> waves = new List<MonsterWaveData>();

        #endregion

        #region 조회

        private static MonsterCatalog _instance;

        public static MonsterCatalog Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<MonsterCatalog>("MonsterCatalog");
                if (_instance == null)
                {
                    _instance = CreateInstance<MonsterCatalog>();
                    _instance.name = "MonsterCatalog (기본값)";
                    _instance.FillDefaults();
                }
                else if (_instance.types.Count == 0)
                {
                    _instance.FillDefaults();
                }
                return _instance;
            }
        }

        public MonsterTypeData Get(MonsterKind kind)
        {
            foreach (MonsterTypeData data in types)
            {
                if (data != null && data.kind == kind) return data;
            }
            return types.Count > 0 ? types[0] : DefaultTypes()[0];
        }

        /// <summary>night번째 밤의 웨이브 구성 (1부터).</summary>
        public MonsterWaveData GetWave(int night)
        {
            MonsterWaveData best = null;
            foreach (MonsterWaveData wave in waves)
            {
                if (wave == null || wave.fromNight > Mathf.Max(1, night)) continue;
                if (best == null || wave.fromNight > best.fromNight) best = wave;
            }
            return best ?? (waves.Count > 0 ? waves[0] : DefaultWaves()[0]);
        }

        /// <summary>night번째 밤의 체력/공격력 배율.</summary>
        public float HealthScale(int night) => 1f + healthPerNight * Mathf.Max(0, night - 1);
        public float DamageScale(int night) => 1f + damagePerNight * Mathf.Max(0, night - 1);

        #endregion

        #region 기본값 (기획표)

        public void FillDefaults()
        {
            types = DefaultTypes();
            waves = DefaultWaves();
        }

        public static List<MonsterTypeData> DefaultTypes()
        {
            return new List<MonsterTypeData>
            {
                new MonsterTypeData
                {
                    kind = MonsterKind.Walker, displayName = "워커",
                    maxHealth = 50f, damage = 8f, attacksPerSecond = 0.7f, moveSpeed = 10f,
                    hitShape = MonsterHitShape.Single, attackRange = 1.5f,
                    bodies = new[] { "Zombie_Businessman_Male_01" },
                },
                new MonsterTypeData
                {
                    kind = MonsterKind.Tank, displayName = "탱커",
                    maxHealth = 200f, damage = 15f, attacksPerSecond = 1.0f, moveSpeed = 15f,
                    hitShape = MonsterHitShape.FrontArc, attackRange = 1.9f, hitRadius = 2.4f,
                    scale = 1.3f, tint = new Color(0.8f, 0.85f, 1f), animationSpeed = 0.85f,
                    bodies = new[] { "Zombie_Footballer_Male_01" },
                },
                new MonsterTypeData
                {
                    kind = MonsterKind.Bomber, displayName = "붐버",
                    maxHealth = 30f, damage = 30f, attacksPerSecond = 0f, moveSpeed = 20f,
                    hitShape = MonsterHitShape.Circle, attackRange = 1.2f, hitRadius = 4.5f, truckDamageMultiplier = 1.5f,
                    selfDestruct = true, fuseSeconds = 6f, detectionRange = 14f,
                    tint = new Color(1f, 0.85f, 0.5f), animationSpeed = 1.3f,
                    bodies = new[] { "Zombie_Roadworker_Male_01" },
                },
                new MonsterTypeData
                {
                    kind = MonsterKind.Berserker, displayName = "버서커",
                    maxHealth = 90f, damage = 14f, attacksPerSecond = 1.3f, moveSpeed = 20f,
                    hitShape = MonsterHitShape.Single, attackRange = 1.6f, detectionRange = 16f,
                    scale = 1.1f, tint = new Color(1f, 0.55f, 0.55f), animationSpeed = 1.25f,
                    bodies = new[] { "Zombie_Military_Male_01" },
                },
                new MonsterTypeData
                {
                    kind = MonsterKind.BigBoy, displayName = "빅 보이",
                    maxHealth = 400f, damage = 40f, attacksPerSecond = 1.6f, moveSpeed = 25f,
                    hitShape = MonsterHitShape.FrontArc, attackRange = 2.6f, hitRadius = 4f, truckDamageMultiplier = 1.5f,
                    detectionRange = 18f, scale = 1.8f, tint = new Color(0.75f, 1f, 0.7f), animationSpeed = 0.9f,
                    bodies = new[] { "Zombie_Biker_Male_01" },
                },
                new MonsterTypeData
                {
                    kind = MonsterKind.Sniper, displayName = "스나이퍼",
                    maxHealth = 40f, damage = 12f, attacksPerSecond = 0.8f, moveSpeed = 13f,
                    hitShape = MonsterHitShape.Single, attackRange = 13f, ranged = true, projectileSpeed = 18f, detectionRange = 18f,
                    tint = new Color(0.75f, 0.8f, 1f),
                    bodies = new[] { "Zombie_Police_Male_01" },
                },
                new MonsterTypeData
                {
                    kind = MonsterKind.Targeter, displayName = "타겟터",
                    maxHealth = 60f, damage = 14f, attacksPerSecond = 0.8f, moveSpeed = 15f,
                    hitShape = MonsterHitShape.Single, attackRange = 12f, ranged = true, projectileSpeed = 16f,
                    prefersTruck = true, truckDamageMultiplier = 1.2f,
                    tint = new Color(1f, 0.75f, 0.4f),
                    bodies = new[] { "Zombie_Firefighter_Male_01" },
                },
                new MonsterTypeData
                {
                    kind = MonsterKind.Polluter, displayName = "오염체",
                    maxHealth = 70f, damage = 5f, attacksPerSecond = 0.5f, moveSpeed = 10f,
                    hitShape = MonsterHitShape.Circle, attackRange = 9f, ranged = true, projectileSpeed = 11f,
                    leavesPool = true, poolDuration = 3f, poolRadius = 2.2f, poolDamagePerSecond = 5f,
                    tint = new Color(0.6f, 1f, 0.5f),
                    bodies = new[] { "Zombie_BioHazardSuit_Male_01" },
                },
            };
        }

        //                                   워커 탱커 붐버 버서 보스 스나 타겟 오염
        private static float[] W(params float[] w) => w;

        public static List<MonsterWaveData> DefaultWaves()
        {
            return new List<MonsterWaveData>
            {
                new MonsterWaveData { fromNight = 1, title = "첫 번째 밤", description = "좀비 무리가 몰려옵니다. 트럭을 지키세요.",
                    weights = W(100, 0, 0, 0, 0, 0, 0, 0) },
                new MonsterWaveData { fromNight = 2, title = "무거운 발소리", description = "탱커와 붐버가 섞여 있습니다. 붐버는 멀리서 처리하세요.",
                    weights = W(65, 15, 20, 0, 0, 0, 0, 0) },
                new MonsterWaveData { fromNight = 3, title = "원거리 습격", description = "원거리 공격을 조심하세요. 타겟터는 트럭을 노립니다.",
                    weights = W(45, 10, 10, 0, 0, 20, 15, 0), bossCount = 1 },
                new MonsterWaveData { fromNight = 4, title = "오염된 밤", description = "오염체가 바닥을 오염시킵니다. 초록색 지대를 피하세요.",
                    weights = W(35, 12, 12, 12, 0, 12, 9, 8) },
                new MonsterWaveData { fromNight = 5, title = "광란의 밤", description = "버서커와 빅 보이가 나타납니다. 끝까지 버티세요!",
                    weights = W(25, 14, 12, 18, 0, 12, 10, 9), bossCount = 1 },
            };
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
