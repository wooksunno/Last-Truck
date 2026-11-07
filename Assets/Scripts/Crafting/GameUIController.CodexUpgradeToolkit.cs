using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 창 "도감"/"강화" 탭을 UI Toolkit(LastTruck.TruckStorageView)으로 그린다.
    /// 도감은 분류 → 아이템 → 제작 링크로 이동하는 상태를 여기서 들고 있다 (다시 그려도 보던 화면이 유지된다).
    /// </summary>
    public partial class GameUIController
    {
        private int _codexCategory;
        private int _codexRecipeIndex;

        /// <summary>도감 탭을 UI Toolkit으로 그렸으면 true.</summary>
        private bool TryShowCodexToolkit()
        {
            if (!EnsureToolkitWindow()) return false;
            TruckInventory inv = ActiveTruckInventory;

            // 제작 화면 등 바깥에서 아이템을 들고 들어오면 그 아이템의 분류로 맞춘다
            if (_codexSelectedItem != null && _codexFromCraft)
                _codexCategory = TruckCodexView.CategoryOf(_codexSelectedItem);

            var m = new LastTruck.TruckStorageView.CodexModel
            {
                tabs = BuildToolkitTabs(inv, 3),
                onClose = ClosePopup,
                category = _codexCategory,
                item = _codexSelectedItem,
                recipeIndex = _codexRecipeIndex,
                backLabel = _codexFromCraft ? "← 제작으로" : "← 목록",
                onCategory = cat =>
                {
                    _codexCategory = cat;
                    _codexSelectedItem = null;
                    ShowPopup("트럭 거점", BuildTruckContent);
                },
                onItem = item =>
                {
                    _codexSelectedItem = item;
                    _codexRecipeIndex = 0;
                    ShowPopup("트럭 거점", BuildTruckContent);
                },
                onRecipeIndex = i =>
                {
                    _codexRecipeIndex = i;
                    ShowPopup("트럭 거점", BuildTruckContent);
                },
                onBack = () =>
                {
                    if (_codexFromCraft)
                    {
                        // 제작 화면에서 "제작 링크 보기"로 왔으면 제작 화면으로 돌아간다
                        _codexFromCraft = false;
                        _truckCodexOpen = false;
                        _codexSelectedItem = null;
                        _craftingPanelOpen = true;
                        ShowPopup("제작", BuildTruckContent);
                        return;
                    }
                    _codexSelectedItem = null;
                    ShowPopup("트럭 거점", BuildTruckContent);
                },
            };
            _storageView.ShowCodex(m);
            _storageToolkitActive = true;
            return true;
        }

        /// <summary>강화 탭을 UI Toolkit으로 그렸으면 true.</summary>
        private bool TryShowUpgradeToolkit()
        {
            if (!EnsureToolkitWindow()) return false;
            TruckInventory truckInv = ActiveTruckInventory;
            TruckUpgradeState state = TruckUpgradeState.Instance;

            var m = new LastTruck.TruckStorageView.UpgradeModel { tabs = BuildToolkitTabs(truckInv, 4), onClose = ClosePopup };
            if (state == null)
            {
                m.message = "트럭을 찾을 수 없습니다.";
                _storageView.ShowUpgrade(m);
                _storageToolkitActive = true;
                return true;
            }

            ItemCatalog catalog = ItemCatalog.GetOrCreate();

            // 파츠 4자리
            for (int s = 0; s < TruckUpgradeRules.SlotCount; s++)
            {
                var slot = (TruckPartSlot)s;
                string partId = state.PartAt(slot);
                ItemData part = string.IsNullOrEmpty(partId) ? null : catalog.GetItem(partId);
                var capSlot = slot;
                m.slots.Add(new LastTruck.TruckStorageView.PartSlot
                {
                    slotIcon = LastTruck.UiIcons.Get(SlotIconNames[s]),
                    slotName = TruckUpgradeRules.SlotNames[s],
                    part = part,
                    hover = part != null
                        ? part.itemName + " - " + TruckUpgradeRules.PartEffect(partId) + "  (누르면 뺍니다)"
                        : TruckUpgradeRules.SlotNames[s] + " - " + SlotHint(slot),
                    onClick = part != null ? () => LastTruck.Networking.NetworkTruck.RemovePart(capSlot) : (System.Action)null,
                });
            }

            // 보관함의 장착 가능한 파츠
            foreach (ItemData it in catalog.Items)
            {
                if (it == null || !TruckUpgradeRules.IsPart(it.itemID) || truckInv == null) continue;
                int count = truckInv.GetItemCount(it);
                if (count <= 0) continue;
                ItemData cap = it;
                m.candidates.Add(new LastTruck.TruckStorageView.PartCandidate
                {
                    item = it, count = count,
                    hover = it.itemName + " - " + TruckUpgradeRules.PartEffect(it.itemID) + "  (누르면 장착)",
                    onClick = () => LastTruck.Networking.NetworkTruck.InstallPart(cap),
                });
            }

            // 스탯 레벨
            for (int i = 0; i < TruckUpgradeRules.StatCount; i++)
            {
                var stat = (TruckStat)i;
                bool turret = TruckUpgradeRules.IsTurretStat(stat);
                bool locked = turret && !state.HasTurret;
                int level = state.LevelOf(stat);
                bool max = level >= TruckUpgradeRules.MaxLevel;
                string val = TruckUpgradeRules.Format(stat, state.ValueAt(stat, level));
                if (!max) val += "  →  " + TruckUpgradeRules.Format(stat, state.ValueAt(stat, level + 1));

                var row = new LastTruck.TruckStorageView.StatRow
                {
                    icon = LastTruck.UiIcons.Get(StatIconNames[i]),
                    iconTint = turret ? new Color(1f, 0.82f, 0.62f) : Color.white,
                    name = TruckUpgradeRules.StatNames[i],
                    locked = locked, maxed = max, level = level, maxLevel = TruckUpgradeRules.MaxLevel,
                    value = val,
                    hover = TruckUpgradeRules.StatNames[i] + (locked ? "  (포탑을 장착해야 강화할 수 있어요)" : max ? "  (최대 레벨)" : "  (+ 를 누르면 레벨 업)"),
                };
                if (!locked && !max)
                {
                    List<RecipeIngredient> cost = TruckUpgradeRules.LevelCost(level);
                    foreach (RecipeIngredient ing in cost)
                        row.costs.Add(new LastTruck.TruckStorageView.CostChip
                        {
                            item = ing.item, need = ing.count, have = truckInv != null ? truckInv.GetItemCount(ing.item) : 0,
                        });
                    row.canAfford = TruckFacilities.CanAfford(truckInv, cost);
                    var capStat = stat;
                    row.onUpgrade = () => LastTruck.Networking.NetworkTruck.Upgrade(capStat);
                }
                m.stats.Add(row);
            }

            _storageView.ShowUpgrade(m);
            _storageToolkitActive = true;
            return true;
        }
    }
}
