using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 가공 설비 규칙.
    ///  - 설비는 트럭에서 "제작"해야 생긴다 (설비 아이템이 트럭 인벤토리에 있으면 설치된 것). 설비가 하나도 없으면 가공 탭이 닫혀 있다.
    ///  - 설비마다 가공 칸이 있다. 처음에는 1칸이고, 설비를 만든 재료 × (현재 칸 수) 를 내면 1칸씩 늘릴 수 있다 (최대 MaxSlots).
    ///  - 칸마다 따로 가공한다 (가공 재료를 고르고 → 빈 칸을 누르면 시작).
    /// </summary>
    public static class TruckFacilities
    {
        public const int MaxSlots = 6;

        /// <summary>가공 탭 순서 = 멀티플레이 대기열 번호. (최대 8개까지 네트워크 상태에 들어간다)</summary>
        public static readonly FacilityType[] All =
        {
            FacilityType.Campfire,
            FacilityType.Grindstone,
            FacilityType.RollerPress,
            FacilityType.PrecisionCutter,
            FacilityType.ChemicalRefinery,
            FacilityType.SuperheatedFurnace,
            FacilityType.LeatherTanningRack,
        };

        public static int IndexOf(FacilityType type) => Array.IndexOf(All, type);

        public static string ItemIdOf(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Campfire: return ItemIds.Campfire;
                case FacilityType.Grindstone: return ItemIds.Grindstone;
                case FacilityType.RollerPress: return ItemIds.RollerPressMachine;
                case FacilityType.PrecisionCutter: return ItemIds.PrecisionCutterMachine;
                case FacilityType.ChemicalRefinery: return ItemIds.ChemicalRefineryTower;
                case FacilityType.SuperheatedFurnace: return ItemIds.SuperheatedFurnace;
                case FacilityType.LeatherTanningRack: return ItemIds.LeatherTanningRack;
                default: return null;
            }
        }

        public static ItemData ItemOf(FacilityType type)
        {
            string id = ItemIdOf(type);
            return id != null ? ItemCatalog.GetOrCreate().GetItem(id) : null;
        }

        public static bool IsFacilityItem(ItemData item)
        {
            if (item == null) return false;
            foreach (FacilityType f in All)
                if (ItemIdOf(f) == item.itemID) return true;
            return false;
        }

        /// <summary>item이 설비이고 이미 트럭에 설치(제작)되어 있는가.</summary>
        public static bool IsInstalledFacilityItem(TruckInventory truck, ItemData item)
        {
            return truck != null && IsFacilityItem(item) && truck.GetItemCount(item) > 0;
        }

        /// <summary>이 설비가 트럭에 설치(제작)되어 있는가.</summary>
        public static bool IsBuilt(TruckInventory truck, FacilityType type)
        {
            ItemData item = ItemOf(type);
            return truck != null && item != null && truck.GetItemCount(item) > 0;
        }

        public static bool AnyBuilt(TruckInventory truck)
        {
            foreach (FacilityType f in All)
                if (IsBuilt(truck, f)) return true;
            return false;
        }

        /// <summary>설비를 만드는 조립 레시피 (없으면 null).</summary>
        public static RecipeData AssemblyRecipe(FacilityType type)
        {
            string id = ItemIdOf(type);
            if (id == null) return null;

            foreach (RecipeData recipe in ItemCatalog.GetOrCreate().Recipes)
            {
                if (recipe != null && recipe.requiredFacility == FacilityType.None && recipe.output != null &&
                    recipe.output.item != null && recipe.output.item.itemID == id)
                    return recipe;
            }
            return null;
        }

        /// <summary>칸 하나를 늘리는 비용 = 설비를 만든 재료 × 현재 칸 수.</summary>
        public static List<RecipeIngredient> UpgradeCost(FacilityType type, int currentSlots)
        {
            var cost = new List<RecipeIngredient>();
            RecipeData recipe = AssemblyRecipe(type);
            if (recipe == null || recipe.inputs == null) return cost;

            int n = Mathf.Max(1, currentSlots);
            foreach (RecipeIngredient ing in recipe.inputs)
            {
                if (ing != null && ing.item != null && ing.count > 0)
                    cost.Add(new RecipeIngredient(ing.item, ing.count * n));
            }
            return cost;
        }

        public static bool CanAfford(TruckInventory truck, List<RecipeIngredient> cost)
        {
            if (truck == null || cost == null || cost.Count == 0) return false;
            foreach (RecipeIngredient ing in cost)
                if (truck.GetMaterialCount(ing.item) < ing.count) return false;
            return true;
        }

        /// <summary>재료가 충분하면 빼고 true.</summary>
        public static bool Pay(TruckInventory truck, List<RecipeIngredient> cost)
        {
            if (!CanAfford(truck, cost)) return false;
            foreach (RecipeIngredient ing in cost)
                truck.RemoveMaterial(ing.item, ing.count);
            return true;
        }

        /// <summary>레시피 재료를 돌려준다(가공 취소).</summary>
        public static void Refund(TruckInventory truck, RecipeData recipe)
        {
            if (truck == null || recipe == null || recipe.inputs == null) return;
            foreach (RecipeIngredient ing in recipe.inputs)
            {
                if (ing != null && ing.item != null && ing.count > 0)
                    truck.AddItem(ing.item, ing.count);
            }
        }
    }

    /// <summary>화면에 보여줄 가공 칸 하나의 상태.</summary>
    public struct ProcessSlotView
    {
        public RecipeData recipe;     // null이면 빈 칸
        public float remaining;       // 남은 시간(초)
        public float total;           // 한 개 가공 시간(초)
        public bool ready;            // 가공이 끝나 받을 수 있음

        public bool IsEmpty => recipe == null;
        public bool IsBusy => recipe != null && !ready;
    }

    /// <summary>
    /// 싱글플레이 트럭 가공 상태 (설비별 칸 수 + 칸마다 따로 도는 타이머).
    /// 멀티플레이는 호스트가 같은 구조를 NetworkTruckProcessing으로 관리한다.
    /// </summary>
    public class TruckProcessingLocal
    {
        private readonly int[] _counts = new int[TruckFacilities.All.Length];
        private readonly ProcessSlotView[][] _slots;

        /// <summary>칸 상태가 바뀔 때마다 1씩 오른다 (UI가 다시 그릴 때 사용).</summary>
        public int Revision { get; private set; }

        public TruckProcessingLocal()
        {
            _slots = new ProcessSlotView[TruckFacilities.All.Length][];
            for (int i = 0; i < _slots.Length; i++)
            {
                _counts[i] = 1;
                _slots[i] = new ProcessSlotView[TruckFacilities.MaxSlots];
            }
        }

        public int SlotCount(int facility) => InRange(facility) ? _counts[facility] : 1;

        public ProcessSlotView Slot(int facility, int index)
        {
            if (!InRange(facility) || index < 0 || index >= TruckFacilities.MaxSlots) return default;
            return _slots[facility][index];
        }

        public int BusyCount(int facility)
        {
            int n = 0;
            for (int i = 0; i < SlotCount(facility); i++) if (_slots[facility][i].IsBusy) n++;
            return n;
        }

        public int ReadyCount(int facility)
        {
            int n = 0;
            for (int i = 0; i < SlotCount(facility); i++) if (_slots[facility][i].ready) n++;
            return n;
        }

        /// <summary>빈 칸에 가공을 시작한다.</summary>
        public bool Place(int facility, int index, RecipeData recipe, float seconds)
        {
            if (!InRange(facility) || recipe == null || index < 0 || index >= _counts[facility]) return false;
            if (!_slots[facility][index].IsEmpty) return false;

            float s = Mathf.Max(0.05f, seconds);
            _slots[facility][index] = new ProcessSlotView { recipe = recipe, remaining = s, total = s, ready = false };
            Revision++;
            return true;
        }

        /// <summary>가공 중인 칸을 비운다 (재료 환불은 호출한 쪽에서). 가공 중이 아니면 false.</summary>
        public bool Cancel(int facility, int index, out RecipeData recipe)
        {
            recipe = null;
            if (!InRange(facility) || index < 0 || index >= TruckFacilities.MaxSlots) return false;
            ProcessSlotView s = _slots[facility][index];
            if (!s.IsBusy) return false;

            recipe = s.recipe;
            _slots[facility][index] = default;
            Revision++;
            return true;
        }

        /// <summary>끝난 칸의 결과물을 받으려고 비운다 (결과물 지급은 호출한 쪽에서).</summary>
        public bool Claim(int facility, int index, out RecipeData recipe)
        {
            recipe = null;
            if (!InRange(facility) || index < 0 || index >= TruckFacilities.MaxSlots) return false;
            ProcessSlotView s = _slots[facility][index];
            if (!s.ready) return false;

            recipe = s.recipe;
            _slots[facility][index] = default;
            Revision++;
            return true;
        }

        /// <summary>칸을 1개 늘린다 (비용 지불은 호출한 쪽에서).</summary>
        public bool AddSlot(int facility)
        {
            if (!InRange(facility) || _counts[facility] >= TruckFacilities.MaxSlots) return false;
            _counts[facility]++;
            Revision++;
            return true;
        }

        public void Tick(float dt)
        {
            for (int f = 0; f < _slots.Length; f++)
            {
                for (int i = 0; i < _counts[f]; i++)
                {
                    ProcessSlotView s = _slots[f][i];
                    if (!s.IsBusy) continue;

                    s.remaining -= dt;
                    if (s.remaining <= 0f)
                    {
                        s.remaining = 0f;
                        s.ready = true;
                        Revision++;
                    }
                    _slots[f][i] = s;
                }
            }
        }

        private bool InRange(int facility) => facility >= 0 && facility < _slots.Length;
    }
}
