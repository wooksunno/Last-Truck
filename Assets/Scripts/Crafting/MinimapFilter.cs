using System;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 미니맵에 어떤 채집물을 점으로 보일지 고르는 설정 (분류별 켜기/끄기). 내 컴퓨터에 저장된다 (PlayerPrefs).
    /// 미니맵 아래 [표시] 단추를 누르면 분류 칩이 펼쳐지고, 칩을 누르면 그 분류가 켜지거나 꺼진다.
    /// </summary>
    public static class MinimapFilter
    {
        public struct Category
        {
            public string name;
            public string iconItemId;   // 칩에 보일 아이콘 (아이템 아이콘)
            public Color color;         // 미니맵 점 색
        }

        public static readonly Category[] Categories =
        {
            new Category { name = "나무", iconItemId = ItemIds.Wood, color = new Color(0.70f, 0.48f, 0.26f) },
            new Category { name = "돌", iconItemId = ItemIds.Stone, color = new Color(0.62f, 0.60f, 0.58f) },
            new Category { name = "구리", iconItemId = ItemIds.CopperOre, color = new Color(1f, 0.52f, 0.28f) },
            new Category { name = "철", iconItemId = ItemIds.IronOre, color = new Color(0.80f, 0.82f, 0.88f) },
            new Category { name = "금", iconItemId = ItemIds.PlatinumOre, color = new Color(1f, 0.85f, 0.25f) },
            new Category { name = "다이아", iconItemId = ItemIds.Diamond, color = new Color(0.35f, 0.75f, 1f) },
            new Category { name = "기름", iconItemId = ItemIds.Oil, color = new Color(0.55f, 0.35f, 0.75f) },
            new Category { name = "식물", iconItemId = ItemIds.Herb, color = new Color(0.45f, 0.90f, 0.40f) },
            new Category { name = "보물상자", iconItemId = null, color = new Color(1f, 0.78f, 0.35f) },
            new Category { name = "기타", iconItemId = null, color = new Color(0.85f, 0.85f, 0.85f) },
        };

        public const int Chest = 8, Other = 9;
        private const string PrefKey = "LastTruck.MinimapFilter.v2";   // (나무/돌 소품을 넣으면서 기본값이 바뀌어 키를 새로 쓴다)
        private const int AllOn = (1 << 10) - 1;
        // 기본: 광석/다이아/기름/보물상자/기타만 켠다. 나무·돌·식물은 맵에 수천 개라 켜면 미니맵이 점으로 뒤덮인다 (필요할 때 켠다)
        private const int DefaultMask = AllOn & ~((1 << 0) | (1 << 1) | (1 << 7));

        private static int _mask = -1;

        /// <summary>설정이 바뀌면 (미니맵 칩을 다시 그린다).</summary>
        public static event Action Changed;

        private static int Mask
        {
            get
            {
                if (_mask < 0) _mask = PlayerPrefs.GetInt(PrefKey, DefaultMask);
                return _mask;
            }
        }

        public static bool IsOn(int category) => category >= 0 && (Mask & (1 << category)) != 0;

        public static void Toggle(int category)
        {
            if (category < 0 || category >= Categories.Length) return;
            _mask = Mask ^ (1 << category);
            PlayerPrefs.SetInt(PrefKey, _mask);
            Changed?.Invoke();
        }

        public static void SetAll(bool on)
        {
            _mask = on ? AllOn : 0;
            PlayerPrefs.SetInt(PrefKey, _mask);
            Changed?.Invoke();
        }

        /// <summary>아이템 ID로 분류를 정한다.</summary>
        public static int CategoryOf(string itemId)
        {
            switch (itemId)
            {
                case ItemIds.Wood: return 0;
                case ItemIds.Stone: return 1;
                case ItemIds.CopperOre: return 2;
                case ItemIds.IronOre: return 3;
                case ItemIds.PlatinumOre: return 4;
                case ItemIds.Diamond: return 5;
                case ItemIds.Oil: return 6;
                case ItemIds.Herb: case ItemIds.Cactus: case ItemIds.WildGrass: return 7;
                default: return Other;
            }
        }

        /// <summary>미니맵에 올라가는 대상의 분류 (자원 노드 / 보물상자).</summary>
        public static int CategoryOf(Component node)
        {
            if (node is TreasureChest) return Chest;
            if (node is GatherableProp gp) return CategoryOf(gp.ItemId);
            if (node is SpecialResourceNode sn && sn.Item != null) return CategoryOf(sn.Item.itemID);
            if (node is ResourceNode rn && rn.OutputItem != null) return CategoryOf(rn.OutputItem.itemID);
            return Other;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _mask = -1; Changed = null; }
    }
}
