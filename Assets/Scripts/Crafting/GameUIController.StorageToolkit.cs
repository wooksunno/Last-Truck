using LastTruck;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 거점 창의 다섯 탭(보관함/제작/가공/도감/강화)을 UI Toolkit(LastTruck.TruckStorageView)으로 그린다.
    /// 시설 팝업/파우치처럼 트럭 창이 아닌 화면은 아직 uGUI 창이다.
    ///  - UI Toolkit 탭이 보이는 동안 uGUI 창은 열린 상태(IsPopupOpen)를 유지한 채 투명하게 숨긴다 (입력 잠금 등 기존 동작 그대로).
    ///  - uGUI 창도 오른쪽 HUD 줄(미니맵/시계/스태미나/인벤토리 핫바)과 위쪽 트럭 판을 가리지 않게 왼쪽으로 옮기고, 넘치면 줄인다.
    /// </summary>
    public partial class GameUIController
    {
        // 창이 비워 둘 화면 가장자리 (기준 해상도 1920×1080)
        private const float HudRightReserve = 520f;
        private const float HudTopReserve = 96f;
        private const float HudLeftMargin = 30f;
        private const float HudBottomMargin = 24f;
        private const float TabStripWidth = 110f;

        private LastTruck.TruckStorageView _storageView;
        private bool _storageViewTried;
        private bool _storageToolkitActive;
        private CanvasGroup _popupGroup;

        /// <summary>보관함 탭을 UI Toolkit으로 그렸으면 true (uGUI 내용은 그리지 않는다).</summary>
        private bool TryShowStorageToolkit(TruckInventory truckInv)
        {
            if (!EnsureToolkitWindow() || truckInv == null)
                return false;

            _storageView.Show(BuildStorageModel(truckInv));
            _storageToolkitActive = true;
            return true;
        }

        private LastTruck.TruckStorageView.Model BuildStorageModel(TruckInventory truckInv)
        {
            var m = new LastTruck.TruckStorageView.Model { title = "트럭 거점", onClose = ClosePopup };

            m.tabs = BuildToolkitTabs(truckInv, 0);

            // 분류 칩: [전체] + 분류 8개 (설비는 보관함에 보이지 않으므로 세지 않는다)
            string[] names = TruckCodexView.CategoryNames;
            Color[] colors = TruckCodexView.CategoryColors;
            int[] counts = new int[names.Length];
            int total = 0;
            foreach (InventorySlot slot in truckInv.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.item == null || TruckFacilities.IsFacilityItem(slot.item)) continue;
                counts[Mathf.Clamp(TruckCodexView.CategoryOf(slot.item), 0, names.Length - 1)]++;
                total++;
            }
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            m.categories.Add(new LastTruck.TruckStorageView.Chip
            {
                icon = LastTruck.UiIcons.Get("grid"), text = total.ToString(), selected = _storageCategory < 0,
                accent = new Color(0.85f, 0.85f, 0.88f), tooltip = "전체",
                onClick = () => { _storageCategory = -1; ShowPopup("트럭 거점", BuildTruckContent); },
            });
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                ItemData rep = i < TruckCodexView.CategoryIconItems.Length ? catalog.GetItem(TruckCodexView.CategoryIconItems[i]) : null;
                m.categories.Add(new LastTruck.TruckStorageView.Chip
                {
                    icon = rep != null ? rep.icon : null, text = counts[i].ToString(), selected = _storageCategory == i,
                    accent = colors[i], tooltip = names[i],
                    onClick = () => { _storageCategory = idx; ShowPopup("트럭 거점", BuildTruckContent); },
                });
            }

            // 정렬
            string[] sortIcons = { "grid", "sortname", "sortcount" };
            for (int i = 0; i < sortIcons.Length; i++)
            {
                int idx = i;
                m.sorts.Add(new LastTruck.TruckStorageView.Chip
                {
                    icon = LastTruck.UiIcons.Get(sortIcons[i]), text = LastTruck.UiIcons.Get(sortIcons[i]) == null ? StorageSortLabels[i] : null,
                    selected = _storageSort == i, accent = new Color(0.95f, 0.78f, 0.35f), tooltip = StorageSortLabels[i],
                    onClick = () => { _storageSort = idx; ShowPopup("트럭 거점", BuildTruckContent); },
                });
            }

            // 보관함 칸
            foreach (InventorySlot slot in GetStorageSlotsSorted(truckInv))
            {
                InventorySlot captured = slot;
                m.slots.Add(new LastTruck.TruckStorageView.Slot
                {
                    item = slot.item, count = slot.count, durability01 = slot.DurabilityRatio,
                    onClick = () => OnStorageSlotClicked(truckInv, captured),
                });
            }
            m.emptyText = _storageCategory >= 0 ? "이 분류의 아이템이 없어요" : "보관함이 비어 있어요. 인벤토리 칸을 누르면 여기에 넣어요";

            // 꺼낼 개수 고르기
            if (_withdrawItem != null)
            {
                int have = truckInv.GetItemCount(_withdrawItem);
                if (have <= 0)
                    _withdrawItem = null;
                else
                {
                    ItemData item = _withdrawItem;
                    m.withdrawItem = item;
                    m.withdrawMax = have;
                    m.withdrawDefault = Mathf.Clamp(_withdrawMax, 1, have);
                    m.onWithdraw = amount =>
                    {
                        PlayerInventory receiver = _activePlayer != null ? _activePlayer : playerInventory;
                        if (receiver != null)
                            LastTruck.Networking.TruckInventorySync.Withdraw(truckInv, receiver, item, Mathf.Clamp(amount, 1, have));
                        _withdrawItem = null;
                        ShowPopup("트럭 거점", BuildTruckContent);
                    };
                    m.onWithdrawCancel = () => { _withdrawItem = null; ShowPopup("트럭 거점", BuildTruckContent); };
                }
            }
            return m;
        }

        /// <summary>오른쪽 책갈피 탭 (0 보관함 / 1 제작 / 2 가공 / 3 도감 / 4 강화).</summary>
        private List<LastTruck.TruckStorageView.Tab> BuildToolkitTabs(TruckInventory truckInv, int selected)
        {
            var tabs = new List<LastTruck.TruckStorageView.Tab>();
            bool anyFacility = TruckFacilities.AnyBuilt(truckInv);
            for (int i = 0; i < TruckTabLabels.Length; i++)
            {
                int idx = i;
                tabs.Add(new LastTruck.TruckStorageView.Tab
                {
                    icon = LastTruck.UiIcons.Get(TabIconNames[i]),
                    label = TruckTabLabels[i],
                    color = TruckTabColors[i],
                    selected = i == selected,
                    locked = i == 2 && !anyFacility,
                    onClick = () => OnTruckTab(idx),
                });
            }
            return tabs;
        }

        private bool EnsureToolkitWindow()
        {
            if (_storageView == null && !_storageViewTried)
            {
                _storageViewTried = true;
                _storageView = LastTruck.TruckStorageView.Create();
            }
            return _storageView != null;
        }

        /// <summary>제작 탭을 UI Toolkit으로 그렸으면 true (uGUI 내용은 그리지 않는다).</summary>
        private bool TryShowCraftToolkit()
        {
            TruckInventory inv = _activeTruck != null ? _activeTruck.TruckInventory : null;
            TruckCraftingManager craft = _activeTruck != null ? _activeTruck.CraftingManager : null;
            if (inv == null || craft == null || !EnsureToolkitWindow())
                return false;
            if (craft.Recipes.Count == 0)
                craft.LoadAssemblyRecipesFromCatalog();

            CharacterAbilityController ability = LastTruck.Networking.LocalPlayerLookup.AbilityController;
            var m = new LastTruck.TruckStorageView.CraftModel { title = "제작", onClose = ClosePopup, tabs = BuildToolkitTabs(inv, 1) };

            // 분류 (레시피가 있는 분류만)
            List<int> cats = TruckCraftView.AvailableCategories(craft);
            int cur = cats.Contains(_craftCat) ? _craftCat : (cats.Count > 0 ? cats[0] : 0);
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            foreach (int c in cats)
            {
                int cc = c;
                ItemData rep = c < TruckCodexView.CategoryIconItems.Length ? catalog.GetItem(TruckCodexView.CategoryIconItems[c]) : null;
                m.categories.Add(new LastTruck.TruckStorageView.Chip
                {
                    icon = rep != null ? rep.icon : null, text = TruckCodexView.CategoryNames[c], selected = c == cur,
                    accent = TruckCodexView.CategoryColors[c], tooltip = TruckCodexView.CategoryNames[c],
                    onClick = () => { _craftCat = cc; _selectedRecipe = null; ShowPopup("제작", BuildTruckContent); },
                });
            }
            m.accent = TruckCodexView.CategoryColors[Mathf.Clamp(cur, 0, TruckCodexView.CategoryColors.Length - 1)];

            System.Func<RecipeData, bool> installed = r => r != null && r.output != null && TruckFacilities.IsInstalledFacilityItem(inv, r.output.item);
            System.Func<RecipeData, bool> canCraft = r => !installed(r) && inv.HasIngredients(r, ability);

            // 카드
            foreach (RecipeData r in craft.Recipes)
            {
                if (r == null || r.output == null || r.output.item == null) continue;
                if (TruckCodexView.CategoryOf(r.output.item) != cur) continue;
                RecipeData captured = r;
                m.cards.Add(new LastTruck.TruckStorageView.CraftCard
                {
                    item = r.output.item, canCraft = canCraft(r), installed = installed(r), selected = r == _selectedRecipe,
                    onClick = () => { _selectedRecipe = captured; ShowPopup("제작", BuildTruckContent); },
                });
            }

            // 상세
            RecipeData sel = _selectedRecipe;
            if (sel != null && sel.output != null && sel.output.item != null)
            {
                ItemData item = sel.output.item;
                bool isWeapon = item.itemType == ItemType.Weapon || item.isWeapon;
                m.item = item;
                m.subtitle = TruckCodexView.CategoryNames[TruckCodexView.CategoryOf(item)] + "  ·  결과 ×" + Mathf.Max(1, sel.output.count);
                foreach (RecipeIngredient ing in sel.inputs)
                {
                    if (ing == null || ing.item == null || ing.count <= 0) continue;
                    int need = ability != null ? ability.GetCalculatedCraftingCost(ing.count, isWeapon) : ing.count;
                    m.ingredients.Add(new LastTruck.TruckStorageView.CraftIngredient
                    {
                        item = ing.item, have = inv.GetMaterialCount(ing.item), need = need, originalNeed = ing.count,
                    });
                }
                m.canCraft = canCraft(sel);
                m.craftLabel = m.canCraft ? "제작하기" : (installed(sel) ? "설치 완료" : "재료가 부족해요");
                m.onCraft = () =>
                {
                    LastTruck.Networking.TruckInventorySync.Craft(craft, sel);
                    AnnounceFacilityBuilt(sel);
                    ShowPopup("제작", BuildTruckContent);
                };
                m.onCodex = () =>
                {
                    _craftingPanelOpen = false;
                    _truckCodexOpen = true;
                    _codexFromCraft = true;
                    _codexSelectedItem = item;
                    ShowPopup("트럭 거점", BuildTruckContent);
                };
            }

            _storageView.ShowCraft(m);
            _storageToolkitActive = true;
            return true;
        }

        private void OnStorageSlotClicked(TruckInventory truckInv, InventorySlot slot)
        {
            if (slot == null || slot.item == null) return;

            // 도구/무기처럼 겹쳐지지 않는(최대 1개) 아이템은 수량을 묻지 않고 바로 꺼낸다
            if (slot.item.maxStack <= 1)
            {
                PlayerInventory receiver = _activePlayer != null ? _activePlayer : playerInventory;
                if (receiver != null)
                    LastTruck.Networking.TruckInventorySync.Withdraw(truckInv, receiver, slot.item, slot.count, slot.CurrentDurability);
                _withdrawItem = null;
                ShowPopup("트럭 거점", BuildTruckContent);
                return;
            }

            _withdrawItem = slot.item;
            _withdrawMax = slot.count;
            ShowPopup("트럭 거점", BuildTruckContent);
        }

        /// <summary>ShowPopup이 내용을 그린 뒤 호출: 보관함 탭이면 uGUI 창을 숨기고, 아니면 UI Toolkit 창을 숨긴다.</summary>
        private void ApplyPopupLayer(RectTransform popupRect)
        {
            if (_popupGroup == null && _popupRoot != null)
            {
                _popupGroup = _popupRoot.GetComponent<CanvasGroup>();
                if (_popupGroup == null) _popupGroup = _popupRoot.AddComponent<CanvasGroup>();
            }

            if (_storageToolkitActive)
            {
                if (_popupGroup != null) { _popupGroup.alpha = 0f; _popupGroup.blocksRaycasts = false; _popupGroup.interactable = false; }
            }
            else
            {
                if (_storageView != null) _storageView.Hide();
                if (_popupGroup != null) { _popupGroup.alpha = 1f; _popupGroup.blocksRaycasts = true; _popupGroup.interactable = true; }
            }

            FitPopupAwayFromHud(popupRect);
        }

        private void HideStorageToolkit()
        {
            _storageToolkitActive = false;
            if (_storageView != null) _storageView.Hide();
            if (_popupGroup != null) { _popupGroup.alpha = 1f; _popupGroup.blocksRaycasts = true; _popupGroup.interactable = true; }
        }

        /// <summary>
        /// uGUI 창을 오른쪽 HUD 줄과 위쪽 트럭 판을 피한 왼쪽 영역 가운데에 놓고, 넘치면 그만큼 줄인다 (책갈피 탭 포함).
        /// </summary>
        private void FitPopupAwayFromHud(RectTransform popupRect)
        {
            if (popupRect == null) return;
            var parent = popupRect.parent as RectTransform;
            if (parent == null) return;

            Vector2 canvas = parent.rect.size;
            if (canvas.x < 10f || canvas.y < 10f) return;
            Vector2 size = popupRect.sizeDelta;
            bool tabs = _activeTruck != null && _activeFacility == null;
            float tabW = tabs ? TabStripWidth : 0f;

            float availW = canvas.x - HudLeftMargin - HudRightReserve;
            float availH = canvas.y - HudTopReserve - HudBottomMargin;
            float s = Mathf.Min(1f, availW / (size.x + tabW), availH / size.y);

            popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.localScale = new Vector3(s, s, 1f);

            float spanCenterX = HudLeftMargin + availW * 0.5f;             // 창 + 탭이 차지하는 폭의 가운데
            float cx = spanCenterX - tabW * s * 0.5f;                       // 창 자체의 가운데
            float cy = HudBottomMargin + availH * 0.5f;
            popupRect.anchoredPosition = new Vector2(cx - canvas.x * 0.5f, cy - canvas.y * 0.5f);
        }
    }
}
