using System;
using System.Collections.Generic;
using LastTruck;
using UnityEngine;
using UnityEngine.UI;
using static CraftingSystem.CraftUI;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 제작 화면: 위쪽 폴더 탭(분류) → 왼쪽 아이템 카드 목록 → 오른쪽 상세(필요 재료/제작 버튼).
    /// </summary>
    public class TruckCraftView
    {
        readonly float _w, _h;
        readonly TruckInventory _inv;
        readonly TruckCraftingManager _craft;
        readonly CharacterAbilityController _ability;
        readonly int _category;
        readonly RecipeData _selected;
        readonly Action _onBack;
        readonly Action<int> _onCategory;
        readonly Action<RecipeData> _onSelect;
        readonly Action<RecipeData> _onCraft;
        readonly Action<ItemData> _onCodex;

        public TruckCraftView(float w, float h, TruckInventory inv, TruckCraftingManager craft, CharacterAbilityController ability,
            int category, RecipeData selected, Action onBack, Action<int> onCategory, Action<RecipeData> onSelect,
            Action<RecipeData> onCraft, Action<ItemData> onCodex)
        {
            _w = w; _h = h; _inv = inv; _craft = craft; _ability = ability; _category = category; _selected = selected;
            _onBack = onBack; _onCategory = onCategory; _onSelect = onSelect; _onCraft = onCraft; _onCodex = onCodex;
        }

        /// <summary>제작 가능한 분류 목록(레시피가 하나라도 있는 분류)</summary>
        public static List<int> AvailableCategories(TruckCraftingManager craft)
        {
            var set = new SortedSet<int>();
            if (craft != null)
                foreach (RecipeData r in craft.Recipes)
                    if (r != null && r.output != null && r.output.item != null)
                        set.Add(TruckCodexView.CategoryOf(r.output.item));
            return new List<int>(set);
        }

        public RectTransform Build(RectTransform parent)
        {
            var root = Mk("CraftRoot", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = _h; le.minHeight = _h; le.flexibleWidth = 1f;
            root.sizeDelta = new Vector2(0f, _h);

            // 상단 바
            var bar = Mk("TopBar", root);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
            bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(0, 50);
            float hintLeft = 8f;
            if (_onBack != null)
            {
                var back = MkButton(bar, "BackButton", "← 뒤로", BtnBrown, 18, () => _onBack?.Invoke());
                var brt = (RectTransform)back.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f); brt.pivot = new Vector2(0, 0.5f);
                brt.anchoredPosition = new Vector2(2, 0); brt.sizeDelta = new Vector2(104, 40);
                hintLeft = 124f;
            }
            var hint = Mk("Hint", bar); hint.anchorMin = Vector2.zero; hint.anchorMax = Vector2.one; hint.offsetMin = new Vector2(hintLeft, 0); hint.offsetMax = Vector2.zero;
            Txt(hint, "트럭에 쌓인 재료로 만들 수 있는 아이템입니다", 18, FontStyle.Normal, Dim, TextAnchor.MiddleLeft);

            // 분류 탭
            List<int> cats = AvailableCategories(_craft);
            int cur = cats.Contains(_category) ? _category : (cats.Count > 0 ? cats[0] : 0);

            var tabs = Mk("Tabs", root);
            tabs.anchorMin = new Vector2(0, 1); tabs.anchorMax = new Vector2(1, 1); tabs.pivot = new Vector2(0, 1);
            tabs.anchoredPosition = new Vector2(14, -56); tabs.sizeDelta = new Vector2(-28, 48);
            var hl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.LowerLeft; hl.spacing = 6;
            hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            foreach (int c in cats)
            {
                int cc = c;
                float w = Mathf.Max(96f, TruckCodexView.CategoryNames[c].Length * 20f + 34f);
                FolderTab(tabs, TruckCodexView.CategoryNames[c], TruckCodexView.CategoryColors[c], c == cur, w, () => _onCategory?.Invoke(cc));
            }

            // 본문 패널
            var panel = Panel(root, "Body", PanelCol, true);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero; panel.offsetMax = new Vector2(0, -102);
            var line = Mk("TopLine", panel);
            line.anchorMin = new Vector2(0, 1); line.anchorMax = new Vector2(1, 1); line.pivot = new Vector2(0.5f, 1);
            line.sizeDelta = new Vector2(-40, 4); line.anchoredPosition = new Vector2(0, -2);
            Img(line, null, TruckCodexView.CategoryColors[cur], false);

            // 왼쪽: 카드 목록
            var left = Mk("List", panel);
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.6f, 1);
            left.offsetMin = new Vector2(14, 14); left.offsetMax = new Vector2(-8, -14);
            RectTransform grid;
            NewScroll(left, false, true, 0, 0, 0, 0, out grid);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(124, 140); g.spacing = new Vector2(12, 12);
            g.padding = new RectOffset(6, 6, 8, 12);
            g.constraint = GridLayoutGroup.Constraint.Flexible; g.childAlignment = TextAnchor.UpperLeft;
            var csf = grid.gameObject.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (_craft != null)
            {
                foreach (RecipeData r in _craft.Recipes)
                {
                    if (r == null || r.output == null || r.output.item == null) continue;
                    if (TruckCodexView.CategoryOf(r.output.item) != cur) continue;
                    BuildCard(grid, r, TruckCodexView.CategoryColors[cur]);
                }
            }

            // 오른쪽: 상세
            var right = Panel(panel, "Detail", new Color(0.15f, 0.12f, 0.10f, 1f));
            right.anchorMin = new Vector2(0.6f, 0); right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(8, 14); right.offsetMax = new Vector2(-16, -16);
            BuildDetail(right);
            return root;
        }

        bool CanCraft(RecipeData r)
        {
            return _inv != null && _inv.HasIngredients(r, _ability);
        }

        void BuildCard(RectTransform grid, RecipeData r, Color accent)
        {
            bool can = CanCraft(r);
            bool sel = r == _selected;
            ItemData item = r.output.item;

            var card = Mk("Craft_" + r.recipeID, grid);
            var bg = Img(card, Round, can ? new Color(0.26f, 0.20f, 0.13f, 1f) : CardCol, true, true);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
            var cb = btn.colors; cb.highlightedColor = CardHover; cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = cb;
            RecipeData captured = r;
            btn.onClick.AddListener(() => _onSelect?.Invoke(captured));

            if (Ring != null)
            {
                var ring = Mk("Ring", card); Stretch(ring);
                Img(ring, Ring, sel ? Amber : (can ? new Color(accent.r, accent.g, accent.b, 0.55f) : new Color(1, 1, 1, 0.08f)));
            }

            var icon = Mk("Icon", card);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1); icon.pivot = new Vector2(0.5f, 1);
            icon.anchoredPosition = new Vector2(0, -14); icon.sizeDelta = new Vector2(72, 72);
            ItemIcon(icon, item, can ? 1f : 0.5f);

            var nm = Mk("Name", card);
            nm.anchorMin = new Vector2(0, 0); nm.anchorMax = new Vector2(1, 0); nm.pivot = new Vector2(0.5f, 0);
            nm.anchoredPosition = new Vector2(0, 8); nm.sizeDelta = new Vector2(-10, 44);
            var t = Txt(nm, item.itemName, 15, FontStyle.Bold, can ? Cream : Dim, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;

            if (can)
            {
                var dot = Mk("Ready", card);
                dot.anchorMin = dot.anchorMax = new Vector2(1, 1); dot.pivot = new Vector2(1, 1);
                dot.anchoredPosition = new Vector2(-8, -8); dot.sizeDelta = new Vector2(16, 16);
                Img(dot, Round, Green);
            }
        }

        void BuildDetail(RectTransform right)
        {
            if (_selected == null || _selected.output == null || _selected.output.item == null)
            {
                var hint = Mk("Hint", right); Stretch(hint, 24, 24, 24, 24);
                var t = Txt(hint, "왼쪽에서 만들 아이템을 고르세요", 22, FontStyle.Bold, Dim, TextAnchor.MiddleCenter);
                return;
            }

            ItemData item = _selected.output.item;
            bool isWeapon = item.itemType == ItemType.Weapon || item.isWeapon;
            bool can = CanCraft(_selected);

            // 헤더
            var slot = Mk("BigIcon", right);
            CraftUI.TopLeft(slot, 20, 20, 96, 96);
            Img(slot, Round, new Color(0.10f, 0.08f, 0.07f, 1f));
            var ic = Mk("Icon", slot); Stretch(ic, 10, 10, 10, 10);
            ItemIcon(ic, item);

            var title = Mk("Title", right); CraftUI.TopLeft(title, 130, 24, 380, 40);
            Txt(title, item.itemName, 28, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);
            var sub = Mk("Sub", right); CraftUI.TopLeft(sub, 130, 68, 380, 30);
            Txt(sub, TruckCodexView.CategoryNames[TruckCodexView.CategoryOf(item)] + "  ·  결과 x" + Mathf.Max(1, _selected.output.count), 17, FontStyle.Normal, Dim, TextAnchor.MiddleLeft);

            var head = Mk("NeedHead", right); CraftUI.TopLeft(head, 24, 134, 300, 30);
            Txt(head, "필요 재료", 20, FontStyle.Bold, Amber, TextAnchor.MiddleLeft);

            // 재료 줄
            float y = 172f;
            foreach (RecipeIngredient ing in _selected.inputs)
            {
                if (ing == null || ing.item == null || ing.count <= 0) continue;
                int have = _inv != null ? _inv.GetItemCount(ing.item) : 0;
                int orig = ing.count, need = orig;
                if (_ability != null) need = _ability.GetCalculatedCraftingCost(orig, isWeapon);
                bool enough = have >= need;

                var row = Mk("Ing_" + ing.item.itemID, right);
                CraftUI.TopLeft(row, 18, y, 100, 56);   // 폭은 아래에서 늘린다
                row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
                row.offsetMin = new Vector2(18, -(y + 56)); row.offsetMax = new Vector2(-18, -y);
                Img(row, Round, new Color(0.10f, 0.08f, 0.07f, 0.9f));

                var rs = Mk("Icon", row);
                rs.anchorMin = rs.anchorMax = new Vector2(0, 0.5f); rs.pivot = new Vector2(0, 0.5f);
                rs.anchoredPosition = new Vector2(8, 0); rs.sizeDelta = new Vector2(42, 42);
                ItemIcon(rs, ing.item);

                var nm = Mk("Name", row);
                nm.anchorMin = new Vector2(0, 0); nm.anchorMax = new Vector2(1, 1); nm.offsetMin = new Vector2(60, 0); nm.offsetMax = new Vector2(-120, 0);
                Txt(nm, ing.item.itemName, 18, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);

                var cnt = Mk("Count", row);
                cnt.anchorMin = new Vector2(1, 0); cnt.anchorMax = new Vector2(1, 1); cnt.pivot = new Vector2(1, 0.5f);
                cnt.anchoredPosition = new Vector2(-14, 0); cnt.sizeDelta = new Vector2(180, 0);
                string txt = need < orig
                    ? have + " / <color=#888888>" + orig + "</color> <color=#FFD700>→" + need + "</color>"
                    : have + " / " + need;
                var ct = Txt(cnt, txt, 20, FontStyle.Bold, enough ? Green : Red, TextAnchor.MiddleRight);
                ct.supportRichText = true;

                y += 62f;
            }

            // 버튼
            var codex = MkButton(right, "CodexButton", "제작 링크 보기", BtnBrown, 18, () => _onCodex?.Invoke(item));
            var crt = (RectTransform)codex.transform;
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(0.4f, 0); crt.pivot = new Vector2(0, 0);
            crt.offsetMin = new Vector2(20, 20); crt.offsetMax = new Vector2(0, 76);

            RecipeData rec = _selected;
            var make = MkButton(right, "CraftButton", can ? "제작하기" : "재료가 부족합니다", BtnGreen, 22, () => _onCraft?.Invoke(rec), can);
            var mrt = (RectTransform)make.transform;
            mrt.anchorMin = new Vector2(0.4f, 0); mrt.anchorMax = new Vector2(1, 0); mrt.pivot = new Vector2(0, 0);
            mrt.offsetMin = new Vector2(12, 20); mrt.offsetMax = new Vector2(-20, 76);
        }
    }
}
