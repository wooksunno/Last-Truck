using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 창의 '강화' 책갈피: 왼쪽 = 파츠 4자리(엔진/전면/장갑/지붕) 장착·해제, 오른쪽 = 스탯 레벨 강화(+ 버튼).
    /// 비용과 파츠는 트럭 보관함에서 낸다. 멀티플레이에서는 호스트가 처리하고 모두에게 동기화된다 (NetworkTruck).
    /// </summary>
    public partial class GameUIController
    {
        private bool _truckUpgradeOpen;
        private TruckUpgradeState _watchedUpgrades;

        private static readonly Color UpgradeAccent = new Color(0.52f, 0.86f, 0.62f);

        public void OpenTruckUpgradePanel()
        {
            if (_activeTruck == null)
                _activeTruck = FindFirstObjectByType<TruckStation>();
            if (_activeTruck == null)
                return;

            _activePlayer = playerInventory;
            _activeFacility = null;
            _craftingPanelOpen = false;
            _truckProcessingPanelOpen = false;
            _truckCodexOpen = false;
            _codexSelectedItem = null;
            _selectedRecipe = null;
            _truckUpgradeOpen = true;
            ShowPopup("트럭 거점", BuildTruckContent);
        }

        private void WatchUpgrades(TruckUpgradeState state)
        {
            if (_watchedUpgrades == state) return;
            if (_watchedUpgrades != null) _watchedUpgrades.Changed -= OnWatchedTruckInventoryChanged;
            _watchedUpgrades = state;
            if (_watchedUpgrades != null) _watchedUpgrades.Changed += OnWatchedTruckInventoryChanged;
        }

        private void BuildTruckUpgradePanel()
        {
            if (_popupScrollRect != null)
                _popupScrollRect.vertical = false;

            TruckInventory truckInv = ActiveTruckInventory;
            TruckUpgradeState state = TruckUpgradeState.Instance;
            WatchUpgrades(state);
            Vector2 inner = PopupInnerSize();

            var rootGo = new GameObject("TruckUpgrade", typeof(RectTransform), typeof(LayoutElement));
            rootGo.transform.SetParent(_popupBody, false);
            var root = (RectTransform)rootGo.transform;
            var le = rootGo.GetComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = inner.y - 10f;

            if (state == null)
            {
                var msg = CraftUI.Mk("Msg", root); CraftUI.Stretch(msg);
                CraftUI.Txt(msg, "트럭을 찾을 수 없습니다.", 20, FontStyle.Bold, CraftUI.Cream, TextAnchor.MiddleCenter);
                return;
            }

            float w = inner.x - 8f;
            float leftW = Mathf.Round(w * 0.38f);
            float rightX = leftW + 18f;
            float rightW = w - rightX;

            // ── 제목 ──
            var title = CraftUI.Mk("Title", root); CraftUI.TopLeft(title, 6f, 0f, w, 34f);
            CraftUI.Txt(title, "트럭 강화", 26, FontStyle.Bold, UpgradeAccent, TextAnchor.MiddleLeft);
            var hint = CraftUI.Mk("Hint", root); CraftUI.TopLeft(hint, 150f, 4f, w - 150f, 30f);
            CraftUI.Txt(hint, "파츠와 강화 재료는 트럭 보관함에서 씁니다. 파츠를 바꾸면 원래 파츠는 보관함으로 돌아가요.", 16, FontStyle.Normal, CraftUI.Dim, TextAnchor.MiddleLeft);

            BuildPartsColumn(root, state, truckInv, 0f, 46f, leftW, inner.y - 60f);
            BuildLevelsColumn(root, state, truckInv, rightX, 46f, rightW, inner.y - 60f);
        }

        // ───── 왼쪽: 파츠 ─────
        private void BuildPartsColumn(RectTransform root, TruckUpgradeState state, TruckInventory truckInv, float x, float y, float w, float h)
        {
            var panel = CraftUI.Panel(root, "Parts", CraftUI.PanelCol);
            CraftUI.TopLeft(panel, x, y, w, h);
            var head = CraftUI.Mk("Head", panel); CraftUI.TopLeft(head, 16f, 10f, w - 32f, 30f);
            CraftUI.Txt(head, "파츠", 22, FontStyle.Bold, CraftUI.Amber, TextAnchor.MiddleLeft);

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            float cy = 48f;
            const float cardH = 92f;
            for (int s = 0; s < TruckUpgradeRules.SlotCount; s++)
            {
                var slot = (TruckPartSlot)s;
                string partId = state.PartAt(slot);
                ItemData part = string.IsNullOrEmpty(partId) ? null : catalog.GetItem(partId);

                var card = CraftUI.Panel(panel, "Slot_" + s, CraftUI.CardCol);
                CraftUI.TopLeft(card, 12f, cy, w - 24f, cardH);

                var tag = CraftUI.Panel(card, "SlotTag", part != null ? UpgradeAccent : new Color(0.35f, 0.32f, 0.3f));
                CraftUI.TopLeft(tag, 8f, 8f, 56f, 26f);
                var tagT = CraftUI.Mk("T", tag); CraftUI.Stretch(tagT);
                CraftUI.Txt(tagT, TruckUpgradeRules.SlotNames[s], 15, FontStyle.Bold, part != null ? new Color(0.1f, 0.18f, 0.12f) : CraftUI.Cream, TextAnchor.MiddleCenter);

                var iconBox = CraftUI.Panel(card, "IconBox", new Color(0f, 0f, 0f, 0.25f));
                CraftUI.TopLeft(iconBox, 8f, 38f, 56f, 48f);
                if (part != null) { var ic = CraftUI.Mk("Icon", iconBox); CraftUI.Stretch(ic, 4, 4, 4, 4); CraftUI.ItemIcon(ic, part); }

                var name = CraftUI.Mk("Name", card); CraftUI.TopLeft(name, 76f, 10f, w - 220f, 28f);
                CraftUI.Txt(name, part != null ? part.itemName : "비어 있음", 20, FontStyle.Bold, part != null ? CraftUI.Cream : CraftUI.Dim, TextAnchor.MiddleLeft);
                var eff = CraftUI.Mk("Effect", card); CraftUI.TopLeft(eff, 76f, 40f, w - 210f, 46f);
                Text effT = CraftUI.Txt(eff, part != null ? TruckUpgradeRules.PartEffect(partId) : SlotHint(slot), 15, FontStyle.Normal, part != null ? UpgradeAccent : CraftUI.Dim, TextAnchor.UpperLeft);
                effT.horizontalOverflow = HorizontalWrapMode.Wrap;

                if (part != null)
                {
                    var capSlot = slot;
                    var b = CraftUI.MkButton(card, "Remove", "빼기", CraftUI.BtnBrown, 17, () => LastTruck.Networking.NetworkTruck.RemovePart(capSlot));
                    CraftUI.TopLeft((RectTransform)b.transform, w - 24f - 96f, 28f, 84f, 38f);
                }
                cy += cardH + 8f;
            }

            // 장착 가능한 파츠 (트럭 보관함에 있는 것)
            var sub = CraftUI.Mk("CandHead", panel); CraftUI.TopLeft(sub, 16f, cy + 4f, w - 32f, 28f);
            CraftUI.Txt(sub, "트럭 보관함의 파츠 (눌러서 장착)", 18, FontStyle.Bold, CraftUI.Amber, TextAnchor.MiddleLeft);
            cy += 38f;

            var cands = new List<ItemData>();
            foreach (ItemData it in catalog.Items)
                if (it != null && TruckUpgradeRules.IsPart(it.itemID) && truckInv != null && truckInv.GetItemCount(it) > 0)
                    cands.Add(it);

            if (cands.Count == 0)
            {
                var none = CraftUI.Mk("None", panel); CraftUI.TopLeft(none, 16f, cy, w - 32f, 60f);
                Text nt = CraftUI.Txt(none, "보관함에 파츠가 없습니다. 제작 탭에서 개조 엔진 · 강철가시 범퍼 · 분쇄 휠 · 트럭 복합 장갑 · 미니 포탑을 만들어 보세요.", 15, FontStyle.Normal, CraftUI.Dim, TextAnchor.UpperLeft);
                nt.horizontalOverflow = HorizontalWrapMode.Wrap;
                return;
            }

            float bx = 12f, bw = (w - 24f - 8f) / 2f;
            int col = 0;
            foreach (ItemData it in cands)
            {
                ItemData cap = it;
                TruckUpgradeRules.TryGetSlot(it.itemID, out TruckPartSlot slot);
                var btn = CraftUI.MkButton(panel, "Cand_" + it.itemID, "", CraftUI.BtnGreen, 16, () => LastTruck.Networking.NetworkTruck.InstallPart(cap));
                var rt = (RectTransform)btn.transform;
                CraftUI.TopLeft(rt, bx + col * (bw + 8f), cy, bw, 46f);
                var ic = CraftUI.Mk("Icon", rt); CraftUI.TopLeft(ic, 6f, 5f, 36f, 36f); CraftUI.ItemIcon(ic, it);
                var lt = rt.Find("Label").GetComponent<Text>();
                lt.text = $"{it.itemName} ×{truckInv.GetItemCount(it)}"; lt.fontSize = 15;
                lt.alignment = TextAnchor.MiddleLeft;
                ((RectTransform)lt.transform).offsetMin = new Vector2(48f, 0f);
                col++;
                if (col == 2) { col = 0; cy += 52f; }
            }
        }

        private static string SlotHint(TruckPartSlot slot)
        {
            switch (slot)
            {
                case TruckPartSlot.Engine: return "개조 엔진 → 이동속도·가속 +25%";
                case TruckPartSlot.Front: return "강철가시 범퍼 / 분쇄 휠 → 좀비 밀치기·로드킬";
                case TruckPartSlot.Armor: return "트럭 복합 장갑 → 내구도 +25%, 받는 피해 -25%";
                default: return "미니 포탑 → 동승석 1번에서 마우스로 조준 사격";
            }
        }

        // ───── 오른쪽: 레벨 강화 ─────
        private void BuildLevelsColumn(RectTransform root, TruckUpgradeState state, TruckInventory truckInv, float x, float y, float w, float h)
        {
            var panel = CraftUI.Panel(root, "Levels", CraftUI.PanelCol);
            CraftUI.TopLeft(panel, x, y, w, h);
            var head = CraftUI.Mk("Head", panel); CraftUI.TopLeft(head, 16f, 10f, w - 32f, 30f);
            CraftUI.Txt(head, "레벨 강화  (최대 Lv.10 · 같은 레벨이면 비용이 같아요)", 22, FontStyle.Bold, CraftUI.Amber, TextAnchor.MiddleLeft);

            float rowH = (h - 56f) / TruckUpgradeRules.StatCount - 6f;
            float ry = 48f;
            for (int i = 0; i < TruckUpgradeRules.StatCount; i++)
            {
                var stat = (TruckStat)i;
                bool turret = TruckUpgradeRules.IsTurretStat(stat);
                bool locked = turret && !state.HasTurret;
                int level = state.LevelOf(stat);
                bool max = level >= TruckUpgradeRules.MaxLevel;

                var row = CraftUI.Panel(panel, "Stat_" + i, locked ? new Color(0.16f, 0.15f, 0.15f, 1f) : CraftUI.CardCol);
                CraftUI.TopLeft(row, 12f, ry, w - 24f, rowH);
                Color txt = locked ? new Color(1f, 1f, 1f, 0.35f) : CraftUI.Cream;

                var nm = CraftUI.Mk("Name", row); CraftUI.TopLeft(nm, 14f, 0f, 130f, rowH);
                CraftUI.Txt(nm, TruckUpgradeRules.StatNames[i], 18, FontStyle.Bold, turret && !locked ? new Color(0.96f, 0.7f, 0.5f) : txt, TextAnchor.MiddleLeft);

                // 레벨 막대
                var lv = CraftUI.Mk("Lv", row); CraftUI.TopLeft(lv, 146f, 4f, 80f, rowH * 0.5f);
                CraftUI.Txt(lv, $"Lv.{level}", 17, FontStyle.Bold, locked ? txt : CraftUI.Amber, TextAnchor.MiddleLeft);
                for (int p = 0; p < TruckUpgradeRules.MaxLevel; p++)
                {
                    var pip = CraftUI.Mk("Pip", row);
                    CraftUI.TopLeft(pip, 146f + p * 8f, rowH * 0.5f + 6f, 6f, 8f);
                    CraftUI.Img(pip, null, p < level ? (locked ? new Color(1f, 1f, 1f, 0.25f) : UpgradeAccent) : new Color(1f, 1f, 1f, 0.12f));
                }

                // 현재 → 다음 값
                string val = TruckUpgradeRules.Format(stat, TruckUpgradeRules.Value(stat, level));
                if (!max) val += "  →  " + TruckUpgradeRules.Format(stat, TruckUpgradeRules.Value(stat, level + 1));
                var vt = CraftUI.Mk("Value", row); CraftUI.TopLeft(vt, 236f, 0f, 150f, rowH);
                CraftUI.Txt(vt, val, 17, FontStyle.Bold, locked ? txt : CraftUI.Green, TextAnchor.MiddleLeft);

                float btnW = 64f;
                float costX = 388f, costW = w - 24f - costX - btnW - 14f;
                if (locked)
                {
                    var lk = CraftUI.Mk("Locked", row); CraftUI.TopLeft(lk, costX, 0f, costW + btnW, rowH);
                    CraftUI.Txt(lk, "미니 포탑 장착 필요", 16, FontStyle.Normal, txt, TextAnchor.MiddleRight);
                }
                else if (max)
                {
                    var mx = CraftUI.Mk("Max", row); CraftUI.TopLeft(mx, costX, 0f, costW + btnW, rowH);
                    CraftUI.Txt(mx, "MAX", 20, FontStyle.Bold, CraftUI.Amber, TextAnchor.MiddleRight);
                }
                else
                {
                    List<RecipeIngredient> cost = TruckUpgradeRules.LevelCost(level);
                    bool afford = TruckFacilities.CanAfford(truckInv, cost);
                    float chipW = cost.Count > 0 ? Mathf.Min(92f, costW / cost.Count) : 0f;
                    for (int c = 0; c < cost.Count; c++)
                    {
                        RecipeIngredient ing = cost[c];
                        int have = truckInv != null ? truckInv.GetItemCount(ing.item) : 0;
                        var chip = CraftUI.Panel(row, "Cost", new Color(0f, 0f, 0f, 0.25f));
                        CraftUI.TopLeft(chip, costX + c * chipW, (rowH - 40f) * 0.5f, chipW - 6f, 40f);
                        var ic = CraftUI.Mk("Icon", chip); CraftUI.TopLeft(ic, 3f, 4f, 32f, 32f); CraftUI.ItemIcon(ic, ing.item);
                        var ct = CraftUI.Mk("N", chip); CraftUI.TopLeft(ct, 36f, 0f, chipW - 42f, 40f);
                        CraftUI.Txt(ct, $"{have}/{ing.count}", 14, FontStyle.Bold, have >= ing.count ? CraftUI.Green : CraftUI.Red, TextAnchor.MiddleCenter);
                    }
                    var capStat = stat;
                    var b = CraftUI.MkButton(row, "Plus", "+", CraftUI.BtnGreen, 28, () => LastTruck.Networking.NetworkTruck.Upgrade(capStat), afford);
                    CraftUI.TopLeft((RectTransform)b.transform, w - 24f - btnW - 8f, (rowH - 44f) * 0.5f, btnW, 44f);
                }
                ry += rowH + 6f;
            }
        }
    }
}
