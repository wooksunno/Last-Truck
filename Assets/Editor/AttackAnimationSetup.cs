#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 캐릭터 애니메이터 컨트롤러에 '공격' 상체 레이어를 추가한다(걷기/달리기 등 기존 애니메이션은 그대로 유지).
/// 상체(몸통+머리+팔)만 덮어쓰는 아바타 마스크를 쓰고, KayKit Rig_Medium 휴머노이드 클립을 재사용한다.
///   AtkSlash  : 오른손 한 손 가로 휘두르기(방망이처럼)   AtkStab : 찌르기(창)   AtkPunch : 주먹
///   AtkShoot1H/2H : 사격 반동(한 손 / 두 손)           AtkThrow : 던지기
///   AtkShooting(bool) : 두 손으로 계속 뿜는 자세(화염방사기/살포기)
/// 메뉴: Tools/Last Truck/Setup Attack Animation (여러 번 실행해도 안전)
/// </summary>
public static class AttackAnimationSetup
{
    const string ClipRoot = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/";
    const string ControllerDir = "Assets/LastTruck/PolyOne/Chibi Character/Animation/Controler";
    const string MaskPath = "Assets/LastTruck/Animations/UpperBodyAttack.mask";
    public const string LayerName = "Attack";

    const string AccessoryDir = "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/";

    [MenuItem("Tools/Last Truck/Setup Attack Animation")]
    static void Menu() { Debug.Log(Setup() + "\n" + AssignHeldModels()); }

    [MenuItem("Tools/Last Truck/Setup Pickaxe (Chop Animation + Held Models)")]
    static void MenuPickaxe() { Debug.Log(Setup() + "\n" + AssignPickaxeModels()); }

    // 곡괭이: 손잡이가 원점, 머리가 +Y 쪽인 모델을 handslot.r에 붙인다 (KayKit 무기와 같은 방향).
    public static Vector3 PickaxeLocalPosition = Vector3.zero;
    public static Vector3 PickaxeLocalEuler = Vector3.zero;

    /// <summary>
    /// 곡괭이 4종을 손에 드는 모델 목록(heldMelee)에 추가/갱신한다. 다른 항목(근접 무기/총)은 건드리지 않는다.
    /// (AssignHeldModels는 목록을 근접 무기 4개로 덮어써 총 모델이 지워지므로 따로 둔다)
    /// </summary>
    public static string AssignPickaxeModels()
    {
        var table = new (string id, string path, float scale)[]
        {
            (CraftingSystem.ItemIds.WoodPickaxe, "Assets/Free_Assets/Models/FBX format/tool-pickaxe.fbx", 3.6f),
            (CraftingSystem.ItemIds.StonePickaxe, "Assets/Free_Assets/Models/FBX format/tool-pickaxe-upgraded.fbx", 3.6f),
            (CraftingSystem.ItemIds.CopperPickaxe, "Assets/PurePoly/Mining_Pack/Prefabs/Tools/PP_Pickaxe_New_01_Copper.prefab", 1.25f),
            (CraftingSystem.ItemIds.IronPickaxe, "Assets/PurePoly/Mining_Pack/Prefabs/Tools/PP_Pickaxe_New_01_Iron.prefab", 1.25f),
        };

        int n = 0;
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LastTruck/Character/Prefabs", "Assets/LastTruck/Multiplayer/Prefabs" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(p);
            var wc = root.GetComponent<Combat.WeaponController>();
            if (wc != null && UpsertHeld(wc, table)) { PrefabUtility.SaveAsPrefabAsset(root, p); n++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        foreach (var wc in Object.FindObjectsByType<Combat.WeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!PrefabUtility.IsPartOfPrefabInstance(wc) && UpsertHeld(wc, table)) n++;
        return "곡괭이 손 모델 연결: " + n + "곳";
    }

    static bool UpsertHeld(Combat.WeaponController wc, (string id, string path, float scale)[] table)
    {
        var so = new SerializedObject(wc);
        var arr = so.FindProperty("heldMelee");
        if (arr == null) return false;
        foreach (var row in table)
        {
            int index = -1;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("itemId").stringValue == row.id) { index = i; break; }
            if (index < 0) { index = arr.arraySize; arr.arraySize++; }

            var e = arr.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("itemId").stringValue = row.id;
            e.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(row.path);
            e.FindPropertyRelative("localPosition").vector3Value = PickaxeLocalPosition;
            e.FindPropertyRelative("localEuler").vector3Value = PickaxeLocalEuler;
            e.FindPropertyRelative("scale").floatValue = row.scale;
            e.FindPropertyRelative("isGun").boolValue = false;
            e.FindPropertyRelative("casing").intValue = 0;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wc);
        return true;
    }

    /// <summary>근접 무기를 들면 오른손에 붙는 모델을 WeaponController에 연결한다(KayKit 액세서리 재사용).</summary>
    public static string AssignHeldModels()
    {
        // itemId, 프리팹, 위치, 회전, 크기
        var table = new (string id, string prefab, float scale)[]
        {
            (ItemIdsLocal.Machete, "sword_1handed", 1.0f),
            (ItemIdsLocal.FlameMachete, "sword_1handed", 1.0f),
            (ItemIdsLocal.VibrationBlade, "sword_1handed", 1.15f),
            (ItemIdsLocal.WoodSpear, "staff", 1.0f),
        };

        int n = 0;
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LastTruck/Character/Prefabs" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(p);
            var wc = root.GetComponent<Combat.WeaponController>();
            if (wc != null && ApplyHeld(wc, table)) { PrefabUtility.SaveAsPrefabAsset(root, p); n++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        foreach (var wc in Object.FindObjectsByType<Combat.WeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ApplyHeld(wc, table)) n++;
        return "손에 드는 근접 무기 모델 연결: " + n + "곳";
    }

    static bool ApplyHeld(Combat.WeaponController wc, (string id, string prefab, float scale)[] table)
    {
        var so = new SerializedObject(wc);
        var arr = so.FindProperty("heldMelee");
        if (arr == null) return false;
        arr.arraySize = table.Length;
        for (int i = 0; i < table.Length; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("itemId").stringValue = table[i].id;
            e.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AccessoryDir + table[i].prefab + ".prefab");
            e.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            e.FindPropertyRelative("localEuler").vector3Value = Vector3.zero;
            e.FindPropertyRelative("scale").floatValue = table[i].scale;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wc);
        return true;
    }

    // CraftingSystem.ItemIds를 에디터 어셈블리에서 직접 참조해도 되지만, 문자열 상수를 한 곳에 모아 둔다
    static class ItemIdsLocal
    {
        public const string Machete = CraftingSystem.ItemIds.Machete;
        public const string FlameMachete = CraftingSystem.ItemIds.FlameMachete;
        public const string VibrationBlade = CraftingSystem.ItemIds.VibrationBlade;
        public const string WoodSpear = CraftingSystem.ItemIds.WoodSpear;
    }

    static AvatarMask EnsureMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask != null) return mask;

        if (!AssetDatabase.IsValidFolder("Assets/LastTruck/Animations"))
            AssetDatabase.CreateFolder("Assets/LastTruck", "Animations");

        mask = new AvatarMask();
        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            if (part == AvatarMaskBodyPart.LastBodyPart) continue;
            bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                   || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                   || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                   || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
            mask.SetHumanoidBodyPartActive(part, on);
        }
        AssetDatabase.CreateAsset(mask, MaskPath);
        return mask;
    }

    static AnimationClip Clip(string rel)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipRoot + rel + ".anim");
        if (c == null) Debug.LogWarning("[AttackAnimationSetup] 클립 없음: " + rel);
        return c;
    }

    static void EnsureParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in ctrl.parameters) if (p.name == name) return;
        ctrl.AddParameter(name, type);
    }

    public static string Setup()
    {
        var sb = new StringBuilder();
        AvatarMask mask = EnsureMask();
        int done = 0;

        foreach (string g in AssetDatabase.FindAssets("t:AnimatorController", new[] { ControllerDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) continue;

            EnsureParam(ctrl, "AtkSlash", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "AtkStab", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "AtkPunch", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "AtkShoot1H", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "AtkShoot2H", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "AtkThrow", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "AtkShooting", AnimatorControllerParameterType.Bool);
            EnsureParam(ctrl, "AtkChop", AnimatorControllerParameterType.Trigger);

            AnimatorControllerLayer existing = null;
            foreach (var l in ctrl.layers) if (l.name == LayerName) existing = l;
            if (existing != null)
            {
                // 나중에 추가된 동작(곡괭이 내려찍기)만 없으면 채워 넣는다
                bool added = EnsureChop(existing.stateMachine);
                if (added) EditorUtility.SetDirty(ctrl);
                sb.AppendLine(ctrl.name + ": 이미 있음" + (added ? " (Chop 상태 추가)" : "(파라미터만 확인)"));
                continue;
            }

            ctrl.AddLayer(LayerName);
            var layers = ctrl.layers;
            int idx = layers.Length - 1;
            layers[idx].avatarMask = mask;
            layers[idx].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[idx].defaultWeight = 1f;
            layers[idx].iKPass = false;
            ctrl.layers = layers;

            var sm = ctrl.layers[idx].stateMachine;

            // 비어 있는 기본 상태: 아무것도 덮어쓰지 않고 아래 레이어(걷기 등)를 그대로 보여준다. Write Defaults를 꺼야 통과된다.
            AnimatorState empty = sm.AddState("Empty", new Vector3(300, 0, 0));
            empty.writeDefaultValues = false;
            sm.defaultState = empty;

            AddAction(sm, empty, "Slash", "Combat Melee/Melee_1H_Attack_Slice_Horizontal", "AtkSlash", 1.9f, new Vector3(600, -150, 0));
            AddAction(sm, empty, "Stab", "Combat Melee/Melee_1H_Attack_Stab", "AtkStab", 1.7f, new Vector3(600, -80, 0));
            AddAction(sm, empty, "Punch", "Combat Melee/Melee_Unarmed_Attack_Punch_A", "AtkPunch", 1.8f, new Vector3(600, -10, 0));
            AddAction(sm, empty, "Shoot1H", "Combat Ranged/Ranged_1H_Shoot", "AtkShoot1H", 2.2f, new Vector3(600, 60, 0));
            AddAction(sm, empty, "Shoot2H", "Combat Ranged/Ranged_2H_Shoot", "AtkShoot2H", 2.2f, new Vector3(600, 130, 0));
            AddAction(sm, empty, "Throw", "General/Throw", "AtkThrow", 1.8f, new Vector3(600, 200, 0));
            EnsureChop(sm);

            // 연속 사격 자세(루프): AtkShooting이 켜진 동안 유지
            AnimatorState loop = sm.AddState("Shooting2H", new Vector3(600, 270, 0));
            loop.motion = Clip("Combat Ranged/Ranged_2H_Shooting");
            loop.writeDefaultValues = false;
            var enter = sm.AddAnyStateTransition(loop);
            enter.AddCondition(AnimatorConditionMode.If, 0, "AtkShooting");
            enter.hasExitTime = false; enter.duration = 0.1f; enter.canTransitionToSelf = false;
            var leave = loop.AddTransition(empty);
            leave.AddCondition(AnimatorConditionMode.IfNot, 0, "AtkShooting");
            leave.hasExitTime = false; leave.duration = 0.15f;

            EditorUtility.SetDirty(ctrl);
            done++;
            sb.AppendLine(ctrl.name + ": '" + LayerName + "' 레이어 추가");
        }

        AssetDatabase.SaveAssets();
        sb.Insert(0, "공격 애니메이션 레이어 적용 컨트롤러: " + done + "개\n");
        return sb.ToString();
    }

    /// <summary>
    /// 곡괭이 내려찍기 (AtkChop): 한 손 위에서 아래로 내려치는 모션. 1.6배속이면 0.4초쯤에 내려찍힌다
    /// (WeaponController.pickaxeImpactDelay와 맞춘 값). 이미 있으면 아무것도 안 한다.
    /// </summary>
    static bool EnsureChop(AnimatorStateMachine sm)
    {
        foreach (var s in sm.states) if (s.state.name == "Chop") return false;
        AnimatorState empty = null;
        foreach (var s in sm.states) if (s.state.name == "Empty") empty = s.state;
        if (empty == null) return false;
        AddAction(sm, empty, "Chop", "Combat Melee/Melee_1H_Attack_Chop", "AtkChop", 1.6f, new Vector3(600, 340, 0));
        return true;
    }

    static void AddAction(AnimatorStateMachine sm, AnimatorState empty, string stateName, string clipRel, string trigger, float speed, Vector3 pos)
    {
        AnimatorState st = sm.AddState(stateName, pos);
        st.motion = Clip(clipRel);
        st.speed = speed;
        st.writeDefaultValues = false;

        var t = sm.AddAnyStateTransition(st);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.canTransitionToSelf = true;     // 연타하면 처음부터 다시 휘두른다

        var back = st.AddTransition(empty);
        back.hasExitTime = true;
        back.exitTime = 0.85f;
        back.duration = 0.12f;
    }
}
#endif
