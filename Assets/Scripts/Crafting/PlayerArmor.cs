using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어 갑옷. 별도 착용 칸 없이 "가방(1~4번 칸) 안에 갑옷을 가지고 있으면" 자동으로 적용된다 (여러 벌이면 가장 좋은 한 벌).
    /// 가방이 4칸뿐이라 갑옷 한 벌이 한 칸을 차지하는 것이 곧 선택의 대가다.
    ///
    ///   가죽 갑옷 : 받는 피해 -10%, 이동 속도 영향 없음
    ///   철제 갑옷 : 받는 피해 -20%, 이동 속도 -5%
    ///   합금 갑옷 : 받는 피해 -30%, 이동 속도 -8%
    ///
    /// 싱글플레이는 PlayerStats.TakeDamage가, 멀티플레이는 호스트의 NetworkHealth.ApplyDamage가 이 값을 쓴다
    /// (멀티플레이에서는 내 갑옷 정보가 입력(NetworkInputData.ArmorTier)에 실려 호스트로 간다).
    /// </summary>
    public static class PlayerArmor
    {
        public const int MaxTier = 3;

        private static readonly string[] Ids = { ItemIds.LeatherArmor, ItemIds.IronPlateArmor, ItemIds.AlloyPlateArmor };
        private static readonly float[] Reduction = { 0.10f, 0.20f, 0.30f };
        private static readonly float[] SpeedPenalty = { 0f, 0.05f, 0.08f };
        public static readonly string[] Names = { "가죽 갑옷", "철제 갑옷", "합금 갑옷" };

        /// <summary>이 아이템이 갑옷이면 등급(1~3), 아니면 0.</summary>
        public static int TierOfItem(string itemId)
        {
            for (int i = 0; i < Ids.Length; i++) if (Ids[i] == itemId) return i + 1;
            return 0;
        }

        /// <summary>가방 안에서 가장 좋은 갑옷의 등급 (없으면 0).</summary>
        public static int BestTier(PlayerInventory inventory)
        {
            if (inventory == null) return 0;
            int best = 0;
            var slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null || slot.IsEmpty || slot.item == null) continue;
                best = Mathf.Max(best, TierOfItem(slot.item.itemID));
            }
            // 배낭 안에 있어도 입은 것으로 친다 (배낭이 있으면 새로 얻은 갑옷이 배낭으로 먼저 들어가서, 예전에는 적용되지 않았다)
            if (inventory.HasBackpack && inventory.Pouch != null)
                foreach (InventorySlot slot in inventory.Pouch.Slots)
                    if (slot != null && !slot.IsEmpty && slot.item != null)
                        best = Mathf.Max(best, TierOfItem(slot.item.itemID));
            return best;
        }

        /// <summary>HUD 표시용: "가죽 갑옷 · 피해 -10%" (갑옷이 없으면 null).</summary>
        public static string LocalSummary
        {
            get
            {
                int t = LocalTier;
                if (t <= 0) return null;
                string s = Names[Mathf.Clamp(t, 1, MaxTier) - 1] + " · 받는 피해 -" + Mathf.RoundToInt(ReductionOfTier(t) * 100f) + "%";
                float sp = SpeedScaleOfTier(t);
                if (sp < 0.999f) s += " · 이동 -" + Mathf.RoundToInt((1f - sp) * 100f) + "%";
                return s;
            }
        }

        /// <summary>받는 피해 감소 비율 (0 ~ 0.3).</summary>
        public static float ReductionOfTier(int tier)
        {
            if (tier <= 0) return 0f;
            return Reduction[Mathf.Clamp(tier, 1, MaxTier) - 1];
        }

        /// <summary>이동 속도 배율 (1 = 영향 없음).</summary>
        public static float SpeedScaleOfTier(int tier)
        {
            if (tier <= 0) return 1f;
            return 1f - SpeedPenalty[Mathf.Clamp(tier, 1, MaxTier) - 1];
        }

        // ---- 내 캐릭터 (싱글/멀티 공통: 내 화면에서 입력을 받는 캐릭터)

        private static PlayerInventory _local;
        private static int _cachedTier;
        private static float _cachedAt = -1f;

        /// <summary>내 캐릭터의 갑옷 등급. 0.2초마다 다시 센다.</summary>
        public static int LocalTier
        {
            get
            {
                if (Time.unscaledTime - _cachedAt > 0.2f)
                {
                    _cachedAt = Time.unscaledTime;
                    if (_local == null)
                    {
                        // 멀티플레이: 내 캐릭터의 가방 (다른 사람 캐릭터가 잡히지 않게)
                        if (LastTruck.Networking.GameLauncher.IsOnlineSession)
                        {
                            LastTruck.Networking.NetworkPlayer me = LastTruck.Networking.NetworkPlayer.Local;
                            if (me != null) _local = me.GetComponent<PlayerInventory>();
                        }
                        else _local = Object.FindFirstObjectByType<PlayerInventory>();
                    }
                    _cachedTier = BestTier(_local);
                }
                return _cachedTier;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _local = null; _cachedTier = 0; _cachedAt = -1f; }
    }
}
