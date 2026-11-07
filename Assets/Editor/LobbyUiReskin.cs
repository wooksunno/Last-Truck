using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastTruck.EditorTools
{
    /// <summary>
    /// 메뉴(로비) 씬의 uGUI 창/버튼에 UI 스킨(Kenney 강철 패널 / 단추)을 입힌다.
    /// 씬에 열려 있는 "Menu" 씬만 바꾼다. 다시 실행해도 같은 결과가 나온다 (색은 이름/원래 색으로 정한다).
    /// 메뉴: LastTruck/UI/로비 UI에 스킨 입히기
    /// </summary>
    public static class LobbyUiReskin
    {
        private static readonly string[] PanelNames =
        {
            "Window", "SessionScroll", "PlayerList", "MyCharacter", "PortraitList", "PreviewBackground", "PortraitBackground",
        };

        [MenuItem("LastTruck/UI/로비 UI에 스킨 입히기")]
        public static void Run()
        {
            Scene menu = SceneManager.GetSceneByName("Menu");
            if (!menu.isLoaded)
            {
                EditorUtility.DisplayDialog("로비 UI", "Menu 씬이 열려 있지 않습니다.", "확인");
                return;
            }
            string log = Apply(menu);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            Debug.Log("[LobbyUiReskin] " + log);
        }

        public static string Apply(Scene menu)
        {
            LastTruckUiSkin skin = AssetDatabase.LoadAssetAtPath<LastTruckUiSkin>("Assets/Resources/UI/LastTruckUiSkin.asset");
            if (skin == null) return "UI 스킨 에셋이 없습니다 (LastTruck > UI > UI 스킨 만들기를 먼저).";

            int panels = 0, buttons = 0, bars = 0;
            foreach (GameObject root in menu.GetRootGameObjects())
            {
                foreach (Image img in root.GetComponentsInChildren<Image>(true))
                {
                    string n = img.gameObject.name;
                    var button = img.GetComponent<Button>();

                    if (button != null)
                    {
                        // 단추: 주황(주 동작) / 초록(준비) / 회색(보조·취소)
                        Color c = img.color;
                        Color intent = c;
                        // 이미 입혀진 단추는 의도를 이름으로 다시 정한다 (색이 스프라이트 배율로 바뀌어 있으므로)
                        if (img.sprite != null && img.sprite.name.StartsWith("button_")) intent = IntentFromName(n, c);
                        if (UiSkinUtil.SkinButton(img, intent)) { buttons++; Tint(img, intent); }
                    }
                    else if (System.Array.IndexOf(PanelNames, n) >= 0)
                    {
                        if (UiSkinUtil.SetSliced(img, skin.panelGreyDark, new Color(0.50f, 0.54f, 0.64f, 1f), 1.7f)) panels++;
                    }
                    else if (n.StartsWith("StatBar"))
                    {
                        if (UiSkinUtil.SetSliced(img, skin.panelGreyDark, new Color(0.55f, 0.58f, 0.66f, 1f), 3f)) bars++;
                    }
                }
            }
            return $"창 {panels}개, 단추 {buttons}개, 능력치 막대 {bars}개에 스킨을 입혔습니다.";
        }

        private static Color IntentFromName(string name, Color fallback)
        {
            if (name.Contains("Ready")) return new Color(0.25f, 0.64f, 0.42f);
            if (name.Contains("Cancel") || name.Contains("Leave") || name.Contains("Close") || name.Contains("Minus") || name.Contains("Plus") || name.Contains("Change"))
                return new Color(0.23f, 0.25f, 0.30f);
            return new Color(0.91f, 0.57f, 0.23f);
        }

        /// <summary>SkinButton이 고른 스프라이트의 색 배율을 의도에 맞게 다듬는다 (주황 단추는 따뜻하게, 회색은 어둡게).</summary>
        private static void Tint(Image img, Color intent)
        {
            bool orange = intent.r > intent.g * 1.3f && intent.g > intent.b * 1.5f;
            bool green = intent.g > intent.r * 1.12f && intent.g > intent.b * 1.12f;
            if (orange) img.color = new Color(1.00f, 0.66f, 0.32f, 1f);        // 크림색 단추 × 주황 = 원래 주황
            else if (green) img.color = new Color(0.50f, 0.85f, 0.58f, 1f);
            else img.color = new Color(0.55f, 0.57f, 0.65f, 1f);
        }
    }
}
