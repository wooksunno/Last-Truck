using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 트럭 창 "도감" 탭 (UI Toolkit).
    ///  - 분류 화면: 분류 칩 8개 → 아이템 타일 (누르면 제작 링크)
    ///  - 제작 링크 화면: 고른 아이템을 만드는 재료 트리(왼쪽 → 오른쪽) + 이 아이템의 활용처. 드래그/휠로 이동, 노드를 누르면 그 아이템으로 이동.
    /// 그래프 배치 방식은 예전 uGUI 도감(TruckCodexView)과 같다.
    /// </summary>
    public partial class TruckStorageView
    {
        public class CodexModel
        {
            public List<Tab> tabs = new List<Tab>();
            public Action onClose;
            public int category;
            public ItemData item;          // null이면 분류 화면
            public int recipeIndex;
            public string backLabel;       // 제작 링크 화면의 뒤로 버튼 글자
            public Action onBack;
            public Action<int> onCategory;
            public Action<ItemData> onItem;
            public Action<int> onRecipeIndex;
        }

        const float GItemW = 92f, GItemH = 92f, GFacW = 168f, GFacH = 56f, GGap = 26f, GPad = 30f, GNameH = 44f;
        const float GRowH = GItemH + GNameH;
        const float GCol = GItemW + 2f * GGap + GFacW;
        const float GMargin = 260f;   // 그래프 바깥 여백(자유 이동용)
        const int GMaxDepth = 5;
        static readonly Color GLine = new Color(0.27f, 0.26f, 0.29f, 1f);

        private CodexModel _codex;

        public void ShowCodex(CodexModel m)
        {
            _codex = m;
            VisualElement frame = BeginWindow(m.item == null ? "codex" : "codex-graph", "도감", m.onClose);
            if (frame == null) return;

            string[] names = TruckCodexView.CategoryNames;
            int cat = Mathf.Clamp(m.category, 0, names.Length - 1);

            if (m.item == null)
            {
                // 분류 칩
                var toolbar = Row("tw-toolbar");
                ItemCatalog catalog = ItemCatalog.GetOrCreate();
                for (int i = 0; i < names.Length; i++)
                {
                    int idx = i;
                    ItemData rep = i < TruckCodexView.CategoryIconItems.Length ? catalog.GetItem(TruckCodexView.CategoryIconItems[i]) : null;
                    toolbar.Add(MakeChip(new Chip
                    {
                        icon = rep != null ? rep.icon : null, text = names[i], selected = i == cat, accent = TruckCodexView.CategoryColors[i],
                        tooltip = names[i], onClick = () => m.onCategory?.Invoke(idx),
                    }, "tw-chip tw-chip--cat"));
                }
                frame.Add(toolbar);

                var body = new VisualElement();
                body.AddToClassList("tw-body");
                UiToolkitSkin.Slice(body, _skin.panelBrownDark, 0.35f, new Color(0.40f, 0.32f, 0.28f, 1f));
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("tw-craft-line");
                line.style.backgroundColor = TruckCodexView.CategoryColors[cat];
                body.Add(line);

                var items = new List<ItemData>();
                foreach (ItemData it in catalog.Items)
                    if (it != null && TruckCodexView.CategoryOf(it) == cat) items.Add(it);
                body.Add(MakeLabel(names[cat] + "   " + items.Count + "종", "tw-proc-title tw-codex-cat-title"));

                _gridScroll = new ScrollView(ScrollViewMode.Vertical);
                _gridScroll.AddToClassList("tw-scroll");
                _gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                var grid = new VisualElement();
                grid.AddToClassList("tw-grid");
                foreach (ItemData it in items) grid.Add(MakeCodexTile(it, TruckCodexView.CategoryColors[cat], m));
                _gridScroll.Add(grid);
                body.Add(_gridScroll);
                frame.Add(body);

                _hoverLabel = MakeLabel("아이템을 누르면 무엇으로 만드는지(제작 링크)와 어디에 쓰이는지 보여 줘요.", "tw-hint");
                frame.Add(_hoverLabel);
            }
            else
            {
                BuildCodexGraph(frame, m, cat);
            }
            EndWindow(m.tabs);
        }

        private VisualElement MakeCodexTile(ItemData it, Color accent, CodexModel m)
        {
            var b = new Button(() => m.onItem?.Invoke(it)) { text = "" };
            b.AddToClassList("tw-card");
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, UiSkinUtil.SlotFilled);
            Color border = new Color(accent.r, accent.g, accent.b, 0.55f);
            b.style.borderTopColor = border; b.style.borderBottomColor = border; b.style.borderLeftColor = border; b.style.borderRightColor = border;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-card-icon");
            if (it.icon != null) icon.style.backgroundImage = new StyleBackground(it.icon);
            b.Add(icon);
            b.Add(MakeLabel(it.itemName, "tw-card-name"));
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(it.itemName + "  (누르면 제작 링크)"));
            return b;
        }

        // ------------------------------------------------------------------ 제작 링크 그래프

        private class GNode
        {
            public ItemData item; public int count; public RecipeData recipe;
            public readonly List<GNode> kids = new List<GNode>();
            public int depth; public float cy;
        }

        private float _gNextRow;
        private int _gMaxDepth;

        private void BuildCodexGraph(VisualElement frame, CodexModel m, int cat)
        {
            ItemData item = m.item;
            List<RecipeData> recs = TruckCodexView.FindRecipesProducing(item);
            int ri = Mathf.Clamp(m.recipeIndex, 0, Mathf.Max(0, recs.Count - 1));
            RecipeData rootRecipe = recs.Count > 0 ? recs[ri] : null;

            // 머리 줄: 뒤로 + 이름/설명 ........ 방법 칩
            var head = Row("tw-toolbar");
            Button back = SmallButton(m.backLabel ?? "← 뒤로", () => m.onBack?.Invoke());
            back.AddToClassList("tw-btn--back");
            head.Add(back);
            if (item.icon != null)
            {
                var ic = new VisualElement { pickingMode = PickingMode.Ignore };
                ic.AddToClassList("tw-proc-title-icon");
                ic.style.backgroundImage = new StyleBackground(item.icon);
                head.Add(ic);
            }
            int itemCat = TruckCodexView.CategoryOf(item);
            head.Add(MakeLabel(item.itemName, "tw-codex-title"));
            head.Add(MakeLabel(TruckCodexView.CategoryNames[itemCat] + (recs.Count == 0 ? "  ·  채집으로 얻는 원재료" : "  ·  제작법 " + recs.Count + "개"), "tw-codex-sub"));
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            head.Add(spacer);
            if (recs.Count > 1)
                for (int i = 0; i < recs.Count; i++)
                {
                    int idx = i;
                    head.Add(MakeChip(new Chip
                    {
                        text = "방법 " + (i + 1) + " · " + TruckCodexView.FacilityName(recs[i].requiredFacility), selected = i == ri,
                        accent = new Color(0.96f, 0.78f, 0.40f), onClick = () => m.onRecipeIndex?.Invoke(idx),
                    }, "tw-chip tw-chip--cat"));
                }
            frame.Add(head);

            // 그래프 영역 (양피지)
            var area = new VisualElement();
            area.AddToClassList("tw-graph-area");
            _gridScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _gridScroll.AddToClassList("tw-scroll");
            _gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            _gridScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            var content = new VisualElement { pickingMode = PickingMode.Ignore };
            content.AddToClassList("tw-graph-content");
            _gridScroll.Add(content);
            area.Add(_gridScroll);
            area.Add(MakeLabel("드래그하거나 휠(쉬프트+휠)로 이동 · 노드를 누르면 그 아이템으로", "tw-graph-hint"));
            frame.Add(area);
            EnableDragPan(_gridScroll);

            // 트리 배치
            GNode root = MakeGNode(item, rootRecipe != null && rootRecipe.output != null ? Mathf.Max(1, rootRecipe.output.count) : 1, 0, rootRecipe, new List<ItemData>());
            _gNextRow = 0f; _gMaxDepth = 0;
            AssignGRows(root);
            float graphRows = Mathf.Max(1f, _gNextRow);
            float x0 = GPad + _gMaxDepth * GCol;
            float graphW = x0 + GItemW + GPad;
            float graphH = GPad + graphRows * GRowH + 8f;

            var lines = new VisualElement { pickingMode = PickingMode.Ignore };
            var nodes = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.style.position = Position.Absolute; nodes.style.position = Position.Absolute;
            lines.style.left = GMargin; lines.style.top = GMargin; nodes.style.left = GMargin; nodes.style.top = GMargin;
            content.Add(lines);
            content.Add(nodes);

            DrawGNode(root, lines, nodes, true, m);

            if (rootRecipe == null)
                nodes.Add(PlaceLabel("채집으로 얻는 원재료", "tw-graph-note", x0 - 40f, graphRows * GRowH + 6f - 30f, GItemW + 80f));

            // 활용처: 이 아이템 → 시설 → 결과물
            List<RecipeData> uses = TruckCodexView.FindRecipesUsing(item);
            if (uses.Count > 0)
            {
                float y = graphH + 10f;
                nodes.Add(PlaceLabel("이 아이템의 활용처", "tw-graph-head", GPad, y, 400f));
                y += 40f;
                int shown = 0;
                foreach (RecipeData r in uses)
                {
                    if (r == null || r.output == null || r.output.item == null) continue;
                    if (shown++ >= 8) break;
                    float cy = y + GItemH * 0.5f;
                    int need = 1;
                    foreach (RecipeIngredient ing in r.inputs) if (ing != null && ing.item == item) need = Mathf.Max(1, ing.count);
                    DrawGItem(nodes, item, need, x0, cy, false, false, m);
                    float fx = x0 + GItemW + GGap;
                    DrawGLine(lines, x0 + GItemW, cy, fx, cy, true);
                    DrawGFacility(nodes, r, fx, cy, TruckCodexView.FindRecipesProducing(r.output.item).Count);
                    float rx = fx + GFacW + GGap;
                    DrawGLine(lines, fx + GFacW, cy, rx, cy, true);
                    DrawGItem(nodes, r.output.item, Mathf.Max(1, r.output.count), rx, cy, false, true, m);
                    y += GRowH;
                }
                graphH = y + 10f;
                graphW = Mathf.Max(graphW, x0 + GItemW + GGap + GFacW + GGap + GItemW + GPad);
            }
            graphH += GPad * 0.5f;
            graphW = Mathf.Max(graphW, 200f);

            content.style.width = graphW + GMargin * 2f;
            content.style.height = graphH + GMargin * 2f;

            // 처음에는 그래프가 화면 가운데(큰 그래프는 결과물 쪽)에 오도록
            ScrollView scroll = _gridScroll;
            float gw = graphW, gh = graphH;
            scroll.schedule.Execute(() =>
            {
                if (scroll.panel == null) return;
                Rect view = scroll.contentViewport.layout;
                float left = gw <= view.width - 80f ? GMargin + gw * 0.5f - view.width * 0.5f : GMargin + gw - view.width + 60f;
                float top = gh <= view.height - 40f ? GMargin + gh * 0.5f - view.height * 0.5f : GMargin - 10f;
                scroll.scrollOffset = new Vector2(Mathf.Max(0f, left), Mathf.Max(0f, top));
            }).ExecuteLater(30);

            _hoverLabel = MakeLabel("왼쪽 재료가 시설을 거쳐 오른쪽 결과물이 돼요.", "tw-hint");
            frame.Add(_hoverLabel);
        }

        private GNode MakeGNode(ItemData item, int count, int depth, RecipeData forced, List<ItemData> path)
        {
            var n = new GNode { item = item, count = count, depth = depth };
            RecipeData r = forced;
            if (r == null && depth < GMaxDepth && !path.Contains(item))
            {
                List<RecipeData> rs = TruckCodexView.FindRecipesProducing(item);
                if (rs.Count > 0) r = rs[0];
            }
            n.recipe = r;
            if (r != null && r.inputs != null)
            {
                path.Add(item);
                foreach (RecipeIngredient ing in r.inputs)
                    if (ing != null && ing.item != null && ing.count > 0)
                        n.kids.Add(MakeGNode(ing.item, ing.count, depth + 1, null, path));
                path.RemoveAt(path.Count - 1);
            }
            return n;
        }

        private void AssignGRows(GNode n)
        {
            _gMaxDepth = Mathf.Max(_gMaxDepth, n.depth);
            if (n.kids.Count == 0)
            {
                n.cy = _gNextRow * GRowH + GItemH * 0.5f;
                _gNextRow += 1f;
                return;
            }
            foreach (GNode k in n.kids) AssignGRows(k);
            n.cy = (n.kids[0].cy + n.kids[n.kids.Count - 1].cy) * 0.5f;
        }

        private void DrawGNode(GNode n, VisualElement lines, VisualElement nodes, bool isRoot, CodexModel m)
        {
            float itemX = GPad + (_gMaxDepth - n.depth) * GCol;
            if (n.kids.Count > 0 && n.recipe != null)
            {
                float facRight = itemX - GGap, facLeft = facRight - GFacW;
                float busX = facLeft - GGap * 0.5f;
                float minY = n.cy, maxY = n.cy;
                foreach (GNode k in n.kids)
                {
                    float kidRight = GPad + (_gMaxDepth - k.depth) * GCol + GItemW;
                    DrawGLine(lines, kidRight, k.cy, busX, k.cy, false);
                    minY = Mathf.Min(minY, k.cy); maxY = Mathf.Max(maxY, k.cy);
                }
                if (maxY > minY) DrawGLine(lines, busX, minY, busX, maxY, false);
                DrawGLine(lines, busX, n.cy, facLeft, n.cy, true);
                DrawGLine(lines, facRight, n.cy, itemX, n.cy, true);
                DrawGFacility(nodes, n.recipe, facLeft, n.cy, TruckCodexView.FindRecipesProducing(n.item).Count);
                foreach (GNode k in n.kids) DrawGNode(k, lines, nodes, false, m);
            }
            DrawGItem(nodes, n.item, n.count, itemX, n.cy, isRoot, !isRoot, m);
        }

        private static void DrawGLine(VisualElement parent, float x1, float y1, float x2, float y2, bool arrow)
        {
            const float T = 3f;
            var l = new VisualElement { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.backgroundColor = GLine;
            float x = Mathf.Min(x1, x2), y = Mathf.Min(y1, y2);
            float w = Mathf.Abs(x2 - x1), h = Mathf.Abs(y2 - y1);
            if (w >= h) { l.style.left = x; l.style.top = y - T * 0.5f; l.style.width = w; l.style.height = T; }
            else { l.style.left = x - T * 0.5f; l.style.top = y; l.style.width = T; l.style.height = h; }
            parent.Add(l);

            if (arrow)
            {
                var a = new ArrowHead { pickingMode = PickingMode.Ignore };
                a.style.position = Position.Absolute;
                a.style.left = x2 - 12f; a.style.top = y2 - 7f; a.style.width = 12f; a.style.height = 14f;
                parent.Add(a);
            }
        }

        private void DrawGItem(VisualElement parent, ItemData item, int count, float x, float cy, bool isRoot, bool clickable, CodexModel m)
        {
            VisualElement tile;
            if (clickable)
            {
                ItemData captured = item;
                var b = new Button(() => m.onItem?.Invoke(captured)) { text = "" };
                b.RegisterCallback<PointerEnterEvent>(_ => SetHint(captured.itemName + "  (누르면 이 아이템의 제작 링크)"));
                tile = b;
            }
            else tile = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.AddToClassList("tw-gnode");
            if (isRoot) tile.AddToClassList("tw-gnode--root");
            tile.style.left = x; tile.style.top = cy - GItemH * 0.5f;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-gnode-icon");
            if (item.icon != null) icon.style.backgroundImage = new StyleBackground(item.icon);
            tile.Add(icon);
            if (count > 1) tile.Add(MakeLabel("×" + count, "tw-gnode-count"));
            parent.Add(tile);
            parent.Add(PlaceLabel(item.itemName, isRoot ? "tw-gnode-name tw-gnode-name--root" : "tw-gnode-name", x - 20f, cy + GItemH * 0.5f + 2f, GItemW + 40f));
        }

        private void DrawGFacility(VisualElement parent, RecipeData recipe, float x, float cy, int altCount)
        {
            var node = new VisualElement { pickingMode = PickingMode.Ignore };
            node.AddToClassList("tw-gfac");
            node.style.left = x; node.style.top = cy - GFacH * 0.5f;
            Sprite fIcon = TruckCodexView.FacilityIcon(recipe.requiredFacility);
            if (fIcon != null)
            {
                var ib = new VisualElement { pickingMode = PickingMode.Ignore };
                ib.AddToClassList("tw-gfac-icon");
                ib.style.backgroundImage = new StyleBackground(fIcon);
                node.Add(ib);
            }
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.Add(MakeLabel(TruckCodexView.FacilityName(recipe.requiredFacility), "tw-gfac-name"));
            texts.Add(MakeLabel(recipe.requiredFacility == FacilityType.None ? "기본 조립" : recipe.processingSeconds.ToString("0.#") + "초", "tw-gfac-sub"));
            node.Add(texts);
            if (altCount > 1) node.Add(MakeLabel("기타 " + (altCount - 1), "tw-gfac-badge"));
            parent.Add(node);
        }

        private Label PlaceLabel(string text, string cls, float x, float y, float w)
        {
            Label l = MakeLabel(text, cls);
            l.style.position = Position.Absolute;
            l.style.left = x; l.style.top = y; l.style.width = w;
            return l;
        }

        /// <summary>그래프를 마우스로 끌어서 상하좌우로 이동.</summary>
        private static void EnableDragPan(ScrollView scroll)
        {
            bool dragging = false;
            Vector2 start = default, startOffset = default;
            scroll.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                dragging = true; start = e.position; startOffset = scroll.scrollOffset;
            }, TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                if ((e.pressedButtons & 1) == 0) { dragging = false; return; }
                Vector2 d = (Vector2)e.position - start;
                if (d.sqrMagnitude > 16f) scroll.scrollOffset = startOffset - d;
            });
            scroll.RegisterCallback<PointerUpEvent>(_ => dragging = false);
            scroll.RegisterCallback<PointerLeaveEvent>(_ => dragging = false);
        }

        /// <summary>오른쪽을 가리키는 작은 삼각형 화살표.</summary>
        private class ArrowHead : VisualElement
        {
            public ArrowHead() { generateVisualContent += Draw; }

            private static void Draw(MeshGenerationContext ctx)
            {
                Rect r = ctx.visualElement.contentRect;
                var p = ctx.painter2D;
                p.fillColor = GLine;
                p.BeginPath();
                p.MoveTo(new Vector2(0f, 0f));
                p.LineTo(new Vector2(r.width, r.height * 0.5f));
                p.LineTo(new Vector2(0f, r.height));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
