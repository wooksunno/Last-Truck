using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 아이템별 개별 아이콘(Resources/Crafting/Icons/&lt;id&gt;.png)을 불러와 Item/Recipe 카탈로그를 채운다.
    /// 아이콘이 없는 아이템은 단색 아이콘으로 대체한다.
    /// </summary>
    public static class CraftingContentFactory
    {
        public const string IconsResourcesPath = "Crafting/Icons";
        public const string IconsAssetFolder = "Assets/Resources/Crafting/Icons";

        // 개별 아이콘 파일이 있는 아이템 ID 목록 (Resources/Crafting/Icons/<id>.png)
        private static readonly string[] IconItemIds =
        {
            ItemIds.Wood, ItemIds.Stone, ItemIds.CopperOre, ItemIds.IronOre, ItemIds.PlatinumOre,
            ItemIds.Herb, ItemIds.PoisonHerb, ItemIds.Fish, ItemIds.Charcoal, ItemIds.ReinforcedWoodPanel,
            ItemIds.WetHide, ItemIds.WoodHandle, ItemIds.Copper, ItemIds.Iron, ItemIds.MechanicalTrigger,
            ItemIds.SuperconductorCatalyst, ItemIds.EternalEnergyCore, ItemIds.MedicalExtract, ItemIds.Meat,
            ItemIds.TannedLeather, ItemIds.ArmorPlate, ItemIds.HighVoltageCable, ItemIds.PurePlatinum,
            ItemIds.PrecisionBarrel, ItemIds.TruckCompositeArmor, ItemIds.PlatinumGear, ItemIds.DiamondCuttingTip,
            ItemIds.Oil, ItemIds.CopperBlade, ItemIds.NapalmGel, ItemIds.PropellantPowder, ItemIds.StimulantPowder,
            ItemIds.LurePheromone, ItemIds.ChemicalGasGrenade, ItemIds.StonePickaxe, ItemIds.CopperPickaxe,
            ItemIds.IronPickaxe, ItemIds.PlatinumDrill, ItemIds.DiamondCrusher, ItemIds.HuntingBow, ItemIds.Machete,
            ItemIds.ReinforcedIronBlade, ItemIds.FlameMachete, ItemIds.VibrationBlade, ItemIds.Pistol, ItemIds.Ak47,
            ItemIds.EmergencyRevivalKit, ItemIds.NeurotoxinExtract, ItemIds.LureTrap, ItemIds.EmergencyPatchBoard,
            ItemIds.ShredderDrillLauncher, ItemIds.CopperHandPump, ItemIds.Flamethrower, ItemIds.ChemicalSprayer,
            ItemIds.PlatinumRailCannon, ItemIds.LeatherGloves, ItemIds.LeatherPouchBackpack,
            ItemIds.HighTensionRepairPack, ItemIds.RefinedFuel, ItemIds.SpikeBumper, ItemIds.Diamond,
            ItemIds.CombatStimulant, ItemIds.Jerky, ItemIds.PlatinumSniperRifle, ItemIds.IronFieldCannon,
            ItemIds.GrilledFood, ItemIds.WeldingKit, ItemIds.GrinderWheel,
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
                // 원재료
                Make(ItemIds.Wood, "나무", ItemType.Raw, 99, sprites),
                Make(ItemIds.Stone, "돌", ItemType.Raw, 99, sprites),
                Make(ItemIds.IronOre, "철 원석", ItemType.Raw, 99, sprites),
                Make(ItemIds.CopperOre, "구리 원석", ItemType.Raw, 99, sprites),
                Make(ItemIds.PlatinumOre, "백금 원석", ItemType.Raw, 99, sprites),
                Make(ItemIds.Diamond, "다이아몬드 원석", ItemType.Raw, 99, sprites),
                Make(ItemIds.Oil, "기름", ItemType.Raw, 99, sprites),
                Make(ItemIds.Herb, "약초", ItemType.Raw, 99, sprites),
                Make(ItemIds.PoisonHerb, "독약초", ItemType.Raw, 99, sprites),
                Make(ItemIds.Fish, "물고기", ItemType.Raw, 99, sprites),
                Make(ItemIds.Meat, "고기", ItemType.Raw, 99, sprites),
                Make(ItemIds.WetHide, "젖은 가죽", ItemType.Raw, 99, sprites),

                // 1차 가공 재료
                Make(ItemIds.Charcoal, "목탄", ItemType.Intermediate, 99, sprites),
                Make(ItemIds.ReinforcedWoodPanel, "강화 목재 패널", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.Iron, "순수 철", ItemType.Intermediate, 99, sprites),
                Make(ItemIds.Copper, "순수 구리", ItemType.Intermediate, 99, sprites),
                Make(ItemIds.PurePlatinum, "순수 백금", ItemType.Intermediate, 99, sprites),
                Make(ItemIds.StonePowder, "돌가루", ItemType.Intermediate, 99, sprites),
                Make(ItemIds.WoodHandle, "원목 손잡이", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.CopperBlade, "구리 절삭날", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.MechanicalTrigger, "기계식 트리거", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.HighVoltageCable, "고전압 케이블", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.PrecisionBarrel, "정밀 총열", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.ReinforcedIronBlade, "강화 철제 날", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.ArmorPlate, "방호 장갑판", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.PlatinumGear, "백금 정밀 기어", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.SuperconductorCatalyst, "초전도 촉매", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.DiamondCuttingTip, "다이아 커팅 팁", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.EternalEnergyCore, "영구 에너지 코어", ItemType.Intermediate, 10, sprites),
                Make(ItemIds.NapalmGel, "네이팜 젤", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.RefinedFuel, "고효율 정제유", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.PropellantPowder, "추진 화약", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.MedicalExtract, "의료용 추출액", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.StimulantPowder, "자극 농축 분말", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.NeurotoxinExtract, "신경 독소 원액", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.LurePheromone, "독성 유인 페로몬", ItemType.Intermediate, 50, sprites),
                Make(ItemIds.GrilledFood, "생선구이", ItemType.Finished, 20, sprites),
                Make(ItemIds.Jerky, "전투용 육포", ItemType.Finished, 20, sprites),
                Make(ItemIds.TannedLeather, "경화 가공 가죽", ItemType.Intermediate, 50, sprites),

                // 설비 (트럭에서 조립 → 가공 탭에서 해당 시설 사용 가능)
                Make(ItemIds.Campfire, "모닥불", ItemType.Finished, 5, sprites),
                Make(ItemIds.Grindstone, "숫돌 연마대", ItemType.Finished, 5, sprites),
                Make(ItemIds.LeatherTanningRack, "가죽 무두질 건조대", ItemType.Finished, 5, sprites),
                Make(ItemIds.RollerPressMachine, "롤러 프레스기", ItemType.Finished, 5, sprites),
                Make(ItemIds.PrecisionCutterMachine, "절삭기", ItemType.Finished, 5, sprites),
                Make(ItemIds.ChemicalRefineryTower, "화학 정제탑", ItemType.Finished, 5, sprites),
                Make(ItemIds.SuperheatedFurnace, "초고온 용광로", ItemType.Finished, 5, sprites),

                // 도구
                Make(ItemIds.StonePickaxe, "돌 곡괭이", ItemType.Tool, 1, sprites, 1),
                Make(ItemIds.CopperPickaxe, "구리 곡괭이", ItemType.Tool, 1, sprites, 2),
                Make(ItemIds.IronPickaxe, "철제 곡괭이", ItemType.Tool, 1, sprites, 3),
                Make(ItemIds.PlatinumDrill, "백금 착암 드릴", ItemType.Tool, 1, sprites, 4),
                Make(ItemIds.DiamondCrusher, "다이아몬드 분쇄기", ItemType.Tool, 1, sprites, 5),
                Make(ItemIds.CopperHandPump, "구리 수동 펌프", ItemType.Tool, 1, sprites),

                // 근접 무기
                Make(ItemIds.WoodSpear, "나무창", ItemType.Finished, 1, sprites),
                Make(ItemIds.HuntingBow, "사냥용 활", ItemType.Finished, 1, sprites),
                Make(ItemIds.Machete, "마체테", ItemType.Finished, 1, sprites),
                Make(ItemIds.FlameMachete, "화염 마체테", ItemType.Finished, 1, sprites),
                Make(ItemIds.VibrationBlade, "진동 블레이드", ItemType.Finished, 1, sprites),

                // 원거리 / 중화기
                Make(ItemIds.Pistol, "권총", ItemType.Finished, 1, sprites),
                Make(ItemIds.Ak47, "AK-47 소총", ItemType.Finished, 1, sprites),
                Make(ItemIds.PlatinumSniperRifle, "백금 저격소총", ItemType.Finished, 1, sprites),
                Make(ItemIds.Flamethrower, "화염방사기", ItemType.Finished, 1, sprites),
                Make(ItemIds.ChemicalSprayer, "화학 살포기", ItemType.Finished, 1, sprites),
                Make(ItemIds.ShredderDrillLauncher, "파쇄 드릴 런처", ItemType.Finished, 1, sprites),

                // 대포
                Make(ItemIds.IronFieldCannon, "철제 야포", ItemType.Finished, 1, sprites),
                Make(ItemIds.PlatinumRailCannon, "백금 레일 캐논", ItemType.Finished, 1, sprites),

                // 트럭 수리 / 보강
                Make(ItemIds.EmergencyPatchBoard, "응급 덧댐 판자", ItemType.Finished, 20, sprites),
                Make(ItemIds.WeldingKit, "철제 용접 키트", ItemType.Finished, 20, sprites),
                Make(ItemIds.HighTensionRepairPack, "고장력 수리 팩", ItemType.Finished, 20, sprites),
                Make(ItemIds.TruckCompositeArmor, "트럭 복합 장갑", ItemType.Finished, 10, sprites),
                Make(ItemIds.SpikeBumper, "강철가시 범퍼", ItemType.Finished, 10, sprites),
                Make(ItemIds.GrinderWheel, "초진동 회전 분쇄 휠", ItemType.Finished, 10, sprites),

                // 생존 / 유틸 / 함정
                Make(ItemIds.LeatherGloves, "가죽 장갑", ItemType.Finished, 10, sprites),
                Make(ItemIds.LeatherPouchBackpack, "가죽 파우치 배낭", ItemType.Finished, 5, sprites),
                Make(ItemIds.EmergencyRevivalKit, "응급 소생 키트", ItemType.Finished, 20, sprites),
                Make(ItemIds.CombatStimulant, "전투 각성제", ItemType.Finished, 20, sprites),
                Make(ItemIds.ChemicalGasGrenade, "화학 독가스 수류탄", ItemType.Finished, 20, sprites),
                Make(ItemIds.LureTrap, "유인 미끼 트랩", ItemType.Finished, 20, sprites),
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

            var recipes = new List<RecipeData>();

            void Add(string id, string name, FacilityType facility, (string id, int count)[] inputs, string outputId, int outputCount)
            {
                var inputPairs = new List<(ItemData, int)>();
                foreach (var (inId, inCount) in inputs)
                    inputPairs.Add((Get(inId), inCount));
                recipes.Add(MakeRecipe(id, name, facility, inputPairs.ToArray(), Get(outputId), outputCount));
            }

            // ── 모닥불 ──────────────────────────────────────────
            Add("bonfire_charcoal", "목탄 굽기", FacilityType.Campfire, new[] { (ItemIds.Wood, 1) }, ItemIds.Charcoal, 1);
            Add("bonfire_pure_iron", "순수 철 제련", FacilityType.Campfire, new[] { (ItemIds.IronOre, 1) }, ItemIds.Iron, 1);
            Add("bonfire_pure_copper", "순수 구리 제련", FacilityType.Campfire, new[] { (ItemIds.CopperOre, 1) }, ItemIds.Copper, 1);
            Add("bonfire_grilled_food", "생선구이", FacilityType.Campfire, new[] { (ItemIds.Fish, 1) }, ItemIds.GrilledFood, 1);

            // ── 초고온 용광로 ───────────────────────────────────
            Add("furnace_pure_platinum", "순수 백금 제련", FacilityType.SuperheatedFurnace, new[] { (ItemIds.PlatinumOre, 1) }, ItemIds.PurePlatinum, 1);

            // ── 숫돌 연마대 ─────────────────────────────────────
            Add("grind_copper_blade", "구리 절삭날 연마", FacilityType.Grindstone, new[] { (ItemIds.Copper, 1) }, ItemIds.CopperBlade, 1);
            Add("grind_reinforced_iron_blade", "강화 철제 날 연마", FacilityType.Grindstone, new[] { (ItemIds.Iron, 1) }, ItemIds.ReinforcedIronBlade, 1);

            // ── 롤러 프레스기 ───────────────────────────────────
            Add("press_reinforced_wood_panel", "강화 목재 패널 압착", FacilityType.RollerPress, new[] { (ItemIds.Wood, 1) }, ItemIds.ReinforcedWoodPanel, 1);
            Add("press_stone_powder", "돌가루 압쇄", FacilityType.RollerPress, new[] { (ItemIds.Stone, 1) }, ItemIds.StonePowder, 1);
            Add("press_armor_plate", "방호 장갑판 압연", FacilityType.RollerPress, new[] { (ItemIds.Iron, 1) }, ItemIds.ArmorPlate, 1);

            // ── 절삭기 ──────────────────────────────────────────
            Add("cut_wood_handle", "원목 손잡이 가공", FacilityType.PrecisionCutter, new[] { (ItemIds.Wood, 1) }, ItemIds.WoodHandle, 1);
            Add("cut_mechanical_trigger", "기계식 트리거 가공", FacilityType.PrecisionCutter, new[] { (ItemIds.Copper, 1) }, ItemIds.MechanicalTrigger, 1);
            Add("cut_high_voltage_cable", "고전압 케이블 가공", FacilityType.PrecisionCutter, new[] { (ItemIds.Copper, 1) }, ItemIds.HighVoltageCable, 1);
            Add("cut_precision_barrel", "정밀 총열 가공", FacilityType.PrecisionCutter, new[] { (ItemIds.Iron, 1) }, ItemIds.PrecisionBarrel, 1);
            Add("cut_platinum_gear", "백금 정밀 기어 가공", FacilityType.PrecisionCutter, new[] { (ItemIds.PurePlatinum, 1) }, ItemIds.PlatinumGear, 1);
            Add("cut_diamond_cutting_tip", "다이아 커팅 팁 가공", FacilityType.PrecisionCutter, new[] { (ItemIds.Diamond, 1) }, ItemIds.DiamondCuttingTip, 1);

            // ── 화학 정제탑 ─────────────────────────────────────
            Add("chem_superconductor_catalyst", "초전도 촉매 정제", FacilityType.ChemicalRefinery, new[] { (ItemIds.PurePlatinum, 1) }, ItemIds.SuperconductorCatalyst, 1);
            Add("chem_eternal_energy_core", "영구 에너지 코어 정제", FacilityType.ChemicalRefinery, new[] { (ItemIds.Diamond, 1) }, ItemIds.EternalEnergyCore, 1);
            Add("chem_napalm_gel", "네이팜 젤 정제", FacilityType.ChemicalRefinery, new[] { (ItemIds.Oil, 1) }, ItemIds.NapalmGel, 1);
            Add("chem_refined_fuel", "고효율 정제유 정제", FacilityType.ChemicalRefinery, new[] { (ItemIds.Oil, 1) }, ItemIds.RefinedFuel, 1);
            Add("chem_propellant_powder", "추진 화약 정제", FacilityType.ChemicalRefinery, new[] { (ItemIds.Oil, 1) }, ItemIds.PropellantPowder, 1);
            Add("chem_medical_extract", "의료용 추출액 정제", FacilityType.ChemicalRefinery, new[] { (ItemIds.Herb, 1) }, ItemIds.MedicalExtract, 1);
            Add("chem_neurotoxin_extract", "신경 독소 원액 정제", FacilityType.ChemicalRefinery, new[] { (ItemIds.PoisonHerb, 1) }, ItemIds.NeurotoxinExtract, 1);

            // ── 가죽 무두질 건조대 ──────────────────────────────
            Add("tan_tanned_leather", "경화 가공 가죽 무두질", FacilityType.LeatherTanningRack, new[] { (ItemIds.WetHide, 1) }, ItemIds.TannedLeather, 1);
            Add("tan_jerky", "전투용 육포 건조", FacilityType.LeatherTanningRack, new[] { (ItemIds.Meat, 1) }, ItemIds.Jerky, 1);
            Add("tan_stimulant_powder", "자극 농축 분말 건조", FacilityType.LeatherTanningRack, new[] { (ItemIds.Herb, 1) }, ItemIds.StimulantPowder, 1);
            Add("tan_lure_pheromone", "독성 유인 페로몬 추출", FacilityType.LeatherTanningRack, new[] { (ItemIds.PoisonHerb, 1) }, ItemIds.LurePheromone, 1);

            // ── 트럭 조립: 설비 ─────────────────────────────────
            Add("assemble_campfire", "모닥불 조립", FacilityType.None, new[] { (ItemIds.Wood, 30), (ItemIds.Stone, 4) }, ItemIds.Campfire, 1);
            Add("assemble_grindstone", "숫돌 연마대 조립", FacilityType.None, new[] { (ItemIds.Stone, 20), (ItemIds.Wood, 5) }, ItemIds.Grindstone, 1);
            Add("assemble_leather_tanning_rack", "가죽 무두질 건조대 조립", FacilityType.None, new[] { (ItemIds.Wood, 25), (ItemIds.Stone, 10), (ItemIds.CopperOre, 8) }, ItemIds.LeatherTanningRack, 1);
            Add("assemble_roller_press", "롤러 프레스기 조립", FacilityType.None, new[] { (ItemIds.Wood, 15), (ItemIds.Stone, 25), (ItemIds.CopperOre, 15) }, ItemIds.RollerPressMachine, 1);
            Add("assemble_precision_cutter", "절삭기 조립", FacilityType.None, new[] { (ItemIds.Wood, 25), (ItemIds.Stone, 20), (ItemIds.IronOre, 10) }, ItemIds.PrecisionCutterMachine, 1);
            Add("assemble_chemical_refinery", "화학 정제탑 조립", FacilityType.None, new[] { (ItemIds.Stone, 15), (ItemIds.CopperOre, 20), (ItemIds.Oil, 5) }, ItemIds.ChemicalRefineryTower, 1);
            Add("assemble_superheated_furnace", "초고온 용광로 조립", FacilityType.None, new[] { (ItemIds.Stone, 30), (ItemIds.IronOre, 20), (ItemIds.Oil, 4) }, ItemIds.SuperheatedFurnace, 1);

            // ── 트럭 조립: 도구 / 채집 ──────────────────────────
            Add("assemble_stone_pickaxe", "돌 곡괭이 조립", FacilityType.None, new[] { (ItemIds.Wood, 10), (ItemIds.Stone, 8) }, ItemIds.StonePickaxe, 1);
            Add("assemble_copper_pickaxe", "구리 곡괭이 조립", FacilityType.None, new[] { (ItemIds.CopperBlade, 1), (ItemIds.WoodHandle, 1) }, ItemIds.CopperPickaxe, 1);
            Add("assemble_iron_pickaxe", "철제 곡괭이 조립", FacilityType.None, new[] { (ItemIds.ReinforcedIronBlade, 1), (ItemIds.WoodHandle, 1), (ItemIds.Copper, 2) }, ItemIds.IronPickaxe, 1);
            Add("assemble_platinum_drill", "백금 착암 드릴 조립", FacilityType.None, new[] { (ItemIds.PlatinumGear, 1), (ItemIds.ReinforcedIronBlade, 1), (ItemIds.Iron, 3) }, ItemIds.PlatinumDrill, 1);
            Add("assemble_diamond_crusher", "다이아몬드 분쇄기 조립", FacilityType.None, new[] { (ItemIds.DiamondCuttingTip, 1), (ItemIds.PlatinumDrill, 1) }, ItemIds.DiamondCrusher, 1);
            Add("assemble_copper_hand_pump", "구리 수동 펌프 조립", FacilityType.None, new[] { (ItemIds.Copper, 4), (ItemIds.WoodHandle, 1) }, ItemIds.CopperHandPump, 1);

            // ── 트럭 조립: 근접 무기 ────────────────────────────
            Add("assemble_wood_spear", "나무창 조립", FacilityType.None, new[] { (ItemIds.Wood, 20), (ItemIds.Stone, 2) }, ItemIds.WoodSpear, 1);
            Add("assemble_hunting_bow", "사냥용 활 조립", FacilityType.None, new[] { (ItemIds.WoodHandle, 1), (ItemIds.TannedLeather, 1) }, ItemIds.HuntingBow, 1);
            Add("assemble_machete", "마체테 조립", FacilityType.None, new[] { (ItemIds.ReinforcedIronBlade, 1), (ItemIds.WoodHandle, 1) }, ItemIds.Machete, 1);
            Add("assemble_flame_machete", "화염 마체테 조립", FacilityType.None, new[] { (ItemIds.ReinforcedIronBlade, 1), (ItemIds.WoodHandle, 1), (ItemIds.NapalmGel, 1) }, ItemIds.FlameMachete, 1);
            Add("assemble_vibration_blade", "진동 블레이드 조립", FacilityType.None, new[] { (ItemIds.PurePlatinum, 1), (ItemIds.PlatinumGear, 1), (ItemIds.ReinforcedIronBlade, 1) }, ItemIds.VibrationBlade, 1);

            // ── 트럭 조립: 원거리 / 중화기 ──────────────────────
            Add("assemble_pistol", "권총 조립", FacilityType.None, new[] { (ItemIds.PrecisionBarrel, 1), (ItemIds.MechanicalTrigger, 1) }, ItemIds.Pistol, 1);
            Add("assemble_ak47", "AK-47 소총 조립", FacilityType.None, new[] { (ItemIds.PrecisionBarrel, 1), (ItemIds.ArmorPlate, 1), (ItemIds.MechanicalTrigger, 1) }, ItemIds.Ak47, 1);
            Add("assemble_platinum_sniper_rifle", "백금 저격소총 조립", FacilityType.None, new[] { (ItemIds.PlatinumGear, 1), (ItemIds.PrecisionBarrel, 1), (ItemIds.MechanicalTrigger, 1) }, ItemIds.PlatinumSniperRifle, 1);
            Add("assemble_flamethrower", "화염방사기 조립", FacilityType.None, new[] { (ItemIds.PrecisionBarrel, 1), (ItemIds.NapalmGel, 2), (ItemIds.WoodHandle, 1) }, ItemIds.Flamethrower, 1);
            Add("assemble_chemical_sprayer", "화학 살포기 조립", FacilityType.None, new[] { (ItemIds.PrecisionBarrel, 1), (ItemIds.NeurotoxinExtract, 2), (ItemIds.RefinedFuel, 1) }, ItemIds.ChemicalSprayer, 1);
            Add("assemble_shredder_drill_launcher", "파쇄 드릴 런처 조립", FacilityType.None, new[] { (ItemIds.ArmorPlate, 1), (ItemIds.PlatinumGear, 1), (ItemIds.RefinedFuel, 1) }, ItemIds.ShredderDrillLauncher, 1);

            // ── 트럭 조립: 대포 ─────────────────────────────────
            Add("assemble_iron_field_cannon", "철제 야포 조립", FacilityType.None, new[] { (ItemIds.ArmorPlate, 3), (ItemIds.PrecisionBarrel, 2), (ItemIds.PropellantPowder, 2) }, ItemIds.IronFieldCannon, 1);
            Add("assemble_platinum_rail_cannon", "백금 레일 캐논 조립", FacilityType.None, new[] { (ItemIds.HighVoltageCable, 2), (ItemIds.SuperconductorCatalyst, 1), (ItemIds.PrecisionBarrel, 2) }, ItemIds.PlatinumRailCannon, 1);

            // ── 트럭 조립: 수리 / 보강 ──────────────────────────
            Add("assemble_emergency_patch_board", "응급 덧댐 판자 조립", FacilityType.None, new[] { (ItemIds.Wood, 10), (ItemIds.Stone, 5) }, ItemIds.EmergencyPatchBoard, 1);
            Add("assemble_welding_kit", "철제 용접 키트 조립", FacilityType.None, new[] { (ItemIds.Iron, 2), (ItemIds.NapalmGel, 1) }, ItemIds.WeldingKit, 1);
            Add("assemble_high_tension_repair_pack", "고장력 수리 팩 조립", FacilityType.None, new[] { (ItemIds.ArmorPlate, 1), (ItemIds.MechanicalTrigger, 1) }, ItemIds.HighTensionRepairPack, 1);
            Add("assemble_truck_composite_armor", "트럭 복합 장갑 조립", FacilityType.None, new[] { (ItemIds.ArmorPlate, 2), (ItemIds.PlatinumGear, 1) }, ItemIds.TruckCompositeArmor, 1);
            Add("assemble_spike_bumper", "강철가시 범퍼 조립", FacilityType.None, new[] { (ItemIds.ReinforcedIronBlade, 4), (ItemIds.ArmorPlate, 2) }, ItemIds.SpikeBumper, 1);
            Add("assemble_grinder_wheel", "초진동 회전 분쇄 휠 조립", FacilityType.None, new[] { (ItemIds.SpikeBumper, 1), (ItemIds.PlatinumGear, 2), (ItemIds.RefinedFuel, 1) }, ItemIds.GrinderWheel, 1);

            // ── 트럭 조립: 생존 / 유틸 / 함정 ───────────────────
            Add("assemble_leather_gloves", "가죽 장갑 조립", FacilityType.None, new[] { (ItemIds.TannedLeather, 1), (ItemIds.Wood, 5) }, ItemIds.LeatherGloves, 1);
            Add("assemble_leather_pouch_backpack", "가죽 파우치 배낭 조립", FacilityType.None, new[] { (ItemIds.TannedLeather, 3), (ItemIds.StonePowder, 5), (ItemIds.Copper, 2) }, ItemIds.LeatherPouchBackpack, 1);
            Add("assemble_emergency_revival_kit", "응급 소생 키트 조립", FacilityType.None, new[] { (ItemIds.MedicalExtract, 2), (ItemIds.MechanicalTrigger, 1) }, ItemIds.EmergencyRevivalKit, 1);
            Add("assemble_combat_stimulant", "전투 각성제 조립", FacilityType.None, new[] { (ItemIds.StimulantPowder, 1), (ItemIds.MedicalExtract, 1) }, ItemIds.CombatStimulant, 1);
            Add("assemble_chemical_gas_grenade", "화학 독가스 수류탄 조립", FacilityType.None, new[] { (ItemIds.NeurotoxinExtract, 1), (ItemIds.StonePowder, 3), (ItemIds.PropellantPowder, 1) }, ItemIds.ChemicalGasGrenade, 1);
            Add("assemble_lure_trap", "유인 미끼 트랩 조립", FacilityType.None, new[] { (ItemIds.LurePheromone, 1), (ItemIds.StonePowder, 5) }, ItemIds.LureTrap, 1);

            return recipes;
        }

        /// <summary>
        /// Resources/Crafting/Icons/&lt;id&gt;.png 를 아이템별로 개별 로드한다.
        /// 그리드 슬라이싱 방식은 정렬 오차로 아이콘이 잘리는 문제가 있어 폐기했다.
        /// </summary>
        public static Dictionary<string, Sprite> BuildRuntimeSpritesFromSheet()
        {
            var result = new Dictionary<string, Sprite>();
            foreach (string id in IconItemIds)
            {
                Sprite sprite = Resources.Load<Sprite>($"{IconsResourcesPath}/{id}");
#if UNITY_EDITOR
                if (sprite == null)
                {
                    string assetPath = $"{IconsAssetFolder}/{id}.png";
                    sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                }
#endif
                if (sprite != null)
                    result[id] = sprite;
            }

            return result;
        }

        public static IReadOnlyList<string> GetIconItemIds() => IconItemIds;

        private static ItemData Make(
            string id,
            string name,
            ItemType type,
            int maxStack,
            Dictionary<string, Sprite> sprites,
            int toolTier = 0)
        {
            sprites.TryGetValue(id, out Sprite icon);
            if (icon == null)
                icon = CreateSolidSprite(id);
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
                case ItemIds.StonePowder: return new Color(0.6f, 0.6f, 0.62f);
                case ItemIds.WoodSpear: return new Color(0.5f, 0.4f, 0.25f);
                case ItemIds.Campfire: return new Color(0.95f, 0.35f, 0.12f);
                case ItemIds.Grindstone: return new Color(0.5f, 0.5f, 0.55f);
                case ItemIds.LeatherTanningRack: return new Color(0.6f, 0.45f, 0.3f);
                case ItemIds.RollerPressMachine: return new Color(0.55f, 0.75f, 0.25f);
                case ItemIds.PrecisionCutterMachine: return new Color(0.2f, 0.55f, 0.85f);
                case ItemIds.ChemicalRefineryTower: return new Color(0.35f, 0.75f, 0.45f);
                case ItemIds.SuperheatedFurnace: return new Color(0.95f, 0.55f, 0.1f);
                default: return new Color(0.5f, 0.5f, 0.55f);
            }
        }
    }
}
