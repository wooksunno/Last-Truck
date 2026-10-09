using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 메뉴 "LastTruck > Multiplayer > 2. 캐릭터 초상화 촬영".
    ///
    /// 캐릭터 목록의 원본 프리팹(modelPrefab)을 보이지 않는 임시 씬에 세우고, Idle 자세로 만든 뒤
    /// 정면에서 얼굴~어깨를 클로즈업으로 찍어 배경이 투명한 PNG로 저장한다 (3D 로우폴리 그대로).
    /// 저장 위치: Assets/LastTruck/Multiplayer/Portraits/{캐릭터}.png → 캐릭터 데이터의 portrait에 자동 연결.
    ///
    /// 구도(거리/각도/여백)와 조명은 이 창에서 바꿔서 다시 찍을 수 있다 (값은 이 컴퓨터에 저장됨).
    /// 투명 배경은 검은 배경/흰 배경으로 두 번 찍어서 차이로 계산한다 (렌더 파이프라인 설정과 상관없이 동작).
    /// </summary>
    public class CharacterPortraitCapture : EditorWindow
    {
        #region 촬영 설정 / 창(UI)

        public const string PortraitDir = LobbyEditorUI.Root + "/Portraits";
        private const string PrefsPrefix = "LastTruck.PortraitCapture.";

        [System.Serializable]
        public struct Settings
        {
            public int size;              // 결과 이미지 한 변 (px)
            public float fieldOfView;     // 카메라 화각
            public float yaw;             // 0 = 정면, +는 캐릭터 기준 오른쪽에서
            public float pitch;           // +는 위에서 내려다봄
            public float shoulderDepth;   // 머리 기준점 아래로 얼마나 더 담을지 (머리 높이 대비)
            public float topMargin;       // 머리 위 여백 (머리 높이 대비)
            public float verticalOffset;  // 화면 중심 상하 이동 (머리 높이 대비, +는 위)
            public float poseTime;        // Idle 애니메이션의 몇 초 지점 자세로 찍을지
            public float keyLight;        // 정면 주 조명 세기
            public float fillLight;       // 보조 조명 세기
            public float rimLight;        // 뒤쪽 윤곽 조명 세기

            public static Settings Default => new Settings
            {
                size = 512,
                fieldOfView = 20f,
                yaw = 0f,
                pitch = 4f,
                shoulderDepth = 0.55f,
                topMargin = 0.08f,
                verticalOffset = 0f,
                poseTime = 0.3f,
                keyLight = 1.25f,
                fillLight = 0.55f,
                rimLight = 0.6f,
            };
        }

        private Settings _settings;
        private Vector2 _scroll;

        [MenuItem("LastTruck/Multiplayer/2. 캐릭터 초상화 촬영", priority = 102)]
        private static void Open()
        {
            var window = GetWindow<CharacterPortraitCapture>("캐릭터 초상화 촬영");
            window.minSize = new Vector2(420f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = LoadSettings();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "캐릭터 원본 프리팹을 정면에서 얼굴~어깨로 찍어 초상화를 만듭니다.\n" +
                "구도가 마음에 안 들면 아래 값을 바꾸고 다시 찍으세요.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _settings.size = EditorGUILayout.IntPopup("이미지 크기", _settings.size,
                new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
            _settings.fieldOfView = EditorGUILayout.Slider("화각 (작을수록 망원)", _settings.fieldOfView, 8f, 60f);
            _settings.yaw = EditorGUILayout.Slider("좌우 각도 (0 = 정면)", _settings.yaw, -60f, 60f);
            _settings.pitch = EditorGUILayout.Slider("상하 각도 (+ 위에서)", _settings.pitch, -30f, 30f);
            _settings.shoulderDepth = EditorGUILayout.Slider("아래로 담는 범위 (어깨)", _settings.shoulderDepth, 0.1f, 1.5f);
            _settings.topMargin = EditorGUILayout.Slider("머리 위 여백", _settings.topMargin, 0f, 0.5f);
            _settings.verticalOffset = EditorGUILayout.Slider("구도 상하 이동", _settings.verticalOffset, -0.5f, 0.5f);
            _settings.poseTime = EditorGUILayout.Slider("Idle 자세 시점 (초)", _settings.poseTime, 0f, 3f);
            _settings.keyLight = EditorGUILayout.Slider("주 조명", _settings.keyLight, 0f, 3f);
            _settings.fillLight = EditorGUILayout.Slider("보조 조명", _settings.fillLight, 0f, 3f);
            _settings.rimLight = EditorGUILayout.Slider("윤곽 조명", _settings.rimLight, 0f, 3f);
            if (EditorGUI.EndChangeCheck()) SaveSettings(_settings);

            if (GUILayout.Button("기본값으로 되돌리기"))
            {
                _settings = Settings.Default;
                SaveSettings(_settings);
            }

            EditorGUILayout.Space();

            CharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(NetworkCharacterBuilder.CatalogPath);
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("캐릭터 목록이 없습니다. 먼저 '1. 네트워크 프리팹 생성'을 실행하세요.", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("전체 촬영", GUILayout.Height(32f)))
                {
                    // 창 그리기(OnGUI) 도중에 렌더링하면 가끔 검은 화면이 찍혀서, 그리기가 끝난 뒤에 찍는다.
                    Settings settings = _settings;
                    EditorApplication.delayCall += () =>
                    {
                        string log = CaptureAll(settings);
                        if (this != null) ShowNotification(new GUIContent("촬영 완료"));
                        Debug.Log("[CharacterPortraitCapture]\n" + log);
                        Repaint();
                    };
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (CharacterStatsData data in catalog.Characters)
            {
                if (data == null) continue;
                EditorGUILayout.BeginHorizontal("box");
                Rect rect = GUILayoutUtility.GetRect(72f, 72f, GUILayout.Width(72f), GUILayout.Height(72f));
                if (data.portrait != null) GUI.DrawTexture(rect, data.portrait.texture, ScaleMode.ScaleToFit);
                else EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(data.DisplayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(data.modelPrefab != null ? "원본: " + data.modelPrefab.name : "원본 프리팹 없음");
                using (new EditorGUI.DisabledScope(data.modelPrefab == null || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("이 캐릭터만 촬영", GUILayout.Width(140f)))
                    {
                        CharacterStatsData target = data;
                        Settings settings = _settings;
                        EditorApplication.delayCall += () =>
                        {
                            CaptureAndAssign(target, settings);
                            AssetDatabase.SaveAssets();
                            if (this != null) Repaint();
                        };
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region 설정 저장 (EditorPrefs)

        public static Settings LoadSettings()
        {
            Settings d = Settings.Default;
            return new Settings
            {
                size = EditorPrefs.GetInt(PrefsPrefix + "size", d.size),
                fieldOfView = EditorPrefs.GetFloat(PrefsPrefix + "fov", d.fieldOfView),
                yaw = EditorPrefs.GetFloat(PrefsPrefix + "yaw", d.yaw),
                pitch = EditorPrefs.GetFloat(PrefsPrefix + "pitch", d.pitch),
                shoulderDepth = EditorPrefs.GetFloat(PrefsPrefix + "shoulder", d.shoulderDepth),
                topMargin = EditorPrefs.GetFloat(PrefsPrefix + "top", d.topMargin),
                verticalOffset = EditorPrefs.GetFloat(PrefsPrefix + "voffset", d.verticalOffset),
                poseTime = EditorPrefs.GetFloat(PrefsPrefix + "pose", d.poseTime),
                keyLight = EditorPrefs.GetFloat(PrefsPrefix + "key", d.keyLight),
                fillLight = EditorPrefs.GetFloat(PrefsPrefix + "fill", d.fillLight),
                rimLight = EditorPrefs.GetFloat(PrefsPrefix + "rim", d.rimLight),
            };
        }

        private static void SaveSettings(Settings s)
        {
            EditorPrefs.SetInt(PrefsPrefix + "size", s.size);
            EditorPrefs.SetFloat(PrefsPrefix + "fov", s.fieldOfView);
            EditorPrefs.SetFloat(PrefsPrefix + "yaw", s.yaw);
            EditorPrefs.SetFloat(PrefsPrefix + "pitch", s.pitch);
            EditorPrefs.SetFloat(PrefsPrefix + "shoulder", s.shoulderDepth);
            EditorPrefs.SetFloat(PrefsPrefix + "top", s.topMargin);
            EditorPrefs.SetFloat(PrefsPrefix + "voffset", s.verticalOffset);
            EditorPrefs.SetFloat(PrefsPrefix + "pose", s.poseTime);
            EditorPrefs.SetFloat(PrefsPrefix + "key", s.keyLight);
            EditorPrefs.SetFloat(PrefsPrefix + "fill", s.fillLight);
            EditorPrefs.SetFloat(PrefsPrefix + "rim", s.rimLight);
        }

        #endregion

        #region 촬영

        /// <summary>목록의 모든 캐릭터를 찍는다. 결과 요약을 돌려준다.</summary>
        public static string CaptureAll(Settings settings)
        {
            CharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(NetworkCharacterBuilder.CatalogPath);
            if (catalog == null) return "캐릭터 목록이 없습니다.";

            var lines = new List<string>();
            List<CharacterStatsData> list = catalog.Characters.Where(c => c != null).ToList();
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CharacterStatsData data = list[i];
                    EditorUtility.DisplayProgressBar("캐릭터 초상화 촬영", data.DisplayName, (float)i / Mathf.Max(1, list.Count));
                    if (data.modelPrefab == null)
                    {
                        lines.Add($"- {data.DisplayName}: 원본 프리팹이 없어 건너뜀");
                        continue;
                    }
                    lines.Add(CaptureAndAssign(data, settings)
                        ? $"- {data.DisplayName}: 완료"
                        : $"- {data.DisplayName}: 실패 (콘솔 확인)");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            return string.Join("\n", lines);
        }

        public static bool CaptureAndAssign(CharacterStatsData data, Settings settings)
        {
            Texture2D texture = null;
            try
            {
                texture = Capture(data.modelPrefab, settings);
                if (texture == null) return false;

                LobbyEditorUI.EnsureFolder(PortraitDir);
                string id = data.name.Replace("CharacterData_", string.Empty);
                foreach (char c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
                string path = $"{PortraitDir}/{id}.png";

                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }

                data.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                EditorUtility.SetDirty(data);
                return data.portrait != null;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }

        /// <summary>프리팹 하나를 찍어서 배경이 투명한 텍스처로 돌려준다.</summary>
        public static Texture2D Capture(GameObject prefab, Settings s)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            PlayableGraph graph = default;
            RenderTexture rt = null;
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (model == null)
                {
                    model = Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(model, scene);
                }
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                // 1) Idle 자세 (T포즈로 찍히지 않게)
                Animator animator = model.GetComponentInChildren<Animator>();
                graph = PoseWithIdle(animator, s.poseTime);

                // 2) 구도 계산: 머리 뼈 기준
                Bounds bounds = CalculateBounds(model);
                Transform head = FindHead(animator, model.transform);
                float top = bounds.max.y;
                float headBaseY = head != null ? head.position.y : Mathf.Lerp(bounds.min.y, top, 0.7f);
                if (headBaseY >= top) headBaseY = Mathf.Lerp(bounds.min.y, top, 0.7f);
                float headHeight = Mathf.Max(0.05f, top - headBaseY);

                float frameBottom = headBaseY - headHeight * s.shoulderDepth;
                float frameTop = top + headHeight * s.topMargin;
                float frameHeight = frameTop - frameBottom;
                Vector3 center = head != null ? head.position : bounds.center;
                center.y = (frameTop + frameBottom) * 0.5f + headHeight * s.verticalOffset;

                Quaternion view = Quaternion.Euler(s.pitch, 180f + s.yaw, 0f); // 캐릭터 정면(+Z)을 바라보는 카메라
                float distance = frameHeight * 0.5f / Mathf.Tan(s.fieldOfView * 0.5f * Mathf.Deg2Rad);
                Vector3 cameraPos = center - view * Vector3.forward * distance;

                // 3) 카메라 + 조명 (임시 씬 안에만 존재)
                var cameraGo = new GameObject("PortraitCamera");
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var cam = cameraGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.enabled = false;
                cam.cameraType = CameraType.Preview;
                cam.fieldOfView = s.fieldOfView;
                cam.nearClipPlane = Mathf.Max(0.01f, distance * 0.05f);
                cam.farClipPlane = distance * 4f + 10f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.allowHDR = false;
                cam.allowMSAA = true;
                cameraGo.transform.SetPositionAndRotation(cameraPos, Quaternion.LookRotation(center - cameraPos));

                AddLight(scene, "Key", s.keyLight, Quaternion.Euler(25f, 180f + 35f, 0f), new Color(1f, 0.97f, 0.92f));
                AddLight(scene, "Fill", s.fillLight, Quaternion.Euler(10f, 180f - 50f, 0f), new Color(0.85f, 0.9f, 1f));
                AddLight(scene, "Rim", s.rimLight, Quaternion.Euler(20f, 20f, 0f), Color.white);

                // 4) 검은 배경 / 흰 배경 두 번 찍어서 투명도 계산
                rt = RenderTexture.GetTemporary(s.size, s.size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.antiAliasing = 1;
                Color[] onBlack = Render(cam, rt, Color.black, s.size);
                Color[] onWhite = Render(cam, rt, Color.white, s.size);

                var result = new Texture2D(s.size, s.size, TextureFormat.RGBA32, false);
                var pixels = new Color[onBlack.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color b = onBlack[i];
                    Color w = onWhite[i];
                    float alpha = 1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f;
                    alpha = Mathf.Clamp01(alpha);
                    pixels[i] = alpha > 0.004f
                        ? new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha), alpha)
                        : new Color(0f, 0f, 0f, 0f);
                }
                result.SetPixels(pixels);
                result.Apply();
                return result;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Color[] Render(Camera cam, RenderTexture rt, Color background, int size)
        {
            cam.backgroundColor = background;
            cam.targetTexture = rt;

            var request = new RenderPipeline.StandardRequest();
            if (GraphicsSettings.currentRenderPipeline != null && RenderPipeline.SupportsRenderRequest(cam, request))
            {
                request.destination = rt;
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.Render();
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readback = new Texture2D(size, size, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;

            Color[] pixels = readback.GetPixels();
            Object.DestroyImmediate(readback);
            return pixels;
        }

        private static void AddLight(Scene scene, string name, float intensity, Quaternion rotation, Color color)
        {
            if (intensity <= 0f) return;
            var go = new GameObject(name + "Light");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        /// <summary>애니메이터 컨트롤러 안의 Idle 클립으로 자세를 잡는다. 없으면 그대로(기본 자세).</summary>
        private static PlayableGraph PoseWithIdle(Animator animator, float time)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return default;
            animator.Rebind(); // 새로 만든 애니메이터를 초기화해야 휴머노이드 뼈 정보/자세 적용이 된다.

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            AnimationClip idle = clips.FirstOrDefault(c => c != null && c.name.ToLowerInvariant().Contains("idle"))
                                 ?? clips.FirstOrDefault(c => c != null);
            if (idle == null) return default;

            PlayableGraph graph = PlayableGraph.Create("PortraitPose");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            var playable = AnimationClipPlayable.Create(graph, idle);
            output.SetSourcePlayable(playable);
            playable.SetTime(Mathf.Min(time, idle.length));
            graph.Evaluate();
            return graph;
        }

        private static Transform FindHead(Animator animator, Transform root)
        {
            if (animator != null && animator.isHuman)
            {
                Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone != null) return bone;
            }

            Transform best = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("head")) continue;
                if (n.Contains("end") || n.Contains("top") || n.Contains("nub")) continue;
                if (best == null || t.GetComponentsInParent<Transform>().Length < best.GetComponentsInParent<Transform>().Length)
                {
                    best = t; // 가장 위 계층의 "head"
                }
            }
            return best;
        }

        /// <summary>
        /// 현재 자세 기준의 실제 크기. 스킨드 메쉬는 에디터에서 bounds가 자세를 따라 갱신되지 않으므로
        /// 현재 자세로 메쉬를 구워서(BakeMesh) 꼭짓점으로 계산한다.
        /// </summary>
        private static Bounds CalculateBounds(GameObject model)
        {
            bool hasBounds = false;
            var bounds = new Bounds(model.transform.position + Vector3.up, Vector3.one * 2f);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    var baked = new Mesh();
                    try
                    {
                        skinned.BakeMesh(baked, true);
                        Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                        foreach (Vector3 vertex in baked.vertices)
                        {
                            Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                            if (!hasBounds) { bounds = new Bounds(world, Vector3.zero); hasBounds = true; }
                            else bounds.Encapsulate(world);
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(baked);
                    }
                }
                else if (!(renderer is ParticleSystemRenderer))
                {
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        #endregion
    }
}
