#if UNITY_EDITOR
using System.Collections.Generic;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 포탑 5종 / 트럭 상위 파츠 / 음식·소모품 / 도구 수리 키트를 만들고(아이템 + 레시피) 카탈로그에 등록하고,
/// 곡괭이 등 도구에 내구도를 넣는다. 다시 실행해도 중복으로 만들지 않는다 (이미 있는 아이템은 값만 맞춘다).
/// 아이콘은 Assets/Resources/Crafting/Icons/&lt;id&gt;.png (같은 이름으로 덮어쓰면 그 그림이 쓰인다).
/// </summary>
public static class NewItemsSetup
{
    const string ItemDir = "Assets/ScriptableObjects/Items/";
    const string RecipeDir = "Assets/ScriptableObjects/Recipes/";
    const string IconDir = "Assets/Resources/Crafting/Icons/";
    const string CatalogPath = "Assets/Resources/Crafting/ItemCatalog.asset";

    struct Recipe
    {
        public FacilityType facility; public string recipeId, recipeName;
        public (string id, int count)[] inputs; public int outCount; public float seconds;
    }

    struct Def
    {
        public string id, name; public ItemType type; public int stack;
        public Recipe recipe;
    }

    static Recipe Assemble(string id, string name, int outCount, params (string id, int count)[] inputs) =>
        new Recipe { facility = FacilityType.None, recipeId = "assemble_" + id, recipeName = name + " 조립", inputs = inputs, outCount = outCount, seconds = 2f };

    /// <summary>자동 포탑: 수동 포탑 1개 + 첨단 부품 (후반 포탑은 영구 에너지 코어까지). 장착되는 파츠라 한 개씩만 쌓인다.</summary>
    static Def AutoTurret(string id, string name, string manualTurretId, bool needsCore)
    {
        var inputs = new List<(string id, int count)>
        {
            (manualTurretId, 1),
            (ItemIds.MechanicalTrigger, 2), (ItemIds.HighVoltageCable, 3), (ItemIds.PlatinumGear, 2), (ItemIds.SuperconductorCatalyst, 2),
        };
        if (needsCore) inputs.Add((ItemIds.EternalEnergyCore, 1));
        return new Def { id = id, name = name, type = ItemType.Finished, stack = 1, recipe = Assemble(id, name, 1, inputs.ToArray()) };
    }

    static readonly Def[] Defs =
    {
        // 포탑 (트럭 지붕 파츠). 미니 포탑은 TruckPartsSetup이 만든다.
        new Def { id = ItemIds.GatlingTurret, name = "기관포 포탑", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.GatlingTurret, "기관포 포탑", 1, (ItemIds.Iron, 12), (ItemIds.MechanicalTrigger, 4), (ItemIds.PrecisionBarrel, 3), (ItemIds.ArmorPlate, 2)) },
        new Def { id = ItemIds.SniperTurret, name = "저격 포탑", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.SniperTurret, "저격 포탑", 1, (ItemIds.PlatinumGear, 2), (ItemIds.PrecisionBarrel, 3), (ItemIds.MechanicalTrigger, 2), (ItemIds.SuperconductorCatalyst, 1)) },
        new Def { id = ItemIds.MortarTurret, name = "박격포 포탑", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.MortarTurret, "박격포 포탑", 1, (ItemIds.ArmorPlate, 4), (ItemIds.PropellantPowder, 3), (ItemIds.PrecisionBarrel, 1), (ItemIds.Iron, 6)) },
        new Def { id = ItemIds.FlameTurret, name = "화염 포탑", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.FlameTurret, "화염 포탑", 1, (ItemIds.PrecisionBarrel, 1), (ItemIds.NapalmGel, 4), (ItemIds.RefinedFuel, 2), (ItemIds.ArmorPlate, 2)) },
        new Def { id = ItemIds.RailTurret, name = "레일 포탑", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.RailTurret, "레일 포탑", 1, (ItemIds.PlatinumGear, 3), (ItemIds.SuperconductorCatalyst, 2), (ItemIds.HighVoltageCable, 3), (ItemIds.PrecisionBarrel, 2), (ItemIds.EternalEnergyCore, 1)) },

        // 자동 포탑 (적재함 파츠): 같은 종류의 수동 포탑 + 첨단 부품. 수동 포탑을 먼저 만들어야 하고 재료도 훨씬 많이 든다.
        AutoTurret(ItemIds.AutoMiniTurret, "자동 미니 포탑", ItemIds.MiniTurret, false),
        AutoTurret(ItemIds.AutoGatlingTurret, "자동 기관포 포탑", ItemIds.GatlingTurret, false),
        AutoTurret(ItemIds.AutoSniperTurret, "자동 저격 포탑", ItemIds.SniperTurret, true),
        AutoTurret(ItemIds.AutoMortarTurret, "자동 박격포 포탑", ItemIds.MortarTurret, true),
        AutoTurret(ItemIds.AutoFlameTurret, "자동 화염 포탑", ItemIds.FlameTurret, true),
        AutoTurret(ItemIds.AutoRailTurret, "자동 레일 포탑", ItemIds.RailTurret, true),

        // 트럭 상위 파츠 (최종)
        new Def { id = ItemIds.TurboEngine, name = "터보 엔진", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.TurboEngine, "터보 엔진", 1, (ItemIds.PlatinumGear, 3), (ItemIds.RefinedFuel, 4), (ItemIds.HighVoltageCable, 2), (ItemIds.EternalEnergyCore, 1)) },
        new Def { id = ItemIds.AlloyArmor, name = "합금 장갑", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.AlloyArmor, "합금 장갑", 1, (ItemIds.ArmorPlate, 5), (ItemIds.PurePlatinum, 3), (ItemIds.PlatinumGear, 2)) },

        // 음식
        new Def { id = ItemIds.HerbPorridge, name = "약초 죽", type = ItemType.Finished, stack = 20,
                  recipe = new Recipe { facility = FacilityType.Campfire, recipeId = "bonfire_" + ItemIds.HerbPorridge, recipeName = "약초 죽",
                                        inputs = new[] { (ItemIds.Herb, 3) }, outCount = 1, seconds = 40f } },
        new Def { id = ItemIds.HeartyStew, name = "든든한 스튜", type = ItemType.Finished, stack = 10,
                  recipe = new Recipe { facility = FacilityType.Campfire, recipeId = "bonfire_" + ItemIds.HeartyStew, recipeName = "든든한 스튜",
                                        inputs = new[] { (ItemIds.Meat, 1), (ItemIds.Fish, 1), (ItemIds.Herb, 2) }, outCount = 1, seconds = 60f } },

        // 소모품 / 수리
        new Def { id = ItemIds.HerbBandage, name = "약초 붕대", type = ItemType.Finished, stack = 20,
                  recipe = Assemble(ItemIds.HerbBandage, "약초 붕대", 2, (ItemIds.Herb, 2), (ItemIds.WetHide, 1)) },
        new Def { id = ItemIds.VitalityTonic, name = "활력 토닉", type = ItemType.Finished, stack = 10,
                  recipe = new Recipe { facility = FacilityType.ChemicalRefinery, recipeId = "chem_" + ItemIds.VitalityTonic, recipeName = "활력 토닉 정제",
                                        inputs = new[] { (ItemIds.MedicalExtract, 2), (ItemIds.StimulantPowder, 1) }, outCount = 1, seconds = 60f } },
        // 독버섯 미끼: 독성 유인 페로몬 + 썩은 고기로 만든다 (독약초는 페로몬을 뽑을 때만 쓴다). 손에 들고 E를 꾹 눌러 놓으면 근처 몬스터가 몰려와서 먹고 중독된다.
        new Def { id = ItemIds.PoisonMushroomBait, name = "독버섯 미끼", type = ItemType.Finished, stack = 10,
                  recipe = Assemble(ItemIds.PoisonMushroomBait, "독버섯 미끼", 2, (ItemIds.LurePheromone, 1), (ItemIds.RottenMeat, 1)) },
        // 플레이어 갑옷 3단계: 가방에 가지고 있으면 받는 피해가 줄어든다 (PlayerArmor). 한 칸을 차지하는 것이 대가.
        new Def { id = ItemIds.LeatherArmor, name = "가죽 갑옷", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.LeatherArmor, "가죽 갑옷", 1, (ItemIds.TannedLeather, 4), (ItemIds.WetHide, 2)) },
        new Def { id = ItemIds.IronPlateArmor, name = "철제 갑옷", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.IronPlateArmor, "철제 갑옷", 1, (ItemIds.ArmorPlate, 3), (ItemIds.TannedLeather, 2), (ItemIds.Iron, 2)) },
        new Def { id = ItemIds.AlloyPlateArmor, name = "합금 갑옷", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.AlloyPlateArmor, "합금 갑옷", 1, (ItemIds.ArmorPlate, 4), (ItemIds.PurePlatinum, 2), (ItemIds.PlatinumGear, 1), (ItemIds.TannedLeather, 2)) },

        // 사막 식물: 선인장/들풀은 채집하면 사라져서 트럭 길을 틔우는 용도. 쓸 데가 없으므로 음식으로 만든다.
        new Def { id = ItemIds.Cactus, name = "선인장", type = ItemType.Raw, stack = 99 },
        new Def { id = ItemIds.WildGrass, name = "들풀", type = ItemType.Raw, stack = 99 },
        new Def { id = ItemIds.CactusSteak, name = "선인장 구이", type = ItemType.Finished, stack = 20,
                  recipe = new Recipe { facility = FacilityType.Campfire, recipeId = "bonfire_" + ItemIds.CactusSteak, recipeName = "선인장 구이",
                                        inputs = new[] { (ItemIds.Cactus, 2) }, outCount = 1, seconds = 30f } },
        new Def { id = ItemIds.CactusJuice, name = "선인장 주스", type = ItemType.Finished, stack = 20,
                  recipe = Assemble(ItemIds.CactusJuice, "선인장 주스", 1, (ItemIds.Cactus, 2)) },
        new Def { id = ItemIds.GrassPorridge, name = "풀죽", type = ItemType.Finished, stack = 20,
                  recipe = new Recipe { facility = FacilityType.Campfire, recipeId = "bonfire_" + ItemIds.GrassPorridge, recipeName = "풀죽",
                                        inputs = new[] { (ItemIds.WildGrass, 5) }, outCount = 1, seconds = 35f } },
        new Def { id = ItemIds.DesertStew, name = "사막 스튜", type = ItemType.Finished, stack = 10,
                  recipe = new Recipe { facility = FacilityType.Campfire, recipeId = "bonfire_" + ItemIds.DesertStew, recipeName = "사막 스튜",
                                        inputs = new[] { (ItemIds.Cactus, 2), (ItemIds.WildGrass, 3), (ItemIds.Meat, 1) }, outCount = 1, seconds = 50f } },

        // 랜턴: 안개/비 날씨에 손에 들면 주변이 환해지고 시야가 트인다 (횃불보다 넓고 안정적, 무기와 같이 들 수 없다).
        new Def { id = ItemIds.Lantern, name = "랜턴", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.Lantern, "랜턴", 1, (ItemIds.Iron, 2), (ItemIds.Copper, 1), (ItemIds.Charcoal, 2)) },
        // 탈출 부품 3종 (스토리 목표): 후반 재료로 만든다. 3종이 모두 트럭 보관함에 있어야 탈출 게이트를 통과할 수 있다.
        new Def { id = ItemIds.EscapeEngineCore, name = "탈출 엔진 코어", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.EscapeEngineCore, "탈출 엔진 코어", 1, (ItemIds.PlatinumGear, 3), (ItemIds.EternalEnergyCore, 1), (ItemIds.ArmorPlate, 2)) },
        new Def { id = ItemIds.EscapeFuelTank, name = "장거리 연료 탱크", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.EscapeFuelTank, "장거리 연료 탱크", 1, (ItemIds.RefinedFuel, 6), (ItemIds.ArmorPlate, 3), (ItemIds.PrecisionBarrel, 1)) },
        new Def { id = ItemIds.EscapeBeacon, name = "구조 신호기", type = ItemType.Finished, stack = 1,
                  recipe = Assemble(ItemIds.EscapeBeacon, "구조 신호기", 1, (ItemIds.HighVoltageCable, 3), (ItemIds.SuperconductorCatalyst, 2), (ItemIds.MechanicalTrigger, 2)) },
        new Def { id = ItemIds.ToolRepairKit, name = "도구 수리 키트", type = ItemType.Finished, stack = 10,
                  recipe = Assemble(ItemIds.ToolRepairKit, "도구 수리 키트", 2, (ItemIds.Iron, 1), (ItemIds.Wood, 4), (ItemIds.StonePowder, 2)) },
    };

    // 도구 내구도 (채집 1회 = 1)
    static readonly (string id, int durability)[] ToolDurability =
    {
        (ItemIds.WoodPickaxe, 20), (ItemIds.StonePickaxe, 40), (ItemIds.CopperPickaxe, 60), (ItemIds.IronPickaxe, 90),
        (ItemIds.PlatinumDrill, 130), (ItemIds.DiamondCrusher, 200), (ItemIds.CopperHandPump, 40),

        // 무기 (근접 = 휘두름 1회, 총/활/포 = 한 발, 화염방사기·분무기 = 0.6초 분사)
        (ItemIds.WoodSpear, 30), (ItemIds.Machete, 80), (ItemIds.FlameMachete, 110), (ItemIds.VibrationBlade, 150),
        (ItemIds.Pistol, 90), (ItemIds.Ak47, 150), (ItemIds.HuntingBow, 60),
        (ItemIds.PlatinumSniperRifle, 40), (ItemIds.ShredderDrillLauncher, 50),
        (ItemIds.IronFieldCannon, 35), (ItemIds.PlatinumRailCannon, 30),
        (ItemIds.Flamethrower, 120), (ItemIds.ChemicalSprayer, 100),
    };

    static ItemData Item(string id) => AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + id + ".asset");

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

    public static string Run()
    {
        var sb = new System.Text.StringBuilder();
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        var so = catalog != null ? new SerializedObject(catalog) : null;
        if (catalog == null) sb.AppendLine("!! 카탈로그를 찾지 못했어요: " + CatalogPath);

        // 1) 아이템 먼저 전부 (레시피가 서로를 참조하므로)
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
                item.icon = IconSprite(def.id);
                EditorUtility.SetDirty(item);
                if (item.icon == null) sb.AppendLine("  (아이콘 없음) " + def.id);
            }
            if (so != null) AddUnique(so.FindProperty("items"), item, sb);
        }

        // 2) 레시피
        foreach (var def in Defs)
        {
            var item = Item(def.id);
            var r = def.recipe;
            if (r.inputs == null) continue;   // 레시피가 없는 원재료 (선인장/들풀)
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeDir + r.recipeId + ".asset");
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeData>();
                recipe.recipeID = r.recipeId;
                recipe.displayName = r.recipeName;
                recipe.requiredFacility = r.facility;
                foreach (var (iid, count) in r.inputs)
                {
                    var input = Item(iid);
                    if (input == null) { sb.AppendLine("!! 재료 없음: " + iid + " (" + r.recipeId + ")"); continue; }
                    recipe.inputs.Add(new RecipeIngredient(input, count));
                }
                recipe.output = new RecipeIngredient(item, r.outCount);
                recipe.processingSeconds = r.seconds;
                AssetDatabase.CreateAsset(recipe, RecipeDir + r.recipeId + ".asset");
                sb.AppendLine("recipe + " + r.recipeId);
            }
            if (so != null) AddUnique(so.FindProperty("recipes"), recipe, sb);
        }

        // 3) 도구 내구도
        foreach (var (id, durability) in ToolDurability)
        {
            var tool = Item(id);
            if (tool == null) { sb.AppendLine("!! 도구 없음: " + id); continue; }
            if (tool.maxDurability != durability)
            {
                tool.maxDurability = durability;
                EditorUtility.SetDirty(tool);
                sb.AppendLine("durability " + id + " = " + durability);
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

    [MenuItem("Tools/Last Truck/Items: Add Turrets, Foods, Repair Kit + Tool Durability")]
    static void Menu() { Debug.Log("[NewItemsSetup]\n" + Run()); }
}
#endif
