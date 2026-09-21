using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CraftingSystem.Editor
{
    /// <summary>
    /// Resources/Crafting/Icons 아래의 아이템별 개별 아이콘을 Sprite로 임포트하고 ItemData/Catalog 에셋을 생성한다.
    /// 아이템/레시피 정의는 CraftingContentFactory를 단일 출처로 사용한다.
    /// </summary>
    public static class CraftingAssetSetup
    {
        private const string ItemsFolder = "Assets/ScriptableObjects/Items";
        private const string RecipesFolder = "Assets/ScriptableObjects/Recipes";
        private const string ResourcesFolder = "Assets/Resources/Crafting";
        private const string IconsFolder = "Assets/Resources/Crafting/Icons";

        [MenuItem("Crafting/Setup Item Sprites And Assets")]
        public static void SetupAll()
        {
            EnsureFolders();
            ConfigureIconImportSettings();

            Dictionary<string, Sprite> sprites = LoadIconSprites();
            List<ItemData> runtimeItems = CraftingContentFactory.CreateAllItems(sprites);
            var items = new List<ItemData>();
            foreach (ItemData runtimeItem in runtimeItems)
            {
                ItemData saved = CreateOrUpdateItem(runtimeItem);
                items.Add(saved);
            }

            List<RecipeData> recipes = CraftingContentFactory.CreateAllRecipes(items);
            var savedRecipes = new List<RecipeData>();
            foreach (RecipeData runtimeRecipe in recipes)
            {
                RecipeData saved = CreateOrUpdateRecipe(runtimeRecipe);
                savedRecipes.Add(saved);
            }

            ItemCatalog catalog = CreateOrUpdateCatalog(items, savedRecipes);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int iconCount = 0;
            foreach (string id in CraftingContentFactory.GetIconItemIds())
            {
                if (sprites.ContainsKey(id))
                    iconCount++;
            }

            Debug.Log($"[CraftingAssetSetup] 완료. 아이템 {items.Count}개(아이콘 {iconCount}개), 레시피 {savedRecipes.Count}개 생성. Catalog={AssetDatabase.GetAssetPath(catalog)}");
        }

        [MenuItem("Crafting/Setup Demo Scene Objects")]
        public static void SetupDemoScene()
        {
            CraftingSceneBootstrap bootstrap = Object.FindFirstObjectByType<CraftingSceneBootstrap>();
            if (bootstrap == null)
            {
                GameObject go = new GameObject("CraftingBootstrap");
                bootstrap = go.AddComponent<CraftingSceneBootstrap>();
            }

            bootstrap.SetupScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CraftingAssetSetup] Demo 씬 오브젝트 셋업 완료.");
        }

        private static void ConfigureIconImportSettings()
        {
            if (!Directory.Exists(IconsFolder))
                return;

            string[] files = Directory.GetFiles(IconsFolder, "*.png");
            foreach (string fullPath in files)
            {
                string assetPath = fullPath.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 100;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Sprite> LoadIconSprites()
        {
            var result = new Dictionary<string, Sprite>();
            foreach (string id in CraftingContentFactory.GetIconItemIds())
            {
                string assetPath = $"{IconsFolder}/{id}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                    result[id] = sprite;
            }

            return result;
        }

        private static void EnsureFolders()
        {
            CreateFolderRecursive(ItemsFolder);
            CreateFolderRecursive(RecipesFolder);
            CreateFolderRecursive(ResourcesFolder);
            CreateFolderRecursive(IconsFolder);
            CreateFolderRecursive("Assets/ScriptableObjects");
            CreateFolderRecursive("Assets/Resources");
        }

        private static void CreateFolderRecursive(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static ItemData CreateOrUpdateItem(ItemData source)
        {
            string path = $"{ItemsFolder}/{source.itemID}.asset";
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.itemID = source.itemID;
            item.itemName = source.itemName;
            item.itemType = source.itemType;
            item.maxStack = source.maxStack;
            item.icon = source.icon;
            item.toolTier = source.toolTier;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static RecipeData CreateOrUpdateRecipe(RecipeData source)
        {
            string path = $"{RecipesFolder}/{source.recipeID}.asset";
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeData>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            recipe.recipeID = source.recipeID;
            recipe.displayName = source.displayName;
            recipe.requiredFacility = source.requiredFacility;
            recipe.processingSeconds = source.processingSeconds;
            recipe.inputs = new List<RecipeIngredient>();
            foreach (RecipeIngredient input in source.inputs)
            {
                // 에셋 참조로 교체
                ItemData assetItem = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/{input.item.itemID}.asset");
                recipe.inputs.Add(new RecipeIngredient(assetItem != null ? assetItem : input.item, input.count));
            }

            ItemData outItem = source.output.item != null
                ? AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/{source.output.item.itemID}.asset")
                : null;
            recipe.output = new RecipeIngredient(outItem != null ? outItem : source.output.item, source.output.count);
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static ItemCatalog CreateOrUpdateCatalog(List<ItemData> items, List<RecipeData> recipes)
        {
            string path = $"{ResourcesFolder}/ItemCatalog.asset";
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.SetContent(items, recipes);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }
    }
}
