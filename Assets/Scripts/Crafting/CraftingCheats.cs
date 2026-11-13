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
                if (TruckFacilities.IsFacilityItem(item))
                    continue;   // 설비는 직접 제작해야 한다 (가공 탭이 열리는 조건)
                if (truckInv.GetItemCount(item) <= 0)
                    truckInv.AddItem(item, 1);
            }
        }

        // ───── Ctrl+A: 설비 전체 + 트럭 부착물 전체 (개발/테스트용) ─────

        /// <summary>개발 빌드(에디터 포함)에서만 치트 단축키(Ctrl+A)가 동작한다.</summary>

        // 자리마다 약한 것 → 강한 것 순서. 순서대로 끼우므로 마지막(가장 좋은) 파츠가 최종 장착되고 나머지는 보관함에 남는다.
        private static readonly string[] PartOrder =
        {
            ItemIds.MiniTurret, ItemIds.GatlingTurret, ItemIds.SniperTurret, ItemIds.MortarTurret, ItemIds.FlameTurret, ItemIds.RailTurret,
            ItemIds.AutoMiniTurret, ItemIds.AutoGatlingTurret, ItemIds.AutoSniperTurret, ItemIds.AutoMortarTurret, ItemIds.AutoFlameTurret, ItemIds.AutoRailTurret,
            ItemIds.TunedEngine, ItemIds.TurboEngine,
            ItemIds.SpikeBumper, ItemIds.GrinderWheel,
            ItemIds.TruckCompositeArmor, ItemIds.AlloyArmor,
        };

        /// <summary>
        /// 설비 전부 + 트럭에 붙일 수 있는 부착물(포탑/엔진/전면/장갑) 전부 + 포탑 탄환을 트럭 보관함에 넣고,
        /// 자리마다 가장 좋은 부착물을 바로 장착한다. 나머지는 보관함에 남아서 "트럭 강화" 화면에서 갈아 끼울 수 있다.
        /// 결과를 한 줄 문장으로 돌려준다.
        /// </summary>
        public static string GrantFacilitiesAndParts()
        {
            if (LastTruck.Networking.TruckInventorySync.IsRemoteClient)
                return "멀티플레이에서는 방장(호스트)만 쓸 수 있어요.";

            TruckInventory truckInv = Object.FindFirstObjectByType<TruckInventory>();
            if (truckInv == null)
                return "트럭을 찾지 못했어요.";

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            TruckUpgradeState upgrades = TruckUpgradeState.Instance != null ? TruckUpgradeState.Instance : truckInv.GetComponent<TruckUpgradeState>();

            int facilities = 0;
            foreach (FacilityType type in TruckFacilities.All)
            {
                ItemData item = TruckFacilities.ItemOf(type);
                if (item == null || TruckFacilities.IsBuilt(truckInv, type)) continue;
                if (truckInv.AddItem(item, 1)) facilities++;
            }

            int parts = 0;
            foreach (string id in PartOrder)
            {
                ItemData item = catalog.GetItem(id);
                if (item == null || !TruckUpgradeRules.TryGetSlot(id, out TruckPartSlot slot)) continue;
                bool installed = upgrades != null && upgrades.PartAt(slot) == id;
                if (installed || truckInv.GetItemCount(item) > 0) continue;
                if (truckInv.AddItem(item, 1)) parts++;
            }

            if (upgrades != null)
            {
                foreach (string id in PartOrder)
                {
                    ItemData item = catalog.GetItem(id);
                    if (item != null && truckInv.GetItemCount(item) > 0)
                        upgrades.TryInstall(item, truckInv, out _);
                }
            }

            foreach (TruckUpgradeRules.Ammo ammo in TruckUpgradeRules.AmmoTypes)
            {
                ItemData round = catalog.GetItem(ammo.id);
                if (round != null) truckInv.AddItem(round, 30);
            }

            return $"설비 {facilities}개, 트럭 부착물 {parts}개를 받았어요 (자리마다 가장 좋은 것을 장착했고 탄환 30발씩 넣었어요)";
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
                // 멀티플레이: 호스트의 트럭에 넣어야 모두에게 보인다 (싱글/호스트는 바로 AddRawMaterialsToTruck).
                LastTruck.Networking.TruckInventorySync.GrantRaw(truckInv, 10);
                afterGrant?.Invoke();
            });
            var brt = (RectTransform)b.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f); brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = new Vector2(-4, 0); brt.sizeDelta = new Vector2(130, 34);
        }
    }
}
