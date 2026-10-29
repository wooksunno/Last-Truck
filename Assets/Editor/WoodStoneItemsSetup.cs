#if UNITY_EDITOR
using System.Collections.Generic;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 나무/돌 소비처 아이템 5종을 만들고(아이템 + 트럭 조립 레시피) 카탈로그에 등록한다. 다시 실행해도 중복으로 만들지 않는다.
///  - 투척용 돌멩이: 돌 3 → 2개. 던지는 무기(피해 12). 던질 때마다 1개 소모 → 돌을 계속 쓴다
///  - 나무 가시 덫: 나무 8 + 돌 3. 밟으면 피해 10 + 1.2초 묶기 (유인 미끼 트랩보다 약하지만 쌈)
///  - 횃불: 나무 3 + 목탄 1 → 2개. 손에 들면 주변을 밝힌다(어두운 동굴). 3분 타면 1개 소모
///  - 나무 바리케이드: 나무 20 + 돌 4. 앞에 세우면 몬스터 길을 막는다. 5분 뒤 무너짐
///  - 돌 방벽: 돌 25 + 나무 4. 더 크고 10분 간다
/// </summary>
public static class WoodStoneItemsSetup
{
    const string ItemDir = "Assets/ScriptableObjects/Items/";
    const string RecipeDir = "Assets/ScriptableObjects/Recipes/";
    const string IconDir = "Assets/Resources/Crafting/Icons/";
    const string CatalogPath = "Assets/Resources/Crafting/ItemCatalog.asset";

    struct Def
    {
        public string id, name; public ItemType type; public bool weapon; public int stack;
        public (string id, int count)[] inputs; public int outCount;
    }

    static readonly Def[] Defs =
    {
        new Def { id = ItemIds.ThrowingStone, name = "투척용 돌멩이", type = ItemType.Finished, weapon = true, stack = 20,
                  inputs = new[] { (ItemIds.Stone, 3) }, outCount = 2 },
        new Def { id = ItemIds.WoodSpikeTrap, name = "나무 가시 덫", type = ItemType.Finished, weapon = false, stack = 20,
                  inputs = new[] { (ItemIds.Wood, 8), (ItemIds.Stone, 3) }, outCount = 1 },
        new Def { id = ItemIds.Torch, name = "횃불", type = ItemType.Tool, weapon = false, stack = 10,
                  inputs = new[] { (ItemIds.Wood, 3), (ItemIds.Charcoal, 1) }, outCount = 2 },
        new Def { id = ItemIds.WoodBarricade, name = "나무 바리케이드", type = ItemType.Finished, weapon = false, stack = 5,
                  inputs = new[] { (ItemIds.Wood, 20), (ItemIds.Stone, 4) }, outCount = 1 },
        new Def { id = ItemIds.StoneWall, name = "돌 방벽", type = ItemType.Finished, weapon = false, stack = 5,
                  inputs = new[] { (ItemIds.Stone, 25), (ItemIds.Wood, 4) }, outCount = 1 },
    };

    static ItemData Item(string id) => AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + id + ".asset");

    public static string Run()
    {
        var sb = new System.Text.StringBuilder();
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        var so = catalog != null ? new SerializedObject(catalog) : null;

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
                item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + def.id + ".png");
                AssetDatabase.CreateAsset(item, ItemDir + def.id + ".asset");
                sb.AppendLine("item + " + def.id + (item.icon == null ? " (아이콘 없음)" : ""));
            }

            string recipeId = "assemble_" + def.id;
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeDir + recipeId + ".asset");
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeData>();
                recipe.recipeID = recipeId;
                recipe.displayName = def.name + " 조립";
                recipe.requiredFacility = FacilityType.None;
                foreach (var (iid, count) in def.inputs) recipe.inputs.Add(new RecipeIngredient(Item(iid), count));
                recipe.output = new RecipeIngredient(item, def.outCount);
                recipe.processingSeconds = 2f;
                AssetDatabase.CreateAsset(recipe, RecipeDir + recipeId + ".asset");
                sb.AppendLine("recipe + " + recipeId);
            }

            if (so != null)
            {
                AddUnique(so.FindProperty("items"), item, sb);
                AddUnique(so.FindProperty("recipes"), recipe, sb);
            }
        }

        if (so != null) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog); }
        AssetDatabase.SaveAssets();
        return sb.Length == 0 ? "already set up" : sb.ToString();
    }

    static void AddUnique(SerializedProperty list, Object obj, System.Text.StringBuilder sb)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == obj) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
        sb.AppendLine("  catalog += " + obj.name);
    }

    [MenuItem("Tools/Last Truck/Items: Add Wood+Stone Items")]
    static void Menu() { Debug.Log("[WoodStoneItemsSetup]\n" + Run()); }
}
#endif
