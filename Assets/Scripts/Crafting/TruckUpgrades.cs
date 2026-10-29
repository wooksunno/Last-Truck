using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>트럭 파츠를 끼우는 자리.</summary>
    public enum TruckPartSlot { Engine = 0, Front = 1, Armor = 2, Roof = 3 }

    /// <summary>레벨로 강화하는 트럭/포탑 스탯 (기획서 7. 트럭 스탯, 6-1 포탑 스탯).</summary>
    public enum TruckStat { Durability = 0, Speed = 1, Light = 2, Attack = 3, TurretAttack = 4, TurretRate = 5, TurretRange = 6, TurretArea = 7 }

    /// <summary>
    /// 트럭 파츠/강화 규칙 (수치는 기획서 기준).
    ///  - 트럭 스탯: 내구도 600(+150) / 이동속도 80(+5) / 조명 20m(+5m) / 공격력 50(+10), 1~10레벨
    ///  - 포탑 스탯: 공격력 20(+5) / 공격속도 1.5(+0.2)/초 / 사거리 30m(+2m) / 피해 범위 3m(+0.4m)
    ///  - 강화 비용은 스탯과 상관없이 레벨마다 같다 ("공격력 LV2 비용 = 피해 범위 LV2 비용")
    ///  - 포탑 피해 = 포탑 공격력 + 탄환 데미지 (나무탄 25 / 돌탄 28 / 구리탄 32 / 철탄 36), 자원 1개 → 탄환 5발
    /// 지금 트럭은 기본 내구도/속도가 기획서 숫자와 단위가 달라서, "기획서 값 ÷ 1레벨 값" 배율로 원래 값에 곱한다
    /// (예: 내구도 2레벨 = 750/600 = 1.25배). 몬스터/조작 밸런스가 깨지지 않게 하기 위함.
    /// </summary>
    public static class TruckUpgradeRules
    {
        public const int MaxLevel = 10;
        public const int StatCount = 8;
        public const int SlotCount = 4;

        public static readonly string[] SlotNames = { "엔진", "전면", "장갑", "지붕" };
        public static readonly string[] StatNames = { "내구도", "이동속도", "조명 반경", "트럭 공격력", "포탑 공격력", "포탑 공격 속도", "포탑 사거리", "포탑 피해 범위" };

        public static bool IsTurretStat(TruckStat s) => s >= TruckStat.TurretAttack;

        /// <summary>기획서 수치.</summary>
        public static float Value(TruckStat s, int level)
        {
            int n = Mathf.Clamp(level, 1, MaxLevel) - 1;
            switch (s)
            {
                case TruckStat.Durability: return 600f + 150f * n;
                case TruckStat.Speed: return 80f + 5f * n;
                case TruckStat.Light: return 20f + 5f * n;
                case TruckStat.Attack: return 50f + 10f * n;
                case TruckStat.TurretAttack: return 20f + 5f * n;
                case TruckStat.TurretRate: return 1.5f + 0.2f * n;
                case TruckStat.TurretRange: return 30f + 2f * n;
                case TruckStat.TurretArea: return 3f + 0.4f * n;
            }
            return 0f;
        }

        public static string Format(TruckStat s, float v)
        {
            switch (s)
            {
                case TruckStat.Light:
                case TruckStat.TurretRange:
                case TruckStat.TurretArea: return v.ToString("0.#") + "m";
                case TruckStat.TurretRate: return v.ToString("0.0") + "/초";
                default: return v.ToString("0");
            }
        }

        /// <summary>현재 레벨 → 다음 레벨 강화 비용 (모든 스탯 동일). 트럭 보관함에서 낸다.</summary>
        public static List<RecipeIngredient> LevelCost(int currentLevel)
        {
            var catalog = ItemCatalog.GetOrCreate();
            var cost = new List<RecipeIngredient>();
            void Add(string id, int n) { var it = catalog.GetItem(id); if (it != null && n > 0) cost.Add(new RecipeIngredient(it, n)); }
            switch (currentLevel)
            {
                case 1: Add(ItemIds.Wood, 20); Add(ItemIds.Stone, 15); break;
                case 2: Add(ItemIds.Wood, 30); Add(ItemIds.Stone, 25); Add(ItemIds.Copper, 2); break;
                case 3: Add(ItemIds.Wood, 40); Add(ItemIds.Stone, 35); Add(ItemIds.Copper, 4); break;
                case 4: Add(ItemIds.Stone, 40); Add(ItemIds.Iron, 4); Add(ItemIds.Copper, 4); break;
                case 5: Add(ItemIds.Stone, 50); Add(ItemIds.Iron, 6); Add(ItemIds.ReinforcedWoodPanel, 4); break;
                case 6: Add(ItemIds.Iron, 8); Add(ItemIds.PurePlatinum, 2); Add(ItemIds.ReinforcedWoodPanel, 6); break;
                case 7: Add(ItemIds.Iron, 10); Add(ItemIds.PurePlatinum, 3); Add(ItemIds.MechanicalTrigger, 2); break;
                case 8: Add(ItemIds.PurePlatinum, 5); Add(ItemIds.MechanicalTrigger, 3); Add(ItemIds.RefinedFuel, 2); break;
                case 9: Add(ItemIds.PurePlatinum, 6); Add(ItemIds.DiamondCuttingTip, 1); Add(ItemIds.RefinedFuel, 3); break;
            }
            return cost;
        }

        // ───── 파츠 ─────
        public static bool TryGetSlot(string itemId, out TruckPartSlot slot)
        {
            switch (itemId)
            {
                case ItemIds.TunedEngine: slot = TruckPartSlot.Engine; return true;
                case ItemIds.SpikeBumper:
                case ItemIds.GrinderWheel: slot = TruckPartSlot.Front; return true;
                case ItemIds.TruckCompositeArmor: slot = TruckPartSlot.Armor; return true;
                case ItemIds.MiniTurret: slot = TruckPartSlot.Roof; return true;
            }
            slot = TruckPartSlot.Engine;
            return false;
        }

        public static bool IsPart(string itemId) => TryGetSlot(itemId, out _);

        public static string PartEffect(string itemId)
        {
            switch (itemId)
            {
                case ItemIds.TunedEngine: return "이동속도·가속 +25%";
                case ItemIds.SpikeBumper: return "좀비를 밀쳐내고 들이받으면 트럭 공격력만큼 피해 (로드킬)";
                case ItemIds.GrinderWheel: return "더 세게 밀치고 들이받는 피해 2배";
                case ItemIds.TruckCompositeArmor: return "최대 내구도 +25%, 받는 피해 -25%";
                case ItemIds.MiniTurret: return "동승석 1번이 포탑 사수 (마우스 조준 · 좌클릭 발사 · R 탄환 만들기)";
            }
            return "";
        }

        public const float EngineBoost = 1.25f;
        public const float ArmorDurabilityBonus = 0.25f;
        public const float ArmorDamageTaken = 0.75f;

        /// <summary>전면 파츠에 따른 들이받기 피해 배율 / 밀치는 거리(m). 파츠가 없으면 조금 밀치기만 한다.</summary>
        public static void FrontRam(string frontPart, out float damageMul, out float pushMeters)
        {
            switch (frontPart)
            {
                case ItemIds.SpikeBumper: damageMul = 1f; pushMeters = 3.5f; return;
                case ItemIds.GrinderWheel: damageMul = 2f; pushMeters = 5f; return;
                default: damageMul = 0.2f; pushMeters = 2f; return;
            }
        }

        // ───── 탄환 ─────
        public struct Ammo { public string id, resourceId; public int damage; }
        /// <summary>좋은 탄환부터 (포탑은 가장 좋은 탄환을 먼저 쓴다).</summary>
        public static readonly Ammo[] AmmoTypes =
        {
            new Ammo { id = ItemIds.IronRound, resourceId = ItemIds.IronOre, damage = 36 },
            new Ammo { id = ItemIds.CopperRound, resourceId = ItemIds.CopperOre, damage = 32 },
            new Ammo { id = ItemIds.StoneRound, resourceId = ItemIds.Stone, damage = 28 },
            new Ammo { id = ItemIds.WoodRound, resourceId = ItemIds.Wood, damage = 25 },
        };
        public const int RoundsPerResource = 5;
    }

    /// <summary>
    /// 트럭에 붙어서 파츠/강화 레벨을 들고 있고, 그 효과를 트럭(내구도·속도·조명)에 적용한다.
    /// 싱글플레이: 여기서 바로 바꾼다. 멀티플레이: 호스트가 바꾸고 NetworkTruck이 모두에게 같은 값을 넣어 준다.
    /// TruckInformation이 자동으로 붙인다.
    /// </summary>
    public class TruckUpgradeState : MonoBehaviour
    {
        private readonly string[] _parts = new string[TruckUpgradeRules.SlotCount];
        private readonly int[] _levels = { 1, 1, 1, 1, 1, 1, 1, 1 };

        private LastTruck.TruckInformation _info;
        private LastTruck.TruckMove _move;
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _baseLightRanges = new List<float>();
        private float _baseMaxDurability, _baseMaxSpeed, _baseAcceleration;
        private bool _captured;

        public event Action Changed;
        public static TruckUpgradeState Instance { get; private set; }

        public string PartAt(TruckPartSlot slot) => _parts[(int)slot];
        public int LevelOf(TruckStat stat) => _levels[(int)stat];
        public bool HasTurret => _parts[(int)TruckPartSlot.Roof] == ItemIds.MiniTurret;

        public float StatValue(TruckStat stat) => TruckUpgradeRules.Value(stat, LevelOf(stat));

        /// <summary>트럭 들이받기 피해 (트럭 공격력 × 전면 파츠 배율).</summary>
        public float RamDamage
        {
            get { TruckUpgradeRules.FrontRam(PartAt(TruckPartSlot.Front), out float mul, out _); return StatValue(TruckStat.Attack) * mul; }
        }

        private void Awake()
        {
            Instance = this;
            CaptureBase();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void CaptureBase()
        {
            if (_captured) return;
            _info = GetComponent<LastTruck.TruckInformation>();
            _move = GetComponent<LastTruck.TruckMove>();
            _baseMaxDurability = _info != null ? _info.MaxDurability : 100f;
            if (_move != null) { _baseMaxSpeed = _move.maxSpeed; _baseAcceleration = _move.maxAcceleration; }
            var tl = GetComponentInChildren<LastTruck.TruckLight>(true);
            foreach (var l in GetComponentsInChildren<Light>(true))
            {
                if (l.type != LightType.Spot && l.type != LightType.Point) continue;
                if (l.GetComponentInParent<TruckTurret>() != null) continue;
                _lights.Add(l); _baseLightRanges.Add(l.range);
            }
            _captured = true;
        }

        /// <summary>네트워크/저장된 값을 통째로 넣는다. 바뀐 게 있으면 효과를 다시 적용한다.</summary>
        public void SetState(string[] parts, int[] levels)
        {
            bool changed = false;
            for (int i = 0; i < _parts.Length; i++)
            {
                string p = parts != null && i < parts.Length && !string.IsNullOrEmpty(parts[i]) ? parts[i] : null;
                if (_parts[i] != p) { _parts[i] = p; changed = true; }
            }
            for (int i = 0; i < _levels.Length; i++)
            {
                int l = levels != null && i < levels.Length ? Mathf.Clamp(levels[i], 1, TruckUpgradeRules.MaxLevel) : 1;
                if (_levels[i] != l) { _levels[i] = l; changed = true; }
            }
            if (changed) { ApplyEffects(); Changed?.Invoke(); }
        }

        public string[] PartsCopy() => (string[])_parts.Clone();
        public int[] LevelsCopy() => (int[])_levels.Clone();

        private void ApplyEffects()
        {
            CaptureBase();
            bool armor = PartAt(TruckPartSlot.Armor) == ItemIds.TruckCompositeArmor;
            bool engine = PartAt(TruckPartSlot.Engine) == ItemIds.TunedEngine;

            if (_info != null)
            {
                float mul = TruckUpgradeRules.Value(TruckStat.Durability, LevelOf(TruckStat.Durability)) / TruckUpgradeRules.Value(TruckStat.Durability, 1);
                _info.SetMaxDurability(_baseMaxDurability * mul * (armor ? 1f + TruckUpgradeRules.ArmorDurabilityBonus : 1f));
                _info.DamageTakenMultiplier = armor ? TruckUpgradeRules.ArmorDamageTaken : 1f;
            }
            if (_move != null)
            {
                float mul = TruckUpgradeRules.Value(TruckStat.Speed, LevelOf(TruckStat.Speed)) / TruckUpgradeRules.Value(TruckStat.Speed, 1);
                if (engine) mul *= TruckUpgradeRules.EngineBoost;
                _move.maxSpeed = _baseMaxSpeed * mul;
                _move.maxAcceleration = _baseAcceleration * mul;
            }
            float lightMul = TruckUpgradeRules.Value(TruckStat.Light, LevelOf(TruckStat.Light)) / TruckUpgradeRules.Value(TruckStat.Light, 1);
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i] != null) _lights[i].range = _baseLightRanges[i] * lightMul;
        }

        // ───── 실제 변경 (싱글플레이 또는 호스트에서만 부른다) ─────

        /// <summary>트럭 보관함에 있는 파츠를 끼운다. 원래 끼워져 있던 파츠는 보관함으로 돌아간다.</summary>
        public bool TryInstall(ItemData part, TruckInventory truck, out string message)
        {
            message = null;
            if (part == null || truck == null || !TruckUpgradeRules.TryGetSlot(part.itemID, out TruckPartSlot slot))
            { message = "트럭에 끼울 수 없는 아이템이에요."; return false; }
            if (PartAt(slot) == part.itemID) { message = "이미 같은 파츠가 끼워져 있어요."; return false; }
            if (truck.GetItemCount(part) < 1) { message = "트럭 보관함에 그 파츠가 없어요."; return false; }

            string old = PartAt(slot);
            truck.RemoveItem(part, 1);
            if (!string.IsNullOrEmpty(old))
            {
                var oldItem = ItemCatalog.GetOrCreate().GetItem(old);
                if (oldItem != null) truck.AddItem(oldItem, 1);
            }
            _parts[(int)slot] = part.itemID;
            ApplyEffects(); Changed?.Invoke();
            message = $"{TruckUpgradeRules.SlotNames[(int)slot]} 자리에 {part.itemName}을(를) 장착했어요.";
            return true;
        }

        /// <summary>파츠를 빼서 트럭 보관함에 넣는다.</summary>
        public bool TryRemove(TruckPartSlot slot, TruckInventory truck, out string message)
        {
            message = null;
            string old = PartAt(slot);
            if (string.IsNullOrEmpty(old) || truck == null) { message = "빼낼 파츠가 없어요."; return false; }
            var item = ItemCatalog.GetOrCreate().GetItem(old);
            if (item == null || !truck.AddItem(item, 1)) { message = "트럭 보관함에 자리가 없어요."; return false; }
            _parts[(int)slot] = null;
            ApplyEffects(); Changed?.Invoke();
            message = $"{item.itemName}을(를) 빼서 트럭 보관함에 넣었어요.";
            return true;
        }

        /// <summary>스탯 한 단계 강화 (트럭 보관함에서 비용을 낸다).</summary>
        public bool TryUpgrade(TruckStat stat, TruckInventory truck, out string message)
        {
            message = null;
            int level = LevelOf(stat);
            if (level >= TruckUpgradeRules.MaxLevel) { message = "이미 최대 레벨이에요."; return false; }
            if (TruckUpgradeRules.IsTurretStat(stat) && !HasTurret) { message = "미니 포탑을 장착해야 포탑을 강화할 수 있어요."; return false; }
            var cost = TruckUpgradeRules.LevelCost(level);
            if (!TruckFacilities.CanAfford(truck, cost)) { message = "트럭 보관함에 재료가 부족해요."; return false; }
            TruckFacilities.Pay(truck, cost);
            _levels[(int)stat] = level + 1;
            ApplyEffects(); Changed?.Invoke();
            message = $"{TruckUpgradeRules.StatNames[(int)stat]} Lv.{level + 1}  ({TruckUpgradeRules.Format(stat, StatValue(stat))})";
            return true;
        }
    }
}
