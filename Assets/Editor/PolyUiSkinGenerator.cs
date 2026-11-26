using System.Collections.Generic;
using System.IO;
using LastTruck;
using UnityEditor;
using UnityEngine;

/// <summary>
/// "원신풍 + 로우폴리" UI 스킨을 코드로 그려서 만든다.
///  - 판: 짙은 남색 반투명, 모서리를 비스듬히 깎은 팔각형, 금색 테두리 + 안쪽 가는 금선, 빛 받는 면/그늘진 면이 갈린 경사(베벨) 테두리,
///        깎인 모서리마다 작은 마름모 장식.
///  - 버튼: 크림색 판 + 금색 테두리 (원신 버튼 느낌, 버튼 위 어두운 글자가 그대로 읽힌다).
///  - 육각/원형: 각진 다각형(원 = 12각형)으로 그려 폴리곤 느낌을 살린다.
/// 메뉴: LastTruck/UI/원신풍 폴리곤 스킨 적용, LastTruck/UI/나무 스킨으로 되돌리기
/// </summary>
public static class PolyUiSkinGenerator
{
    private const string Dir = "Assets/Art/UI/PolyGenshin";
    private const string SkinPath = "Assets/Resources/UI/LastTruckUiSkin.asset";
    private const string BackupPath = Dir + "/LastTruckUiSkin_WoodBackup.asset";

    // 원신풍 색
    private static readonly Color Gold = new Color32(214, 190, 140, 255);
    private static readonly Color GoldBright = new Color32(255, 236, 190, 255);
    private static readonly Color GoldDark = new Color32(168, 138, 92, 255);

    [MenuItem("LastTruck/UI/원신풍 폴리곤 스킨 적용")]
    public static void Apply()
    {
        Directory.CreateDirectory(Dir);
        var skin = AssetDatabase.LoadAssetAtPath<LastTruckUiSkin>(SkinPath);
        if (skin == null) { Debug.LogError("UI 스킨 에셋이 없습니다: " + SkinPath); return; }
        if (!File.Exists(BackupPath) && !skin.polyTheme) AssetDatabase.CopyAsset(SkinPath, BackupPath);   // 나무 스킨 백업 (한 번만)

        Color navyTop = new Color32(44, 54, 80, 244), navyBot = new Color32(24, 29, 46, 248);
        Color navyDTop = new Color32(30, 37, 58, 242), navyDBot = new Color32(16, 20, 33, 248);
        Color slateTop = new Color32(62, 68, 82, 244), slateBot = new Color32(38, 42, 54, 248);
        Color slateDTop = new Color32(44, 48, 60, 242), slateDBot = new Color32(26, 29, 38, 248);

        skin.panelBrown = Panel("poly_panel", 128, 128, 24, 14f, navyTop, navyBot, Gold);
        skin.panelBrownDark = Panel("poly_panel_dark", 128, 128, 24, 14f, navyDTop, navyDBot, Gold);
        skin.panelGrey = Panel("poly_panel_grey", 128, 128, 24, 14f, slateTop, slateBot, GoldDark);
        skin.panelGreyDark = Panel("poly_panel_grey_dark", 128, 128, 24, 14f, slateDTop, slateDBot, GoldDark);
        skin.panelGreyBolts = skin.panelGrey;
        skin.panelGreyBoltsDark = skin.panelGreyDark;

        skin.buttonBrown = Button("poly_button", new Color32(246, 240, 226, 255), new Color32(222, 211, 188, 255), GoldDark, false);
        skin.buttonGrey = Button("poly_button_grey", new Color32(130, 136, 150, 255), new Color32(98, 104, 118, 255), new Color32(150, 150, 150, 255), false);
        skin.buttonRed = Button("poly_button_red", new Color32(212, 92, 80, 255), new Color32(168, 56, 50, 255), Gold, false);
        skin.buttonBrownClose = Button("poly_button_close", new Color32(246, 240, 226, 255), new Color32(222, 211, 188, 255), GoldDark, true);
        skin.buttonRedClose = Button("poly_button_red_close", new Color32(212, 92, 80, 255), new Color32(168, 56, 50, 255), Gold, true);

        skin.hexBrown = Ngon("poly_hex", 96, 128, 6, 90f, navyTop, navyBot, Gold);
        skin.hexBrownDark = Ngon("poly_hex_dark", 96, 128, 6, 90f, navyDTop, navyDBot, Gold);
        skin.roundBrown = Ngon("poly_round", 128, 128, 12, 15f, navyTop, navyBot, Gold);
        skin.roundBrownDark = Ngon("poly_round_dark", 128, 128, 12, 15f, navyDTop, navyDBot, Gold);
        skin.roundGreyDark = Ngon("poly_round_grey", 128, 128, 12, 15f, slateDTop, slateDBot, GoldDark);
        skin.bannerModern = Panel("poly_banner", 512, 96, 0, 22f, navyTop, navyBot, Gold);
        skin.minimapRingBrown = Ring("poly_ring", 256, 24);
        skin.hudFade = Fade("hud_fade", 256, 64, false);
        skin.hudBand = Fade("hud_band", 256, 64, true);

        skin.polyTheme = true;
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        Debug.Log("[PolyUiSkin] 원신풍 폴리곤 스킨을 적용했습니다. (되돌리기: LastTruck/UI/나무 스킨으로 되돌리기)");
    }

    [MenuItem("LastTruck/UI/나무 스킨으로 되돌리기")]
    public static void Revert()
    {
        var backup = AssetDatabase.LoadAssetAtPath<LastTruckUiSkin>(BackupPath);
        var skin = AssetDatabase.LoadAssetAtPath<LastTruckUiSkin>(SkinPath);
        if (backup == null || skin == null) { Debug.LogError("백업이 없습니다: " + BackupPath); return; }
        EditorUtility.CopySerialized(backup, skin);
        skin.polyTheme = false;
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        Debug.Log("[PolyUiSkin] 나무 스킨으로 되돌렸습니다.");
    }

    // ───────────────────────── 그리기 ─────────────────────────

    private struct Edge { public Vector2 n; public float o; }

    /// <summary>모서리를 깎은 사각형(팔각형)의 변들 (중심 기준, 안쪽이 음수).</summary>
    private static List<Edge> ChamferRect(float hw, float hh, float c)
    {
        float s = 1f / Mathf.Sqrt(2f);
        float diag = (hw + hh - c) * s;
        return new List<Edge>
        {
            new Edge { n = new Vector2(0, 1), o = hh }, new Edge { n = new Vector2(0, -1), o = hh },
            new Edge { n = new Vector2(1, 0), o = hw }, new Edge { n = new Vector2(-1, 0), o = hw },
            new Edge { n = new Vector2(s, s), o = diag }, new Edge { n = new Vector2(-s, s), o = diag },
            new Edge { n = new Vector2(s, -s), o = diag }, new Edge { n = new Vector2(-s, -s), o = diag },
        };
    }

    private static List<Edge> RegularPolygon(int sides, float rx, float ry, float rotDeg)
    {
        var list = new List<Edge>();
        for (int i = 0; i < sides; i++)
        {
            float a = (rotDeg + 360f * (i + 0.5f) / sides) * Mathf.Deg2Rad;
            // 타원에 맞춘 근사: 축마다 늘인 정다각형
            Vector2 n = new Vector2(Mathf.Cos(a) / rx, Mathf.Sin(a) / ry).normalized;
            float o = Mathf.Cos(Mathf.PI / sides) / new Vector2(Mathf.Cos(a) / rx, Mathf.Sin(a) / ry).magnitude;
            list.Add(new Edge { n = n, o = o });
        }
        return list;
    }

    /// <summary>점 p의 다각형 거리(음수 = 안쪽)와 가장 가까운 변 번호.</summary>
    private static float Dist(List<Edge> edges, Vector2 p, out int edge)
    {
        float best = float.MinValue; edge = 0;
        for (int i = 0; i < edges.Count; i++)
        {
            float d = Vector2.Dot(p, edges[i].n) - edges[i].o;
            if (d > best) { best = d; edge = i; }
        }
        return best;
    }

    private static readonly Vector2 Light = new Vector2(-0.55f, 0.83f);   // 왼쪽 위에서 오는 빛

    private static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

    /// <summary>공통 채색: 바깥 금테 → 베벨(면마다 명암) → 안쪽 금선 → 몸통(위가 밝은 그라데이션).</summary>
    private static Color Paint(List<Edge> edges, Vector2 p, float v01, Color top, Color bot, Color rim, float rimW, float bevel, bool innerLine)
    {
        float d = Dist(edges, p, out int e);
        if (d > 0.75f) return Color.clear;
        float aa = Mathf.Clamp01(0.75f - d);
        float facet = Vector2.Dot(edges[e].n, Light);
        Color body = Color.Lerp(bot, top, v01);
        Color col;
        if (d > -rimW)
            col = Shade(rim, 1f + 0.28f * facet);                         // 금테 (면마다 밝기가 다르게 → 각진 느낌)
        else if (d > -rimW - bevel)
            col = Shade(body, 1f + 0.38f * facet);                        // 경사면
        else
        {
            col = body;
            if (innerLine && Mathf.Abs(d + rimW + bevel + 3.5f) < 0.8f)    // 안쪽 가는 금선
                col = Color.Lerp(col, rim, 0.45f);
        }
        col.a *= aa;
        return col;
    }

    private static Sprite Panel(string name, int w, int h, int border, float chamfer, Color top, Color bot, Color rim)
    {
        var edges = ChamferRect(w * 0.5f - 1f, h * 0.5f - 1f, chamfer);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float s = 1f / Mathf.Sqrt(2f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
                Color c = Paint(edges, p, (y + 0.5f) / h, top, bot, rim, 2.4f, 5f, true);
                // 깎인 모서리 가운데마다 작은 금 마름모
                for (int k = 4; k < 8; k++)
                {
                    Vector2 n = edges[k].n;
                    Vector2 mid = n * edges[k].o;
                    Vector2 q = p - mid;
                    float diamond = Mathf.Abs(q.x) + Mathf.Abs(q.y);
                    if (diamond < 4.2f) c = Color.Lerp(c, GoldBright, Mathf.Clamp01(4.2f - diamond) * (c.a > 0f ? 1f : 0f));
                }
                tex.SetPixel(x, y, c);
            }
        return Save(name, tex, border > 0 ? new Vector4(border, border, border, border) : Vector4.zero);
    }

    private static Sprite Button(string name, Color top, Color bot, Color rim, bool closeX)
    {
        const int w = 96, h = 48;
        var edges = ChamferRect(w * 0.5f - 1f, h * 0.5f - 1f, 10f);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
                Color c = Paint(edges, p, (y + 0.5f) / h, top, bot, rim, 2.2f, 4f, false);
                // 아래 절반은 살짝 어두운 면 (보석을 깎은 듯한 두 면)
                if (c.a > 0f && p.y < -p.x * 0.18f) c = Shade(c, 0.95f);
                if (closeX && c.a > 0f)
                {
                    float d1 = Mathf.Abs(p.x - p.y) / Mathf.Sqrt(2f), d2 = Mathf.Abs(p.x + p.y) / Mathf.Sqrt(2f);
                    bool inBox = Mathf.Abs(p.x) < 9f && Mathf.Abs(p.y) < 9f;
                    if (inBox && Mathf.Min(d1, d2) < 2.2f) c = new Color(0.23f, 0.26f, 0.33f, c.a);
                }
                tex.SetPixel(x, y, c);
            }
        return Save(name, tex, new Vector4(20, 18, 20, 18));
    }

    private static Sprite Ngon(string name, int w, int h, int sides, float rotDeg, Color top, Color bot, Color rim)
    {
        var edges = RegularPolygon(sides, w * 0.5f - 1.5f, h * 0.5f - 1.5f, rotDeg);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
                tex.SetPixel(x, y, Paint(edges, p, (y + 0.5f) / h, top, bot, rim, 3f, 6f, true));
            }
        return Save(name, tex, Vector4.zero);
    }

    private static Sprite Ring(string name, int size, int sides)
    {
        var outer = RegularPolygon(sides, size * 0.5f - 2f, size * 0.5f - 2f, 0f);
        var inner = RegularPolygon(sides, size * 0.5f - 16f, size * 0.5f - 16f, 0f);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                float dOut = Dist(outer, p, out int e);
                float dIn = Dist(inner, p, out _);
                if (dOut > 0.75f || dIn < -0.75f) { tex.SetPixel(x, y, Color.clear); continue; }
                float a = Mathf.Clamp01(0.75f - dOut) * Mathf.Clamp01(0.75f + dIn);
                float facet = Vector2.Dot(outer[e].n, Light);
                Color c = Shade(Gold, 1f + 0.3f * facet);
                if (dOut > -2.5f || dIn < 2.5f) c = Shade(GoldDark, 1f + 0.2f * facet);   // 안팎 가장자리 선
                c.a = a;
                tex.SetPixel(x, y, c);
            }
        return Save(name, tex, Vector4.zero);
    }

    /// <summary>
    /// 투명 HUD 뒤에 까는 부드러운 그늘 (짙은 남색). band=false: 왼쪽 진함 → 오른쪽 사라짐, band=true: 가운데 진함 → 양 끝 사라짐.
    /// 위아래 가장자리도 살짝 흐리게 해서 "판"이 아니라 "그늘"로 보이게 한다. 9-slice 없이 늘려 쓴다.
    /// </summary>
    private static Sprite Fade(string name, int w, int h, bool band)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color ink = new Color32(8, 11, 22, 255);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float hx = band ? 1f - Mathf.Abs(u * 2f - 1f) : 1f - u;
                hx = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(hx * 1.25f));
                float vy = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - Mathf.Abs(v * 2f - 1f)) * 2.2f));
                tex.SetPixel(x, y, new Color(ink.r, ink.g, ink.b, 0.62f * hx * vy));
            }
        return Save(name, tex, Vector4.zero);
    }

    private static Sprite Save(string name, Texture2D tex, Vector4 border)
    {
        tex.Apply();
        string path = Dir + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = border;
        imp.spritePixelsPerUnit = 100f;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
