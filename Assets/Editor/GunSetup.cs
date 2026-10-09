#if UNITY_EDITOR
using Combat;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 총/활 연결 일괄 설정:
///  - 활(Bow_Held/Arrow), 저격총 손 모델, 총알 프리팹이 비어 있는 캐릭터에 기존 프리팹을 연결
///  - Kenney 블래스터를 총별 임시 모델로 연결(heldMelee 목록에 isGun 항목 추가)
///  - 탄피 프리팹(권총탄/소총탄) 생성, 총구 불꽃 연결, 저격총용 Kenney 프리팹 생성
/// 메뉴: Tools/Last Truck/Setup Guns
/// </summary>
public static class GunSetup
{
    const string Kenney = "Assets/Free_Assets/kenney_blaster-kit_2.1/Models/FBX format/";
    const string WeaponsDir = "Assets/LastTruck/Weapons/";
    const string VfxDir = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/";

    [MenuItem("Tools/Last Truck/Setup Guns")]
    static void Menu() { Debug.Log(Setup()); }

    static GameObject Load(string path) { return AssetDatabase.LoadAssetAtPath<GameObject>(path); }

    // 탄피 프리팹: Ammo_fbx.fbx의 해당 메시를 복제해 Rigidbody/Collider/ShellCasing를 붙인다
    static GameObject MakeCasing(string meshChild, string prefabName, float targetLength)
    {
        string path = WeaponsDir + prefabName + ".prefab";
        var existing = Load(path);
        if (existing != null) return existing;

        var fbx = Load("Assets/Free_Assets/Ammo/Ammo_fbx.fbx");
        if (fbx == null) return null;
        Transform src = fbx.transform.Find(meshChild);
        if (src == null) return null;

        var root = new GameObject(prefabName);
        var visual = Object.Instantiate(src.gameObject, root.transform);
        visual.name = "Mesh";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // 크기를 실제 렌더러 크기로 재서 가장 긴 변이 targetLength가 되도록 맞춘다
        var r = visual.GetComponentInChildren<Renderer>();
        float longest = r != null ? Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) : 1f;
        float s = longest > 0.0001f ? targetLength / longest : 1f;
        visual.transform.localScale = visual.transform.localScale * s;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.02f; rb.linearDamping = 0.1f; rb.angularDamping = 0.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var col = root.AddComponent<CapsuleCollider>();
        col.direction = 2; col.radius = targetLength * 0.2f; col.height = targetLength;
        root.AddComponent<ShellCasing>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // 저격총용 Kenney 프리팹: 기존 시스템이 요구하는 LeftGrip/Muzzle 자식 포함(+Z가 총구, 피벗이 오른손 손잡이)
    static GameObject MakeSniper()
    {
        string path = WeaponsDir + "Sniper_Kenney.prefab";
        var existing = Load(path);
        if (existing != null) return existing;
        var model = Load(Kenney + "blaster-f.fbx");
        if (model == null) return null;

        var root = new GameObject("Sniper_Kenney");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        visual.name = "Model";
        visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        visual.transform.localScale = Vector3.one * 1.0f;
        visual.transform.localPosition = new Vector3(0f, 0.02f, 0.1f);
        var lg = new GameObject("LeftGrip"); lg.transform.SetParent(root.transform, false); lg.transform.localPosition = new Vector3(0f, 0.02f, 0.28f);
        var mz = new GameObject("Muzzle"); mz.transform.SetParent(root.transform, false); mz.transform.localPosition = new Vector3(0f, 0.04f, 0.65f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // 활: KayKit 3D 활(시위 포함). 기존 Bow_Held는 납작한 판이라 거의 안 보여서 대체한다.
    // 규칙(WeaponController): 피벗=손잡이, 로컬 +Y=활 위쪽, +Z=화살이 나가는 방향 → KayKit 활을 Y축 180도 돌려 시위가 -Z, 배가 +Z를 향하게 한다.
    static GameObject MakeBow()
    {
        string path = WeaponsDir + "Bow_KayKit.prefab";
        var model = Load("Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/bow_withString.prefab");
        if (model == null) return Load(path);

        // 실측: 원본 활은 위아래(활 길이)가 로컬 Z축, 시위가 x≈+0.34, 나무 손잡이(배)는 x≈0, 배가 -X 쪽으로 불룩.
        // 규칙(+Y=위, +Z=앞=배)에 맞게 Z→Y, -X→Z로 돌리고 손잡이가 피벗(손)에 오게 옮긴다.
        const float scale = 0.72f;   // 시위가 손잡이에서 약 0.24m 뒤(WeaponController.NockRestLocalPosition)에 오는 크기
        const float gripX = 0.0f;
        var rot = Quaternion.LookRotation(Vector3.up, Vector3.left);

        var root = new GameObject("Bow_KayKit");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        visual.name = "Model";
        visual.transform.localRotation = rot;
        visual.transform.localScale = Vector3.one * scale;   // 치비 몸에 맞게 조금 크게
        visual.transform.localPosition = -(rot * new Vector3(gripX * scale, 0f, 0f));

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // 총을 양손으로 잡는 IK가 동작하려면 베이스 레이어의 IK Pass가 켜져 있어야 한다
    static int EnableIkPass()
    {
        int n = 0;
        foreach (string g in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets/LastTruck/PolyOne/Chibi Character/Animation/Controler" }))
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(AssetDatabase.GUIDToAssetPath(g));
            if (ctrl == null || ctrl.layers.Length == 0) continue;
            var layers = ctrl.layers;
            if (!layers[0].iKPass) { layers[0].iKPass = true; ctrl.layers = layers; EditorUtility.SetDirty(ctrl); n++; }
        }
        return n;
    }

    static void SetProjectileHitVfx(GameObject prefab, GameObject vfx)
    {
        if (prefab == null) return;
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        var proj = root.GetComponent<ArrowProjectile>();
        if (proj != null)
        {
            var so = new SerializedObject(proj);
            so.FindProperty("hitVfxPrefab").objectReferenceValue = vfx;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    struct GunEntry { public string id, model; public float scale; public int casing; public Vector3 pos; }

    public static string Setup()
    {
        var sb = new StringBuilder();
        sb.AppendLine("IK Pass 켠 컨트롤러: " + EnableIkPass());
        var bow = MakeBow();
        if (bow == null) bow = Load(WeaponsDir + "Bow_Held.prefab");
        var arrow = Load(WeaponsDir + "Arrow.prefab");
        var bullet = Load(WeaponsDir + "Bullet.prefab");
        SetProjectileHitVfx(arrow, null);
        SetProjectileHitVfx(bullet, Load(VfxDir + "vfx_Impact_01.prefab"));
        var sniper = MakeSniper();
        var casingPistol = MakeCasing("Pistol_caliber_1", "Casing_Pistol", 0.16f);
        var casingRifle = MakeCasing("Rifle_caliber_1", "Casing_Rifle", 0.22f);
        var flash = Load(VfxDir + "vfx_MuzzleFlash_01.prefab");
        sb.AppendLine("프리팹: bow=" + (bow != null) + " arrow=" + (arrow != null) + " bullet=" + (bullet != null) + " sniper=" + (sniper != null) + " casingP=" + (casingPistol != null) + " casingR=" + (casingRifle != null) + " flash=" + (flash != null));

        var hand = new Vector3(0f, 0.03f, 0.1f);
        var guns = new[]
        {
            new GunEntry { id = CraftingSystem.ItemIds.Pistol, model = "blaster-b", scale = 1.0f, casing = 1, pos = hand },
            new GunEntry { id = CraftingSystem.ItemIds.Ak47, model = "blaster-d", scale = 1.0f, casing = 2, pos = hand },
            new GunEntry { id = CraftingSystem.ItemIds.ShredderDrillLauncher, model = "blaster-p", scale = 1.0f, casing = 0, pos = hand },
            new GunEntry { id = CraftingSystem.ItemIds.IronFieldCannon, model = "blaster-h", scale = 1.15f, casing = 0, pos = hand },
            new GunEntry { id = CraftingSystem.ItemIds.PlatinumRailCannon, model = "blaster-l", scale = 1.15f, casing = 0, pos = hand },
            new GunEntry { id = CraftingSystem.ItemIds.Flamethrower, model = "blaster-m", scale = 1.05f, casing = 0, pos = hand },
            new GunEntry { id = CraftingSystem.ItemIds.ChemicalSprayer, model = "blaster-q", scale = 1.05f, casing = 0, pos = hand },
        };

        int n = 0;
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LastTruck/Character/Prefabs" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(p);
            var wc = root.GetComponent<WeaponController>();
            if (wc != null && Apply(wc, bow, arrow, bullet, sniper, casingPistol, casingRifle, flash, guns)) { PrefabUtility.SaveAsPrefabAsset(root, p); n++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        foreach (var wc in Object.FindObjectsByType<WeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Apply(wc, bow, arrow, bullet, sniper, casingPistol, casingRifle, flash, guns)) n++;

        sb.AppendLine("WeaponController에 총/활/탄피 연결: " + n + "곳");
        return sb.ToString();
    }

    static bool Apply(WeaponController wc, GameObject bow, GameObject arrow, GameObject bullet, GameObject sniper,
        GameObject casingPistol, GameObject casingRifle, GameObject flash, GunEntry[] guns)
    {
        var so = new SerializedObject(wc);
        if (so.FindProperty("bowPrefab") == null) return false;

        so.FindProperty("bowPrefab").objectReferenceValue = bow;
        so.FindProperty("arrowPrefab").objectReferenceValue = arrow != null ? arrow.GetComponent<ArrowProjectile>() : null;
        so.FindProperty("bulletPrefab").objectReferenceValue = bullet != null ? bullet.GetComponent<ArrowProjectile>() : null;
        so.FindProperty("rifleHeldPrefab").objectReferenceValue = sniper != null ? sniper : AssetDatabase.LoadAssetAtPath<GameObject>(WeaponsDir + "SniperRifle_Placeholder.prefab");
        so.FindProperty("casingPistolPrefab").objectReferenceValue = casingPistol;
        so.FindProperty("casingRiflePrefab").objectReferenceValue = casingRifle;
        so.FindProperty("muzzleFlashVfxPrefab").objectReferenceValue = flash;
        so.FindProperty("bulletHitVfxPrefab").objectReferenceValue = Load(VfxDir + "vfx_Impact_01.prefab");
        so.FindProperty("explosionVfxPrefab").objectReferenceValue = Load(VfxDir + "vfx_Explosion_01.prefab");
        so.FindProperty("carryTiltAngle").floatValue = 10f;
        so.FindProperty("bulletHitVfxScale").floatValue = 1.0f;
        so.FindProperty("explosionVfxScale").floatValue = 0.9f;
        so.FindProperty("gunKickBack").floatValue = 0.14f;
        so.FindProperty("gunKickPitch").floatValue = 16f;

        // heldMelee 목록에서 기존 항목을 보존하고, 총 항목은 덮어쓴다
        var arr = so.FindProperty("heldMelee");
        var keep = new System.Collections.Generic.List<(string id, Object prefab, float scale, Vector3 pos, Vector3 euler, bool gun, int casing)>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            string id = e.FindPropertyRelative("itemId").stringValue;
            if (e.FindPropertyRelative("isGun").boolValue) continue;
            keep.Add((id, e.FindPropertyRelative("prefab").objectReferenceValue, e.FindPropertyRelative("scale").floatValue,
                e.FindPropertyRelative("localPosition").vector3Value, e.FindPropertyRelative("localEuler").vector3Value, false, 0));
        }
        foreach (var g in guns)
        {
            var m = AssetDatabase.LoadAssetAtPath<GameObject>(Kenney + g.model + ".fbx");
            keep.Add((g.id, m, g.scale, g.pos, Vector3.zero, true, g.casing));
        }
        arr.arraySize = keep.Count;
        for (int i = 0; i < keep.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("itemId").stringValue = keep[i].id;
            e.FindPropertyRelative("prefab").objectReferenceValue = keep[i].prefab;
            e.FindPropertyRelative("scale").floatValue = keep[i].scale;
            e.FindPropertyRelative("localPosition").vector3Value = keep[i].pos;
            e.FindPropertyRelative("localEuler").vector3Value = keep[i].euler;
            e.FindPropertyRelative("isGun").boolValue = keep[i].gun;
            e.FindPropertyRelative("casing").intValue = keep[i].casing;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wc);
        return true;
    }
}
#endif
