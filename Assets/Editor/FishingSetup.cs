#if UNITY_EDITOR
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 낚싯대 아이템(도구, 내구도 40)과 트럭 조립 레시피(나무 6 + 약초 2)를 만들고 카탈로그에 등록한다. 다시 실행해도 중복으로 만들지 않는다.
/// </summary>
public static class FishingSetup
{
    const string ItemDir = "Assets/ScriptableObjects/Items/";
    const string RecipeDir = "Assets/ScriptableObjects/Recipes/";
    const string IconPath = "Assets/Resources/Crafting/Icons/fishing_rod.png";
    const string CatalogPath = "Assets/Resources/Crafting/ItemCatalog.asset";

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

        var item = Item(ItemIds.FishingRod);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemID = ItemIds.FishingRod;
            item.itemName = "낚싯대";
            item.itemType = ItemType.Tool;
            item.isWeapon = false;
            item.maxStack = 1;
            item.toolTier = 0;
            item.maxDurability = 40;
            AssetDatabase.CreateAsset(item, ItemDir + ItemIds.FishingRod + ".asset");
            sb.AppendLine("item + fishing_rod");
        }
        if (item.icon == null)
        {
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            EditorUtility.SetDirty(item);
            if (item.icon == null) sb.AppendLine("  (아이콘 없음)");
        }

        const string recipeId = "assemble_fishing_rod";
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeDir + recipeId + ".asset");
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.recipeID = recipeId;
            recipe.displayName = "낚싯대 조립";
            recipe.requiredFacility = FacilityType.None;
            recipe.inputs.Add(new RecipeIngredient(Item(ItemIds.Wood), 6));
            recipe.inputs.Add(new RecipeIngredient(Item(ItemIds.Herb), 2));
            recipe.output = new RecipeIngredient(item, 1);
            recipe.processingSeconds = 2f;
            AssetDatabase.CreateAsset(recipe, RecipeDir + recipeId + ".asset");
            sb.AppendLine("recipe + " + recipeId);
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

    [MenuItem("Tools/Last Truck/Items: Add Fishing Rod")]
    static void Menu() { Debug.Log("[FishingSetup]\n" + Run()); }
}
#endif
