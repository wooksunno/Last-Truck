using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static CraftingSystem.CraftUI;

namespace CraftingSystem
{
    /// <summary>
    /// 제작 도감.
    ///  1) 분류 화면: 폴더 탭(원재료/가공 재료/도구/…)을 고르면 그 분류의 아이템이 격자로 나온다.
    ///  2) 제작 링크 화면: 아이템을 누르면 재료 → 가공 시설 → 결과물 노드 그래프로 이동한다.
    ///     그래프는 상하좌우로 자유롭게 끌어서(또는 휠/쉬프트+휠) 움직일 수 있다.
    ///  제작 링크에서 "뒤로"를 누르면 방금 보던 분류 화면으로 돌아온다.
    /// </summary>
    public class TruckCodexView
    {
        // ---------- 그래프 레이아웃 ----------
        const float ItemW = 92f, ItemH = 92f, FacW = 168f, FacH = 56f, Gap = 26f, Pad = 30f, NameH = 44f;
        const float RowH = ItemH + NameH;
        const float Col = ItemW + 2f * Gap + FacW;
        const float PanX = 760f, PanY = 420f;      // 그래프 바깥 여백(자유 이동용)
        const int MaxDepth = 5;

        static readonly Color GraphBg = Parchment;
        static readonly Color NodeCol = new Color(0.17f, 0.18f, 0.22f, 1f);
        static readonly Color LineCol = new Color(0.27f, 0.26f, 0.29f, 1f);
        static readonly Color DarkText = new Color(0.22f, 0.19f, 0.16f, 1f);

        // ---------- 분류 ----------
        public static readonly string[] CategoryNames = { "원재료", "가공 재료", "도구", "무기", "설비", "트럭 강화", "음식(소모품)" };
        public static readonly Color[] CategoryColors =
        {
            new Color(0.45f, 0.82f, 0.66f), new Color(0.96f, 0.78f, 0.40f), new Color(0.50f, 0.74f, 0.96f), new Color(0.96f, 0.55f, 0.50f),
            new Color(0.70f, 0.62f, 0.96f), new Color(0.98f, 0.66f, 0.34f), new Color(0.96f, 0.68f, 0.86f),
        };

        static readonly HashSet<string> WeaponIds = new HashSet<string>
        {
            ItemIds.WoodSpear, ItemIds.HuntingBow, ItemIds.Machete, ItemIds.FlameMachete, ItemIds.VibrationBlade,
            ItemIds.Pistol, ItemIds.Ak47, ItemIds.PlatinumSniperRifle, ItemIds.Flamethrower, ItemIds.ChemicalSprayer,
            ItemIds.ShredderDrillLauncher, ItemIds.IronFieldCannon, ItemIds.PlatinumRailCannon, ItemIds.FragGrenade, ItemIds.ChemicalGasGrenade,
        };
        static readonly HashSet<string> FacilityIds = new HashSet<string>
        {
            ItemIds.Campfire, ItemIds.Grindstone, ItemIds.LeatherTanningRack, ItemIds.RollerPressMachine,
            ItemIds.PrecisionCutterMachine, ItemIds.ChemicalRefineryTower, ItemIds.SuperheatedFurnace,
        };
        static readonly HashSet<string> TruckIds = new HashSet<string>
        {
            ItemIds.EmergencyPatchBoard, ItemIds.WeldingKit, ItemIds.HighTensionRepairPack,
            ItemIds.TruckCompositeArmor, ItemIds.SpikeBumper, ItemIds.GrinderWheel,
        };

        public static int CategoryOf(ItemData it)
        {
            if (it == null) return 1;
            string id = it.itemID ?? "";
            if (WeaponIds.Contains(id)) return 3;
            if (FacilityIds.Contains(id)) return 4;
            if (TruckIds.Contains(id)) return 5;
            switch (it.itemType)
            {
                case ItemType.Raw: return 0;
                case ItemType.Intermediate: return 1;
                case ItemType.Tool: return 2;
                case ItemType.Weapon: return 3;
                default: return it.isWeapon ? 3 : 6;     // 음식, 소생 키트, 장갑/파우치/트랩 등 소모·생존 아이템
            }
        }

        // ---------- 상태 ----------
        readonly Action _onBack;
        readonly Action<ItemData> _onSelected;
        readonly float _w, _h;

        RectTransform _root, _catPane, _graphPane, _tabsRow, _gridContent, _graphContent, _tabs2;
        Text _breadcrumb, _catTitle, _title, _subtitle;
        Image _catLine;
        ScrollRect _catScroll, _graphScroll;
        Button _backBtn;
        bool _graphMode;
        bool _cameFromOutside;   // 제작 화면 등 바깥에서 곧바로 제작 링크로 들어온 경우
        int _category;
        ItemData _selected;
        int _recipeIndex;

        public TruckCodexView(float width, float height, Action onBack, Action<ItemData> onSelected)
        {
            _w = width; _h = height; _onBack = onBack; _onSelected = onSelected;
        }

        // =====================================================================================
        // 생성
        // =====================================================================================
        public RectTransform Build(RectTransform parent, ItemData initial)
        {
            _root = Mk("CodexRoot", parent);
            var le = _root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = _h; le.minHeight = _h; le.flexibleWidth = 1f;
            _root.sizeDelta = new Vector2(0f, _h);   // 부모 VerticalLayoutGroup이 높이를 제어하지 않으므로 직접 지정

            BuildTopBar();
            BuildCategoryPane();
            BuildGraphPane();

            if (initial != null) { _category = CategoryOf(initial); _cameFromOutside = true; ShowItem(initial); }
            else ShowCategory(0);
            return _root;
        }

        void BuildTopBar()
        {
            var bar = Mk("TopBar", _root);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
            bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(0, 50);

            _backBtn = MkButton(bar, "BackButton", "← 뒤로", BtnBrown, 18, OnBack);
            var brt = (RectTransform)_backBtn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f); brt.pivot = new Vector2(0, 0.5f);
            brt.anchoredPosition = new Vector2(2, 0); brt.sizeDelta = new Vector2(104, 40);

            var bc = Mk("Breadcrumb", bar);
            bc.anchorMin = new Vector2(0, 0); bc.anchorMax = new Vector2(1, 1);
            bc.offsetMin = new Vector2(124, 0); bc.offsetMax = Vector2.zero;
            _breadcrumb = Txt(bc, "도감", 20, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);
        }

        void OnBack()
        {
            if (_graphMode && !_cameFromOutside) ShowCategory(_category);   // 제작 링크 → 방금 보던 분류 창
            else _onBack?.Invoke();                    // 분류 창 → 트럭 거점
        }

        // ---------- 분류 화면 ----------
        void BuildCategoryPane()
        {
            _catPane = Mk("CategoryPane", _root);
            _catPane.anchorMin = Vector2.zero; _catPane.anchorMax = Vector2.one;
            _catPane.offsetMin = Vector2.zero; _catPane.offsetMax = new Vector2(0, -56);

            // 폴더 탭 줄 (맨 위, 아래쪽 정렬)
            _tabsRow = Mk("Tabs", _catPane);
            _tabsRow.anchorMin = new Vector2(0, 1); _tabsRow.anchorMax = new Vector2(1, 1); _tabsRow.pivot = new Vector2(0, 1);
            _tabsRow.anchoredPosition = new Vector2(14, 0); _tabsRow.sizeDelta = new Vector2(-28, 48);
            var hl = _tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.LowerLeft; hl.spacing = 6;
            hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            // 내용 패널
            var panel = Panel(_catPane, "Content", PanelCol, true);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero; panel.offsetMax = new Vector2(0, -46);

            var line = Mk("TopLine", panel);
            line.anchorMin = new Vector2(0, 1); line.anchorMax = new Vector2(1, 1); line.pivot = new Vector2(0.5f, 1);
            line.sizeDelta = new Vector2(-40, 4); line.anchoredPosition = new Vector2(0, -2);
            _catLine = Img(line, null, Amber, false);

            var tt = Mk("Title", panel);
            tt.anchorMin = new Vector2(0, 1); tt.anchorMax = new Vector2(1, 1); tt.pivot = new Vector2(0, 1);
            tt.anchoredPosition = new Vector2(26, -14); tt.sizeDelta = new Vector2(-52, 40);
            _catTitle = Txt(tt, "", 24, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);

            _catScroll = NewScroll(panel, false, true, 16, 14, 16, 62, out _gridContent);
            var g = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(128, 144); g.spacing = new Vector2(12, 12);
            g.padding = new RectOffset(10, 10, 6, 14);
            g.constraint = GridLayoutGroup.Constraint.Flexible;
            g.childAlignment = TextAnchor.UpperLeft;
            var csf = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void ShowCategory(int cat)
        {
            _graphMode = false;
            _cameFromOutside = false;
            _category = Mathf.Clamp(cat, 0, CategoryNames.Length - 1);
            _graphPane.gameObject.SetActive(false);
            _catPane.gameObject.SetActive(true);
            _breadcrumb.text = "도감   >   " + CategoryNames[_category];

            // 탭 다시 만들기
            for (int i = _tabsRow.childCount - 1; i >= 0; i--) { var ch = _tabsRow.GetChild(i).gameObject; ch.SetActive(false); UnityEngine.Object.Destroy(ch); }
            for (int i = 0; i < CategoryNames.Length; i++)
            {
                int idx = i;
                float w = Mathf.Max(96f, CategoryNames[i].Length * 20f + 34f);
                FolderTab(_tabsRow, CategoryNames[i], CategoryColors[i], i == _category, w, () => ShowCategory(idx));
            }
            _catLine.color = CategoryColors[_category];

            // 아이템 격자
            for (int i = _gridContent.childCount - 1; i >= 0; i--) { var ch = _gridContent.GetChild(i).gameObject; ch.SetActive(false); UnityEngine.Object.Destroy(ch); }
            int count = 0;
            foreach (ItemData it in ItemCatalog.GetOrCreate().Items)
            {
                if (it == null || CategoryOf(it) != _category) continue;
                BuildTile(_gridContent, it, CategoryColors[_category]);
                count++;
            }
            _catTitle.text = CategoryNames[_category] + "   " + count + "종";
            _catScroll.verticalNormalizedPosition = 1f;
        }

        void BuildTile(RectTransform grid, ItemData item, Color accent)
        {
            var tile = Mk("Tile_" + item.itemID, grid);
            var bg = Img(tile, Round, CardCol, true, true);
            var btn = tile.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
            var cb = btn.colors; cb.highlightedColor = CardHover; cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = cb;
            ItemData captured = item;
            btn.onClick.AddListener(() => ShowItem(captured));

            var strip = Mk("Accent", tile);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1); strip.pivot = new Vector2(0.5f, 1);
            strip.anchoredPosition = new Vector2(0, -8); strip.sizeDelta = new Vector2(-40, 4);
            Img(strip, null, new Color(accent.r, accent.g, accent.b, 0.85f), false);

            var icon = Mk("Icon", tile);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1); icon.pivot = new Vector2(0.5f, 1);
            icon.anchoredPosition = new Vector2(0, -22); icon.sizeDelta = new Vector2(74, 74);
            ItemIcon(icon, item);

            var nm = Mk("Name", tile);
            nm.anchorMin = new Vector2(0, 0); nm.anchorMax = new Vector2(1, 0); nm.pivot = new Vector2(0.5f, 0);
            nm.anchoredPosition = new Vector2(0, 8); nm.sizeDelta = new Vector2(-10, 40);
            var t = Txt(nm, item.itemName, 15, FontStyle.Bold, Cream, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // ---------- 제작 링크 화면 ----------
        void BuildGraphPane()
        {
            _graphPane = Mk("GraphPane", _root);
            _graphPane.anchorMin = Vector2.zero; _graphPane.anchorMax = Vector2.one;
            _graphPane.offsetMin = Vector2.zero; _graphPane.offsetMax = new Vector2(0, -56);

            var header = Mk("Header", _graphPane);
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
            header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0, 64);
            Img(header, Round, PanelCol);

            var tt = Mk("Title", header);
            tt.anchorMin = new Vector2(0, 0.42f); tt.anchorMax = new Vector2(0.55f, 1); tt.offsetMin = new Vector2(20, 0); tt.offsetMax = Vector2.zero;
            _title = Txt(tt, "", 26, FontStyle.Bold, Cream, TextAnchor.LowerLeft);
            var st = Mk("Subtitle", header);
            st.anchorMin = new Vector2(0, 0); st.anchorMax = new Vector2(0.55f, 0.46f); st.offsetMin = new Vector2(20, 4); st.offsetMax = Vector2.zero;
            _subtitle = Txt(st, "", 15, FontStyle.Normal, Dim, TextAnchor.UpperLeft);

            _tabs2 = Mk("RecipeTabs", header);
            _tabs2.anchorMin = new Vector2(0.5f, 0); _tabs2.anchorMax = new Vector2(1, 1);
            _tabs2.offsetMin = new Vector2(0, 10); _tabs2.offsetMax = new Vector2(-14, -10);
            var hl = _tabs2.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleRight; hl.spacing = 8;
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            var graph = Mk("Graph", _graphPane);
            graph.anchorMin = Vector2.zero; graph.anchorMax = Vector2.one;
            graph.offsetMin = Vector2.zero; graph.offsetMax = new Vector2(0, -72);
            Img(graph, Round, GraphBg, true, true);

            _graphScroll = NewScroll(graph, true, true, 4, 4, 4, 4, out _graphContent);
            _graphScroll.movementType = ScrollRect.MovementType.Elastic;
            _graphScroll.elasticity = 0.08f;
            _graphScroll.scrollSensitivity = 40f;
            _graphContent.anchorMin = _graphContent.anchorMax = new Vector2(0, 1);
            _graphContent.pivot = new Vector2(0, 1);
            var wheel = _graphScroll.viewport.gameObject.AddComponent<WheelPan>();
            wheel.target = _graphScroll;

            var hint = Mk("PanHint", graph);
            hint.anchorMin = new Vector2(1, 1); hint.anchorMax = new Vector2(1, 1); hint.pivot = new Vector2(1, 1);
            hint.anchoredPosition = new Vector2(-14, -10); hint.sizeDelta = new Vector2(420, 24);
            Txt(hint, "드래그하거나 휠(쉬프트+휠)로 상하좌우 이동", 14, FontStyle.Normal, new Color(0.45f, 0.4f, 0.34f, 0.9f), TextAnchor.MiddleRight);
        }

        void ShowItem(ItemData item)
        {
            if (item == null) return;
            _graphMode = true;
            _selected = item;
            _recipeIndex = 0;
            _onSelected?.Invoke(item);
            _catPane.gameObject.SetActive(false);
            _graphPane.gameObject.SetActive(true);
            RefreshGraph();
        }

        void RefreshGraph()
        {
            int cat = CategoryOf(_selected);
            _breadcrumb.text = "도감   >   " + CategoryNames[_category] + "   >   " + _selected.itemName + "   >   제작 링크";
            _title.text = _selected.itemName;

            List<RecipeData> recs = FindRecipesProducing(_selected);
            _recipeIndex = Mathf.Clamp(_recipeIndex, 0, Mathf.Max(0, recs.Count - 1));
            _subtitle.text = CategoryNames[cat] + (recs.Count == 0 ? "  ·  채집으로 얻는 원재료" : "  ·  제작법 " + recs.Count + "개");

            for (int i = _tabs2.childCount - 1; i >= 0; i--) { var ch = _tabs2.GetChild(i).gameObject; ch.SetActive(false); UnityEngine.Object.Destroy(ch); }
            if (recs.Count > 1)
            {
                for (int i = 0; i < recs.Count; i++)
                {
                    int idx = i;
                    var b = MkButton(_tabs2, "Tab" + i, "방법 " + (i + 1) + " · " + FacilityName(recs[i].requiredFacility), i == _recipeIndex ? BtnAmber : BtnBrown, 16, () => { _recipeIndex = idx; RefreshGraph(); });
                    var l = b.gameObject.AddComponent<LayoutElement>(); l.preferredWidth = 200;
                }
            }

            BuildGraph(recs.Count > 0 ? recs[_recipeIndex] : null);
        }

        // ---------- 그래프 ----------
        class GNode
        {
            public ItemData item; public int count; public RecipeData recipe;
            public readonly List<GNode> kids = new List<GNode>();
            public int depth; public float cy;
        }

        GNode MakeNode(ItemData item, int count, int depth, RecipeData forced, List<ItemData> path)
        {
            var n = new GNode { item = item, count = count, depth = depth };
            RecipeData r = forced;
            if (r == null && depth < MaxDepth && !path.Contains(item))
            {
                List<RecipeData> rs = FindRecipesProducing(item);
                if (rs.Count > 0) r = rs[0];
            }
            n.recipe = r;
            if (r != null && r.inputs != null)
            {
                path.Add(item);
                foreach (RecipeIngredient ing in r.inputs)
                    if (ing != null && ing.item != null && ing.count > 0)
                        n.kids.Add(MakeNode(ing.item, ing.count, depth + 1, null, path));
                path.RemoveAt(path.Count - 1);
            }
            return n;
        }

        float _nextRow;
        int _maxDepth;

        void AssignRows(GNode n)
        {
            _maxDepth = Mathf.Max(_maxDepth, n.depth);
            if (n.kids.Count == 0)
            {
                n.cy = _nextRow * RowH + ItemH * 0.5f;
                _nextRow += 1f;
                return;
            }
            foreach (GNode k in n.kids) AssignRows(k);
            n.cy = (n.kids[0].cy + n.kids[n.kids.Count - 1].cy) * 0.5f;
        }

        void ClearGraph()
        {
            for (int i = _graphContent.childCount - 1; i >= 0; i--) { var ch = _graphContent.GetChild(i).gameObject; ch.SetActive(false); UnityEngine.Object.Destroy(ch); }
        }

        void BuildGraph(RecipeData rootRecipe)
        {
            ClearGraph();

            var root = MakeNode(_selected, rootRecipe != null && rootRecipe.output != null ? Mathf.Max(1, rootRecipe.output.count) : 1, 0, rootRecipe, new List<ItemData>());
            _nextRow = 0f; _maxDepth = 0;
            AssignRows(root);
            float graphRows = Mathf.Max(1f, _nextRow);

            // 그래프 본체 컨테이너: 바깥 여백(PanX/PanY)을 두고 그 안에 배치한다
            var holder = Mk("Graph", _graphContent);
            var linesRt = Mk("Lines", holder); Stretch(linesRt);
            var nodesRt = Mk("Nodes", holder); Stretch(nodesRt);

            float x0 = Pad + _maxDepth * Col;           // 루트 아이템의 x
            float graphW = x0 + ItemW + Pad;
            float graphH = Pad + graphRows * RowH + 8f;

            DrawNode(root, linesRt, nodesRt, true);

            // 활용처: 선택한 아이템 → 시설 → 결과물 (루트 오른쪽으로 이어지는 흐름)
            List<RecipeData> uses = FindRecipesUsing(_selected);
            if (uses.Count > 0)
            {
                float y = graphH + 10f;
                var head = Mk("UsedInHeader", nodesRt);
                TopLeft(head, Pad, y, 400f, 30f);
                Txt(head, "이 아이템의 활용처", 20, FontStyle.Bold, DarkText, TextAnchor.MiddleLeft);
                y += 40f;
                int shown = 0;
                foreach (RecipeData r in uses)
                {
                    if (r == null || r.output == null || r.output.item == null) continue;
                    if (shown++ >= 8) break;
                    float cy = y + ItemH * 0.5f;
                    float ax = x0;
                    int need = 1;
                    foreach (RecipeIngredient ing in r.inputs) if (ing != null && ing.item == _selected) need = Mathf.Max(1, ing.count);
                    DrawItem(nodesRt, _selected, need, ax, cy, false, false);
                    float fx = ax + ItemW + Gap;
                    DrawLine(linesRt, ax + ItemW, cy, fx, cy, true);
                    DrawFacility(nodesRt, r, fx, cy, FindRecipesProducing(r.output.item).Count);
                    float rx = fx + FacW + Gap;
                    DrawLine(linesRt, fx + FacW, cy, rx, cy, true);
                    DrawItem(nodesRt, r.output.item, Mathf.Max(1, r.output.count), rx, cy, false, true);
                    y += RowH;
                }
                graphH = y + 10f;
                graphW = Mathf.Max(graphW, x0 + ItemW + Gap + FacW + Gap + ItemW + Pad);
            }
            graphH += Pad * 0.5f;
            graphW = Mathf.Max(graphW, 200f);

            TopLeft(holder, PanX, PanY, graphW, graphH);
            float cw = graphW + PanX * 2f, ch = graphH + PanY * 2f;
            _graphContent.anchorMin = _graphContent.anchorMax = new Vector2(0, 1);
            _graphContent.pivot = new Vector2(0, 1);
            _graphContent.anchoredPosition = Vector2.zero;
            _graphContent.sizeDelta = new Vector2(cw, ch);
            _graphScroll.horizontal = true; _graphScroll.vertical = true;

            // 처음에는 그래프가 화면 가운데(큰 그래프는 결과물 쪽)에 오도록 위치를 잡는다
            float viewW = _w - 8f, viewH = _h - 56f - 72f - 8f;
            float left = graphW <= viewW - 80f ? PanX + graphW * 0.5f - viewW * 0.5f : PanX + graphW - viewW + 60f;
            float top = graphH <= viewH - 40f ? PanY + graphH * 0.5f - viewH * 0.5f : PanY - 10f;
            Canvas.ForceUpdateCanvases();
            _graphScroll.horizontalNormalizedPosition = Mathf.Clamp01(left / Mathf.Max(1f, cw - viewW));
            _graphScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / Mathf.Max(1f, ch - viewH));

            if (rootRecipe == null)
            {
                var note = Mk("RawNote", nodesRt);
                TopLeft(note, x0 - 40f, graphRows * RowH + 6f - 30f, ItemW + 80f, 28f);
                Txt(note, "채집으로 얻는 원재료", 16, FontStyle.Normal, DarkText, TextAnchor.MiddleCenter);
            }
        }

        void DrawNode(GNode n, RectTransform lines, RectTransform nodes, bool isRoot)
        {
            float itemX = Pad + (_maxDepth - n.depth) * Col;
            if (n.kids.Count > 0 && n.recipe != null)
            {
                float facRight = itemX - Gap, facLeft = facRight - FacW;
                float busX = facLeft - Gap * 0.5f;
                float minY = n.cy, maxY = n.cy;
                foreach (GNode k in n.kids)
                {
                    float kidRight = Pad + (_maxDepth - k.depth) * Col + ItemW;
                    DrawLine(lines, kidRight, k.cy, busX, k.cy, false);
                    minY = Mathf.Min(minY, k.cy); maxY = Mathf.Max(maxY, k.cy);
                }
                if (maxY > minY) DrawLine(lines, busX, minY, busX, maxY, false);
                DrawLine(lines, busX, n.cy, facLeft, n.cy, true);
                DrawLine(lines, facRight, n.cy, itemX, n.cy, true);
                DrawFacility(nodes, n.recipe, facLeft, n.cy, FindRecipesProducing(n.item).Count);
                foreach (GNode k in n.kids) DrawNode(k, lines, nodes, false);
            }
            DrawItem(nodes, n.item, n.count, itemX, n.cy, isRoot, !isRoot);
        }

        void DrawLine(RectTransform parent, float x1, float y1, float x2, float y2, bool arrow)
        {
            const float T = 3f;
            var l = Mk("Line", parent);
            float x = Mathf.Min(x1, x2), y = Mathf.Min(y1, y2);
            float w = Mathf.Abs(x2 - x1), h = Mathf.Abs(y2 - y1);
            if (w >= h) TopLeft(l, x, y - T * 0.5f, w, T); else TopLeft(l, x - T * 0.5f, y, T, h);
            Img(l, null, LineCol);

            if (arrow && Tri != null)
            {
                var a = Mk("Arrow", parent);
                a.anchorMin = a.anchorMax = new Vector2(0, 1); a.pivot = new Vector2(0.5f, 0.5f);
                a.sizeDelta = new Vector2(16, 16);
                a.anchoredPosition = new Vector2(x2 - 6f, -y2);
                a.localRotation = Quaternion.Euler(0, 0, -90f);
                Img(a, Tri, LineCol, false);
            }
        }

        void DrawItem(RectTransform parent, ItemData item, int count, float x, float cy, bool isRoot, bool clickable)
        {
            var tile = Mk("Item_" + item.itemID, parent);
            TopLeft(tile, x, cy - ItemH * 0.5f, ItemW, ItemH);
            var bg = Img(tile, Round, NodeCol, true, clickable);
            if (clickable)
            {
                var btn = tile.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
                var cb = btn.colors; cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = cb;
                ItemData captured = item;
                btn.onClick.AddListener(() => ShowItem(captured));
            }
            if (Ring != null)
            {
                var ring = Mk("Ring", tile); Stretch(ring);
                Img(ring, Ring, isRoot ? Amber : new Color(1f, 1f, 1f, 0.12f));
            }

            var icon = Mk("Icon", tile); Stretch(icon, 12, 12, 12, 12);
            ItemIcon(icon, item);

            if (count > 1)
            {
                var c = Mk("Count", tile);
                c.anchorMin = c.anchorMax = new Vector2(1, 0); c.pivot = new Vector2(1, 0);
                c.anchoredPosition = new Vector2(-6, 3); c.sizeDelta = new Vector2(60, 24);
                Txt(c, "x" + count, 18, FontStyle.Bold, Amber, TextAnchor.LowerRight);
            }

            var nm = Mk("Name", parent);
            TopLeft(nm, x - 20f, cy + ItemH * 0.5f + 2f, ItemW + 40f, NameH - 4f);
            var t = Txt(nm, item.itemName, 15, isRoot ? FontStyle.Bold : FontStyle.Normal, DarkText, TextAnchor.UpperCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void DrawFacility(RectTransform parent, RecipeData recipe, float x, float cy, int altCount)
        {
            var node = Mk("Facility_" + recipe.requiredFacility, parent);
            TopLeft(node, x, cy - FacH * 0.5f, FacW, FacH);
            Img(node, Round, NodeCol);

            Sprite fIcon = FacilityIcon(recipe.requiredFacility);
            float textLeft = 12f;
            if (fIcon != null)
            {
                var iconBg = Mk("IconBg", node);
                iconBg.anchorMin = iconBg.anchorMax = new Vector2(0, 0.5f); iconBg.pivot = new Vector2(0, 0.5f);
                iconBg.anchoredPosition = new Vector2(7, 0); iconBg.sizeDelta = new Vector2(38, 38);
                Img(iconBg, Round, new Color(0.30f, 0.31f, 0.37f, 1f));
                var ic = Mk("Icon", iconBg); Stretch(ic, 4, 4, 4, 4);
                var im = Img(ic, fIcon, Color.white, false); im.preserveAspect = true;
                textLeft = 52f;
            }

            var name = Mk("Name", node);
            name.anchorMin = new Vector2(0, 0.45f); name.anchorMax = new Vector2(1, 1); name.offsetMin = new Vector2(textLeft, 0); name.offsetMax = new Vector2(-6, -3);
            var nt = Txt(name, FacilityName(recipe.requiredFacility), 15, FontStyle.Bold, Color.white, TextAnchor.LowerLeft);
            nt.horizontalOverflow = HorizontalWrapMode.Wrap; nt.verticalOverflow = VerticalWrapMode.Truncate; nt.resizeTextForBestFit = true; nt.resizeTextMinSize = 10; nt.resizeTextMaxSize = 15;

            var sub = Mk("Sub", node);
            sub.anchorMin = new Vector2(0, 0); sub.anchorMax = new Vector2(1, 0.48f); sub.offsetMin = new Vector2(textLeft, 3); sub.offsetMax = new Vector2(-6, 0);
            Txt(sub, recipe.requiredFacility == FacilityType.None ? "기본 조립" : recipe.processingSeconds.ToString("0.#") + "초", 13, FontStyle.Normal, new Color(0.75f, 0.75f, 0.8f), TextAnchor.UpperLeft);

            if (altCount > 1)
            {
                var badge = Mk("Badge", node);
                badge.anchorMin = badge.anchorMax = new Vector2(1, 1); badge.pivot = new Vector2(1, 0.5f);
                badge.anchoredPosition = new Vector2(-4, 0); badge.sizeDelta = new Vector2(54, 18);
                Img(badge, Round, Amber);
                var bt = Mk("Text", badge); Stretch(bt);
                Txt(bt, "기타 " + (altCount - 1), 12, FontStyle.Bold, new Color(0.25f, 0.17f, 0.04f), TextAnchor.MiddleCenter);
            }
        }

        // =====================================================================================
        // 데이터 헬퍼 (제작/가공 화면도 함께 쓴다)
        // =====================================================================================
        public static string FacilityName(FacilityType t)
        {
            switch (t)
            {
                case FacilityType.None: return "트럭 조립";
                case FacilityType.Campfire: return "모닥불";
                case FacilityType.Grindstone: return "숫돌 연마대";
                case FacilityType.RollerPress: return "롤러 프레스기";
                case FacilityType.PrecisionCutter: return "절삭기";
                case FacilityType.ChemicalRefinery: return "화학 정제탑";
                case FacilityType.SuperheatedFurnace: return "초고온 용광로";
                case FacilityType.LeatherTanningRack: return "가죽 무두질 건조대";
                default: return t.ToString();
            }
        }

        public static Sprite FacilityIcon(FacilityType t)
        {
            string id = null;
            switch (t)
            {
                case FacilityType.Campfire: id = ItemIds.Campfire; break;
                case FacilityType.Grindstone: id = ItemIds.Grindstone; break;
                case FacilityType.RollerPress: id = ItemIds.RollerPressMachine; break;
                case FacilityType.PrecisionCutter: id = ItemIds.PrecisionCutterMachine; break;
                case FacilityType.ChemicalRefinery: id = ItemIds.ChemicalRefineryTower; break;
                case FacilityType.SuperheatedFurnace: id = ItemIds.SuperheatedFurnace; break;
                case FacilityType.LeatherTanningRack: id = ItemIds.LeatherTanningRack; break;
            }
            if (id == null) return null;
            ItemData it = ItemCatalog.GetOrCreate().GetItem(id);
            return it != null ? it.icon : null;
        }

        public static List<RecipeData> FindRecipesProducing(ItemData item)
        {
            var result = new List<RecipeData>();
            if (item == null) return result;
            foreach (RecipeData r in ItemCatalog.GetOrCreate().Recipes)
                if (r != null && r.output != null && r.output.item == item) result.Add(r);
            return result;
        }

        public static List<RecipeData> FindRecipesUsing(ItemData item)
        {
            var result = new List<RecipeData>();
            if (item == null) return result;
            foreach (RecipeData r in ItemCatalog.GetOrCreate().Recipes)
            {
                if (r == null || r.inputs == null) continue;
                foreach (RecipeIngredient ing in r.inputs)
                    if (ing != null && ing.item == item) { result.Add(r); break; }
            }
            return result;
        }
    }
}
