using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 채집 조건(도구 등급 / 전용 장비) 판정 공통 로직.
    /// 조건을 못 맞춘 채로 채집을 시도해도 E키 꾹 누르기는 진행되지만 시간이 UnmetTimeMultiplier배 걸리고, 끝나도 아이템을 얻지 못한다.
    /// </summary>
    public static class GatherRules
    {
        public const float UnmetTimeMultiplier = 10f;

        private static GameObject _player;

        public static GameObject Player
        {
            get
            {
                if (_player == null) _player = GameObject.FindWithTag("Player");
                return _player;
            }
        }

        /// <summary>1/2/3 키로 선택한 손 슬롯 기준으로 요구 도구 등급/장비를 만족하는지.</summary>
        public static bool MeetsRequirement(GameObject player, int requiredTier, string requiredItemId = "")
        {
            if (requiredTier <= 0 && string.IsNullOrEmpty(requiredItemId))
                return true;

            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            InventorySlot hand = inventory != null ? inventory.SelectedSlot : null;
            bool hasHand = hand != null && !hand.IsEmpty && hand.item != null;
            bool tierOk = requiredTier <= 0 || (hasHand && hand.item.toolTier >= requiredTier);
            bool itemOk = string.IsNullOrEmpty(requiredItemId) || (hasHand && hand.item.itemID == requiredItemId);
            return tierOk && itemOk;
        }

        /// <summary>
        /// 조건을 만족하는 도구로 캘 때의 시간 배율(낮을수록 빠름).
        /// 요구 등급과 같은 도구면 0.5배, 등급이 한 단계 높을 때마다 0.75배씩 더 빨라진다(최소 0.2배).
        /// 요구 등급이 없는 자원(나무/약초 등)은 1배.
        /// </summary>
        public static float ToolSpeedFactor(GameObject player, int requiredTier)
        {
            if (requiredTier <= 0)
                return 1f;

            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            InventorySlot hand = inventory != null ? inventory.SelectedSlot : null;
            int handTier = hand != null && !hand.IsEmpty && hand.item != null ? hand.item.toolTier : 0;
            int excess = Mathf.Max(0, handTier - requiredTier);
            return Mathf.Max(0.2f, 0.5f * Mathf.Pow(0.75f, excess));
        }

        /// <summary>플레이어 본인이 아닌 곳(UI/프로퍼티)에서 쓰는 편의 오버로드: 태그가 Player인 오브젝트 기준.</summary>
        public static bool MeetsRequirement(int requiredTier, string requiredItemId = "") =>
            MeetsRequirement(Player, requiredTier, requiredItemId);
    }
}
