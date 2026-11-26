using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 석유 펌프잭 화면: 펌프잭에서 E를 누르면 열린다 (트럭 창과 같은 UI Toolkit 창).
    /// 가운데는 땅속 기름이 관을 타고 빨려 올라와 탱크에 차는 단면 장면(OilPumpDiorama), 오른쪽은 모인 기름 + 받기/회수 버튼.
    /// </summary>
    public partial class GameUIController
    {
        private OilPumpjack _activePump;

        /// <summary>펌프 화면을 연다. 창을 만들 수 없으면 false (그러면 펌프잭이 예전처럼 바로 받기/회수한다).</summary>
        public bool OpenPumpScreen(OilPumpjack pump)
        {
            if (pump == null || !EnsureToolkitWindow()) return false;
            _activePump = pump;
            _activeFacility = null;
            _truckCodexOpen = false; _craftingPanelOpen = false;
            _truckProcessingPanelOpen = false; _truckUpgradeOpen = false; _truckAchievementsOpen = false;
            ShowPopup("석유 펌프잭", BuildPumpContent);
            return true;
        }

        private void BuildPumpContent()
        {
            OilPumpjack pump = _activePump;
            if (pump == null || _storageView == null) return;
            GameObject player = playerInventory != null ? playerInventory.gameObject : GatherRules.Player;
            ItemData oil = ItemCatalog.GetOrCreate().GetItem(ItemIds.Oil);
            var m = new LastTruck.TruckStorageView.PumpModel
            {
                title = "석유 펌프잭",
                pump = pump,
                oilIcon = oil != null ? oil.icon : null,
                onClose = ClosePopup,
                onCollect = () => { if (pump != null) pump.Collect(player); },
                onRetrieve = () => { if (pump != null && pump.Retrieve(player)) ClosePopup(); },
            };
            _storageView.ShowPump(m);
            _storageToolkitActive = true;
        }
    }
}
