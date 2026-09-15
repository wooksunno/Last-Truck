using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 아이템 시트(5x3)에서 스프라이트를 만들고 Item/Recipe 카탈로그를 채운다.
    /// </summary>
    public static class CraftingContentFactory
    {
        public const string SpriteSheetSearchFolder = "Assets/ui";
        public const int SheetColumns = 5;
        public const int SheetRows = 3;

        // 시트 인덱스 → 아이템 ID (5번 슬롯은 여분 아이콘이라 건너뜀)
        private static readonly (int index, string id)[] SpriteMap =
        {
            (0, ItemIds.Wood),
            (1, ItemIds.Stone),
            (2, ItemIds.IronOre),
            (3, ItemIds.CopperOre),
            (4, ItemIds.Iron),
            (6, ItemIds.Copper),
            (7, ItemIds.WoodHandle),
            (8, ItemIds.CopperBlade),
            (9, ItemIds.Machete),
            (10, ItemIds.WoodSpear),
            (11, ItemIds.StonePickaxe),
            (12, ItemIds.IronPickaxe),
        };

        public static void PopulateCatalog(ItemCatalog catalog)
        {
            Dictionary<string, Sprite> sprites = BuildRuntimeSpritesFromSheet();
            List<ItemData> items = CreateAllItems(sprites);
            List<RecipeData> recipes = CreateAllRecipes(items);
            catalog.SetContent(items, recipes);
        }

        public static List<ItemData> CreateAllItems(Dictionary<string, Sprite> sprites)
        {
            sprites ??= new Dictionary<string, Sprite>();

            return new List<ItemData>
            {
                Make(ItemIds.Wood, "나무", ItemType.Raw, 99, sprites),
                Make(ItemIds.Stone, "돌", ItemType.Raw, 99, sprites),
                Make(ItemIds.IronOre, "철 원석", ItemType.Raw, 99, sprites),
                Make(ItemIds.CopperOre, "구리 원석", ItemType.Raw, 99, sprites),
                Make(ItemIds.Iron, "철", ItemType.Intermediate, 99, sprites),
                Make(ItemIds.Copper, "구리", ItemType.Intermediate, 99, sprites),
                Make(ItemIds.WoodHandle, "원목 손잡이", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.CopperBlade, "구리 절삭날", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.Machete, "마체테", ItemType.Finished, 10, sprites),
                Make(ItemIds.WoodSpear, "나무창", ItemType.Finished, 10, sprites),
                Make(ItemIds.StonePickaxe, "돌 곡괭이", ItemType.Tool, 5, sprites, 1),
                Make(ItemIds.CopperPickaxe, "구리 곡괭이", ItemType.Tool, 5, sprites, 2),
                Make(ItemIds.IronPickaxe, "철제 곡괭이", ItemType.Tool, 5, sprites, 3),
                Make(ItemIds.Diamond, "다이아몬드", ItemType.Raw, 99, sprites),
                Make(ItemIds.Platinum, "백금 원석", ItemType.Raw, 99, sprites),
                Make(ItemIds.PoisonHerb, "독초", ItemType.Raw, 99, sprites),
            };
        }

        public static List<RecipeData> CreateAllRecipes(List<ItemData> items)
        {
            ItemData Get(string id)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null && items[i].itemID == id)
                        return items[i];
                }

                return null;
            }

            var recipes = new List<RecipeData>
            {
                // 모닥불 제련
                MakeRecipe("smelt_iron", "철 제련", FacilityType.Campfire,
                    new[] { (Get(ItemIds.IronOre), 1) }, Get(ItemIds.Iron), 1),
                MakeRecipe("smelt_copper", "구리 제련", FacilityType.Campfire,
                    new[] { (Get(ItemIds.CopperOre), 1) }, Get(ItemIds.Copper), 1),

                // 절삭기
                MakeRecipe("cut_wood_handle", "원목 손잡이 가공", FacilityType.PrecisionCutter,
                    new[] { (Get(ItemIds.Wood), 1) }, Get(ItemIds.WoodHandle), 1),
                MakeRecipe("cut_copper_blade", "구리 절삭날 가공", FacilityType.PrecisionCutter,
                    new[] { (Get(ItemIds.Copper), 1) }, Get(ItemIds.CopperBlade), 1),

                // 롤러 프레스기 (금속 압연/압축)
                MakeRecipe("press_copper_blade", "구리 압연 (절삭날)", FacilityType.RollerPress,
                    new[] { (Get(ItemIds.Copper), 2) }, Get(ItemIds.CopperBlade), 1),
                MakeRecipe("press_refine_iron_ore", "철 원석 고압 정제", FacilityType.RollerPress,
                    new[] { (Get(ItemIds.IronOre), 2) }, Get(ItemIds.Iron), 1),

                // 트럭 최종 조립
                MakeRecipe("assemble_machete", "마체테 조립", FacilityType.None,
                    new[] { (Get(ItemIds.Iron), 1), (Get(ItemIds.WoodHandle), 1), (Get(ItemIds.CopperBlade), 1) },
                    Get(ItemIds.Machete), 1),
                MakeRecipe("assemble_wood_spear", "나무창 조립", FacilityType.None,
                    new[] { (Get(ItemIds.Wood), 2), (Get(ItemIds.Stone), 1) },
                    Get(ItemIds.WoodSpear), 1),
                MakeRecipe("assemble_stone_pickaxe", "돌 곡괭이 조립", FacilityType.None,
                    new[] { (Get(ItemIds.Stone), 2), (Get(ItemIds.WoodHandle), 1) },
                    Get(ItemIds.StonePickaxe), 1),
                MakeRecipe("assemble_copper_pickaxe", "구리 곡괭이 조립", FacilityType.None,
                    new[] { (Get(ItemIds.Copper), 2), (Get(ItemIds.WoodHandle), 1) },
                    Get(ItemIds.CopperPickaxe), 1),
                MakeRecipe("assemble_iron_pickaxe", "철제 곡괭이 조립", FacilityType.None,
                    new[] { (Get(ItemIds.Iron), 2), (Get(ItemIds.WoodHandle), 1) },
                    Get(ItemIds.IronPickaxe), 1),
            };

            return recipes;
        }

        public static Dictionary<string, Sprite> BuildRuntimeSpritesFromSheet()
        {
            var result = new Dictionary<string, Sprite>();
            Texture2D texture = LoadSheetTexture();
            if (texture == null)
            {
                Debug.LogWarning("[CraftingContentFactory] 아이템 시트 텍스처를 찾지 못했습니다.");
                return result;
            }

            // 에디터에서 이미 Multiple Sprite로 슬라이스된 경우
            Sprite[] existing = null;
#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(assetPath))
            {
                Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var list = new List<Sprite>();
                foreach (Object asset in assets)
                {
                    if (asset is Sprite sprite)
                        list.Add(sprite);
                }

                existing = list.ToArray();
            }
#endif
            if (existing != null && existing.Length > 0)
            {
                foreach (var map in SpriteMap)
                {
                    if (map.index >= 0 && map.index < existing.Length)
                        result[map.id] = existing[map.index];
                    else
                    {
                        // 이름 기반 폴백
                        foreach (Sprite sprite in existing)
                        {
                            if (sprite != null && sprite.name.Contains(map.id))
                            {
                                result[map.id] = sprite;
                                break;
                            }
                        }
                    }
                }

                if (result.Count > 0)
                    return result;
            }

            // 런타임 격자 분할
            if (!texture.isReadable)
            {
                Debug.LogWarning("[CraftingContentFactory] 시트가 Read/Write 비활성입니다. 단색 아이콘으로 대체합니다.");
                foreach (var map in SpriteMap)
                    result[map.id] = CreateSolidSprite(map.id);
                return result;
            }

            int cellW = texture.width / SheetColumns;
            int cellH = texture.height / SheetRows;

            foreach (var map in SpriteMap)
            {
                int col = map.index % SheetColumns;
                int rowFromTop = map.index / SheetColumns;
                int rowFromBottom = SheetRows - 1 - rowFromTop;
                var rect = new Rect(col * cellW, rowFromBottom * cellH, cellW, cellH);
                Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                sprite.name = map.id;
                result[map.id] = sprite;
            }

            return result;
        }

        public static Texture2D LoadSheetTexture()
        {
            // Resources 복사본 우선
            Texture2D fromResources = Resources.Load<Texture2D>("Crafting/ItemIconSheet");
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteSheetSearchFolder, "Assets/UI", "Assets/ui" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png") || path.EndsWith(".jpg"))
                {
                    var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                        return tex;
                }
            }
#endif
            return null;
        }

        private static ItemData Make(
            string id,
            string name,
            ItemType type,
            int maxStack,
            Dictionary<string, Sprite> sprites,
            int toolTier = 0)
        {
            sprites.TryGetValue(id, out Sprite icon);
            return ItemData.CreateRuntime(id, name, type, maxStack, icon, toolTier);
        }

        private static RecipeData MakeRecipe(
            string id,
            string displayName,
            FacilityType facility,
            (ItemData item, int count)[] inputs,
            ItemData output,
            int outputCount)
        {
            RecipeData recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.recipeID = id;
            recipe.displayName = displayName;
            recipe.requiredFacility = facility;
            recipe.name = id;
            recipe.inputs = new List<RecipeIngredient>();
            if (inputs != null)
            {
                foreach ((ItemData item, int count) in inputs)
                {
                    if (item != null && count > 0)
                        recipe.inputs.Add(new RecipeIngredient(item, count));
                }
            }

            recipe.output = new RecipeIngredient(output, outputCount);
            return recipe;
        }

        private static Sprite CreateSolidSprite(string id)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color color = ColorFromId(id);
            Color[] pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = id;
            return sprite;
        }

        private static Color ColorFromId(string id)
        {
            switch (id)
            {
                case ItemIds.Wood: return new Color(0.55f, 0.35f, 0.15f);
                case ItemIds.Stone: return new Color(0.55f, 0.55f, 0.58f);
                case ItemIds.IronOre: return new Color(0.45f, 0.35f, 0.3f);
                case ItemIds.CopperOre: return new Color(0.75f, 0.4f, 0.2f);
                case ItemIds.Iron: return new Color(0.7f, 0.72f, 0.75f);
                case ItemIds.Copper: return new Color(0.85f, 0.5f, 0.25f);
                case ItemIds.WoodHandle: return new Color(0.65f, 0.45f, 0.25f);
                case ItemIds.CopperBlade: return new Color(0.9f, 0.55f, 0.3f);
                case ItemIds.Machete: return new Color(0.6f, 0.65f, 0.7f);
                case ItemIds.WoodSpear: return new Color(0.5f, 0.4f, 0.25f);
                case ItemIds.StonePickaxe: return new Color(0.5f, 0.5f, 0.45f);
                case ItemIds.CopperPickaxe: return new Color(0.8f, 0.5f, 0.3f);
                case ItemIds.IronPickaxe: return new Color(0.35f, 0.38f, 0.42f);
                case ItemIds.Diamond: return new Color(0.7f, 0.9f, 1f);
                case ItemIds.Platinum: return new Color(0.75f, 0.8f, 0.85f);
                case ItemIds.PoisonHerb: return new Color(0.45f, 0.15f, 0.6f);
                default: return Color.magenta;
            }
        }
    }
}
