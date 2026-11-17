using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 메뉴 "LastTruck > Character > 캐릭터 모델 FBX로 교체".
    ///
    /// 블렌더 등에서 FBX를 새로 내보낸 뒤, 캐릭터 프리팹(싱글용/네트워크용) 안의 메쉬 + 뼈대를
    /// 그 FBX가 Unity에 실제로 불러와진 모양 그대로 다시 넣는다. (크기/위치를 손으로 맞출 필요 없음)
    ///
    ///  - 바뀌는 것: 스킨 메쉬(몸 조각들)와 그 뼈대 오브젝트.
    ///  - 그대로인 것: 루트의 스크립트/콜라이더, Animator(Controller, Avatar), Camera_RayPosition 같은 다른 자식,
    ///    몸 조각의 머티리얼(같은 이름의 조각이 있으면 예전 머티리얼을 그대로 씀).
    ///  - 무기는 오른손 뼈 아래 "handslot.r"에 붙으므로 FBX 뼈대에 handslot.r이 있어야 한다.
    /// </summary>
    public class CharacterModelSwapTool : EditorWindow
    {
        private GameObject _fbx;
        private GameObject _singlePrefab;
        private GameObject _networkPrefab;
        private bool _keepMaterials = true;

        [MenuItem("LastTruck/Character/캐릭터 모델 FBX로 교체", priority = 200)]
        private static void Open()
        {
            var window = GetWindow<CharacterModelSwapTool>(true, "캐릭터 모델 교체");
            window.minSize = new Vector2(420, 230);
            window.TryAutoFill();
        }

        private void TryAutoFill()
        {
            // 처음 열 때 Explorer 기본값을 채워 둔다 (다른 캐릭터는 직접 끌어다 놓으면 된다)
            if (_fbx == null) _fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastTruck/Explorer.fbx");
            if (_singlePrefab == null) _singlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastTruck/Character/Prefabs/Explorer.prefab");
            if (_networkPrefab == null) _networkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Explorer.prefab");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("FBX의 메쉬와 뼈대를 프리팹에 다시 넣습니다. 스크립트, Animator 설정, 다른 자식 오브젝트는 그대로 둡니다.", MessageType.Info);
            _fbx = (GameObject)EditorGUILayout.ObjectField("새 모델 (FBX)", _fbx, typeof(GameObject), false);
            _singlePrefab = (GameObject)EditorGUILayout.ObjectField("싱글용 프리팹", _singlePrefab, typeof(GameObject), false);
            _networkPrefab = (GameObject)EditorGUILayout.ObjectField("네트워크 프리팹", _networkPrefab, typeof(GameObject), false);
            _keepMaterials = EditorGUILayout.Toggle("예전 머티리얼 유지", _keepMaterials);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_fbx == null || (_singlePrefab == null && _networkPrefab == null)))
            {
                if (GUILayout.Button("교체하기", GUILayout.Height(32)))
                {
                    var log = new System.Text.StringBuilder();
                    if (_singlePrefab != null) log.AppendLine(Swap(_singlePrefab, _fbx, _keepMaterials));
                    if (_networkPrefab != null) log.AppendLine(Swap(_networkPrefab, _fbx, _keepMaterials));
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("캐릭터 모델 교체", log.ToString(), "확인");
                }
            }
        }

        /// <summary>프리팹 하나의 모델을 FBX로 교체하고 결과 문장을 돌려준다.</summary>
        public static string Swap(GameObject prefabAsset, GameObject fbx, bool keepMaterials)
        {
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(path)) return $"- {prefabAsset.name}: 프리팹 경로를 찾지 못했습니다.";

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // 1) 모델이 붙어 있는 오브젝트 = 스킨 메쉬를 움직이는 Animator가 있는 곳
                SkinnedMeshRenderer[] oldSkins = contents.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (oldSkins.Length == 0) return $"- {prefabAsset.name}: 스킨 메쉬가 없어 건너뜀";

                Animator animator = oldSkins[0].GetComponentInParent<Animator>(true);
                Transform modelRoot = animator != null ? animator.transform : oldSkins[0].transform.parent;
                if (modelRoot == null) return $"- {prefabAsset.name}: 모델 위치를 찾지 못했습니다.";

                // 2) 지울 자식: 스킨 메쉬가 있는 자식 + 뼈대(스킨이 쓰는 뼈)의 최상위 자식
                var oldMaterials = new Dictionary<string, Material[]>();
                var remove = new HashSet<Transform>();
                foreach (SkinnedMeshRenderer skin in oldSkins)
                {
                    if (!skin.transform.IsChildOf(modelRoot)) continue;
                    oldMaterials[skin.name] = skin.sharedMaterials;
                    Transform top = TopChildUnder(modelRoot, skin.transform);
                    if (top != null) remove.Add(top);
                    foreach (Transform bone in skin.bones.Where(b => b != null))
                    {
                        Transform boneTop = TopChildUnder(modelRoot, bone);
                        if (boneTop != null) remove.Add(boneTop);
                    }
                    if (skin.rootBone != null)
                    {
                        Transform rootTop = TopChildUnder(modelRoot, skin.rootBone);
                        if (rootTop != null) remove.Add(rootTop);
                    }
                }
                foreach (Transform t in remove) Object.DestroyImmediate(t.gameObject);

                // 3) FBX를 그대로 꺼내 그 자식들을 모델 위치로 옮긴다 (FBX 루트의 크기/회전은 자식에 반영됨)
                GameObject temp = (GameObject)Object.Instantiate(fbx);
                temp.transform.SetParent(modelRoot, false);
                temp.transform.localPosition = Vector3.zero;
                temp.transform.localRotation = Quaternion.identity;
                temp.transform.localScale = Vector3.one;

                int layer = modelRoot.gameObject.layer;
                var moved = new List<Transform>();
                foreach (Transform child in temp.transform.Cast<Transform>().ToList())
                {
                    child.SetParent(modelRoot, true);
                    moved.Add(child);
                }
                Object.DestroyImmediate(temp);

                int skinCount = 0;
                foreach (Transform t in moved)
                {
                    foreach (Transform x in t.GetComponentsInChildren<Transform>(true))
                    {
                        x.gameObject.layer = layer;
                        // FBX에서 딸려 온 스크립트/물리는 뺀다 (모델은 그림만)
                        foreach (Collider c in x.GetComponents<Collider>()) Object.DestroyImmediate(c);
                        foreach (Rigidbody r in x.GetComponents<Rigidbody>()) Object.DestroyImmediate(r);
                        Animator extra = x.GetComponent<Animator>();
                        if (extra != null && extra != animator) Object.DestroyImmediate(extra);
                    }
                    foreach (SkinnedMeshRenderer skin in t.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        skinCount++;
                        if (keepMaterials && oldMaterials.TryGetValue(skin.name, out Material[] mats) && mats != null && mats.Length > 0)
                            skin.sharedMaterials = mats;
                        skin.updateWhenOffscreen = false;
                    }
                }

                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.Rebind();
                }

                bool hasHandSlot = contents.GetComponentsInChildren<Transform>(true).Any(t => t.name == "handslot.r");

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return $"- {prefabAsset.name}: 몸 조각 {skinCount}개 교체 완료" + (hasHandSlot ? "" : "  (주의: handslot.r이 없어 무기가 손에 안 붙습니다)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>t의 조상 중 parent 바로 아래 자식을 찾는다 (t가 parent 밖이면 null).</summary>
        private static Transform TopChildUnder(Transform parent, Transform t)
        {
            if (t == null || t == parent || !t.IsChildOf(parent)) return null;
            while (t.parent != null && t.parent != parent) t = t.parent;
            return t.parent == parent ? t : null;
        }
    }
}
