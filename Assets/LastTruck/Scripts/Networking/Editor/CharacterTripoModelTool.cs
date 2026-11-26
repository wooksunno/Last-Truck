using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 메뉴 "LastTruck > Character > 새 캐릭터 모델 적용 (explorer 1, army, chef)".
    /// (해당 캐릭터의 머티리얼이 아직 없으면 Unity가 스크립트를 불러온 뒤 한 번 자동으로 실행된다)
    ///
    /// Mixamo 뼈대(Blender/Tripo) 캐릭터 FBX를 기존 캐릭터 프리팹에 넣는 도구. 캐릭터마다:
    ///  1. 머티리얼 [이름]_Stylized.mat 생성 (캐릭터 공용 셰이더, KayKit 캐릭터와 같은 설정 + 각자 basecolor 텍스처)
    ///  2. FBX 임포트: Humanoid 아바타, 머티리얼 연결, 기존 캐릭터와 키가 같도록 Scale Factor 조정
    ///  3. 싱글용 프리팹과 NetworkPlayer_ 프리팹의 모델(메쉬+뼈대)을 FBX로 교체, Avatar 교체, 오른손 무기 자리(handslot.r)
    /// 스크립트/능력/스탯은 그대로 둔다.
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterTripoModelTool
    {
        private const string TemplateMaterialPath = "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Materials/engineer.mat"; // 캐릭터 공용 셰이더(LastTruck/StylizedCharacter) 설정
        private const string AutoRunKey = "LastTruck.CharacterTripoModelTool.AutoRan.v11";

        private struct Entry
        {
            public string Fbx, Texture, Material, Prefab, Network;
            public bool Auto;   // Unity가 스크립트를 불러올 때 아직 적용 안 됐으면 자동 적용
        }

        private const string AppliedListPath = "Assets/LastTruck/Character/Baked/applied_models.txt";

        private static string AppliedKey(Entry e) => e.Prefab + "|" + AssetDatabase.AssetPathToGUID(e.Fbx);

        private static bool IsApplied(Entry e)
        {
            if (!System.IO.File.Exists(AppliedListPath)) return false;
            return System.IO.File.ReadAllLines(AppliedListPath).Contains(AppliedKey(e));
        }

        private static void MarkApplied(Entry e)
        {
            if (IsApplied(e)) return;
            if (!AssetDatabase.IsValidFolder(BakedDir)) AssetDatabase.CreateFolder("Assets/LastTruck/Character", "Baked");
            System.IO.File.AppendAllText(AppliedListPath, AppliedKey(e) + "\n");
            AssetDatabase.ImportAsset(AppliedListPath);
        }

        private static readonly Entry[] Entries =
        {
            new Entry
            {
                Fbx = "Assets/LastTruck/explorer 1.fbx",
                Texture = "Assets/LastTruck/explorer_boy_3d_model_basecolor.jpg",
                Material = "Assets/LastTruck/Materials/explorer1_Stylized.mat",
                Prefab = "Assets/LastTruck/Character/Prefabs/Explorer.prefab",
                Network = "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Explorer.prefab",
                Auto = true,
            },
            new Entry
            {
                Fbx = "Assets/LastTruck/army.fbx",
                Texture = "Assets/LastTruck/6c8a04a0-2f65-422e-b4a0-0b2e9c7de169_basecolor.jpg",
                Material = "Assets/LastTruck/Materials/army_Stylized.mat",
                Prefab = "Assets/LastTruck/Character/Prefabs/Army.prefab",
                Network = "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Army.prefab",
                Auto = true,
            },
            new Entry
            {
                Fbx = "Assets/LastTruck/chef.fbx",
                Texture = "Assets/LastTruck/chef_character_3d_model_basecolor.jpg",
                Material = "Assets/LastTruck/Materials/chef_Stylized.mat",
                Prefab = "Assets/LastTruck/Character/Prefabs/Chef.prefab",
                Network = "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Chef.prefab",
                Auto = true,
            },
        };

        static CharacterTripoModelTool()
        {
            if (SessionState.GetBool(AutoRunKey, false)) return;
            SessionState.SetBool(AutoRunKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                RestoreAllPrefabs();
                FixAllPrefabs();

                // 한 번만: 발 높이 맞추기
                const string footMarker = "Assets/LastTruck/Character/Baked/feet_on_ground.txt";
                if (!System.IO.File.Exists(footMarker) && System.IO.File.Exists("Assets/LastTruck/Character/Baked/resized_to_blacksmith.txt"))
                {
                    string footLog = FixFootHeights();
                    System.IO.File.WriteAllText(footMarker, footLog);
                    AssetDatabase.ImportAsset(footMarker);
                    return;
                }

                // 한 번만: 모든 새 캐릭터를 BlackSmith 키에 맞춰 다시 적용 (크기 조정)
                const string resizeMarker = "Assets/LastTruck/Character/Baked/resized_to_blacksmith.txt";
                if (!System.IO.File.Exists(resizeMarker))
                {
                    string resizeLog = Run(Entries);
                    if (!AssetDatabase.IsValidFolder(BakedDir)) AssetDatabase.CreateFolder("Assets/LastTruck/Character", "Baked");
                    System.IO.File.WriteAllText(resizeMarker, "BlackSmith 키에 맞춰 다시 적용함\n");
                    AssetDatabase.ImportAsset(resizeMarker);
                    Debug.Log("[CharacterTripoModelTool] BlackSmith 키에 맞춤\n" + resizeLog);
                    EditorUtility.DisplayDialog("캐릭터 크기 조정", resizeLog, "확인");
                    return;
                }
                // 아직 적용 안 된 것(프리팹의 Avatar가 그 FBX의 아바타가 아닌 것)만
                Entry[] todo = Entries.Where(e => e.Auto && NeedsApply(e) && !IsApplied(e)).ToArray();
                if (todo.Length == 0) return;
                string log = Run(todo);
                Debug.Log("[CharacterTripoModelTool]\n" + log);
                EditorUtility.DisplayDialog("새 캐릭터 모델 적용", log, "확인");
            };
        }

        private static bool NeedsApply(Entry e)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(e.Fbx) == null) return false;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.Prefab);
            if (prefab == null) return false;
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Avatar fbxAvatar = AssetDatabase.LoadAllAssetsAtPath(e.Fbx).OfType<Avatar>().FirstOrDefault();
            return animator == null || fbxAvatar == null || animator.avatar != fbxAvatar;
        }

        [MenuItem("LastTruck/Character/새 캐릭터 모델 적용 (explorer 1, army, chef)", priority = 202)]
        private static void Menu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("새 캐릭터 모델 적용", "플레이 모드에서는 실행할 수 없습니다.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("새 캐릭터 모델 적용",
                    "Explorer / Army / Chef 프리팹(싱글+네트워크)의 모델을 explorer 1 / army / chef 로 다시 교체합니다.", "교체", "취소")) return;
            string log = Run(Entries);
            Debug.Log("[CharacterTripoModelTool]\n" + log);
            EditorUtility.DisplayDialog("새 캐릭터 모델 적용", log, "확인");
        }

        private static string Run(Entry[] entries)
        {
            var log = new System.Text.StringBuilder();
            foreach (Entry e in entries)
            {
                try { log.AppendLine(Apply(e)); MarkApplied(e); }
                catch (System.Exception ex) { log.AppendLine($"- {System.IO.Path.GetFileName(e.Fbx)}: 실패 ({ex.Message})"); Debug.LogException(ex); }
            }
            AssetDatabase.SaveAssets();
            // 교체 직후 저장된 뼈 위치가 FBX 원래 값(아주 작은 값)으로 남는 경우가 있어,
            // 프리팹을 다시 열어 바인드포즈 기준으로 뼈 위치를 한 번 더 맞춘다 (머리만 보이는 문제 방지).
            RestoreAllPrefabs();
            log.AppendLine();
            log.Append(FixFootHeights());
            return log.ToString();
        }

        private static string Apply(Entry e)
        {
            var log = new System.Text.StringBuilder();
            string name = System.IO.Path.GetFileNameWithoutExtension(e.Fbx);
            log.AppendLine($"[{name}]");

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(e.Prefab);
            if (source == null) return log + $"- 프리팹을 찾지 못했습니다: {e.Prefab}";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(e.Fbx) == null) return log + $"- FBX를 찾지 못했습니다: {e.Fbx}";

            // 1) 머티리얼 (캐릭터 공용 셰이더)
            Material material = AssetDatabase.LoadAssetAtPath<Material>(e.Material);
            if (material == null)
            {
                Material template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterialPath);
                if (template == null) return log + "- 기준 머티리얼(KayKit engineer.mat)을 찾지 못했습니다.";
                material = new Material(template) { name = System.IO.Path.GetFileNameWithoutExtension(e.Material) };
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(e.Texture);
                if (tex != null)
                {
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", tex);
                }
                else log.AppendLine($"- 주의: 텍스처를 찾지 못했습니다 ({e.Texture})");
                AssetDatabase.CreateAsset(material, e.Material);
                log.AppendLine($"- 머티리얼 생성: {e.Material}");
            }

            // 2) FBX 임포트 설정
            var importer = (ModelImporter)AssetImporter.GetAtPath(e.Fbx);
            bool reimport = false;
            if (importer.animationType != ModelImporterAnimationType.Human) { importer.animationType = ModelImporterAnimationType.Human; reimport = true; }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; reimport = true; }
            foreach (string matName in FbxMaterialNames(e.Fbx))
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), matName);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out Object current) || current != material)
                {
                    importer.AddRemap(id, material);
                    reimport = true;
                }
            }
            if (reimport) importer.SaveAndReimport();

            // 키 맞추기: 대장장이(BlackSmith) 캐릭터의 전체 키(발끝~머리끝)와 같게 (비율은 그대로, 크기만)
            // (프리팹마다 맨 위 크기가 다를 수 있어서, 실제 보이는 키가 같도록 그 비율도 반영한다)
            GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>(HeightReferencePrefab);
            float refHeight = MeasureBodyHeight(reference) * ModelRootScale(reference) / Mathf.Max(1e-4f, ModelRootScale(source));
            if (refHeight <= 0.01f) refHeight = MeasureBodyHeight(source);
            for (int i = 0; i < 2 && refHeight > 0.01f; i++)
            {
                float newHeight = MeasureBodyHeight(AssetDatabase.LoadAssetAtPath<GameObject>(e.Fbx));
                if (newHeight <= 0.0001f) break;
                float k = refHeight / newHeight;
                if (Mathf.Abs(k - 1f) < 0.01f) break;
                importer = (ModelImporter)AssetImporter.GetAtPath(e.Fbx);
                importer.globalScale *= k;
                importer.SaveAndReimport();
                log.AppendLine($"- 크기 조정 (BlackSmith 키 {refHeight:0.00}에 맞춤): Scale Factor {importer.globalScale:0.####}");
            }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(e.Fbx);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(e.Fbx).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
                log.AppendLine("- 주의: Humanoid 아바타가 올바르지 않습니다 (Rig 탭 > Configure에서 확인).");

            // 3) 모델 교체
            log.AppendLine(Swap(e.Prefab, fbx, avatar, material));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(e.Network) != null)
                log.AppendLine(Swap(e.Network, fbx, avatar, material));
            else
                log.AppendLine($"- 네트워크 프리팹이 없어 건너뜀: {e.Network}");
            return log.ToString();
        }

        private static IEnumerable<string> FbxMaterialNames(string fbxPath)
        {
            var names = new HashSet<string>();
            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath))
                if (o is Material m) names.Add(m.name);
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx != null)
                foreach (Renderer r in fbx.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && AssetDatabase.GetAssetPath(m) == fbxPath) names.Add(m.name);
            return names;
        }

        /// <summary>모델 맨 위(애니메이터가 있는 곳) 기준 머리 뼈 높이 (에셋을 꺼내지 않고 그대로 잰다).</summary>
        private static float MeasureHeadHeight(GameObject asset)
        {
            if (asset == null) return 0f;
            Animator animator = asset.GetComponentInChildren<Animator>(true);
            Transform root = animator != null ? animator.transform : asset.transform;
            Avatar avatar = animator != null ? animator.avatar : null;
            if (avatar == null) avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)).OfType<Avatar>().FirstOrDefault();
            Transform head = FindHumanBone(root, avatar, "Head") ?? FindDeep(root, "head") ?? FindDeep(root, "mixamorig:Head");
            if (head == null) return 0f;
            // 루트의 위치/회전/크기는 빼고 잰다 (교체할 때 FBX를 모델 위치에 크기 1로 넣기 때문)
            return root.InverseTransformPoint(head.position).y;
        }

        /// <summary>키 기준이 되는 캐릭터 (모든 캐릭터를 이 키에 맞춘다).</summary>
        private const string HeightReferencePrefab = "Assets/LastTruck/Character/Prefabs/BlackSmith.prefab";

        /// <summary>
        /// 캐릭터 몸 전체 높이(발끝~머리끝): 스킨 메쉬들의 기본 자세 범위를 모델 맨 위(애니메이터) 기준으로 잰다.
        /// 손에 든 무기 같은 일반 메쉬는 빼고 잰다.
        /// </summary>
        private static float MeasureBodyHeight(GameObject asset)
        {
            if (asset == null) return 0f;
            Animator animator = asset.GetComponentInChildren<Animator>(true);
            Transform root = animator != null ? animator.transform : asset.transform;
            float min = float.MaxValue, max = float.MinValue;
            foreach (SkinnedMeshRenderer skin in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                Mesh mesh = skin.sharedMesh;
                // 바인드포즈 기준 정점 위치 = (뼈 기준) → 메쉬 노드 공간. 메쉬 bounds는 메쉬 노드 공간이다.
                Bounds b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float y = root.InverseTransformPoint(skin.transform.TransformPoint(c)).y;
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
            }
            return max > min ? max - min : 0f;
        }

        private static float ModelRootScale(GameObject asset)
        {
            if (asset == null) return 1f;
            Animator animator = asset.GetComponentInChildren<Animator>(true);
            return (animator != null ? animator.transform : asset.transform).lossyScale.y;
        }

        private static Transform FindHumanBone(Transform root, Avatar avatar, string humanName)
        {
            if (avatar == null || !avatar.isHuman) return null;
            foreach (HumanBone hb in avatar.humanDescription.human)
                if (hb.humanName == humanName) return FindDeep(root, hb.boneName);
            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>프리팹 하나의 모델(스킨 메쉬 + 뼈대)을 FBX로 바꾼다.</summary>
        private static string Swap(string path, GameObject fbx, Avatar avatar, Material material)
        {
            string label = System.IO.Path.GetFileNameWithoutExtension(path);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SkinnedMeshRenderer[] oldSkins = contents.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (oldSkins.Length == 0) return $"- {label}: 스킨 메쉬가 없어 건너뜀";

                Animator animator = oldSkins[0].GetComponentInParent<Animator>(true);
                Transform modelRoot = animator != null ? animator.transform : oldSkins[0].transform.parent;
                if (modelRoot == null) return $"- {label}: 모델 위치를 찾지 못했습니다.";

                // 예전 오른손 무기 자리 (위치/방향을 새 손에 옮겨 준다)
                Avatar oldAvatar = animator != null ? animator.avatar : null;
                Transform oldHand = FindHumanBone(modelRoot, oldAvatar, "RightHand");
                Transform oldSlot = oldHand != null ? oldHand.Find("handslot.r") : FindDeep(modelRoot, "handslot.r");
                Quaternion slotRotInRoot = Quaternion.identity;
                Vector3 slotOffsetInRoot = Vector3.zero;
                bool hasOldSlot = oldSlot != null && oldHand != null;
                if (hasOldSlot)
                {
                    slotRotInRoot = Quaternion.Inverse(modelRoot.rotation) * oldSlot.rotation;
                    slotOffsetInRoot = modelRoot.InverseTransformVector(oldSlot.position - oldHand.position);
                }

                // 지울 것: 스킨 메쉬 오브젝트 + 뼈대의 최상위
                var remove = new HashSet<Transform>();
                foreach (SkinnedMeshRenderer skin in oldSkins)
                {
                    if (!skin.transform.IsChildOf(modelRoot)) continue;
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

                // 새 모델: FBX의 자식들을 모델 위치로 옮긴다
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
                        foreach (Collider c in x.GetComponents<Collider>()) Object.DestroyImmediate(c);
                        foreach (Rigidbody r in x.GetComponents<Rigidbody>()) Object.DestroyImmediate(r);
                        Animator extra = x.GetComponent<Animator>();
                        if (extra != null && extra != animator) Object.DestroyImmediate(extra);
                    }
                    foreach (SkinnedMeshRenderer skin in t.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        skinCount++;
                        if (material != null)
                            skin.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, skin.sharedMaterials.Length)).ToArray();
                        skin.updateWhenOffscreen = false;
                    }
                }

                if (animator != null)
                {
                    if (avatar != null) animator.avatar = avatar;
                    animator.applyRootMotion = false;
                }

                // 무기 자리: 오른손 뼈 아래 handslot.r (+ 왼손 handslot.l)
                Transform newHand = FindHumanBone(modelRoot, avatar, "RightHand") ?? FindDeep(modelRoot, "mixamorig:RightHand");
                if (newHand != null && newHand.Find("handslot.r") == null)
                {
                    var slot = new GameObject("handslot.r").transform;
                    slot.SetParent(newHand, false);
                    slot.gameObject.layer = layer;
                    if (hasOldSlot)
                    {
                        slot.position = newHand.position + modelRoot.TransformVector(slotOffsetInRoot);
                        slot.rotation = modelRoot.rotation * slotRotInRoot;
                    }
                }
                Transform newLeft = FindHumanBone(modelRoot, avatar, "LeftHand") ?? FindDeep(modelRoot, "mixamorig:LeftHand");
                if (newLeft != null && newLeft.Find("handslot.l") == null)
                {
                    var slotL = new GameObject("handslot.l").transform;
                    slotL.SetParent(newLeft, false);
                    slotL.gameObject.layer = layer;
                }

                if (animator != null) animator.Rebind();
                // Rebind는 뼈 위치를 아바타 기본값으로 되돌리므로 반드시 그 뒤에 뼈대를 편다
                FixArmatureScale(modelRoot, avatar, label);

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return $"- {label}: 모델을 {fbx.name}로 교체 (몸 조각 {skinCount}개, Avatar={(avatar != null ? avatar.name : "없음")})";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private const string BakedDir = "Assets/LastTruck/Character/Baked";

        /// <summary>
        /// 블렌더에서 내보낸 FBX는 뼈대 부모(Armature)의 크기가 100이다. Humanoid 애니메이션은 골반(Hips) 위치를
        /// 그 크기를 무시하고 넣기 때문에 몸이 50m 위로 날아가거나 늘어나 보인다.
        /// → 모든 뼈의 크기를 1로 펴고(보이는 위치/방향은 그대로), 메쉬의 바인드포즈와 Humanoid 아바타를 새 뼈대에 맞게 다시 만든다.
        /// 고칠 게 없으면(이미 모든 뼈 크기가 1) false.
        /// </summary>
        private static bool FixArmatureScale(Transform modelRoot, Avatar sourceAvatar, string saveName)
        {
            SkinnedMeshRenderer[] skins = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0) return false;

            // 뼈대 = 스킨이 쓰는 뼈 + 그 부모들(모델 루트 아래)
            var boneSet = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer skin in skins)
                foreach (Transform b in skin.bones)
                    for (Transform x = b; x != null && x != modelRoot; x = x.parent) boneSet.Add(x);
            bool needs = boneSet.Any(b => (b.localScale - Vector3.one).sqrMagnitude > 1e-4f);
            if (!needs) return false;

            // 위에서부터 순서대로 (부모 먼저)
            List<Transform> ordered = boneSet.OrderBy(Depth).ToList();
            var oldL2W = new Dictionary<Transform, Matrix4x4>();
            var worldPos = new Dictionary<Transform, Vector3>();
            var worldRot = new Dictionary<Transform, Quaternion>();
            foreach (Transform b in ordered) { oldL2W[b] = b.localToWorldMatrix; worldPos[b] = b.position; worldRot[b] = b.rotation; }

            // 크기를 1로 펴면서 실제 위치/방향은 유지
            foreach (Transform b in ordered)
            {
                b.localScale = Vector3.one;
                b.position = worldPos[b];
                b.rotation = worldRot[b];
            }

            if (!AssetDatabase.IsValidFolder(BakedDir)) AssetDatabase.CreateFolder("Assets/LastTruck/Character", "Baked");

            // 메쉬 바인드포즈 다시 계산: newBind = newW2L * oldL2W * oldBind (지금 자세와 상관없이 같은 모양)
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Mesh src = skin.sharedMesh;
                if (src == null) continue;
                Transform[] bones = skin.bones;
                Matrix4x4[] binds = src.bindposes;
                var newBinds = new Matrix4x4[binds.Length];
                for (int i = 0; i < binds.Length; i++)
                {
                    Transform b = i < bones.Length ? bones[i] : null;
                    newBinds[i] = (b != null && oldL2W.ContainsKey(b)) ? b.worldToLocalMatrix * oldL2W[b] * binds[i] : binds[i];
                }
                Mesh mesh = Object.Instantiate(src);
                mesh.name = saveName + "_" + src.name;
                mesh.bindposes = newBinds;
                string meshPath = $"{BakedDir}/{saveName}_Mesh.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);

                // 화면 밖 판정용 범위도 루트 뼈 크기 변화만큼 키운다
                Transform root = skin.rootBone;
                if (root != null && oldL2W.ContainsKey(root))
                {
                    float k = oldL2W[root].lossyScale.x / Mathf.Max(1e-6f, root.lossyScale.x);
                    Bounds bb = skin.localBounds;
                    skin.localBounds = new Bounds(bb.center * k, bb.size * k);
                }
                skin.sharedMesh = mesh;
            }

            // Humanoid 아바타를 새 뼈대로 다시 만든다 (뼈 이름 연결은 원래 아바타 그대로)
            Animator animator = modelRoot.GetComponent<Animator>();
            if (sourceAvatar != null && sourceAvatar.isHuman && animator != null)
            {
                HumanDescription hd = sourceAvatar.humanDescription;
                var skeleton = new List<SkeletonBone>();
                foreach (Transform x in modelRoot.GetComponentsInChildren<Transform>(true))
                {
                    skeleton.Add(new SkeletonBone
                    {
                        name = x.name,
                        position = x.localPosition,
                        rotation = x.localRotation,
                        scale = x.localScale,
                    });
                }
                hd.skeleton = skeleton.ToArray();
                Avatar avatar = AvatarBuilder.BuildHumanAvatar(modelRoot.gameObject, hd);
                if (avatar != null && avatar.isValid)
                {
                    avatar.name = saveName + "_Avatar";
                    string avatarPath = $"{BakedDir}/{saveName}_Avatar.asset";
                    AssetDatabase.DeleteAsset(avatarPath);
                    AssetDatabase.CreateAsset(avatar, avatarPath);
                    animator.avatar = avatar;
                }
                else
                {
                    Debug.LogWarning($"[CharacterTripoModelTool] {saveName}: 새 아바타를 만들지 못했습니다.");
                }
            }
            return true;
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            for (; t != null; t = t.parent) d++;
            return d;
        }

        /// <summary>원래 FBX의 아바타 (뼈 이름 연결용). 지금 붙은 아바타가 우리가 만든 것이어도 그대로 쓸 수 있다.</summary>
        private static Avatar SourceAvatarFor(Animator animator)
        {
            return animator != null ? animator.avatar : null;
        }

        private static readonly string[] FixTargets =
        {
            "Assets/LastTruck/Character/Prefabs/Explorer.prefab",
            "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Explorer.prefab",
            "Assets/LastTruck/Character/Prefabs/Army.prefab",
            "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Army.prefab",
            "Assets/LastTruck/Character/Prefabs/Chef.prefab",
            "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Chef.prefab",
        };

        /// <summary>이미 교체된 캐릭터 프리팹들의 Armature 크기 문제를 고친다 (고칠 게 없으면 아무것도 안 함).</summary>
        [MenuItem("LastTruck/Character/캐릭터 Armature 크기 고치기 (몸이 안 보일 때)", priority = 203)]
        public static void FixAllPrefabs()
        {
            var log = new System.Text.StringBuilder();
            foreach (string path in FixTargets)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Animator animator = contents.GetComponentInChildren<Animator>(true);
                    if (animator == null) continue;
                    if (FixArmatureScale(animator.transform, SourceAvatarFor(animator), System.IO.Path.GetFileNameWithoutExtension(path)))
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        log.AppendLine("- " + System.IO.Path.GetFileNameWithoutExtension(path));
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.Length > 0 ? "[CharacterTripoModelTool] Armature 크기 수정:\n" + log : "[CharacterTripoModelTool] Armature 크기: 고칠 프리팹 없음");
        }

        /// <summary>
        /// 뼈 위치를 메쉬 바인드포즈에서 다시 계산해 되돌린다 (몸은 안 보이고 머리만 보일 때).
        /// 바인드포즈는 "기본 자세에서 뼈가 있던 곳"이므로, 뼈가 쪼그라들어 있어도 원래 자리를 알 수 있다.
        /// </summary>
        private static bool RestoreBonesFromBindposes(Transform modelRoot)
        {
            SkinnedMeshRenderer skin = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.sharedMesh != null).OrderByDescending(s => s.bones.Length).FirstOrDefault();
            if (skin == null) return false;
            Transform[] bones = skin.bones;
            Matrix4x4[] binds = skin.sharedMesh.bindposes;
            Matrix4x4 meshL2W = skin.transform.localToWorldMatrix;

            var target = new Dictionary<Transform, Matrix4x4>();
            for (int i = 0; i < bones.Length && i < binds.Length; i++)
                if (bones[i] != null) target[bones[i]] = meshL2W * binds[i].inverse;

            bool changed = false;
            foreach (Transform b in target.Keys.OrderBy(Depth))
            {
                Matrix4x4 m = target[b];
                Vector3 pos = m.GetColumn(3);
                if ((b.position - pos).sqrMagnitude > 1e-8f) changed = true;
                b.position = pos;
                b.rotation = m.rotation;
            }
            return changed;
        }

        [MenuItem("LastTruck/Character/캐릭터 뼈대 복구 (머리만 보이고 몸이 안 보일 때)", priority = 204)]
        public static void RestoreAllPrefabs()
        {
            var log = new System.Text.StringBuilder();
            foreach (string path in FixTargets)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Animator animator = contents.GetComponentInChildren<Animator>(true);
                    if (animator == null) continue;
                    if (RestoreBonesFromBindposes(animator.transform))
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        log.AppendLine("- " + System.IO.Path.GetFileNameWithoutExtension(path));
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.Length > 0 ? "[CharacterTripoModelTool] 뼈대 복구:\n" + log : "[CharacterTripoModelTool] 뼈대 복구: 고칠 프리팹 없음");
        }

        // ───────────────────── 발 높이 맞추기 ─────────────────────
        // 캐릭터의 가장 낮은 곳(발바닥)과 충돌체 바닥 사이 간격을 BlackSmith와 같게 맞춘다.
        // Idle 애니메이션 자세로 재고, 다르면 뼈대 부모(Armature)를 위/아래로 옮긴다 (Humanoid는 Armature 위치를 건드리지 않는다).

        private static readonly (string target, string reference)[] FootPairs =
        {
            ("Assets/LastTruck/Character/Prefabs/Explorer.prefab", "Assets/LastTruck/Character/Prefabs/BlackSmith.prefab"),
            ("Assets/LastTruck/Character/Prefabs/Army.prefab", "Assets/LastTruck/Character/Prefabs/BlackSmith.prefab"),
            ("Assets/LastTruck/Character/Prefabs/Chef.prefab", "Assets/LastTruck/Character/Prefabs/BlackSmith.prefab"),
            ("Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Explorer.prefab", "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_BlackSmith.prefab"),
            ("Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Army.prefab", "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_BlackSmith.prefab"),
            ("Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Chef.prefab", "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_BlackSmith.prefab"),
        };

        /// <summary>충돌체 바닥(월드 y). CharacterController 또는 CapsuleCollider 기준.</summary>
        private static bool ColliderBottom(GameObject root, out float bottom)
        {
            bottom = 0f;
            var cc = root.GetComponent<CharacterController>();
            if (cc != null)
            {
                bottom = root.transform.TransformPoint(cc.center).y - cc.height * 0.5f * root.transform.lossyScale.y;
                return true;
            }
            var cap = root.GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                bottom = root.transform.TransformPoint(cap.center).y - cap.height * 0.5f * root.transform.lossyScale.y;
                return true;
            }
            return false;
        }

        /// <summary>Idle 자세에서 몸 메쉬의 가장 낮은 점(월드 y). posed = 애니메이션 자세로 잰 경우 true.</summary>
        private static float LowestPoint(GameObject root, out bool posed)
        {
            posed = false;
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                for (int i = 0; i < 10; i++) animator.Update(0.1f);
                posed = true;
            }
            float min = float.MaxValue;
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null || !skin.gameObject.activeInHierarchy) continue;
                skin.BakeMesh(baked, true);
                Matrix4x4 m = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                foreach (Vector3 v in baked.vertices) min = Mathf.Min(min, m.MultiplyPoint3x4(v).y);
            }
            Object.DestroyImmediate(baked);
            return min == float.MaxValue ? 0f : min;
        }

        private static bool FootGap(string path, out float gap, out bool posed)
        {
            gap = 0f; posed = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return false;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!ColliderBottom(contents, out float bottom)) return false;
                gap = LowestPoint(contents, out posed) - bottom;
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [MenuItem("LastTruck/Character/새 캐릭터 발 높이 맞추기 (BlackSmith 기준)", priority = 205)]
        private static void FixFootHeightsMenu()
        {
            string log = FixFootHeights();
            EditorUtility.DisplayDialog("발 높이 맞추기", log, "확인");
        }

        public static string FixFootHeights()
        {
            var log = new System.Text.StringBuilder();
            foreach (var (target, reference) in FootPairs)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(target);
                if (!FootGap(reference, out float refGap, out bool refPosed) || !FootGap(target, out float gap, out bool posed))
                {
                    log.AppendLine($"- {name}: 잴 수 없음 (건너뜀)");
                    continue;
                }
                float diff = gap - refGap; // + 이면 기준보다 떠 있음
                log.AppendLine($"- {name}: 발바닥-바닥 간격 {gap:0.000} (BlackSmith {refGap:0.000}, 차이 {diff:+0.000;-0.000}{(posed && refPosed ? "" : ", 기본 자세로 잼")})");
                if (Mathf.Abs(diff) < 0.01f) continue;

                GameObject contents = PrefabUtility.LoadPrefabContents(target);
                try
                {
                    Animator animator = contents.GetComponentInChildren<Animator>(true);
                    Transform hips = animator != null ? (FindHumanBone(animator.transform, animator.avatar, "Hips") ?? FindDeep(animator.transform, "mixamorig:Hips")) : null;
                    Transform armature = hips != null ? hips.parent : null;
                    if (armature == null || armature == animator.transform)
                    {
                        log.AppendLine("    뼈대 부모(Armature)가 없어 옮기지 못함");
                        continue;
                    }
                    // 뼈대 부모와 스킨 메쉬 오브젝트를 같이 옮긴다 (둘의 상대 위치가 그대로라 바인드포즈와도 맞는다)
                    var move = new HashSet<Transform> { armature };
                    foreach (SkinnedMeshRenderer skin in contents.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (!skin.transform.IsChildOf(armature)) move.Add(skin.transform);
                    foreach (Transform tr in move) tr.position -= Vector3.up * diff;
                    PrefabUtility.SaveAsPrefabAsset(contents, target);
                    log.AppendLine($"    → {(diff > 0 ? "내림" : "올림")} {Mathf.Abs(diff):0.000}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterTripoModelTool] 발 높이 맞추기\n" + log);
            return log.ToString();
        }

        private static Transform TopChildUnder(Transform parent, Transform t)
        {
            if (t == null || t == parent || !t.IsChildOf(parent)) return null;
            while (t.parent != null && t.parent != parent) t = t.parent;
            return t.parent == parent ? t : null;
        }
    }
}
