using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Character Data", menuName = "Character/CharacterStats Data")]
    public class CharacterStatsData : ScriptableObject
    {
        // 기본
        [System.Serializable]
        public struct BasicStats
        {
            public string playerName;
            public float maxHealth;
            public float attackPower;
            public float moveSpeed;
        }

        // 유틸리티
        [System.Serializable]
        public struct UtilityStats
        {
            public float costReduction;     // 제작비용 감소
            public float harvestSpeed;      // 채집 속도
            public float repairBonus;       // 수리 보너스 증가
        }

        [Header("Basic Info")]
        [SerializeField] private BasicStats basicStats;

        [Header("Utility Info")]
        [SerializeField] private UtilityStats utilityStats;

        [Header("Unique Ability")]
        [SerializeField] private CharacterAbility uniqueAbility;

        // 프로퍼티
        public string PlayerName => basicStats.playerName;
        public float MaxHealth => basicStats.maxHealth;
        public float AttackPower => basicStats.attackPower;
        public float MoveSpeed => basicStats.moveSpeed;
        public float ScaledMoveSpeed => basicStats.moveSpeed * 0.3f;

        public float CostReduction => utilityStats.costReduction;
        public float HarvestSpeed => utilityStats.harvestSpeed;
        public float RepairBonus => utilityStats.repairBonus;

        public CharacterAbility UniqueAbility => uniqueAbility;

        // 멀티플레이 캐릭터 선택 화면용 (에디터 메뉴 "LastTruck > Multiplayer"가 자동으로 채운다)
        [Header("캐릭터 선택 화면 (멀티플레이)")]
        [Tooltip("선택 화면/대기실에 보이는 얼굴 초상화. '캐릭터 초상화 촬영' 메뉴가 자동으로 만든다.")]
        public Sprite portrait;

        [Tooltip("선택 화면 가운데에 보이는 캐릭터 설명.")]
        [TextArea(3, 8)]
        public string description;

        [Tooltip("원본 캐릭터 프리팹 (모델/애니메이터). 초상화 촬영과 네트워크 프리팹 생성에 쓰인다.")]
        public GameObject modelPrefab;

        [Tooltip("게임에서 실제로 스폰되는 네트워크 캐릭터 프리팹. '네트워크 프리팹 생성' 메뉴가 자동으로 만든다.")]
        public Fusion.NetworkObject networkPrefab;

        /// <summary>화면에 보여줄 이름. PlayerName이 비어 있으면 에셋 이름에서 "CharacterData_"를 뗀 값.</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(PlayerName)) return PlayerName;
                return name.Replace("CharacterData_", string.Empty);
            }
        }
    }
}
