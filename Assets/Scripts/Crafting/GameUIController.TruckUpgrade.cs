using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 창의 '강화' 책갈피: 왼쪽 = 파츠 4자리(엔진/전면/장갑/지붕) 장착·해제, 오른쪽 = 스탯 레벨 강화(+ 버튼).
    /// 비용과 파츠는 트럭 보관함에서 낸다. 멀티플레이에서는 호스트가 처리하고 모두에게 동기화된다 (NetworkTruck).
    ///
    /// 글자를 줄이고 그림으로 보여 준다: 자리/스탯은 그림(Resources/UI/Icons), 비용은 아이템 그림 + 숫자.
    /// 자세한 설명(이름/효과)은 마우스를 올리면 아래 한 줄에만 나온다. 그림 파일이 없으면 예전처럼 글자로 보인다.
    /// </summary>
    public partial class GameUIController
    {
        private bool _truckUpgradeOpen;
        private TruckUpgradeState _watchedUpgrades;

        private static readonly Color UpgradeAccent = new Color(0.52f, 0.86f, 0.62f);

        // 자리/스탯에 쓰는 그림 이름 (TruckPartSlot / TruckStat 순서)
        private static readonly string[] SlotIconNames = { "engine", "bumper", "plate", "turret" };
        private static readonly string[] StatIconNames = { "shield", "speed", "bulb", "sword", "turret", "rate", "crosshair", "burst" };

        private Text _upgradeInfo;

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

        // 마우스를 올리면 맨 아래 한 줄에 설명을 보여 준다
        private void SetUpgradeInfo(string text)
        {
            if (_upgradeInfo != null) _upgradeInfo.text = text ?? "";
        }

        private void AddHoverInfo(GameObject target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;
            var trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => SetUpgradeInfo(text));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => SetUpgradeInfo(""));
            trigger.triggers.Add(exit);
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
            _upgradeInfo = null;

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
            float colH = inner.y - 60f - 36f;

            // 맨 아래 한 줄: 마우스를 올린 것의 이름/효과
            var info = CraftUI.Mk("Info", root);
            CraftUI.TopLeft(info, 8f, inner.y - 46f, w - 16f, 30f);
            _upgradeInfo = CraftUI.Txt(info, "", 18, FontStyle.Bold, CraftUI.Cream, TextAnchor.MiddleLeft);

            // 위쪽 제목 줄: 그림 하나 (글자 제목 대신)
            Image head = LastTruck.UiIcons.Make(root, "upgrade", new Vector2(40f, 40f));
            if (head != null)
            {
                var hrt = (RectTransform)head.transform;
                hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f); hrt.pivot = new Vector2(0f, 1f);
                hrt.anchoredPosition = new Vector2(6f, 4f);
            }
            else
            {
                var title = CraftUI.Mk("Title", root); CraftUI.TopLeft(title, 6f, 0f, w, 34f);
                CraftUI.Txt(title, "트럭 강화", 26, FontStyle.Bold, UpgradeAccent, TextAnchor.MiddleLeft);
            }

            BuildPartsColumn(root, state, truckInv, 0f, 46f, leftW, colH);
            BuildLevelsColumn(root, state, truckInv, rightX, 46f, rightW, colH);
        }

        // ───── 왼쪽: 파츠 (2×2 칸 + 보관함의 장착 가능한 파츠) ─────
        private void BuildPartsColumn(RectTransform root, TruckUpgradeState state, TruckInventory truckInv, float x, float y, float w, float h)
        {
            var panel = CraftUI.Panel(root, "Parts", CraftUI.PanelCol);
            CraftUI.TopLeft(panel, x, y, w, h);

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            float gap = 12f;
            float cardW = (w - gap * 3f) / 2f;
            float cardH = 132f;

            for (int s = 0; s < TruckUpgradeRules.SlotCount; s++)
            {
                var slot = (TruckPartSlot)s;
                string partId = state.PartAt(slot);
                ItemData part = string.IsNullOrEmpty(partId) ? null : catalog.GetItem(partId);

                float cx = gap + (s % 2) * (cardW + gap);
                float cy = gap + (s / 2) * (cardH + gap);

                Color cardColor = part != null ? new Color(0.20f, 0.30f, 0.22f, 1f) : CraftUI.CardCol;
                var card = CraftUI.Panel(panel, "Slot_" + s, cardColor, true);
                CraftUI.TopLeft(card, cx, cy, cardW, cardH);

                // 큰 그림: 끼워진 파츠, 비어 있으면 자리 모양(흐리게)
                var big = CraftUI.Mk("Big", card);
                big.anchorMin = big.anchorMax = new Vector2(0.5f, 0.5f);
                big.pivot = new Vector2(0.5f, 0.5f);
                big.anchoredPosition = Vector2.zero;
                big.sizeDelta = new Vector2(cardH - 36f, cardH - 36f);
                if (part != null)
                {
                    CraftUI.ItemIcon(big, part);
                }
                else
                {
                    Image silhouette = LastTruck.UiIcons.Make(big, SlotIconNames[s], big.sizeDelta, new Color(1f, 1f, 1f, 0.28f));
                    if (silhouette != null) CraftUI.Stretch((RectTransform)silhouette.transform);
                    else CraftUI.Txt(big, TruckUpgradeRules.SlotNames[s], 20, FontStyle.Bold, CraftUI.Dim, TextAnchor.MiddleCenter);
                }

                string hover;
                if (part != null)
                {
                    // 끼워져 있으면: 작은 자리 그림(왼쪽 위) + 빼기 표시(오른쪽 위). 카드를 누르면 뺀다.
                    Image tag = LastTruck.UiIcons.Make(card, SlotIconNames[s], new Vector2(30f, 30f));
                    if (tag != null)
                    {
                        var trt = (RectTransform)tag.transform;
                        trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 1f);
                        trt.anchoredPosition = new Vector2(8f, -8f);
                    }
                    Image remove = LastTruck.UiIcons.Make(card, "close", new Vector2(26f, 26f));
                    if (remove != null)
                    {
                        var rrt = (RectTransform)remove.transform;
                        rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 1f); rrt.pivot = new Vector2(1f, 1f);
                        rrt.anchoredPosition = new Vector2(-8f, -8f);
                    }

                    var capSlot = slot;
                    var cardImg = card.GetComponent<Image>();
                    var btn = card.gameObject.AddComponent<Button>();
                    btn.targetGraphic = cardImg;
                    var cb = btn.colors; cb.highlightedColor = CraftUI.CardHover; cb.pressedColor = new Color(0.85f, 0.8f, 0.8f, 1f); btn.colors = cb;
                    btn.onClick.AddListener(() => LastTruck.Networking.NetworkTruck.RemovePart(capSlot));
                    hover = part.itemName + " - " + TruckUpgradeRules.PartEffect(partId) + "  (누르면 뺍니다)";
                }
                else
                {
                    hover = TruckUpgradeRules.SlotNames[s] + " - " + SlotHint(slot);
                }
                AddHoverInfo(card.gameObject, hover);
            }

            // 장착 가능한 파츠 (트럭 보관함에 있는 것): 그림 + 개수, 누르면 장착
            float cy2 = gap + 2f * (cardH + gap) + 6f;
            var line = CraftUI.Mk("Line", panel); CraftUI.TopLeft(line, 12f, cy2 - 6f, w - 24f, 3f);
            CraftUI.Img(line, null, new Color(1f, 0.9f, 0.7f, 0.18f));

            var cands = new List<ItemData>();
            foreach (ItemData it in catalog.Items)
                if (it != null && TruckUpgradeRules.IsPart(it.itemID) && truckInv != null && truckInv.GetItemCount(it) > 0)
                    cands.Add(it);

            if (cands.Count == 0)
            {
                // 비어 있음: 흐린 보관함 그림 하나
                Image none = LastTruck.UiIcons.Make(panel, "crate", new Vector2(64f, 64f), new Color(1f, 1f, 1f, 0.3f));
                if (none != null)
                {
                    var nrt = (RectTransform)none.transform;
                    nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f); nrt.pivot = new Vector2(0.5f, 1f);
                    nrt.anchoredPosition = new Vector2(0f, -(cy2 + 16f));
                    AddHoverInfo(none.gameObject, "보관함에 파츠가 없습니다. 제작 탭에서 만들어 보세요.");
                }
                return;
            }

            float bw = (w - 24f - 8f) / 2f;
            int col = 0;
            foreach (ItemData it in cands)
            {
                ItemData cap = it;
                var btn = CraftUI.MkButton(panel, "Cand_" + it.itemID, "", CraftUI.BtnGreen, 16, () => LastTruck.Networking.NetworkTruck.InstallPart(cap));
                var rt = (RectTransform)btn.transform;
                CraftUI.TopLeft(rt, 12f + col * (bw + 8f), cy2 + 6f, bw, 56f);
                var ic = CraftUI.Mk("Icon", rt); CraftUI.TopLeft(ic, 8f, 6f, 44f, 44f); CraftUI.ItemIcon(ic, it);
                var lt = rt.Find("Label").GetComponent<Text>();
                lt.text = "×" + truckInv.GetItemCount(it); lt.fontSize = 20;
                lt.alignment = TextAnchor.MiddleRight;
                ((RectTransform)lt.transform).offsetMax = new Vector2(-12f, 0f);
                AddHoverInfo(btn.gameObject, it.itemName + " - " + TruckUpgradeRules.PartEffect(it.itemID) + "  (누르면 장착)");
                col++;
                if (col == 2) { col = 0; cy2 += 62f; }
            }
        }

        private static string SlotHint(TruckPartSlot slot)
        {
            switch (slot)
            {
                case TruckPartSlot.Engine: return "개조 엔진 +25% / 터보 엔진 +50% → 이동속도·가속";
                case TruckPartSlot.Front: return "강철가시 범퍼 / 분쇄 휠 → 좀비 밀치기·로드킬";
                case TruckPartSlot.Armor: return "복합 장갑 (내구도 +25%, 피해 -25%) / 합금 장갑 (내구도 +50%, 피해 -40%)";
                default: return "포탑 6종 (미니·기관포·저격·박격포·화염·레일) → 동승석 1번에서 마우스로 조준 사격";
            }
        }

        // ───── 오른쪽: 레벨 강화 (스탯 8줄: 그림 + 레벨 막대 + 값 + 비용 + 버튼) ─────
        private void BuildLevelsColumn(RectTransform root, TruckUpgradeState state, TruckInventory truckInv, float x, float y, float w, float h)
        {
            var panel = CraftUI.Panel(root, "Levels", CraftUI.PanelCol);
            CraftUI.TopLeft(panel, x, y, w, h);

            float rowH = (h - 20f) / TruckUpgradeRules.StatCount - 6f;
            float ry = 12f;
            for (int i = 0; i < TruckUpgradeRules.StatCount; i++)
            {
                var stat = (TruckStat)i;
                bool turret = TruckUpgradeRules.IsTurretStat(stat);
                bool locked = turret && !state.HasTurret;
                int level = state.LevelOf(stat);
                bool max = level >= TruckUpgradeRules.MaxLevel;

                var row = CraftUI.Panel(panel, "Stat_" + i, locked ? new Color(0.16f, 0.15f, 0.15f, 1f) : CraftUI.CardCol, true);
                CraftUI.TopLeft(row, 12f, ry, w - 24f, rowH);
                Color txt = locked ? new Color(1f, 1f, 1f, 0.35f) : CraftUI.Cream;
                AddHoverInfo(row.gameObject, TruckUpgradeRules.StatNames[i] + (locked ? "  (포탑을 장착해야 강화할 수 있어요)" : ""));

                // 스탯 그림 (글자 이름 대신)
                float iconSize = Mathf.Min(rowH - 10f, 46f);
                Image statIcon = LastTruck.UiIcons.Make(row, StatIconNames[i], new Vector2(iconSize, iconSize),
                    locked ? new Color(1f, 1f, 1f, 0.3f) : (turret ? new Color(1f, 0.82f, 0.62f) : Color.white));
                if (statIcon != null)
                {
                    var srt = (RectTransform)statIcon.transform;
                    srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f); srt.pivot = new Vector2(0f, 0.5f);
                    srt.anchoredPosition = new Vector2(12f, 0f);
                }
                else
                {
                    var nm = CraftUI.Mk("Name", row); CraftUI.TopLeft(nm, 14f, 0f, 130f, rowH);
                    CraftUI.Txt(nm, TruckUpgradeRules.StatNames[i], 18, FontStyle.Bold, turret && !locked ? new Color(0.96f, 0.7f, 0.5f) : txt, TextAnchor.MiddleLeft);
                }

                // 레벨 막대 (칸 10개) + 숫자
                float lvX = 76f;
                var lv = CraftUI.Mk("Lv", row); CraftUI.TopLeft(lv, lvX, 4f, 100f, rowH * 0.5f);
                CraftUI.Txt(lv, level.ToString(), 20, FontStyle.Bold, locked ? txt : CraftUI.Amber, TextAnchor.MiddleLeft);
                for (int p = 0; p < TruckUpgradeRules.MaxLevel; p++)
                {
                    var pip = CraftUI.Mk("Pip", row);
                    CraftUI.TopLeft(pip, lvX + p * 10f, rowH * 0.5f + 6f, 8f, 10f);
                    CraftUI.Img(pip, null, p < level ? (locked ? new Color(1f, 1f, 1f, 0.25f) : UpgradeAccent) : new Color(1f, 1f, 1f, 0.12f));
                }

                // 현재 → 다음 값 (숫자만)
                string val = TruckUpgradeRules.Format(stat, state.ValueAt(stat, level));
                if (!max) val += "  →  " + TruckUpgradeRules.Format(stat, state.ValueAt(stat, level + 1));
                var vt = CraftUI.Mk("Value", row); CraftUI.TopLeft(vt, 196f, 0f, 170f, rowH);
                CraftUI.Txt(vt, val, 18, FontStyle.Bold, locked ? txt : CraftUI.Green, TextAnchor.MiddleLeft);

                float btnW = 64f;
                float costX = 376f, costW = w - 24f - costX - btnW - 14f;
                if (locked)
                {
                    Image lk = LastTruck.UiIcons.Make(row, "lock", new Vector2(36f, 36f), new Color(1f, 1f, 1f, 0.7f));
                    if (lk != null)
                    {
                        var lrt = (RectTransform)lk.transform;
                        lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f); lrt.pivot = new Vector2(1f, 0.5f);
                        lrt.anchoredPosition = new Vector2(-16f, 0f);
                    }
                    else
                    {
                        var lkT = CraftUI.Mk("Locked", row); CraftUI.TopLeft(lkT, costX, 0f, costW + btnW, rowH);
                        CraftUI.Txt(lkT, "미니 포탑 장착 필요", 16, FontStyle.Normal, txt, TextAnchor.MiddleRight);
                    }
                }
                else if (max)
                {
                    Image star = LastTruck.UiIcons.Make(row, "star", new Vector2(40f, 40f));
                    if (star != null)
                    {
                        var mrt = (RectTransform)star.transform;
                        mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 0.5f); mrt.pivot = new Vector2(1f, 0.5f);
                        mrt.anchoredPosition = new Vector2(-16f, 0f);
                    }
                    else
                    {
                        var mx = CraftUI.Mk("Max", row); CraftUI.TopLeft(mx, costX, 0f, costW + btnW, rowH);
                        CraftUI.Txt(mx, "MAX", 20, FontStyle.Bold, CraftUI.Amber, TextAnchor.MiddleRight);
                    }
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
                        var chip = CraftUI.Panel(row, "Cost", new Color(0f, 0f, 0f, 0.25f), true);
                        CraftUI.TopLeft(chip, costX + c * chipW, (rowH - 40f) * 0.5f, chipW - 6f, 40f);
                        var ic = CraftUI.Mk("Icon", chip); CraftUI.TopLeft(ic, 3f, 4f, 32f, 32f); CraftUI.ItemIcon(ic, ing.item);
                        var ct = CraftUI.Mk("N", chip); CraftUI.TopLeft(ct, 36f, 0f, chipW - 42f, 40f);
                        CraftUI.Txt(ct, $"{have}/{ing.count}", 14, FontStyle.Bold, have >= ing.count ? CraftUI.Green : CraftUI.Red, TextAnchor.MiddleCenter);
                        AddHoverInfo(chip.gameObject, ing.item.itemName);
                    }
                    var capStat = stat;
                    var b = CraftUI.MkButton(row, "Plus", "", CraftUI.BtnGreen, 28, () => LastTruck.Networking.NetworkTruck.Upgrade(capStat), afford);
                    CraftUI.TopLeft((RectTransform)b.transform, w - 24f - btnW - 8f, (rowH - 44f) * 0.5f, btnW, 44f);
                    Image plus = LastTruck.UiIcons.Make(b.transform, "plus", new Vector2(30f, 30f), afford ? Color.white : new Color(1f, 1f, 1f, 0.4f));
                    if (plus == null) b.transform.Find("Label").GetComponent<Text>().text = "+";
                    var prt = plus != null ? (RectTransform)plus.transform : null;
                    if (prt != null) { prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.anchoredPosition = Vector2.zero; }
                }
                ry += rowH + 6f;
            }
        }
    }
}
