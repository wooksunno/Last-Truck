using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 멀티플레이 에디터 도구들이 같이 쓰는 uGUI 생성 도우미 (색상/폰트/버튼/라벨 등).
    /// 기존 Menu 씬(로비)과 같은 스타일로 UI를 만든다. Editor 폴더라 빌드에는 포함되지 않는다.
    /// </summary>
    internal static class LobbyEditorUI
    {
        public const string Root = "Assets/LastTruck/Multiplayer";
        public const string FontAssetPath = Root + "/Fonts/Pretendard-Regular SDF.asset";
        public const string IconDir = Root + "/Icons";
        public const string UIPrefabDir = Root + "/Prefabs/UI";

        public static readonly Color BgColor = Hex("15171C");
        public static readonly Color PanelColor = Hex("1F232B");
        public static readonly Color RowColor = Hex("2A2F39");
        public static readonly Color FieldColor = Hex("2E333D");
        public static readonly Color ButtonColor = Hex("3A404C");
        public static readonly Color AccentColor = Hex("E8913A");
        public static readonly Color ReadyColor = Hex("3FA46A");
        public static readonly Color TextColor = Hex("F2F2F2");
        public static readonly Color SubTextColor = Hex("9AA3B2");
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        private static TMP_FontAsset _font;
        private static Sprite _roundSprite;
        private static TMP_DefaultControls.Resources _tmpResources;
        private static bool _loaded;

        public static TMP_FontAsset Font { get { Load(); return _font; } }
        public static Sprite RoundSprite { get { Load(); return _roundSprite; } }

        public static void Load()
        {
            if (_loaded && _roundSprite != null) return;
            _loaded = true;
            _roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _tmpResources = new TMP_DefaultControls.Resources
            {
                standard = _roundSprite,
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (_font == null)
            {
                Debug.LogWarning($"[LobbyEditorUI] 한글 폰트 {FontAssetPath} 를 찾지 못해 기본 TMP 폰트를 씁니다 (한글이 깨질 수 있음).");
                _font = TMP_Settings.defaultFontAsset;
            }
        }

        public static Sprite LoadIcon(string fileName)
        {
            string path = IconDir + "/" + fileName;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static GameObject UIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform RT(GameObject go) => (RectTransform)go.transform;
        public static RectTransform RT(Component c) => (RectTransform)c.transform;

        public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            Place(rt, new Vector2(0.5f, 0.5f), position, size);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image AddImage(GameObject go, Color color, Sprite slicedSprite = null)
        {
            var image = go.AddComponent<Image>();
            image.color = color;
            if (slicedSprite != null)
            {
                image.sprite = slicedSprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public static TextMeshProUGUI AddLabel(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment, Vector2 position, Vector2 rectSize, Vector2? anchor = null, bool bold = false)
        {
            GameObject go = UIObject(name, parent);
            Place(RT(go), anchor ?? new Vector2(0.5f, 0.5f), position, rectSize);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            StyleText(tmp, text, size, color, alignment, bold);
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void StyleText(TMP_Text tmp, string text, float size, Color color, TextAlignmentOptions alignment,
            bool bold = false)
        {
            Load();
            if (_font != null) tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        }

        public static void ApplyButtonColors(Selectable selectable)
        {
            ColorBlock colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
            Color color, out TextMeshProUGUI labelText, float fontSize = 28f, Vector2? anchor = null)
        {
            Load();
            GameObject go = TMP_DefaultControls.CreateButton(_tmpResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            Place(RT(go), anchor ?? new Vector2(0.5f, 0.5f), position, size);

            go.GetComponent<Image>().color = color;
            Button button = go.GetComponent<Button>();
            ApplyButtonColors(button);

            labelText = go.GetComponentInChildren<TextMeshProUGUI>();
            StyleText(labelText, label, fontSize, TextColor, TextAlignmentOptions.Center, true);
            labelText.raycastTarget = false;
            return button;
        }

        public static void Wire(Object target, params (string field, Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var pair in pairs)
            {
                SerializedProperty property = so.FindProperty(pair.field);
                if (property == null)
                {
                    Debug.LogError($"[LobbyEditorUI] {target.GetType().Name}.{pair.field} 필드를 찾지 못했습니다.");
                    continue;
                }
                property.objectReferenceValue = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"[LobbyEditorUI] {target.GetType().Name}.{field} 배열 필드를 찾지 못했습니다.");
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.magenta;
        }
    }
}
