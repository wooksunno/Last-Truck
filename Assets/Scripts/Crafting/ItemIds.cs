using System;

namespace CraftingSystem
{
    public static class ItemIds
    {
        // 원재료
        public const string Wood = "wood";
        public const string Stone = "stone";
        public const string IronOre = "iron_ore";
        public const string CopperOre = "copper_ore";
        public const string PlatinumOre = "platinum_ore";
        public const string Diamond = "diamond"; // 다이아몬드 원석
        public const string Oil = "oil";
        public const string Herb = "herb"; // 약초
        public const string PoisonHerb = "poison_herb"; // 독약초
        public const string Fish = "fish";
        public const string Meat = "meat";
        public const string WetHide = "wet_hide"; // 젖은 가죽

        // 1차 가공된 재료
        public const string Charcoal = "charcoal"; // 목탄
        public const string ReinforcedWoodPanel = "reinforced_wood_panel"; // 강화 목재 패널
        public const string Iron = "iron"; // 순수 철
        public const string Copper = "copper"; // 순수 구리
        public const string PurePlatinum = "pure_platinum"; // 순수 백금
        public const string StonePowder = "stone_powder"; // 돌가루
        public const string WoodHandle = "wood_handle"; // 원목 손잡이
        public const string CopperBlade = "copper_blade"; // 구리 절삭날
        public const string MechanicalTrigger = "mechanical_trigger"; // 기계식 트리거
        public const string HighVoltageCable = "high_voltage_cable"; // 고전압 케이블
        public const string PrecisionBarrel = "precision_barrel"; // 정밀 총열
        public const string ReinforcedIronBlade = "reinforced_iron_blade"; // 강화 철제 날
        public const string ArmorPlate = "armor_plate"; // 방호 장갑판
        public const string PlatinumGear = "platinum_gear"; // 백금 정밀 기어
        public const string SuperconductorCatalyst = "superconductor_catalyst"; // 초전도 촉매
        public const string DiamondCuttingTip = "diamond_cutting_tip"; // 다이아 커팅 팁
        public const string EternalEnergyCore = "eternal_energy_core"; // 영구 에너지 코어
        public const string NapalmGel = "napalm_gel"; // 네이팜 젤
        public const string RefinedFuel = "refined_fuel"; // 고효율 정제유
        public const string PropellantPowder = "propellant_powder"; // 추진 화약
        public const string MedicalExtract = "medical_extract"; // 의료용 추출액
        public const string StimulantPowder = "stimulant_powder"; // 자극 농축 분말
        public const string NeurotoxinExtract = "neurotoxin_extract"; // 신경 독소 원액
        public const string LurePheromone = "lure_pheromone"; // 독성 유인 페로몬
        public const string GrilledFood = "grilled_food"; // 생선구이/구운 고기
        public const string Jerky = "jerky"; // 전투용 육포
        public const string TannedLeather = "tanned_leather"; // 경화 가공 가죽

        // 설비(트럭에서 조립하는 가공 시설 아이템)
        public const string Campfire = "campfire"; // 모닥불
        public const string Grindstone = "grindstone"; // 숫돌 연마대
        public const string LeatherTanningRack = "leather_tanning_rack"; // 가죽 무두질 건조대
        public const string RollerPressMachine = "roller_press_machine"; // 롤러 프레스기
        public const string PrecisionCutterMachine = "precision_cutter_machine"; // 절삭기
        public const string ChemicalRefineryTower = "chemical_refinery_tower"; // 화학 정제탑
        public const string SuperheatedFurnace = "superheated_furnace"; // 초고온 용광로

        // 도구
        public const string WoodPickaxe = "wood_pickaxe";
        public const string StonePickaxe = "stone_pickaxe";
        public const string CopperPickaxe = "copper_pickaxe";
        public const string IronPickaxe = "iron_pickaxe";
        public const string PlatinumDrill = "platinum_drill"; // 백금 착암 드릴
        public const string DiamondCrusher = "diamond_crusher"; // 다이아몬드 분쇄기
        public const string CopperHandPump = "copper_hand_pump"; // 구리 수동 펌프

        // 근접 무기
        public const string WoodSpear = "wood_spear";
        public const string HuntingBow = "hunting_bow"; // 사냥용 활
        public const string Machete = "machete";
        public const string FlameMachete = "flame_machete"; // 화염 마체테
        public const string VibrationBlade = "vibration_blade"; // 진동 블레이드

        // 원거리/중화기
        public const string Pistol = "pistol"; // 권총
        public const string Ak47 = "ak47"; // AK-47 소총
        public const string PlatinumSniperRifle = "platinum_sniper_rifle"; // 백금 저격소총
        public const string Flamethrower = "flamethrower"; // 화염방사기
        public const string ChemicalSprayer = "chemical_sprayer"; // 화학 살포기
        public const string ShredderDrillLauncher = "shredder_drill_launcher"; // 파쇄 드릴 런처

        // 대포
        public const string IronFieldCannon = "iron_field_cannon"; // 철제 야포
        public const string PlatinumRailCannon = "platinum_rail_cannon"; // 백금 레일 캐논

        // 트럭 수리/보강
        public const string EmergencyPatchBoard = "emergency_patch_board"; // 응급 덧댐 판자
        public const string WeldingKit = "welding_kit"; // 철제 용접 키트
        public const string HighTensionRepairPack = "high_tension_repair_pack"; // 고장력 수리 팩
        public const string TruckCompositeArmor = "truck_composite_armor"; // 트럭 복합 장갑
        public const string SpikeBumper = "spike_bumper"; // 강철가시 범퍼
        public const string GrinderWheel = "grinder_wheel"; // 초진동 회전 분쇄 휠

        // 생존/유틸/함정
        public const string LeatherGloves = "leather_gloves"; // 가죽 장갑
        public const string LeatherPouchBackpack = "leather_pouch_backpack"; // 가죽 파우치 배낭
        public const string EmergencyRevivalKit = "emergency_revival_kit"; // 응급 소생 키트
        public const string CombatStimulant = "combat_stimulant"; // 전투 각성제
        public const string ChemicalGasGrenade = "chemical_gas_grenade"; // 화학 독가스 수류탄
        public const string LureTrap = "lure_trap"; // 유인 미끼 트랩

        // 맵 특산물 구역 전용 자원 (기존 유지)
        public const string Platinum = PlatinumOre;
    }

    public static class ItemMatching
    {
        public static bool IsSameItem(ItemData a, ItemData b)
        {
            if (a == null || b == null)
                return false;

            if (ReferenceEquals(a, b))
                return true;

            if (!string.IsNullOrEmpty(a.itemID) && !string.IsNullOrEmpty(b.itemID))
                return string.Equals(a.itemID, b.itemID, StringComparison.Ordinal);

            return false;
        }
    }
}
