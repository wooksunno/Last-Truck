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
        public const string RottenMeat = "rotten_meat"; // 썩은 고기 (몬스터 전리품, 약초와 합치면 고기)
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
        public const string Steak = "steak"; // 스테이크
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
        public const string OilPumpjack = "oil_pumpjack"; // 석유 펌프잭 (기름 웅덩이에 설치)

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
        public const string TunedEngine = "tuned_engine"; // 개조 엔진 (트럭 파츠: 엔진)
        public const string FishingRod = "fishing_rod"; // 낚싯대 (물가에서 왼쪽 클릭으로 던진다)
        public const string MiniTurret = "mini_turret"; // 미니 포탑 (트럭 파츠: 지붕)

        // 포탑 탄환 (자원 1개 → 5발)
        public const string WoodRound = "wood_round"; // 나무탄
        public const string StoneRound = "stone_round"; // 돌탄
        public const string CopperRound = "copper_round"; // 구리탄
        public const string IronRound = "iron_round"; // 철탄

        // 생존/유틸/함정
        public const string LeatherGloves = "leather_gloves"; // 가죽 장갑
        public const string LeatherPouchBackpack = "leather_pouch_backpack"; // 가죽 파우치 배낭
        public const string EmergencyRevivalKit = "emergency_revival_kit"; // 응급 소생 키트
        public const string CombatStimulant = "combat_stimulant"; // 전투 각성제
        public const string ChemicalGasGrenade = "chemical_gas_grenade"; // 화학 독가스 수류탄
        public const string FragGrenade = "frag_grenade"; // 일반 수류탄
        public const string LureTrap = "lure_trap"; // 유인 미끼 트랩
        // 나무/돌 소비처 (나무·돌을 계속 쓰게 만드는 아이템)
        public const string ThrowingStone = "throwing_stone"; // 투척용 돌멩이
        public const string WoodSpikeTrap = "wood_spike_trap"; // 나무 가시 덫
        public const string Torch = "torch"; // 횃불 (손에 들면 주변을 밝힌다)
        public const string WoodBarricade = "wood_barricade"; // 나무 바리케이드 (몬스터 길막)
        public const string StoneWall = "stone_wall"; // 돌 방벽 (몬스터 길막, 더 오래 감)

        // 포탑 (트럭 지붕 파츠). MiniTurret이 기본형이고 나머지는 성격이 다른 상위/특화형 (TruckUpgradeRules.Turrets)
        public const string GatlingTurret = "gatling_turret"; // 기관포 포탑: 빠른 연사
        public const string SniperTurret = "sniper_turret"; // 저격 포탑: 느리지만 아주 강하고 멀리
        public const string MortarTurret = "mortar_turret"; // 박격포 포탑: 넓은 범위 폭발
        public const string FlameTurret = "flame_turret"; // 화염 포탑: 가까이 불바다
        public const string RailTurret = "rail_turret"; // 레일 포탑: 일직선 관통 (최종)

        // 자동 포탑 (트럭 적재함 파츠). 위 포탑을 재료로 쓰고 첨단 부품이 더 들어가는 상위 버전: 사수 없이 가까운 적을 알아서 쏜다.
        // id는 항상 "auto_" + 수동 포탑 id (TruckUpgradeRules가 이 규칙으로 수치를 만든다)
        public const string AutoMiniTurret = "auto_mini_turret";
        public const string AutoGatlingTurret = "auto_gatling_turret";
        public const string AutoSniperTurret = "auto_sniper_turret";
        public const string AutoMortarTurret = "auto_mortar_turret";
        public const string AutoFlameTurret = "auto_flame_turret";
        public const string AutoRailTurret = "auto_rail_turret";

        // 트럭 파츠 (상위)
        public const string TurboEngine = "turbo_engine"; // 터보 엔진 (엔진 자리, 최종)
        public const string AlloyArmor = "alloy_armor"; // 합금 장갑 (장갑 자리, 최종)

        // 음식 / 소모품 / 수리
        public const string HerbPorridge = "herb_porridge"; // 약초 죽
        public const string HeartyStew = "hearty_stew"; // 든든한 스튜
        public const string HerbBandage = "herb_bandage"; // 약초 붕대
        public const string VitalityTonic = "vitality_tonic"; // 활력 토닉 (최종 소모품)
        public const string ToolRepairKit = "tool_repair_kit"; // 도구 수리 키트

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
