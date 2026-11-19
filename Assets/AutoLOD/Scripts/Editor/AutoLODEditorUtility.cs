using UnityEngine;
using UnityEditor;
using AutoLOD.MeshDecimator;
using System.IO;
using MathNet.Numerics;
using UnityEditor.PackageManager;

namespace AutoLOD.Utilities
{
    public static class AutoLODEditorUtility
    {
        public static GUIStyle centeredStyle;
        public static GUIStyle titleStyle;
        public static GUIStyle pathCreatedStyle;
        public static GUIStyle pathInputStyle;
        public static GUIStyle subHeaderStyle;
        public static GUIStyle dropBoxStyle;
        public static GUIStyle smallFont;


        public static Color[] LodColors = new Color[9] {
            new Color(0.23f, 0.27f, 0.12f),
            new Color(0.18f, 0.21f, 0.26f),
            new Color(0.16f, 0.25f, 0.29f),
            new Color(0.25f, 0.14f, 0.12f),
            new Color(0.20f, 0.18f, 0.25f),
            new Color(0.32f, 0.22f, 0.11f),
            new Color(0.35f, 0.32f, 0.04f),
            new Color(0.32f, 0.27f, 0.12f),
            new Color(0.32f, 0f, 0f)
        };

        static AutoLODEditorUtility()
        {
            InitializeStyle();
        }

        public static void InitializeStyle()
        {
            if (centeredStyle == null)
            {
                centeredStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    stretchWidth = true
                };
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (pathCreatedStyle == null)
            {
                pathCreatedStyle = new GUIStyle(EditorStyles.label);
                pathCreatedStyle.normal.textColor = Color.green;
                pathCreatedStyle.fontSize = 10;
                pathCreatedStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (pathInputStyle == null)
            {
                pathInputStyle = new GUIStyle(EditorStyles.textField);
                pathInputStyle.focused.textColor = pathInputStyle.normal.textColor;
                pathInputStyle.hover.textColor = pathInputStyle.normal.textColor;
            }

            if (subHeaderStyle == null)
            {
                subHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader);
                subHeaderStyle.fontStyle = FontStyle.Normal;
                subHeaderStyle.stretchWidth = true;
            }

            if (dropBoxStyle == null)
            {
                dropBoxStyle = new GUIStyle(GUI.skin.box);
                dropBoxStyle.alignment = TextAnchor.MiddleCenter;
                dropBoxStyle.fontSize = 10;
                dropBoxStyle.hover.textColor = Color.green;
            }

            if (smallFont == null)
                smallFont = new GUIStyle
                {
                    fontSize = 8,
                    alignment = TextAnchor.LowerRight
                };
        }

        public static void DrawPropertiesPanel(AutoLODProperties properties, Editor editor, out bool needsRepaint)
        {
            needsRepaint = false;
            pathInputStyle.normal.textColor = !Directory.Exists(Application.dataPath + "/" + properties._filePath) ? Color.yellow : Color.green;
            pathInputStyle.hover.textColor = !Directory.Exists(Application.dataPath + "/" + properties._filePath) ? Color.yellow : Color.green;
            pathInputStyle.focused.textColor = !Directory.Exists(Application.dataPath + "/" + properties._filePath) ? Color.yellow : Color.green;
            SerializedProperty property;
            properties._backend = (MeshDecimatorBackend)EditorGUILayout.EnumPopup("Backend", properties._backend);
            property = editor.serializedObject.FindProperty("_lodLevels");
            EditorGUILayout.IntSlider(property, 1, 8);
            property = editor.serializedObject.FindProperty("_reductionRate");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(properties._autoReductionRate))
            {
                EditorGUILayout.Slider(property, 1.1f, 6);
            }
            properties._autoReductionRate = GUILayout.Toggle(properties._autoReductionRate, "Auto", EditorStyles.miniButton, GUILayout.Width(50));
            if (properties._autoReductionRate)
            {
                properties._reductionRate = 1f/properties._performance;
            }
            EditorGUILayout.EndHorizontal();

            Rect lodPreviewRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(lodPreviewRect, MouseCursor.ResizeHorizontal);

            float fullWidth = lodPreviewRect.width;
            Rect cullingRect = lodPreviewRect;
            cullingRect.width = Mathf.Sqrt(properties._relativeHeightCulling) * fullWidth;
            cullingRect.x = lodPreviewRect.x + fullWidth - cullingRect.width;

            int controlId = GUIUtility.GetControlID(FocusType.Passive, lodPreviewRect);
            Event e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (lodPreviewRect.Contains(e.mousePosition) && e.button == 0)
                    {
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        if (cullingRect.Contains(e.mousePosition))
                        {
                            float newWidth = Mathf.Clamp(cullingRect.width - e.delta.x, 0f, fullWidth);
                            float newCull = Mathf.Clamp(Mathf.Pow(newWidth / fullWidth, 2f), 0.001f, Mathf.Pow(properties._performance, properties._lodLevels));
                            if (!Mathf.Approximately(newCull, properties._relativeHeightCulling))
                            {
                                properties._relativeHeightCulling = newCull;
                                needsRepaint = true;
                            }
                        }
                        else
                        {
                            float newPerf = Mathf.Clamp(properties._performance - e.delta.x / fullWidth, 0.1f, 0.9f);
                            if (!Mathf.Approximately(newPerf, properties._performance))
                            {
                                properties._performance = newPerf;
                                properties._relativeHeightCulling = Mathf.Min(properties._relativeHeightCulling, Mathf.Pow(newPerf, properties._lodLevels));
                                needsRepaint = true;
                            }
                        }
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }

            EditorGUI.DrawRect(lodPreviewRect, LodColors[0]);
            EditorGUI.LabelField(lodPreviewRect, "LOD 0" + "\n100%", EditorStyles.miniBoldLabel);
            float ratio = Mathf.Sqrt(properties._performance);
            float xInit = lodPreviewRect.x;
            lodPreviewRect.width *= ratio;
            lodPreviewRect.x = xInit + (1f - ratio) * fullWidth;
            for (int lvl = 1; lvl < properties._lodLevels; ++lvl)
            {
                Color lodColor = LodColors[lvl];
                EditorGUI.DrawRect(lodPreviewRect, lodColor);
                EditorGUI.LabelField(lodPreviewRect, "LOD " + lvl + "\n" + (100f * Mathf.Pow(properties._performance, lvl)).ToString("#.") + "%", EditorStyles.miniBoldLabel);
                lodPreviewRect.x += (1f - ratio) * lodPreviewRect.width;
                lodPreviewRect.width *= ratio;
            }

            EditorGUI.DrawRect(cullingRect, LodColors[8]);
            EditorGUI.LabelField(cullingRect, "Culled" + "\n" + (100f * properties._relativeHeightCulling).ToString("#.0") + "%", EditorStyles.miniBoldLabel);

            EditorGUILayout.Separator();

            if (properties._backend == MeshDecimatorBackend.Fast)
            {
                property = editor.serializedObject.FindProperty("_flatShading");
                EditorGUILayout.PropertyField(property, new GUIContent("Flat shading", "The Fast backend can't detect sharp edges and will break the flat shading effect. This option can force the output to be flat shaded."));
            }
            else
            {
                properties._flatShading = false;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save LOD meshes to Assets/", GUILayout.Width(168));
            properties._filePath = EditorGUILayout.TextField(properties._filePath, pathInputStyle);

            if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(24), GUILayout.Height(18)))
            {
                GUI.FocusControl(null);
                string absPath = EditorUtility.SaveFolderPanel("Save Path", "Assets/" + properties._filePath, "Assets/" + properties._filePath);

                if (absPath.Contains(Application.dataPath))
                {
                    properties._filePath = absPath.Substring(Application.dataPath.Length);
                    if (properties._filePath.StartsWith("/"))
                        properties._filePath = properties._filePath.Substring(1);
                }
                else
                {
                    if (absPath != "")
                        Debug.LogWarning("Invalid path: " + absPath + ". Please save the file under the Assets/ folder");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!Directory.Exists(Application.dataPath + "/" + properties._filePath))
            {
                EditorGUILayout.LabelField("The path will be created.", pathCreatedStyle);
            }
        }
    }
}
