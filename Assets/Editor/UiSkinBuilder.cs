using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck.EditorTools
{
    /// <summary>
    /// UI 스킨 에셋을 (다시) 만든다. Resources/UI 아래에:
    ///  - LastTruckUiSkin.asset        : Kenney 스프라이트 + 글꼴 묶음 (uGUI / UI Toolkit 공용)
    ///  - LastTruckHud.uss             : UI Toolkit HUD 스타일
    ///  - LastTruckTheme.tss           : UI Toolkit 기본 테마
    ///  - LastTruckHudPanelSettings    : HUD 패널 설정 (팝업 뒤, 정렬 95)
    ///  - LastTruckOverlayPanelSettings: 토스트 패널 설정 (팝업 앞, 정렬 500)
    /// 메뉴: LastTruck/UI/UI 스킨 만들기
    /// </summary>
    public static class UiSkinBuilder
    {
        private const string Dir = "Assets/Resources/UI";
        private const string Kenney = "Assets/Free_Assets/kenney_ui-pack-adventure/PNG/Double/";
        private const string FontPath = "Assets/LastTruck/Multiplayer/Fonts/Pretendard-Regular.ttf";

        private const string Uss = @"/* Last Truck HUD (UI Toolkit). 판 모양은 코드에서 Kenney 스프라이트를 입히고, 여기서는 배치/글자/막대 색만 정한다. */
.lt-root { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }

.lt-text {
    color: rgb(255, 244, 220);
    -unity-font-style: bold;
    font-size: 20px;
    -unity-text-outline-width: 1.2px;
    -unity-text-outline-color: rgba(38, 22, 8, 0.95);
}

.lt-status-plate { position: absolute; right: 36px; top: 276px; width: 290px; padding: 18px 22px 16px 22px; }
.lt-truck-plate  { position: absolute; top: 10px; left: 50%; translate: -50% 0; width: 480px; padding: 16px 28px 14px 28px; }

.lt-row   { flex-direction: row; align-items: center; height: 30px; margin: 2px 0; }
.lt-tag   { width: 54px; font-size: 20px; -unity-text-align: middle-left; }
.lt-track {
    flex-grow: 1; height: 24px; justify-content: center; overflow: hidden;
    background-color: rgba(28, 16, 8, 0.92);
    border-radius: 12px; border-width: 2px; border-color: rgba(255, 236, 190, 0.45);
}
.lt-fill  { position: absolute; left: 0; top: 0; bottom: 0; width: 100%; border-radius: 10px; }
.lt-fill-hp    { background-color: rgb(236, 84, 74); }
.lt-fill-sp    { background-color: rgb(244, 200, 64); }
.lt-fill-tired { background-color: rgb(140, 128, 116); }
.lt-fill-truck { background-color: rgb(92, 204, 86); }
.lt-shine { position: absolute; left: 6px; right: 6px; top: 2px; height: 5px; border-radius: 3px; background-color: rgba(255, 255, 255, 0.28); }
.lt-value { position: absolute; left: 0; right: 0; top: 0; bottom: 0; -unity-text-align: middle-center; font-size: 15px; }

.lt-toast { position: absolute; bottom: 190px; left: 50%; translate: -50% 0; min-width: 380px; padding: 18px 36px; align-items: center; }
.lt-toast-text { font-size: 24px; color: rgb(255, 214, 120); -unity-text-align: middle-center; white-space: normal; }
";

        [MenuItem("LastTruck/UI/UI 스킨 만들기")]
        public static void BuildMenu()
        {
            Build();
            EditorUtility.DisplayDialog("UI 스킨", "Resources/UI 아래에 UI 스킨 에셋을 만들었습니다.", "확인");
        }

        public static void Build()
        {
            KenneyUiSetup.Apply();
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

            // 1) USS / TSS (텍스트 파일)
            File.WriteAllText(Dir + "/LastTruckHud.uss", Uss.Replace("\r\n", "\n"));
            File.WriteAllText(Dir + "/LastTruckTheme.tss", "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(Dir + "/LastTruckHud.uss", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Dir + "/LastTruckTheme.tss", ImportAssetOptions.ForceUpdate);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Dir + "/LastTruckTheme.tss");

            // 2) 패널 설정 (HUD / 토스트)
            MakePanelSettings(Dir + "/LastTruckHudPanelSettings.asset", 95, theme);
            MakePanelSettings(Dir + "/LastTruckOverlayPanelSettings.asset", 500, theme);
            MakePanelSettings(Dir + "/LastTruckFxPanelSettings.asset", 10, theme);        // 속도선 (HUD 뒤, 가장 아래)
            MakePanelSettings(Dir + "/LastTruckRadialPanelSettings.asset", 110, theme);   // 트럭 원형 메뉴 (팝업 앞, 토스트 뒤)

            // 3) 스킨 (스프라이트 + 글꼴)
            string skinPath = Dir + "/LastTruckUiSkin.asset";
            var skin = AssetDatabase.LoadAssetAtPath<LastTruckUiSkin>(skinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<LastTruckUiSkin>();
                AssetDatabase.CreateAsset(skin, skinPath);
            }

            skin.panelBrown = LoadSprite("panel_brown");
            skin.panelBrownDark = LoadSprite("panel_brown_dark");
            skin.panelGrey = LoadSprite("panel_grey");
            skin.panelGreyDark = LoadSprite("panel_grey_dark");
            skin.panelGreyBolts = LoadSprite("panel_grey_bolts");
            skin.panelGreyBoltsDark = LoadSprite("panel_grey_bolts_dark");
            skin.buttonBrown = LoadSprite("button_brown");
            skin.buttonGrey = LoadSprite("button_grey");
            skin.buttonRed = LoadSprite("button_red");
            skin.buttonBrownClose = LoadSprite("button_brown_close");
            skin.buttonRedClose = LoadSprite("button_red_close");
            skin.hexBrown = LoadSprite("hexagon_brown");
            skin.hexBrownDark = LoadSprite("hexagon_brown_dark");
            skin.roundBrown = LoadSprite("round_brown");
            skin.roundBrownDark = LoadSprite("round_brown_dark");
            skin.roundGreyDark = LoadSprite("round_grey_dark");
            skin.bannerModern = LoadSprite("banner_modern");
            skin.minimapRingBrown = LoadSprite("minimap_ring_brown_detail");
            skin.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiSkinBuilder] UI 스킨 에셋을 만들었습니다: " + Dir);
        }

        private static Sprite LoadSprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Kenney + name + ".png");
            if (s == null) Debug.LogWarning("[UiSkinBuilder] 스프라이트를 찾지 못했습니다: " + name);
            return s;
        }

        private static void MakePanelSettings(string path, int sortingOrder, ThemeStyleSheet theme)
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.sortingOrder = sortingOrder;
            ps.themeStyleSheet = theme;
            EditorUtility.SetDirty(ps);
        }
    }
}
