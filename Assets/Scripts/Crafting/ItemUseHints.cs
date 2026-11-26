using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 손에 든 아이템을 "어떻게 쓰는지" 한 줄 안내 (핫바 위에 뜬다).
    /// 처음 하는 사람이 수리 키트/강화 키트/배낭처럼 쓰는 법이 숨어 있는 아이템을 몰라서 못 쓰던 문제를 막는다.
    /// </summary>
    public static class ItemUseHints
    {
        public static string For(ItemData item)
        {
            if (item == null) return null;
            string id = item.itemID;

            // 먹기/수리 키트/강화 키트 (왼쪽 클릭 꾹)
            string eat = EatController.UseHint(item);
            if (eat != null) return eat;

            if (id == ItemIds.LeatherPouchBackpack) return "B 또는 왼쪽 클릭 · 배낭 열기 (주운 재료가 배낭에 먼저 들어가요)";
            if (id == ItemIds.Lantern) return "가방에 있기만 하면 허리에 달려 주변을 밝혀요";
            if (id == ItemIds.Binoculars) return "오른쪽 클릭 꾹 · 멀리 보기";
            if (id == ItemIds.FishingRod) return "물가에서 왼쪽 클릭 · 낚싯대 던지기";
            int armor = PlayerArmor.TierOfItem(id);
            if (armor > 0)
            {
                float sp = PlayerArmor.SpeedScaleOfTier(armor);
                return "따로 입을 필요 없이 가방이나 배낭에 있으면 자동 착용 · 받는 피해 -" + Mathf.RoundToInt(PlayerArmor.ReductionOfTier(armor) * 100f) + "%"
                       + (sp < 0.999f ? " · 이동 -" + Mathf.RoundToInt((1f - sp) * 100f) + "%" : "");
            }
            if (id == ItemIds.EmergencyPatchBoard || id == ItemIds.WeldingKit || id == ItemIds.HighTensionRepairPack)
                return "부서진 트럭 옆에서 E 꾹 · 트럭 수리";
            if (TrapController.IsPlaceable(id)) return "E 꾹 · 발밑/앞에 설치";
            if (TruckFacilities.IsFacilityItem(item)) return "트럭 보관함에 있으면 가공 설비로 쓰여요";

            if (TruckCodexView.IsWeaponItem(item) || item.isWeapon || item.itemType == ItemType.Weapon)
                return "왼쪽 클릭 · 공격";
            if (item.toolTier > 0)
                return "자원 앞에서 E 꾹 · 채집 (곡괭이 " + item.toolTier + "등급)";
            if (item.itemType == ItemType.Raw || item.itemType == ItemType.Intermediate)
                return "재료 · 트럭(Tab)에서 제작/가공에 써요";
            return null;
        }
    }
}
