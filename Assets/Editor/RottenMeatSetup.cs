#if UNITY_EDITOR
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 썩은 고기 아이템(원재료)과 트럭 조립 레시피 "썩은 고기 손질"(썩은 고기 1 + 약초 1 → 고기 1)을 만들고 카탈로그에 등록한다.
/// 다시 실행해도 중복으로 만들지 않는다.
/// </summary>
public static class RottenMeatSetup
{
    const string ItemDir = "Assets/ScriptableObjects/Items/";
    const string RecipeDir = "Assets/ScriptableObjects/Recipes/";
    const string IconPath = "Assets/Resources/Crafting/Icons/rotten_meat.png";
    const string CatalogPath = "Assets/Resources/Crafting/ItemCatalog.asset";
    const string RecipeId = "assemble_cleaned_meat";

    static ItemData Item(string id) => AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + id + ".asset");

    public static string Run()
    {
        var sb = new System.Text.StringBuilder();

        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            sb.AppendLine("icon -> sprite");
        }

        var item = Item(ItemIds.RottenMeat);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemID = ItemIds.RottenMeat;
            item.itemName = "썩은 고기";
            item.itemType = ItemType.Raw;
            item.isWeapon = false;
            item.maxStack = 99;
            AssetDatabase.CreateAsset(item, ItemDir + ItemIds.RottenMeat + ".asset");
            sb.AppendLine("item + rotten_meat");
        }
        if (item.icon == null)
        {
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            EditorUtility.SetDirty(item);
            if (item.icon == null) sb.AppendLine("  (아이콘 없음)");
        }

        var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeDir + RecipeId + ".asset");
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.recipeID = RecipeId;
            recipe.displayName = "썩은 고기 손질";
            recipe.requiredFacility = FacilityType.None;
            recipe.inputs.Add(new RecipeIngredient(item, 1));
            recipe.inputs.Add(new RecipeIngredient(Item(ItemIds.Herb), 1));
            recipe.output = new RecipeIngredient(Item(ItemIds.Meat), 1);
            recipe.processingSeconds = 1f;
            AssetDatabase.CreateAsset(recipe, RecipeDir + RecipeId + ".asset");
            sb.AppendLine("recipe + " + RecipeId);
        }

        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        if (catalog != null)
        {
            var so = new SerializedObject(catalog);
            AddUnique(so.FindProperty("items"), item, sb);
            AddUnique(so.FindProperty("recipes"), recipe, sb);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
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

    [MenuItem("Tools/Last Truck/Items: Add Rotten Meat")]
    static void Menu() { Debug.Log("[RottenMeatSetup]\n" + Run()); }
}
#endif
