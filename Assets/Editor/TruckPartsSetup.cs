#if UNITY_EDITOR
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 트럭 파츠/탄환 아이템을 만들고(아이템 + 트럭 조립 레시피) 카탈로그에 등록한다. 다시 실행해도 중복으로 만들지 않는다.
///  - 미니 포탑(지붕): 순수 철 10 + 기계식 트리거 2 + 정밀 총열 1
///  - 개조 엔진(엔진): 순수 철 8 + 순수 구리 6 + 고효율 정제유 2
///  - 탄환 4종: 나무/돌/구리 광석/철 광석 1 → 5발 (포탑 사수가 트럭 안에서 R로도 만들 수 있다)
/// 강철가시 범퍼 / 분쇄 휠 / 트럭 복합 장갑은 이미 있는 아이템을 그대로 파츠로 쓴다 (TruckUpgradeRules).
/// </summary>
public static class TruckPartsSetup
{
    const string ItemDir = "Assets/ScriptableObjects/Items/";
    const string RecipeDir = "Assets/ScriptableObjects/Recipes/";
    const string IconDir = "Assets/Resources/Crafting/Icons/";
    const string CatalogPath = "Assets/Resources/Crafting/ItemCatalog.asset";

    struct Def
    {
        public string id, name; public ItemType type; public int stack;
        public (string id, int count)[] inputs; public int outCount;
    }

    static readonly Def[] Defs =
    {
        new Def { id = ItemIds.MiniTurret, name = "미니 포탑", type = ItemType.Finished, stack = 1,
                  inputs = new[] { (ItemIds.Iron, 10), (ItemIds.MechanicalTrigger, 2), (ItemIds.PrecisionBarrel, 1) }, outCount = 1 },
        new Def { id = ItemIds.TunedEngine, name = "개조 엔진", type = ItemType.Finished, stack = 1,
                  inputs = new[] { (ItemIds.Iron, 8), (ItemIds.Copper, 6), (ItemIds.RefinedFuel, 2) }, outCount = 1 },
        new Def { id = ItemIds.WoodRound, name = "나무탄", type = ItemType.Intermediate, stack = 99,
                  inputs = new[] { (ItemIds.Wood, 1) }, outCount = TruckUpgradeRules.RoundsPerResource },
        new Def { id = ItemIds.StoneRound, name = "돌탄", type = ItemType.Intermediate, stack = 99,
                  inputs = new[] { (ItemIds.Stone, 1) }, outCount = TruckUpgradeRules.RoundsPerResource },
        new Def { id = ItemIds.CopperRound, name = "구리탄", type = ItemType.Intermediate, stack = 99,
                  inputs = new[] { (ItemIds.CopperOre, 1) }, outCount = TruckUpgradeRules.RoundsPerResource },
        new Def { id = ItemIds.IronRound, name = "철탄", type = ItemType.Intermediate, stack = 99,
                  inputs = new[] { (ItemIds.IronOre, 1) }, outCount = TruckUpgradeRules.RoundsPerResource },
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
                item.isWeapon = false;
                item.maxStack = def.stack;
                item.toolTier = 0;
                AssetDatabase.CreateAsset(item, ItemDir + def.id + ".asset");
                sb.AppendLine("item + " + def.id);
            }
            if (item.icon == null)
            {
                item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + def.id + ".png");
                EditorUtility.SetDirty(item);
                if (item.icon == null) sb.AppendLine("  (아이콘 없음) " + def.id);
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

    [MenuItem("Tools/Last Truck/Items: Add Truck Parts + Ammo")]
    static void Menu() { Debug.Log("[TruckPartsSetup]\n" + Run()); }
}
#endif
