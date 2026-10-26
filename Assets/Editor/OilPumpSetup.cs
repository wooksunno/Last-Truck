#if UNITY_EDITOR
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 석유 펌프잭 시스템 설치:
///  1) 아이템 "석유 펌프잭"(oil_pumpjack)과 트럭 조립 레시피를 만들고 카탈로그에 등록한다.
///     레시피: 구리 수동 펌프 1 + 순수 철 6 + 순수 구리 4  (손 펌프를 먼저 만들어 쓰다가 자동화로 넘어가는 흐름)
///  2) 씬의 사막 기름 웅덩이(기름 SpecialResourceNode)마다 유정(OilWellSite)을 놓는다.
/// 다시 실행해도 중복으로 만들지 않는다.
/// </summary>
public static class OilPumpSetup
{
    const string ItemPath = "Assets/ScriptableObjects/Items/oil_pumpjack.asset";
    const string RecipePath = "Assets/ScriptableObjects/Recipes/assemble_oil_pumpjack.asset";
    const string CatalogPath = "Assets/Resources/Crafting/ItemCatalog.asset";

    static ItemData LoadItem(string id) => AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/ScriptableObjects/Items/{id}.asset");

    public static string Run()
    {
        var sb = new System.Text.StringBuilder();

        // 1) 아이템
        var pump = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
        if (pump == null)
        {
            var handPump = LoadItem(ItemIds.CopperHandPump);
            pump = ScriptableObject.CreateInstance<ItemData>();
            pump.itemID = ItemIds.OilPumpjack;
            pump.itemName = "석유 펌프잭";
            pump.itemType = ItemType.Tool;
            pump.maxStack = 3;
            pump.icon = handPump != null ? handPump.icon : null;   // 전용 아이콘이 생기면 교체
            pump.toolTier = 0;
            AssetDatabase.CreateAsset(pump, ItemPath);
            sb.AppendLine("created item " + ItemPath);
        }

        // 2) 레시피 (트럭 조립)
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipePath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.recipeID = "assemble_oil_pumpjack";
            recipe.displayName = "석유 펌프잭 조립";
            recipe.requiredFacility = FacilityType.None;
            recipe.inputs.Add(new RecipeIngredient(LoadItem(ItemIds.CopperHandPump), 1));
            recipe.inputs.Add(new RecipeIngredient(LoadItem(ItemIds.Iron), 6));
            recipe.inputs.Add(new RecipeIngredient(LoadItem(ItemIds.Copper), 4));
            recipe.output = new RecipeIngredient(pump, 1);
            recipe.processingSeconds = 2f;
            AssetDatabase.CreateAsset(recipe, RecipePath);
            sb.AppendLine("created recipe " + RecipePath);
        }

        // 3) 카탈로그 등록
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        if (catalog != null)
        {
            var so = new SerializedObject(catalog);
            AddUnique(so.FindProperty("items"), pump, sb, "item");
            AddUnique(so.FindProperty("recipes"), recipe, sb, "recipe");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
        AssetDatabase.SaveAssets();

        // 4) 유정: 사막 기름 웅덩이마다 하나 (웅덩이 노드와 같은 부모, 같은 위치)
        int sites = 0;
        foreach (var node in Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (node.Item == null || node.Item.itemID != ItemIds.Oil) continue;
            if (node.transform.parent == null || !node.transform.parent.name.StartsWith("OilField")) continue;
            string siteName = "WellSite_" + node.name;
            if (node.transform.parent.Find(siteName) != null) continue;

            var go = new GameObject(siteName);
            Undo.RegisterCreatedObjectUndo(go, "Oil well site");
            go.layer = 4;   // Water = PlayerInteract.interactLayer
            go.transform.SetParent(node.transform.parent, false);
            go.transform.position = node.transform.position;
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 3.2f;
            go.AddComponent<OilWellSite>().Configure(node);
            sites++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        sb.AppendLine("well sites added=" + sites);
        return sb.ToString();
    }

    static void AddUnique(SerializedProperty list, Object obj, System.Text.StringBuilder sb, string label)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == obj) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
        sb.AppendLine("catalog += " + label);
    }

    [MenuItem("Tools/Last Truck/Oil: Setup Pumpjack + Well Sites")]
    static void Menu() { Debug.Log("[OilPumpSetup]\n" + Run()); }
}
#endif
