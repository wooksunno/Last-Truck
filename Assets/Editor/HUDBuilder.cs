#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD(인벤토리/미니맵/채집 라벨/팝업 틀/체력바)를 런타임 코드가 아니라 씬의 캔버스에 직접 그린다.
///  - 캔버스를 갱신 빈도별로 분리(정적 틀 vs 매 프레임 바뀌는 UI) → 한쪽이 바뀌어도 다른 캔버스는 리빌드되지 않는다
///  - 모든 장식 Image/Text는 raycastTarget 해제(클릭이 필요한 것만 켠다)
///  - 슬롯/마커는 미리 배치해 두고 값만 갱신 → 런타임 생성/삭제(GC) 없음
/// 메뉴: Tools/Last Truck/Build HUD (여러 번 실행해도 같은 결과로 다시 그린다)
/// </summary>
public static class HUDBuilder
{
    const string SpriteDir = "Assets/LastTruck/UI/Sprites";

    // ---------- 팔레트 (캐주얼 + 따뜻한 나무/가죽 톤, 포인트는 호박색) ----------
    static readonly Color PanelFill = new Color(0.15f, 0.115f, 0.09f, 0.90f);
    static readonly Color PanelBorder = new Color(0.96f, 0.75f, 0.35f, 0.80f);
    static readonly Color Cream = new Color(1f, 0.95f, 0.82f, 1f);
    static readonly Color Amber = new Color(1f, 0.80f, 0.30f, 1f);
    static readonly Color Dim = new Color(1f, 0.95f, 0.82f, 0.55f);
    static readonly Color ShadowCol = new Color(0f, 0f, 0f, 0.38f);

    static Sprite sRound, sRoundRing, sRoundSoft, sPill, sPillRing, sCircle, sRing, sRingThick, sCircleSoft, sTriangle, sTab;
    static Font sFont;

    // =====================================================================================
    // 스프라이트 생성
    // =====================================================================================
    static float RoundedSdf(float x, float y, float w, float h, float r)
    {
        // (x,y): 중심 기준 좌표, 반크기 (w,h)
        float qx = Mathf.Abs(x) - (w - r);
        float qy = Mathf.Abs(y) - (h - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static Sprite MakeSprite(string name, int size, System.Func<float, float, float> alphaAt, Vector4 border)
    {
        Directory.CreateDirectory(SpriteDir);
        string path = SpriteDir + "/" + name + ".png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        const int ss = 3; // 슈퍼샘플링으로 가장자리 부드럽게
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float fx = x + (sx + 0.5f) / ss - size * 0.5f;
                        float fy = y + (sy + 0.5f) / ss - size * 0.5f;
                        a += alphaAt(fx, fy);
                    }
                a /= ss * ss;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.spriteBorder = border;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static void GenerateSprites()
    {
        float AA(float d) { return Mathf.Clamp01(0.5f - d); }

        sRound = MakeSprite("ui_round", 64, (x, y) => AA(RoundedSdf(x, y, 32, 32, 18)), new Vector4(22, 22, 22, 22));
        sRoundRing = MakeSprite("ui_round_ring", 64, (x, y) =>
        {
            float d = RoundedSdf(x, y, 32, 32, 18);
            return AA(d) - AA(d + 3f);
        }, new Vector4(22, 22, 22, 22));
        sRoundSoft = MakeSprite("ui_round_soft", 64, (x, y) =>
        {
            float d = RoundedSdf(x, y, 22, 22, 14);
            return Mathf.Clamp01(0.5f - d / 18f) * 0.9f;
        }, new Vector4(26, 26, 26, 26));
        sPill = MakeSprite("ui_pill", 64, (x, y) => AA(RoundedSdf(x, y, 32, 32, 32)), new Vector4(31, 31, 31, 31));
        sPillRing = MakeSprite("ui_pill_ring", 64, (x, y) =>
        {
            float d = RoundedSdf(x, y, 32, 32, 32);
            return AA(d) - AA(d + 3f);
        }, new Vector4(31, 31, 31, 31));
        sCircle = MakeSprite("ui_circle", 128, (x, y) => AA(Mathf.Sqrt(x * x + y * y) - 63f), Vector4.zero);
        sRing = MakeSprite("ui_ring", 128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return AA(r - 63f) - AA(r - 63f + 7f);
        }, Vector4.zero);
        sRingThick = MakeSprite("ui_ring_thick", 128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return AA(r - 63f) - AA(r - 63f + 17f);
        }, Vector4.zero);
        sCircleSoft = MakeSprite("ui_circle_soft", 128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((63f - r) / 22f) * 0.9f;
        }, Vector4.zero);
        sTriangle = MakeSprite("ui_triangle", 64, (x, y) =>
        {
            // 위쪽 꼭짓점 삼각형(모서리 살짝 둥글게)
            float px = x, py = y + 4f;
            float d = Mathf.Max(Mathf.Abs(px) * 0.9f + py * 0.5f - 15f, -py - 24f);
            return AA(d);
        }, Vector4.zero);
        // 폴더 인덱스 탭: 위쪽 모서리만 둥글고 아래는 평평(텍스처 아래로 이어짐)
        sTab = MakeSprite("ui_tab", 64, (x, y) => AA(RoundedSdf(x, y + 20f, 32, 52, 18)), new Vector4(20, 4, 20, 22));
        AssetDatabase.SaveAssets();
    }

    static void LoadSprites()
    {
        sRound = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_round.png");
        sRoundRing = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_round_ring.png");
        sRoundSoft = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_round_soft.png");
        sPill = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_pill.png");
        sPillRing = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_pill_ring.png");
        sCircle = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_circle.png");
        sRing = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_ring.png");
        sRingThick = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_ring_thick.png");
        sCircleSoft = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_circle_soft.png");
        sTriangle = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_triangle.png");
        sTab = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/ui_tab.png");
    }

    // =====================================================================================
    // 헬퍼
    // =====================================================================================
    static RectTransform Mk(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced = true, bool raycast = false)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color;
        img.type = sprite != null && sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = raycast;
        return img;
    }

    static Text Txt(RectTransform rt, string text, int size, FontStyle style, Color color, TextAnchor anchor, bool outline = false)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = sFont; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.03f, 0.02f, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            o.useGraphicAlpha = false;
        }
        return t;
    }

    static Canvas NewCanvas(string name, Transform parent, int order, bool scaleWithScreen, bool raycaster)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
        var sc = go.GetComponent<CanvasScaler>();
        if (scaleWithScreen)
        {
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
        }
        if (raycaster) go.AddComponent<GraphicRaycaster>();
        return c;
    }

    /// <summary>그림자 + 둥근 패널 + 테두리</summary>
    static RectTransform Panel(string name, Transform parent, Vector2 size, Color fill, bool withBorder = true, bool withShadow = true, bool raycast = false)
    {
        var rt = Mk(name, parent);
        Img(rt, sRound, fill, true, raycast);
        if (withShadow)
        {
            var sh = Mk("Shadow", rt); Stretch(sh, -16, -22, -16, -10);
            Img(sh, sRoundSoft, ShadowCol);
            sh.SetAsFirstSibling();
        }
        if (withBorder)
        {
            var b = Mk("Border", rt); Stretch(b);
            Img(b, sRoundRing, PanelBorder);
        }
        return rt;
    }

    static void SetRef(Object target, string prop, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[HUDBuilder] 필드 없음: " + target.GetType().Name + "." + prop); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================================
    // 인벤토리 HUD (우하단)
    // =====================================================================================
    const int Slots = 4, Cols = 4;
    const float SlotSize = 104f, SlotGap = 10f, Pad = 18f;

    static PlayerInventoryHUD BuildInventory(Transform root)
    {
        var canvas = NewCanvas("InventoryCanvas", root, 90, true, true);
        float w = Pad * 2 + Cols * SlotSize + (Cols - 1) * SlotGap;
        int rows = Mathf.CeilToInt(Slots / (float)Cols);
        float gridTop = 62f;
        float h = gridTop + rows * SlotSize + (rows - 1) * SlotGap + Pad;

        var panel = Panel("PlayerInventoryPanel", canvas.transform, new Vector2(w, h), PanelFill);
        Place(panel, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(w, h));

        var title = Mk("Title", panel); Place(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -12), new Vector2(220, 36));
        Txt(title, "인벤토리", 26, FontStyle.Bold, Cream, TextAnchor.MiddleLeft, true);

        var used = Mk("UsedCount", panel); Place(used, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Pad, -12), new Vector2(110, 36));
        var usedText = Txt(used, "0/" + Slots, 22, FontStyle.Bold, Amber, TextAnchor.MiddleRight, true);

        var line = Mk("Divider", panel); Place(line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(-Pad * 2, 2));
        Img(line, null, new Color(1f, 0.9f, 0.7f, 0.16f), false);

        // 슬롯
        var views = new List<InventorySlotView>();
        for (int i = 0; i < Slots; i++)
        {
            int col = i % Cols, row = i / Cols;
            var slot = Mk("Slot_" + (i + 1), panel);
            Place(slot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f),
                new Vector2(Pad + SlotSize * 0.5f + col * (SlotSize + SlotGap), -(gridTop + SlotSize * 0.5f + row * (SlotSize + SlotGap))),
                new Vector2(SlotSize, SlotSize));
            var bg = Img(slot, sRound, new Color(0.08f, 0.065f, 0.055f, 0.92f), true, true);
            var btn = slot.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            var glow = Mk("Glow", slot); Stretch(glow, -14, -14, -14, -14);
            var glowImg = Img(glow, sRoundSoft, new Color(1f, 0.78f, 0.28f, 0.30f)); glowImg.enabled = false;

            var border = Mk("Border", slot); Stretch(border);
            var borderImg = Img(border, sRoundRing, new Color(1, 1, 1, 0.14f));

            var icon = Mk("Icon", slot); Stretch(icon, 14, 14, 14, 14);
            var iconImg = Img(icon, null, Color.white, false); iconImg.preserveAspect = true; iconImg.enabled = false;

            var letter = Mk("Letter", slot); Stretch(letter);
            var letterTxt = Txt(letter, "", 26, FontStyle.Bold, Cream, TextAnchor.MiddleCenter, true); letterTxt.enabled = false;

            var keyBadge = Mk("KeyBadge", slot); Place(keyBadge, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -6), new Vector2(26, 26));
            Img(keyBadge, sRound, new Color(0f, 0f, 0f, 0.5f));
            var keyTxtRt = Mk("Key", keyBadge); Stretch(keyTxtRt);
            var keyTxt = Txt(keyTxtRt, (i + 1).ToString(), 16, FontStyle.Bold, Cream, TextAnchor.MiddleCenter);

            var count = Mk("Count", slot); Place(count, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-8, 5), new Vector2(64, 30));
            var countTxt = Txt(count, "", 24, FontStyle.Bold, Color.white, TextAnchor.LowerRight, true); countTxt.enabled = false;

            var view = slot.gameObject.AddComponent<InventorySlotView>();
            SetRef(view, "background", bg); SetRef(view, "border", borderImg); SetRef(view, "glow", glowImg);
            SetRef(view, "icon", iconImg); SetRef(view, "letter", letterTxt); SetRef(view, "count", countTxt);
            SetRef(view, "key", keyTxt); SetRef(view, "button", btn);
            views.Add(view);
        }


        var hud = panel.gameObject.AddComponent<PlayerInventoryHUD>();
        var so = new SerializedObject(hud);
        var list = so.FindProperty("slots");
        list.arraySize = views.Count;
        for (int i = 0; i < views.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
        so.FindProperty("usedCountText").objectReferenceValue = usedText;
        so.ApplyModifiedPropertiesWithoutUndo();
        return hud;
    }

    // =====================================================================================
    // 팝업 틀 (트럭/제작/가공 패널의 바깥 프레임)
    // =====================================================================================
    static void BuildPopup(Transform root, GameUIController ctrl)
    {
        var canvas = NewCanvas("PopupCanvas", root, 120, true, true);
        var panel = Panel("InteractionPopup", canvas.transform, new Vector2(560, 640), new Color(0.12f, 0.095f, 0.08f, 0.97f), true, true, true);
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 640));

        var title = Mk("Title", panel); Place(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-200, 40));
        var titleTxt = Txt(title, "Popup", 28, FontStyle.Bold, Cream, TextAnchor.UpperCenter, true);
        titleTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

        Button MkButton(string name, string label, Vector2 pos, Vector2 size, Color col)
        {
            var rt = Mk(name, panel); Place(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), pos, size);
            var img = Img(rt, sRound, col, true, true);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = new Color(1.15f, 1.1f, 1.05f, 1f); cb.pressedColor = new Color(0.8f, 0.78f, 0.75f, 1f); b.colors = cb;
            var lt = Mk("Label", rt); Stretch(lt);
            Txt(lt, label, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, true);
            return b;
        }
        var close = MkButton("CloseButton", "X", new Vector2(-14, -12), new Vector2(40, 40), new Color(0.80f, 0.30f, 0.27f, 1f));
        var journal = MkButton("JournalButton", "도감", new Vector2(-62, -12), new Vector2(78, 40), new Color(0.72f, 0.52f, 0.20f, 1f));

        var body = Mk("Body", panel); Stretch(body, 16, 16, 16, 62);
        Img(body, sRound, new Color(0f, 0f, 0f, 0.22f), true, true);
        body.gameObject.AddComponent<RectMask2D>();
        var scroll = body.gameObject.AddComponent<ScrollRect>();

        var content = Mk("Content", body);
        Place(content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8; v.childAlignment = TextAnchor.UpperCenter;
        v.childControlHeight = false; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
        v.padding = new RectOffset(4, 4, 4, 4);
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = body; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        panel.gameObject.SetActive(false);

        SetRef(ctrl, "authoredPopupCanvas", canvas);
        SetRef(ctrl, "authoredPopupRoot", panel);
        SetRef(ctrl, "authoredPopupTitle", titleTxt);
        SetRef(ctrl, "authoredPopupBody", content);
        SetRef(ctrl, "authoredPopupScroll", scroll);
        SetRef(ctrl, "authoredCloseButton", close);
        SetRef(ctrl, "authoredJournalButton", journal);
    }

    // =====================================================================================
    // 미니맵 (우상단)
    // =====================================================================================
    static void BuildMinimap(Transform root)
    {
        var canvas = NewCanvas("MinimapCanvas", root, 40, true, false);
        float size = 220f;
        var mm = Mk("MinimapRoot", canvas.transform);
        Place(mm, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -36), new Vector2(size, size));

        var shadow = Mk("Shadow", mm); Stretch(shadow, -22, -30, -22, -14); Img(shadow, sCircleSoft, ShadowCol, false);
        var plate = Mk("Plate", mm); Stretch(plate, -8, -8, -8, -8); Img(plate, sCircle, new Color(0.20f, 0.15f, 0.10f, 1f), false);
        var map = Mk("Background", mm); Stretch(map); Img(map, sCircle, new Color(0.17f, 0.18f, 0.15f, 0.94f), false);

        var masked = Mk("Masked", mm); Stretch(masked);
        var maskImg = Img(masked, sCircle, Color.white, false);
        var mask = masked.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;

        var dots = Mk("DotLayer", masked); Place(dots, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var arrow = Mk("TruckArrow", dots); Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 26));
        Img(arrow, sTriangle, new Color(1f, 0.66f, 0.12f, 1f), false);
        var truck = Mk("TruckMarker", dots); Place(truck, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
        Img(truck, sCircle, new Color(1f, 0.66f, 0.12f, 1f), false);
        var player = Mk("PlayerMarker", dots); Place(player, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 22));
        Img(player, sTriangle, new Color(0.35f, 0.88f, 1f, 1f), false);

        // 테두리 링 + 북쪽 표시
        var ring = Mk("Ring", mm); Stretch(ring, -8, -8, -8, -8); Img(ring, sRing, new Color(0.96f, 0.76f, 0.36f, 1f), false);
        var north = Mk("North", mm); Place(north, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(30, 24));
        Txt(north, "N", 18, FontStyle.Bold, Cream, TextAnchor.MiddleCenter, true);

        var comp = mm.gameObject.AddComponent<MinimapUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("minimapPixelRadius").floatValue = size * 0.5f;
        so.FindProperty("authoredDotLayer").objectReferenceValue = dots;
        so.FindProperty("authoredPlayerMarker").objectReferenceValue = player;
        so.FindProperty("authoredTruckArrow").objectReferenceValue = arrow;
        so.FindProperty("authoredTruckMarker").objectReferenceValue = truck;
        so.FindProperty("dotSprite").objectReferenceValue = sCircle;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================================
    // 채집 게이지 + 이름 라벨 (대상 위치로 매 프레임 이동 → 단독 캔버스)
    // =====================================================================================
    static void BuildGather(Transform root)
    {
        var canvas = NewCanvas("GatherCanvas", root, 50, false, false);   // 라벨 위치를 화면 픽셀로 계산하므로 1:1 스케일 유지

        var gauge = Mk("GatherProgressRoot", canvas.transform);
        Place(gauge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(64, 64));
        var gbg = Mk("Background", gauge); Stretch(gbg); Img(gbg, sCircle, new Color(0.10f, 0.08f, 0.07f, 0.80f), false);
        var gtrack = Mk("Track", gauge); Stretch(gtrack, 4, 4, 4, 4); Img(gtrack, sRingThick, new Color(1f, 1f, 1f, 0.12f), false);
        var gfill = Mk("Fill", gauge); Stretch(gfill, 4, 4, 4, 4);
        var fillImg = Img(gfill, sRingThick, Amber, false);
        fillImg.type = Image.Type.Filled; fillImg.fillMethod = Image.FillMethod.Radial360; fillImg.fillOrigin = (int)Image.Origin360.Top; fillImg.fillClockwise = true; fillImg.fillAmount = 0f;
        var gring = Mk("Ring", gauge); Stretch(gring); Img(gring, sRing, new Color(0.96f, 0.76f, 0.36f, 0.9f), false);
        gauge.gameObject.SetActive(false);

        var label = Mk("GatherLabel", canvas.transform);
        Place(label, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 42));
        Img(label, sPill, new Color(0.12f, 0.095f, 0.08f, 0.86f));
        var lb = Mk("Border", label); Stretch(lb); Img(lb, sPillRing, new Color(0.96f, 0.76f, 0.36f, 0.85f));
        var nameRt = Mk("Name", label); nameRt.anchorMin = new Vector2(0, 0.45f); nameRt.anchorMax = new Vector2(1, 1); nameRt.offsetMin = nameRt.offsetMax = Vector2.zero;
        var nameTxt = Txt(nameRt, "이름", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, true);
        var subRt = Mk("Sub", label); subRt.anchorMin = new Vector2(0, 0); subRt.anchorMax = new Vector2(1, 0.48f); subRt.offsetMin = subRt.offsetMax = Vector2.zero;
        var subTxt = Txt(subRt, "", 15, FontStyle.Normal, new Color(0.88f, 0.86f, 0.8f), TextAnchor.MiddleCenter);
        label.gameObject.SetActive(false);

        var holder = Mk("GatherProgressUI", canvas.transform);
        var comp = holder.gameObject.AddComponent<GatherProgressUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("authoredGaugeRoot").objectReferenceValue = gauge.gameObject;
        so.FindProperty("authoredFill").objectReferenceValue = fillImg;
        so.FindProperty("authoredLabelRoot").objectReferenceValue = label.gameObject;
        so.FindProperty("authoredNameText").objectReferenceValue = nameTxt;
        so.FindProperty("authoredSubText").objectReferenceValue = subTxt;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================================
    // 체력바 (기존 InformUI/PlayerHP 스크롤바를 같은 구조 그대로 다시 꾸밈)
    // =====================================================================================
    static void RestyleHP()
    {
        var hp = GameObject.Find("PlayerHP");
        if (hp == null) return;
        var rt = (RectTransform)hp.transform;
        Place(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -284), new Vector2(236, 34));

        var sb = hp.GetComponentInChildren<Scrollbar>(true);
        if (sb == null) return;
        sb.interactable = false;
        sb.transition = Selectable.Transition.None;
        var bg = sb.GetComponent<Image>();
        if (bg != null) { bg.sprite = sPill; bg.type = Image.Type.Sliced; bg.color = new Color(0.10f, 0.08f, 0.07f, 0.92f); bg.raycastTarget = false; }
        if (sb.handleRect != null)
        {
            var hi = sb.handleRect.GetComponent<Image>();
            if (hi != null) { hi.sprite = sPill; hi.type = Image.Type.Sliced; hi.color = new Color(0.93f, 0.33f, 0.30f, 1f); hi.raycastTarget = false; }
            // 핸들 안쪽 여백 살짝
            var sliding = sb.handleRect.parent as RectTransform;
            if (sliding != null) { sliding.offsetMin = new Vector2(3, 3); sliding.offsetMax = new Vector2(-3, -3); }
        }

        // 중복 생성 방지 후 장식 추가 (테두리 / 라벨)
        foreach (var n in new[] { "HPBorder", "HPLabel" })
        {
            var old = hp.transform.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var border = Mk("HPBorder", hp.transform); Stretch(border); Img(border, sPillRing, new Color(0.96f, 0.76f, 0.36f, 0.9f));
        var label = Mk("HPLabel", hp.transform); Place(label, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(60, 30));
        Txt(label, "HP", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, true);
        // 경계선이 스크롤바 위로 그려지도록 마지막 순서 유지
        border.SetAsLastSibling(); label.SetAsLastSibling();
        EditorUtility.SetDirty(hp);
    }

    // =====================================================================================
    // 전체 빌드
    // =====================================================================================
    [MenuItem("Tools/Last Truck/Build HUD")]
    public static void BuildAllMenu() { Debug.Log(BuildAll()); }

    public static string BuildAll()
    {
        var log = new StringBuilder();
        GenerateSprites();
        LoadSprites();
        sFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var gameUi = Object.FindFirstObjectByType<GameUIController>(FindObjectsInactive.Include);
        if (gameUi == null) return "GameUIController가 씬에 없습니다.";

        // UI 루트 그룹
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null || uiRoot.transform.parent != null) uiRoot = new GameObject("UI");
        var inform = GameObject.Find("InformUI");
        int sibling = inform != null ? inform.transform.GetSiblingIndex() : gameUi.transform.GetSiblingIndex();
        uiRoot.transform.SetSiblingIndex(sibling);
        if (inform != null) inform.transform.SetParent(uiRoot.transform, true);
        gameUi.transform.SetParent(uiRoot.transform, true);

        // 다시 그릴 때를 위해 이전 결과 삭제
        foreach (var n in new[] { "InventoryCanvas", "PopupCanvas", "MinimapCanvas", "GatherCanvas" })
        {
            var old = uiRoot.transform.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var hud = BuildInventory(uiRoot.transform);
        BuildPopup(uiRoot.transform, gameUi);
        BuildMinimap(uiRoot.transform);
        BuildGather(uiRoot.transform);
        RestyleHP();

        SetRef(gameUi, "inventoryHud", hud);
        SetRef(gameUi, "roundedSprite", sRound);
        SetRef(gameUi, "roundedRingSprite", sRoundRing);
        SetRef(gameUi, "triangleSprite", sTriangle);
        SetRef(gameUi, "tabSprite", sTab);
        log.AppendLine("HUD 캔버스 4개 + 체력바 재디자인 완료");

        // 인벤토리 슬롯 수: 프리팹 + 씬 인스턴스
        int changed = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LastTruck/Character/Prefabs" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var go = PrefabUtility.LoadPrefabContents(p);
            var pi = go.GetComponent<PlayerInventory>();
            if (pi != null)
            {
                var so = new SerializedObject(pi);
                var ms = so.FindProperty("maxSlots");
                if (ms != null && ms.intValue != Slots) { ms.intValue = Slots; so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(go, p); changed++; }
            }
            PrefabUtility.UnloadPrefabContents(go);
        }
        foreach (var pi in Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(pi);
            var ms = so.FindProperty("maxSlots");
            if (ms != null && ms.intValue != Slots) { ms.intValue = Slots; so.ApplyModifiedPropertiesWithoutUndo(); changed++; }
        }
        log.AppendLine("인벤토리 슬롯 " + Slots + "칸으로 변경: " + changed + "곳");

        EditorUtility.SetDirty(gameUi);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return log.ToString();
    }

    // =====================================================================================
    // 하이어라키 정리: 맵 관련 루트 오브젝트를 'Map' 아래 그룹별로 묶는다
    // =====================================================================================
    static readonly Dictionary<string, string[]> MapGroups = new Dictionary<string, string[]>
    {
        { "01_Terrain", new[] { "Terrain", "Terrain_Stone", "ForestGroundTiles", "PP_Meadow_Lake_01", "PP_Meadow_Lake_02", "PP_Meadow_Lake_03", "PP_Meadow_Lake_04", "PP_Meadow_Lake_04 (1)", "PP_Meadow_02", "PP_Meadow_02 (1)", "ForestPondWater", "ForestWestBoundary", "ForestSouthBoundary" } },
        { "02_Nature", new[] { "Vegetation", "Stones&Rocks", "Mushrooms", "ForestDecor", "ForestRocks", "ForestNature" } },
        { "03_Ores&Crystals", new[] { "Crystals&Ores&Veins", "HallOres", "HallOres2", "Coins", "Resource_Desert_Stone", "Resource_Desert_Stone (1)", "Resource_Desert_Oil", "Resource_Desert_Oil (1)", "Resource_Desert_Herb", "Resource_Desert_Herb (1)" } },
        { "04_Cave", new[] { "Cave", "Cave Props", "Rails&Mine Carts", "Stalactite&Stalagmite&Stalagnate", "Runes", "Bridges", "Blacksmithing House", "A_Connectors", "A_DeepPit", "PP_Cave_Wall_Curved_01", "PP_Cave_Wall_Curved_02", "PP_Cave_Wall_Curved_02 (1)", "PP_Cave_Wall_Curved_02 (2)", "PP_Wooden_Stairs_03", "PP_Wooden_Stairs_03 (1)", "ForestMine", "ForestCaveEntrance", "ForestCaveNook" } },
        { "05_Props", new[] { "Props", "Weapons", "Tools" } },
        { "06_Lighting&FX", new[] { "Lighting", "FX" } },
    };

    [MenuItem("Tools/Last Truck/Group Map In Hierarchy")]
    public static void GroupMapMenu() { Debug.Log(GroupMap()); }

    public static string GroupMap()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = new Dictionary<string, List<GameObject>>();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (!roots.TryGetValue(go.name, out var l)) roots[go.name] = l = new List<GameObject>();
            l.Add(go);
        }

        GameObject map = null;
        foreach (var go in scene.GetRootGameObjects()) if (go.name == "Map") map = go;
        if (map == null) map = new GameObject("Map");
        map.transform.position = Vector3.zero; map.transform.rotation = Quaternion.identity; map.transform.localScale = Vector3.one;

        // Map을 맨 처음 이동 대상(Terrain 그룹)의 자리에 둔다
        int firstIndex = int.MaxValue;
        foreach (var kv in MapGroups)
            foreach (var n in kv.Value)
                if (roots.TryGetValue(n, out var l)) foreach (var go in l) firstIndex = Mathf.Min(firstIndex, go.transform.GetSiblingIndex());
        if (firstIndex != int.MaxValue) map.transform.SetSiblingIndex(firstIndex);

        int moved = 0; var missing = new List<string>();
        foreach (var kv in MapGroups)
        {
            Transform grp = map.transform.Find(kv.Key);
            if (grp == null)
            {
                var g = new GameObject(kv.Key);
                g.transform.SetParent(map.transform, false);
                grp = g.transform;
            }
            foreach (var n in kv.Value)
            {
                if (!roots.TryGetValue(n, out var list)) { missing.Add(n); continue; }
                foreach (var go in list) { go.transform.SetParent(grp, true); moved++; }
            }
        }

        EditorUtility.SetDirty(map);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "Map 아래로 " + moved + "개 루트 이동" + (missing.Count > 0 ? " (씬에 없음: " + string.Join(", ", missing) + ")" : "");
    }
}
#endif
