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

        /// <summary>
        /// 모든 채집(나무/돌/약초/광석)의 E 꾹 누르기 시간에 곱해지는 전체 배율. 자원이 너무 빨리 캐진다는 의견으로 1.25배(25% 느리게).
        /// 더 느리게/빠르게 하려면 이 값만 바꾼다. (트럭 탑승/수리/덫 설치 같은 채집이 아닌 꾹 누르기에는 적용되지 않는다)
        /// </summary>
        public const float GlobalTimeScale = 1.25f;

        private static GameObject _player;

        public static GameObject Player
        {
            get
            {
                // 멀티플레이: 태그로 찾으면 다른 사람 캐릭터가 잡힐 수 있으므로 내 캐릭터를 쓴다.
                if (LastTruck.Networking.GameLauncher.IsOnlineSession)
                {
                    LastTruck.Networking.NetworkPlayer local = LastTruck.Networking.NetworkPlayer.Local;
                    return local != null ? local.gameObject : null;
                }
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
        /// 요구 등급과 같은 도구면 0.75배, 등급이 한 단계 높을 때마다 0.9배씩 더 빨라진다(최소 0.5배).
        /// (예전에는 0.5배 / 0.75배씩 / 최소 0.2배라서 상위 곡괭이·분쇄기로는 모든 광석이 순식간에 캐졌다)
        /// 요구 등급이 없는 자원(나무/약초 등)은 1배 (나무/약초는 도끼/호미로 빨라진다 - SpecialToolFactor).
        /// </summary>
        public static float ToolSpeedFactor(GameObject player, int requiredTier)
        {
            if (requiredTier <= 0)
                return 1f;

            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            InventorySlot hand = inventory != null ? inventory.SelectedSlot : null;
            int handTier = hand != null && !hand.IsEmpty && hand.item != null ? hand.item.toolTier : 0;
            int excess = Mathf.Max(0, handTier - requiredTier);
            return Mathf.Max(0.5f, 0.75f * Mathf.Pow(0.9f, excess));
        }

        /// <summary>
        /// 전용 도구 시간 배율: 나무는 도끼로, 약초/독약초는 호미로 캐야 빠르다.
        ///   나무   : 맨손·곡괭이·분쇄기 1.4배(느림) / 돌도끼 0.8배 / 철도끼 0.55배
        ///   약초   : 호미가 아니면 1.5배(느림) / 호미 0.6배
        /// 전용 도구를 쓰면 도구가 한 번 닳는다 (배율이 1 미만일 때).
        /// 곡괭이/분쇄기는 돌과 광석 전용이라 나무와 약초에는 도움이 되지 않는다.
        /// </summary>
        public static float SpecialToolFactor(GameObject player, string resourceItemId)
        {
            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            ItemData hand = inventory != null ? inventory.SelectedItem : null;
            string handId = hand != null ? hand.itemID : "";
            if (resourceItemId == ItemIds.Wood)
            {
                if (handId == ItemIds.IronAxe) return 0.55f;
                if (handId == ItemIds.StoneAxe) return 0.8f;
                return 1.4f;   // 맨손/곡괭이/분쇄기로는 나무를 베기 힘들다
            }
            if (resourceItemId == ItemIds.Herb || resourceItemId == ItemIds.PoisonHerb)
                return handId == ItemIds.HerbHoe ? 0.6f : 1.5f;
            return 1f;
        }

        /// <summary>플레이어 본인이 아닌 곳(UI/프로퍼티)에서 쓰는 편의 오버로드: 태그가 Player인 오브젝트 기준.</summary>
        public static bool MeetsRequirement(int requiredTier, string requiredItemId = "") =>
            MeetsRequirement(Player, requiredTier, requiredItemId);
    }
}
