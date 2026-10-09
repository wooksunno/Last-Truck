using UnityEngine;
using UnityEngine.UI;
using static CraftingSystem.CraftUI;

namespace CraftingSystem
{
    /// <summary>
    /// 개발용 치트 모음. 정식 빌드에서 빼려면:
    ///   1) 이 파일(CraftingCheats.cs)을 삭제하고
    ///   2) GameUIController 안의 "[CHEAT]" 표시 블록 한 군데를 지우면 된다.
    /// (또는 GameUIController 인스펙터의 showCheatButtons를 꺼 두면 화면에서만 사라진다.)
    /// </summary>
    public static class CraftingCheats
    {
        /// <summary>트럭 보관함에 원재료를 넣어 준다(+ 아직 없는 아이템은 1개씩).</summary>
        public static void AddRawMaterialsToTruck(TruckInventory truckInv, int amount)
        {
            if (truckInv == null)
                return;

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            truckInv.AddItem(catalog.GetItem(ItemIds.Wood), amount);
            truckInv.AddItem(catalog.GetItem(ItemIds.Stone), amount);
            truckInv.AddItem(catalog.GetItem(ItemIds.IronOre), amount);
            truckInv.AddItem(catalog.GetItem(ItemIds.CopperOre), amount);

            // 테스트 편의: 아직 하나도 없는 아이템(무기/도구/식료품 등 전부)은 최소 1개씩 채워 넣는다.
            foreach (ItemData item in catalog.Items)
            {
                if (item == null)
                    continue;
                if (truckInv.GetItemCount(item) <= 0)
                    truckInv.AddItem(item, 1);
            }
        }

        /// <summary>패널 맨 아래에 눈에 덜 띄는 "개발용" 줄을 붙인다.</summary>
        public static void BuildFooter(RectTransform parent, TruckInventory truckInv, System.Action afterGrant)
        {
            var row = Mk("CheatFooter", parent);
            row.sizeDelta = new Vector2(0f, 40f);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 40; le.minHeight = 40;

            var label = Mk("Label", row);
            label.anchorMin = new Vector2(0, 0); label.anchorMax = new Vector2(0.5f, 1); label.offsetMin = new Vector2(6, 0); label.offsetMax = Vector2.zero;
            Txt(label, "개발용", 14, FontStyle.Normal, new Color(1f, 1f, 1f, 0.28f), TextAnchor.MiddleLeft);

            var b = MkButton(row, "CheatRawPlus10", "원재료 +10", new Color(0.25f, 0.30f, 0.26f, 0.9f), 15, () =>
            {
                AddRawMaterialsToTruck(truckInv, 10);
                afterGrant?.Invoke();
            });
            var brt = (RectTransform)b.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f); brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = new Vector2(-4, 0); brt.sizeDelta = new Vector2(130, 34);
        }
    }
}
