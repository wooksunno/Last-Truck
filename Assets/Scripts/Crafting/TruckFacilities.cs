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
        public static void Refund(TruckInventory truck, RecipeData recipe) => Refund(truck, recipe, null);

        /// <summary>
        /// 레시피 재료를 돌려준다. saved: 능력 할인으로 덜 낸 개수(재료 순서대로, null이면 할인 없음).
        /// 낸 만큼만 돌려줘야 취소로 재료가 늘어나지 않는다.
        /// </summary>
        public static void Refund(TruckInventory truck, RecipeData recipe, int[] saved)
        {
            if (truck == null || recipe == null || recipe.inputs == null) return;
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                RecipeIngredient ing = recipe.inputs[i];
                if (ing == null || ing.item == null || ing.count <= 0) continue;
                int back = ing.count - (saved != null && i < saved.Length ? saved[i] : 0);
                if (back > 0) truck.AddItem(ing.item, back);
            }
        }

        /// <summary>
        /// 이 사람이 레시피를 1회 만들 때 실제로 내는 재료 (요리사 음식 할인 / 대장장이 무기 할인 반영).
        /// ability가 null이면 레시피 수량 그대로.
        /// </summary>
        public static List<RecipeIngredient> CostFor(RecipeData recipe, LastTruck.CharacterAbilityController ability)
        {
            var cost = new List<RecipeIngredient>();
            if (recipe == null || recipe.inputs == null) return cost;
            ItemData output = recipe.output != null ? recipe.output.item : null;
            foreach (RecipeIngredient ing in recipe.inputs)
            {
                if (ing == null || ing.item == null || ing.count <= 0) continue;
                int need = ability != null ? ability.GetCalculatedCraftingCost(ing.count, output) : ing.count;
                cost.Add(new RecipeIngredient(ing.item, Mathf.Max(1, need)));
            }
            return cost;
        }

        /// <summary>능력 할인으로 덜 내는 개수 (재료 순서대로). 할인이 하나도 없으면 null.</summary>
        public static int[] Savings(RecipeData recipe, LastTruck.CharacterAbilityController ability)
        {
            if (recipe == null || recipe.inputs == null || ability == null) return null;
            ItemData output = recipe.output != null ? recipe.output.item : null;
            int[] saved = new int[recipe.inputs.Count];
            bool any = false;
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                RecipeIngredient ing = recipe.inputs[i];
                if (ing == null || ing.item == null || ing.count <= 0) continue;
                saved[i] = Mathf.Max(0, ing.count - Mathf.Max(1, ability.GetCalculatedCraftingCost(ing.count, output)));
                any |= saved[i] > 0;
            }
            return any ? saved : null;
        }

        /// <summary>제작 창 안내용: "약초 1 (원래 2), 고기 1" 처럼 실제로 내는 재료 목록.</summary>
        public static string CostText(RecipeData recipe, LastTruck.CharacterAbilityController ability)
        {
            if (recipe == null || recipe.inputs == null) return string.Empty;
            ItemData output = recipe.output != null ? recipe.output.item : null;
            var parts = new List<string>();
            foreach (RecipeIngredient ing in recipe.inputs)
            {
                if (ing == null || ing.item == null || ing.count <= 0) continue;
                int need = ability != null ? Mathf.Max(1, ability.GetCalculatedCraftingCost(ing.count, output)) : ing.count;
                parts.Add(need < ing.count ? $"{ing.item.itemName} ×{need} (원래 {ing.count})" : $"{ing.item.itemName} ×{need}");
            }
            return string.Join(", ", parts);
        }
    }

    /// <summary>화면에 보여줄 가공 칸 하나의 상태.</summary>
    public struct ProcessSlotView
    {
        public RecipeData recipe;     // null이면 빈 칸
        public float remaining;       // 지금 가공 중인 1개의 남은 시간(초)
        public float total;           // 한 개 가공 시간(초)
        public int queued;            // 아직 가공할 개수 (지금 가공 중인 것 포함). 같은 재료를 골라 칸을 누를 때마다 1씩 늘어난다
        public int readyCount;        // 다 만들어져 받을 수 있는 개수
        public int discountedQueued;  // queued 중 능력 할인으로 재료를 덜 내고 넣은 개수 (취소할 때 낸 만큼만 돌려주기 위해)
        public int[] savedPerItem;    // 할인으로 1개당 덜 낸 재료 개수 (재료 순서대로)
        public bool ready => readyCount > 0;   // 받을 결과물이 있음

        public bool IsEmpty => recipe == null;
        public bool IsBusy => recipe != null && queued > 0;
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

        /// <summary>한 칸에 쌓아 둘 수 있는 최대 개수 (가공 대기 + 완성품). 더 많이 동시에 가공하려면 칸을 늘린다.</summary>
        public const int MaxPerSlot = 6;

        /// <summary>
        /// 칸에 재료 1개를 넣는다. 빈 칸이면 가공을 시작하고, 같은 재료가 들어 있는 칸이면 대기 개수를 1 늘린다
        /// (앞의 것이 끝나면 이어서 가공). 다른 재료가 들어 있거나 가득 차 있으면 false.
        /// </summary>
        public bool Place(int facility, int index, RecipeData recipe, float seconds) => Place(facility, index, recipe, seconds, null);

        /// <summary>saved: 능력 할인으로 덜 낸 재료 개수 (null이면 할인 없음). 취소할 때 낸 만큼만 돌려준다.</summary>
        public bool Place(int facility, int index, RecipeData recipe, float seconds, int[] saved)
        {
            if (!InRange(facility) || recipe == null || index < 0 || index >= _counts[facility]) return false;

            ProcessSlotView s = _slots[facility][index];
            float sec = Mathf.Max(0.05f, seconds);
            if (s.IsEmpty)
            {
                s = new ProcessSlotView { recipe = recipe, remaining = sec, total = sec, queued = 1 };
            }
            else
            {
                if (s.recipe != recipe || s.queued + s.readyCount >= MaxPerSlot) return false;
                if (s.queued == 0) { s.remaining = sec; s.total = sec; s.discountedQueued = 0; }   // 완성품만 남아 있던 칸이면 새로 타이머 시작
                s.queued++;
            }
            if (saved != null)
            {
                s.discountedQueued++;
                s.savedPerItem = saved;
            }
            _slots[facility][index] = s;
            Revision++;
            return true;
        }

        /// <summary>
        /// 가공을 멈추고 아직 안 만든 재료 개수를 돌려준다 (재료 환불은 호출한 쪽에서). 완성품은 칸에 남는다. 가공 중이 아니면 false.
        /// </summary>
        public bool Cancel(int facility, int index, out RecipeData recipe, out int count) =>
            Cancel(facility, index, out recipe, out count, out _, out _);

        /// <summary>discounted: count 중 할인으로 넣은 개수, saved: 그때 1개당 덜 낸 재료 개수.</summary>
        public bool Cancel(int facility, int index, out RecipeData recipe, out int count, out int discounted, out int[] saved)
        {
            recipe = null; count = 0; discounted = 0; saved = null;
            if (!InRange(facility) || index < 0 || index >= TruckFacilities.MaxSlots) return false;
            ProcessSlotView s = _slots[facility][index];
            if (!s.IsBusy) return false;

            recipe = s.recipe;
            count = s.queued;
            discounted = Mathf.Clamp(s.discountedQueued, 0, s.queued);
            saved = s.savedPerItem;
            s.queued = 0;
            s.discountedQueued = 0;
            s.remaining = 0f;
            _slots[facility][index] = s.readyCount > 0 ? s : default;
            Revision++;
            return true;
        }

        /// <summary>완성품을 모두 꺼낸다 (결과물 지급은 호출한 쪽에서). 남은 가공은 계속된다. 완성품이 없으면 false.</summary>
        public bool Claim(int facility, int index, out RecipeData recipe, out int count)
        {
            recipe = null; count = 0;
            if (!InRange(facility) || index < 0 || index >= TruckFacilities.MaxSlots) return false;
            ProcessSlotView s = _slots[facility][index];
            if (!s.ready) return false;

            recipe = s.recipe;
            count = s.readyCount;
            s.readyCount = 0;
            _slots[facility][index] = s.queued > 0 ? s : default;
            Revision++;
            return true;
        }

        /// <summary>가방이 가득 차서 못 받은 완성품을 칸에 되돌려 둔다.</summary>
        public void ReturnReady(int facility, int index, RecipeData recipe, int count)
        {
            if (!InRange(facility) || index < 0 || index >= TruckFacilities.MaxSlots || recipe == null || count <= 0) return;
            ProcessSlotView s = _slots[facility][index];
            if (s.IsEmpty) s = new ProcessSlotView { recipe = recipe };
            else if (s.recipe != recipe) return;
            s.readyCount += count;
            _slots[facility][index] = s;
            Revision++;
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
                        // 1개 완성 → 대기 중인 게 더 있으면 이어서 가공
                        // (할인 없이 넣은 것부터 끝난 것으로 본다 → 취소할 때 절대 낸 것보다 많이 돌려주지 않는다)
                        if (s.queued - s.discountedQueued <= 0 && s.discountedQueued > 0) s.discountedQueued--;
                        s.readyCount++;
                        s.queued--;
                        s.remaining = s.queued > 0 ? s.total : 0f;
                        Revision++;
                    }
                    _slots[f][i] = s;
                }
            }
        }

        private bool InRange(int facility) => facility >= 0 && facility < _slots.Length;
    }
}
