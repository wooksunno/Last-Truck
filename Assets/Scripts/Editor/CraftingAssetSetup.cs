using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CraftingSystem.Editor
{
    /// <summary>
    /// 아이템 시트를 Multiple Sprite로 분할하고 ItemData/Catalog 에셋을 생성한다.
    /// </summary>
    public static class CraftingAssetSetup
    {
        private const string ItemsFolder = "Assets/ScriptableObjects/Items";
        private const string RecipesFolder = "Assets/ScriptableObjects/Recipes";
        private const string ResourcesFolder = "Assets/Resources/Crafting";
        private const string UiFolder = "Assets/ui";

        private static readonly (int index, string id, string name, ItemType type, int maxStack)[] ItemDefs =
        {
            (0, ItemIds.Wood, "나무", ItemType.Raw, 99),
            (1, ItemIds.Stone, "돌", ItemType.Raw, 99),
            (2, ItemIds.IronOre, "철 원석", ItemType.Raw, 99),
            (3, ItemIds.CopperOre, "구리 원석", ItemType.Raw, 99),
            (4, ItemIds.Iron, "철", ItemType.Intermediate, 99),
            (6, ItemIds.Copper, "구리", ItemType.Intermediate, 99),
            (7, ItemIds.WoodHandle, "원목 손잡이", ItemType.Intermediate, 50),
            (8, ItemIds.CopperBlade, "구리 절삭날", ItemType.Intermediate, 50),
            (9, ItemIds.Machete, "마체테", ItemType.Finished, 10),
            (10, ItemIds.WoodSpear, "나무창", ItemType.Finished, 10),
            (11, ItemIds.StonePickaxe, "돌 곡괭이", ItemType.Tool, 5),
            (12, ItemIds.IronPickaxe, "철제 곡괭이", ItemType.Tool, 5),
        };

        [MenuItem("Crafting/Setup Item Sprites And Assets")]
        public static void SetupAll()
        {
            string sheetPath = FindSheetPath();
            if (string.IsNullOrEmpty(sheetPath))
            {
                Debug.LogError("[CraftingAssetSetup] Assets/ui 아이템 시트를 찾지 못했습니다.");
                return;
            }

            SliceSpriteSheet(sheetPath);
            EnsureFolders();

            Dictionary<string, Sprite> sprites = LoadSlicedSprites(sheetPath);
            var items = new List<ItemData>();
            foreach (var def in ItemDefs)
            {
                sprites.TryGetValue(def.id, out Sprite icon);
                ItemData item = CreateOrUpdateItem(def.id, def.name, def.type, def.maxStack, icon);
                items.Add(item);
            }

            List<RecipeData> recipes = CraftingContentFactory.CreateAllRecipes(items);
            var savedRecipes = new List<RecipeData>();
            foreach (RecipeData runtimeRecipe in recipes)
            {
                RecipeData saved = CreateOrUpdateRecipe(runtimeRecipe);
                savedRecipes.Add(saved);
            }

            ItemCatalog catalog = CreateOrUpdateCatalog(items, savedRecipes);
            CopySheetToResources(sheetPath);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CraftingAssetSetup] 완료. 아이템 {items.Count}개, 레시피 {savedRecipes.Count}개 생성. Catalog={AssetDatabase.GetAssetPath(catalog)}");
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

        private static string FindSheetPath()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { UiFolder, "Assets/UI" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png") || path.EndsWith(".jpg"))
                    return path;
            }

            return null;
        }

        private static void SliceSpriteSheet(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            int width = tex != null ? tex.width : 1536;
            int height = tex != null ? tex.height : 1024;
            int cols = CraftingContentFactory.SheetColumns;
            int rows = CraftingContentFactory.SheetRows;
            int cellW = width / cols;
            int cellH = height / rows;

            var metas = new List<SpriteMetaData>();
            string[] namesByIndex = new string[cols * rows];
            foreach (var def in ItemDefs)
                namesByIndex[def.index] = def.id;
            namesByIndex[5] = "unused_ore_variant";

            for (int i = 0; i < cols * rows; i++)
            {
                int col = i % cols;
                int rowFromTop = i / cols;
                int rowFromBottom = rows - 1 - rowFromTop;
                string spriteName = string.IsNullOrEmpty(namesByIndex[i]) ? $"icon_{i}" : namesByIndex[i];

                metas.Add(new SpriteMetaData
                {
                    name = spriteName,
                    rect = new Rect(col * cellW, rowFromBottom * cellH, cellW, cellH),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }

            importer.spritesheet = metas.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static Dictionary<string, Sprite> LoadSlicedSprites(string assetPath)
        {
            var result = new Dictionary<string, Sprite>();
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                    result[sprite.name] = sprite;
            }

            return result;
        }

        private static void EnsureFolders()
        {
            CreateFolderRecursive(ItemsFolder);
            CreateFolderRecursive(RecipesFolder);
            CreateFolderRecursive(ResourcesFolder);
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

        private static ItemData CreateOrUpdateItem(string id, string displayName, ItemType type, int maxStack, Sprite icon)
        {
            string path = $"{ItemsFolder}/{id}.asset";
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.itemID = id;
            item.itemName = displayName;
            item.itemType = type;
            item.maxStack = maxStack;
            item.icon = icon;
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

        private static void CopySheetToResources(string sheetPath)
        {
            string dest = $"{ResourcesFolder}/ItemIconSheet.png";
            if (File.Exists(Path.GetFullPath(dest)))
                AssetDatabase.DeleteAsset(dest);

            AssetDatabase.CopyAsset(sheetPath, dest);
            var importer = AssetImporter.GetAtPath(dest) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
    }
}
