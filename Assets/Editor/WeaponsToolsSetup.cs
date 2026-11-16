#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 무기 5종 + 도구 5종을 만든다 (아이템 + 트럭 조립 레시피 + 내구도 + 카탈로그 등록 + 손에 드는 모델 + 아이콘).
/// 다시 실행해도 중복으로 만들지 않는다 (이미 있으면 건너뛰고, 아이콘은 파일이 없을 때만 그린다).
///  무기: 구리 단검 / 전투 도끼 / 산탄총 / 석궁 / 화염병
///  도구: 돌도끼 / 철도끼 / 채집용 호미 / 쌍안경 / 신호탄
/// </summary>
public static class WeaponsToolsSetup
{
    const string ItemDir = "Assets/ScriptableObjects/Items/";
    const string RecipeDir = "Assets/ScriptableObjects/Recipes/";
    const string IconDir = "Assets/Resources/Crafting/Icons/";
    const string CatalogPath = "Assets/Resources/Crafting/ItemCatalog.asset";
    const string KayKit = "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/";
    const string Kenney = "Assets/Free_Assets/Models/FBX format/";

    struct Def
    {
        public string id, name; public ItemType type; public bool weapon; public int stack, durability;
        public (string id, int count)[] inputs; public int outCount;
    }

    static readonly Def[] Defs =
    {
        // 무기
        new Def { id = ItemIds.CopperDagger, name = "구리 단검", type = ItemType.Finished, weapon = true, stack = 1, durability = 70,
                  inputs = new[] { (ItemIds.CopperBlade, 1), (ItemIds.Wood, 4) }, outCount = 1 },
        new Def { id = ItemIds.BattleAxe, name = "전투 도끼", type = ItemType.Finished, weapon = true, stack = 1, durability = 70,
                  inputs = new[] { (ItemIds.ReinforcedIronBlade, 2), (ItemIds.WoodHandle, 1), (ItemIds.Iron, 2) }, outCount = 1 },
        new Def { id = ItemIds.Shotgun, name = "산탄총", type = ItemType.Finished, weapon = true, stack = 1, durability = 60,
                  inputs = new[] { (ItemIds.PrecisionBarrel, 2), (ItemIds.MechanicalTrigger, 1), (ItemIds.WoodHandle, 1) }, outCount = 1 },
        new Def { id = ItemIds.Crossbow, name = "석궁", type = ItemType.Finished, weapon = true, stack = 1, durability = 70,
                  inputs = new[] { (ItemIds.WoodHandle, 2), (ItemIds.MechanicalTrigger, 1), (ItemIds.TannedLeather, 1) }, outCount = 1 },
        new Def { id = ItemIds.Molotov, name = "화염병", type = ItemType.Finished, weapon = true, stack = 10,
                  inputs = new[] { (ItemIds.Oil, 1), (ItemIds.Charcoal, 1) }, outCount = 2 },
        // 도구
        new Def { id = ItemIds.StoneAxe, name = "돌도끼", type = ItemType.Tool, stack = 1, durability = 40,
                  inputs = new[] { (ItemIds.Wood, 8), (ItemIds.Stone, 6) }, outCount = 1 },
        new Def { id = ItemIds.IronAxe, name = "철도끼", type = ItemType.Tool, stack = 1, durability = 90,
                  inputs = new[] { (ItemIds.ReinforcedIronBlade, 1), (ItemIds.WoodHandle, 1) }, outCount = 1 },
        new Def { id = ItemIds.HerbHoe, name = "채집용 호미", type = ItemType.Tool, stack = 1, durability = 50,
                  inputs = new[] { (ItemIds.Wood, 6), (ItemIds.Stone, 3) }, outCount = 1 },
        new Def { id = ItemIds.Binoculars, name = "쌍안경", type = ItemType.Tool, stack = 1,
                  inputs = new[] { (ItemIds.Copper, 2), (ItemIds.Iron, 1) }, outCount = 1 },
        new Def { id = ItemIds.SignalFlare, name = "신호탄", type = ItemType.Finished, stack = 10,
                  inputs = new[] { (ItemIds.PropellantPowder, 1), (ItemIds.Charcoal, 1) }, outCount = 2 },
    };

    struct Held
    {
        public string id, path; public Vector3 pos, euler; public float scale; public bool gun, plusZ; public int casing;
    }

    // 손에 드는 모델 (WeaponController.heldMelee). KayKit 근접 무기는 기존 마체테(sword_1handed)와 같은 기준, Kenney 도구는 곡괭이와 같은 기준.
    static readonly Held[] HeldModels =
    {
        new Held { id = ItemIds.CopperDagger, path = KayKit + "dagger.prefab", scale = 1f },
        new Held { id = ItemIds.BattleAxe, path = KayKit + "axe_2handed.prefab", scale = 0.95f },
        new Held { id = ItemIds.StoneAxe, path = Kenney + "tool-axe.fbx", scale = 3.6f },
        new Held { id = ItemIds.IronAxe, path = Kenney + "tool-axe-upgraded.fbx", scale = 3.6f },
        new Held { id = ItemIds.HerbHoe, path = Kenney + "tool-hoe.fbx", scale = 3.6f },
        new Held { id = ItemIds.Shotgun, path = KayKit + "shotgun.prefab", pos = new Vector3(0f, 0.03f, 0.05f), scale = 0.55f, gun = true, plusZ = true, casing = 2 },
        new Held { id = ItemIds.Crossbow, path = KayKit + "crossbow_2handed.prefab", pos = new Vector3(0f, 0.03f, 0.05f), scale = 0.55f, gun = true, plusZ = true },
    };

    static readonly string[] CharacterPrefabs =
    {
        "Assets/LastTruck/Character/Prefabs/Army.prefab",
        "Assets/LastTruck/Character/Prefabs/BlackSmith.prefab",
        "Assets/LastTruck/Character/Prefabs/BlackSmith_2.prefab",
        "Assets/LastTruck/Character/Prefabs/Explorer.prefab",
        "Assets/LastTruck/Character/Prefabs/Miner.prefab",
        "Assets/LastTruck/Character/Prefabs/Police.prefab",
        "Assets/LastTruck/Multiplayer/Prefabs/Characters/NetworkPlayer_Base.prefab",
    };

    static ItemData Item(string id) => AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + id + ".asset");

    public static string Run()
    {
        var sb = new System.Text.StringBuilder();
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        var so = catalog != null ? new SerializedObject(catalog) : null;
        if (catalog == null) sb.AppendLine("!! 카탈로그 없음: " + CatalogPath);

        // 1) 아이콘 (없을 때만 그린다)
        foreach (var def in Defs)
        {
            string path = IconDir + def.id + ".png";
            if (System.IO.File.Exists(path)) continue;
            try { RenderIcon(def.id, path); sb.AppendLine("icon + " + def.id); }
            catch (Exception e) { sb.AppendLine("!! 아이콘 실패 " + def.id + ": " + e.Message); }
        }
        AssetDatabase.Refresh();

        // 2) 아이템
        foreach (var def in Defs)
        {
            var item = Item(def.id);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                item.itemID = def.id;
                item.itemName = def.name;
                item.itemType = def.type;
                item.isWeapon = def.weapon;
                item.maxStack = def.stack;
                item.toolTier = 0;
                AssetDatabase.CreateAsset(item, ItemDir + def.id + ".asset");
                sb.AppendLine("item + " + def.id);
            }
            if (item.maxDurability != def.durability) { item.maxDurability = def.durability; EditorUtility.SetDirty(item); }
            if (item.icon == null)
            {
                item.icon = IconSprite(def.id);
                EditorUtility.SetDirty(item);
                if (item.icon == null) sb.AppendLine("  (아이콘 없음) " + def.id);
            }
            if (so != null) AddUnique(so.FindProperty("items"), item, sb);
        }

        // 3) 레시피 (트럭 조립)
        foreach (var def in Defs)
        {
            string rid = "assemble_" + def.id;
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeDir + rid + ".asset");
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeData>();
                recipe.recipeID = rid;
                recipe.displayName = def.name + " 조립";
                recipe.requiredFacility = FacilityType.None;
                foreach (var (iid, count) in def.inputs)
                {
                    var input = Item(iid);
                    if (input == null) { sb.AppendLine("!! 재료 없음: " + iid + " (" + rid + ")"); continue; }
                    recipe.inputs.Add(new RecipeIngredient(input, count));
                }
                recipe.output = new RecipeIngredient(Item(def.id), def.outCount);
                recipe.processingSeconds = 2f;
                AssetDatabase.CreateAsset(recipe, RecipeDir + rid + ".asset");
                sb.AppendLine("recipe + " + rid);
            }
            if (so != null) AddUnique(so.FindProperty("recipes"), recipe, sb);
        }
        if (so != null) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog); }
        AssetDatabase.SaveAssets();

        // 4) 손에 드는 모델 (모든 캐릭터 프리팹)
        foreach (string prefabPath in CharacterPrefabs)
            AddHeldModels(prefabPath, sb);

        AssetDatabase.SaveAssets();
        return sb.Length == 0 ? "already set up" : sb.ToString();
    }

    static void AddHeldModels(string prefabPath, System.Text.StringBuilder sb)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) { sb.AppendLine("!! 프리팹 없음 " + prefabPath); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var wc = root.GetComponentInChildren<Combat.WeaponController>(true);
            if (wc == null) { sb.AppendLine("  (WeaponController 없음) " + prefabPath); return; }
            var so = new SerializedObject(wc);
            var list = so.FindProperty("heldMelee");
            int added = 0;
            foreach (var h in HeldModels)
            {
                bool exists = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("itemId").stringValue == h.id) { exists = true; break; }
                if (exists) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(h.path);
                if (model == null) { sb.AppendLine("!! 모델 없음 " + h.path); continue; }
                list.arraySize++;
                var e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("itemId").stringValue = h.id;
                e.FindPropertyRelative("prefab").objectReferenceValue = model;
                e.FindPropertyRelative("localPosition").vector3Value = h.pos;
                e.FindPropertyRelative("localEuler").vector3Value = h.euler;
                e.FindPropertyRelative("scale").floatValue = h.scale;
                e.FindPropertyRelative("isGun").boolValue = h.gun;
                e.FindPropertyRelative("casing").intValue = h.casing;
                e.FindPropertyRelative("facesPlusZ").boolValue = h.plusZ;
                added++;
            }
            if (added > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                sb.AppendLine("held +" + added + " " + System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ───────────────────────── 아이콘 ─────────────────────────

    const int IconLayer = 31;
    const int IconSize = 256;

    /// <summary>모델을 비스듬히 눕혀서 투명 배경에 그리고, 어두운 테두리를 둘러 PNG로 저장한다.</summary>
    static void RenderIcon(string id, string path)
    {
        var holder = new GameObject("IconSubject");
        holder.transform.position = new Vector3(0f, -5000f, 0f);
        GameObject subject = BuildIconSubject(id, out Quaternion rot);
        if (subject == null) { Object.DestroyImmediate(holder); throw new Exception("모델을 만들지 못함"); }
        subject.transform.SetParent(holder.transform, false);
        subject.transform.localRotation = rot;
        foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IconLayer;

        Bounds b = new Bounds(); bool any = false;
        foreach (var r in holder.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }

        var camGo = new GameObject("IconCam");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.cullingMask = 1 << IconLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        cam.transform.position = b.center - cam.transform.forward * 20f;
        float half = Mathf.Max(b.extents.x, b.extents.y) * 1.12f + 0.01f;
        cam.orthographicSize = half;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 60f;

        var lightGo = new GameObject("IconLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.cullingMask = 1 << IconLayer;
        lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

        var rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();

        AddOutline(tex, 3, new Color(0.12f, 0.1f, 0.08f, 1f));
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        Object.DestroyImmediate(holder);
    }

    static GameObject BuildIconSubject(string id, out Quaternion rot)
    {
        rot = Quaternion.identity;
        Quaternion diagonal = Quaternion.AngleAxis(-40f, Vector3.forward);                         // 세로로 긴 무기를 대각선으로
        Quaternion sideways = Quaternion.AngleAxis(25f, Vector3.forward) * Quaternion.Euler(0f, 90f, 0f);   // +Z로 긴 총을 옆모습으로
        switch (id)
        {
            case ItemIds.CopperDagger: rot = diagonal; return Inst(KayKit + "dagger.prefab");
            case ItemIds.BattleAxe: rot = Quaternion.AngleAxis(-30f, Vector3.forward); return Inst(KayKit + "axe_2handed.prefab");
            case ItemIds.Shotgun: rot = sideways; return Inst(KayKit + "shotgun.prefab");
            case ItemIds.Crossbow: rot = Quaternion.AngleAxis(-40f, Vector3.forward) * Quaternion.Euler(-90f, 0f, 0f);   // 위에서 본 모습 (활 날개가 보이게) return Inst(KayKit + "crossbow_2handed.prefab");
            case ItemIds.StoneAxe: rot = Quaternion.AngleAxis(-35f, Vector3.forward); return Inst(Kenney + "tool-axe.fbx");
            case ItemIds.IronAxe: rot = Quaternion.AngleAxis(-35f, Vector3.forward); return Inst(Kenney + "tool-axe-upgraded.fbx");
            case ItemIds.HerbHoe: rot = Quaternion.AngleAxis(-35f, Vector3.forward); return Inst(Kenney + "tool-hoe.fbx");
            case ItemIds.Molotov:
            {
                rot = Quaternion.AngleAxis(-20f, Vector3.forward);
                var root = new GameObject("Molotov");
                var bottle = Inst(Kenney + "bottle.fbx");
                if (bottle != null) { bottle.transform.SetParent(root.transform, false); }
                else Prim(root, PrimitiveType.Capsule, new Vector3(0f, 0.1f, 0f), new Vector3(0.1f, 0.12f, 0.1f), new Color(0.35f, 0.55f, 0.3f));
                float top = TopOf(root);
                Prim(root, PrimitiveType.Cylinder, new Vector3(0f, top + 0.02f, 0f), new Vector3(0.025f, 0.03f, 0.025f), new Color(0.85f, 0.8f, 0.7f));
                Prim(root, PrimitiveType.Sphere, new Vector3(0.01f, top + 0.07f, 0f), new Vector3(0.05f, 0.07f, 0.05f), new Color(1f, 0.55f, 0.1f));
                return root;
            }
            case ItemIds.SignalFlare:
            {
                rot = Quaternion.AngleAxis(-40f, Vector3.forward);
                var root = new GameObject("Flare");
                Prim(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.12f, 0.35f, 0.12f), new Color(0.85f, 0.15f, 0.12f));
                Prim(root, PrimitiveType.Cylinder, new Vector3(0f, 0.37f, 0f), new Vector3(0.125f, 0.04f, 0.125f), new Color(0.95f, 0.92f, 0.85f));
                Prim(root, PrimitiveType.Cylinder, new Vector3(0f, -0.37f, 0f), new Vector3(0.125f, 0.04f, 0.125f), new Color(0.2f, 0.2f, 0.2f));
                Prim(root, PrimitiveType.Sphere, new Vector3(0f, 0.46f, 0f), new Vector3(0.13f, 0.13f, 0.13f), new Color(1f, 0.8f, 0.5f));
                return root;
            }
            case ItemIds.Binoculars:
            {
                rot = Quaternion.Euler(-25f, 20f, 0f);
                var root = new GameObject("Binoculars");
                Color body = new Color(0.22f, 0.25f, 0.2f), metal = new Color(0.12f, 0.12f, 0.12f), lens = new Color(0.45f, 0.7f, 0.9f);
                foreach (float x in new[] { -0.16f, 0.16f })
                {
                    Prim(root, PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), new Vector3(0.22f, 0.2f, 0.22f), body, Quaternion.Euler(90f, 0f, 0f));
                    Prim(root, PrimitiveType.Cylinder, new Vector3(x, 0f, -0.22f), new Vector3(0.24f, 0.03f, 0.24f), metal, Quaternion.Euler(90f, 0f, 0f));
                    Prim(root, PrimitiveType.Cylinder, new Vector3(x, 0f, -0.25f), new Vector3(0.18f, 0.01f, 0.18f), lens, Quaternion.Euler(90f, 0f, 0f));
                    Prim(root, PrimitiveType.Cylinder, new Vector3(x, 0f, 0.24f), new Vector3(0.14f, 0.05f, 0.14f), metal, Quaternion.Euler(90f, 0f, 0f));
                }
                Prim(root, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.02f), new Vector3(0.14f, 0.07f, 0.22f), metal);
                return root;
            }
        }
        return null;
    }

    static GameObject Inst(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return asset != null ? (GameObject)Object.Instantiate(asset) : null;
    }

    static float TopOf(GameObject go)
    {
        float top = 0f;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y - go.transform.position.y);
        return top;
    }

    static void Prim(GameObject parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent.transform, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot ?? Quaternion.identity;
        g.transform.localScale = scale;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    /// <summary>투명한 곳 중 그림 가장자리에서 width픽셀 안쪽을 테두리 색으로 칠한다 (기존 손그림 아이콘처럼 윤곽이 또렷해진다).</summary>
    static void AddOutline(Texture2D tex, int width, Color outline)
    {
        int w = tex.width, h = tex.height;
        Color[] src = tex.GetPixels();
        Color[] dst = (Color[])src.Clone();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (src[i].a > 0.5f) { dst[i].a = 1f; continue; }
                bool near = false;
                for (int dy = -width; dy <= width && !near; dy++)
                    for (int dx = -width; dx <= width && !near; dx++)
                    {
                        if (dx * dx + dy * dy > width * width) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (src[ny * w + nx].a > 0.5f) near = true;
                    }
                if (near) dst[i] = outline;
            }
        tex.SetPixels(dst);
        tex.Apply();
    }

    static Sprite IconSprite(string id)
    {
        string path = IconDir + id + ".png";
        if (!System.IO.File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void AddUnique(SerializedProperty list, Object obj, System.Text.StringBuilder sb)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == obj) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
        sb.AppendLine("  catalog += " + obj.name);
    }

    [MenuItem("Tools/Last Truck/Items: Add Weapons & Tools (dagger, axe, shotgun, crossbow, molotov, binoculars...)")]
    static void Menu() { Debug.Log("[WeaponsToolsSetup]\n" + Run()); }
}
#endif
