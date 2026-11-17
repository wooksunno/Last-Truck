using Combat;
using CraftingSystem;
using UnityEngine;

namespace LastTruck
{
    [RequireComponent(typeof(WeaponController))]
    public class CharacterAbilityController : MonoBehaviour
    {
        [Header("자동 연동 컴포넌트")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private Character character;

        private int lastNightRepairIndex = -1;

        /// <summary>디버그/로그에 쓰는 캐릭터 이름 (캐릭터 데이터의 이름, 없으면 오브젝트 이름).</summary>
        public string CharacterName =>
            character != null && character.StatsData != null ? character.StatsData.DisplayName : name;

        private void Report(string key, string message, float throttle = 0f)
        {
            AbilityDebug.Report(CharacterName, key, message, throttle);
        }

        public CharacterAbility CurrentAbility
        {
            get
            {
                if (character != null && character.StatsData != null)
                {
                    return character.StatsData.UniqueAbility;
                }
                return null;
            }
        }

        private void Reset()
        {
            weaponController = GetComponent<WeaponController>();
        }

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();

            if (character == null)
                character = GetComponent<Character>();

            if (CurrentAbility != null && character != null)
            {
                CurrentAbility.Execute(character);
                Report("init", $"고유 능력 적용: {CurrentAbility.AbilityTitle} ({CurrentAbility.name})", 1f);
            }
            else if (character != null && character.StatsData != null && character.StatsData.UniqueAbility == null)
            {
                Report("init", "고유 능력이 비어 있음 (CharacterData의 Unique Ability를 확인하세요)", 1f);
            }
        }

        // 군인
        public int GetCalculatedDamage(int originalDamage, out bool isEnhanced)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null)
            {
                isEnhanced = false;
                return originalDamage;
            }

            int result = ability.CalculateAttackDamage(originalDamage, out isEnhanced);
            if (isEnhanced) Report("army", $"군인 강화 공격! 피해 {originalDamage} → {result}");
            return result;
        }

        // 탐험가
        public int GetCalculatedResourceAmount(int baseAmount)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null)
            {
                return baseAmount;
            }

            int result = ability.CalculateGatherAmount(baseAmount);
            if (result != baseAmount) Report("gather", $"탐험가 채집량 {baseAmount} → {result}");
            return result;
        }

        // 대장장이
        public int GetCalculatedCraftingCost(int originalCost, bool isWeapon)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) { return originalCost; }
            int result = ability.CalculateCraftingCost(originalCost, isWeapon);
            if (result != originalCost) Report("craft", $"제작 재료 {originalCost} → {result}", 5f);
            return result;
        }

        /// <summary>
        /// 결과물을 아는 경우의 재료 수량 보정. 무기면 대장장이 할인, 음식이면 요리사 할인을 적용한다.
        /// (제작 창 표시 / 재료 확인 / 실제 제작이 모두 이 함수를 써야 수량이 서로 어긋나지 않는다)
        /// </summary>
        public int GetCalculatedCraftingCost(int originalCost, ItemData outputItem)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null || outputItem == null) return originalCost;

            // 무기/음식 판단은 게임 UI 분류(TruckCodexView)와 같은 기준을 쓴다.
            // (itemType/isWeapon만 보면 활/총/수류탄/돌 던지기처럼 UI에서는 무기인데 데이터상 무기가 아닌 아이템이 빠진다)
            bool isWeapon = TruckCodexView.IsWeaponItem(outputItem) || outputItem.itemType == ItemType.Weapon || outputItem.isWeapon;
            bool isFood = TruckCodexView.IsFoodItem(outputItem) || ItemIds.IsFood(outputItem);
            int cost = ability.CalculateCraftingCost(originalCost, isWeapon);
            if (isFood)
                cost = ability.CalculateFoodCraftingCost(cost);
            // 제작 창이 다시 그려질 때마다 불리므로 같은 내용은 5초에 한 번만 기록
            if (cost != originalCost)
                Report("craft:" + outputItem.itemID, $"{outputItem.itemName} 재료 {originalCost} → {cost}개 ({(isWeapon ? "무기" : "음식")} 할인)", 5f);
            return cost;
        }

        /// <summary>이 결과물을 만들 때 재료 할인이 하나라도 적용되는가 (제작 창의 할인 표시용).</summary>
        public bool HasCraftingDiscount(RecipeData recipe)
        {
            if (recipe == null || recipe.inputs == null || recipe.output == null || recipe.output.item == null) return false;
            foreach (RecipeIngredient ingredient in recipe.inputs)
            {
                if (ingredient == null || ingredient.item == null || ingredient.count <= 0) continue;
                if (GetCalculatedCraftingCost(ingredient.count, recipe.output.item) < ingredient.count) return true;
            }
            return false;
        }

        public float GetCalculatedProcessingTime(float originalSeconds, bool isWeapon)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return originalSeconds;

            float result = ability.CalculateProcessingTime(originalSeconds, isWeapon);
            if (!Mathf.Approximately(result, originalSeconds))
                Report("process", $"가공 시간 {originalSeconds:0.#}초 → {result:0.#}초", 5f);
            return result;
        }

        // 광부
        public float GetCalculatedMiningTime(float baseTime)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseTime;

            float result = ability.CalculateGatherTime(baseTime);
            if (!Mathf.Approximately(result, baseTime))
                Report("mine-time", $"채집 시간 {baseTime:0.##}초 → {result:0.##}초", 5f);
            return result;
        }

        /// <summary>채집 대상 아이템을 아는 경우 (광부: 광물만 채집 시간 감소).</summary>
        public float GetCalculatedMiningTime(ItemData targetItem, float baseTime)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseTime;

            float result = ability.CalculateGatherTime(targetItem, baseTime);
            // 채집 UI가 매 프레임 묻는 값이라 같은 내용은 5초에 한 번만 기록
            if (!Mathf.Approximately(result, baseTime))
                Report("mine-time", $"광부 {(targetItem != null ? targetItem.itemName : "채집")} 시간 {baseTime:0.##}초 → {result:0.##}초", 5f);
            return result;
        }

        public int GetCalculatedMiningAmount(ItemData targetItem, int baseAmount)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseAmount;

            int result = ability.CalculateGatherAmount(targetItem, baseAmount);
            if (result != baseAmount)
                Report("mine-amount", $"광부 {(targetItem != null ? targetItem.itemName : "광물")} 채집량 {baseAmount} → {result}");
            return result;
        }

        // 경찰
        // 경찰 - 표식 시스템
        public int GetCalculatedPoliceDamage(int baseDamage, bool targetHasMark, out float markDuration)
        {
            markDuration = 0f;
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseDamage;

            markDuration = ability.MarkDuration; // 경찰이면 2.0f, 다른 캐릭터면 0f 반환

            int result = ability.CalculateMarkBonusDamage(baseDamage, targetHasMark);
            if (markDuration > 0f)
            {
                if (targetHasMark && result != baseDamage) Report("police", $"경찰 표식 추가 피해 {baseDamage} → {result}");
                else if (!targetHasMark) Report("police", $"경찰 표식 부여 ({markDuration:0.#}초)");
            }
            return result;
        }

        

        // 복서
        public bool TryGetStun(bool isMeleeAttack, out float stunDuration)
        {
            stunDuration = 0f;
            var ability = CurrentAbility;
            if (!enabled || ability == null) return false;

            bool stunned = ability.TryApplyStun(isMeleeAttack, out stunDuration);
            if (stunned) Report("boxer", $"복서 기절 발동 ({stunDuration:0.#}초)");
            return stunned;
        }

        // 수리공 - 일반 수리
        public float GetCalculatedRepairTime(float baseSeconds)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseSeconds;
            float result = ability.CalculateRepairTime(baseSeconds);
            // 트럭 E 상호작용이 매 프레임 묻는 값이라 같은 내용은 5초에 한 번만 기록
            if (!Mathf.Approximately(result, baseSeconds))
                Report("repair-time", $"수리공 수리 시간 {baseSeconds:0.#}초 → {result:0.#}초", 5f);
            return result;
        }

        public int GetCalculatedRepairCost(int baseCost)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseCost;
            int result = ability.CalculateRepairCost(baseCost);
            if (result != baseCost) Report("repair-cost", $"수리공 수리 재료 {baseCost}개 → {result}개", 5f);
            return result;
        }

        // 수리공 - 야간 전용 수리 (트럭 근처에서 E 길게 누름, 밤당 1회)
        public bool CanUseNightRepair(int nightIndex)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null || !ability.CanUseNightRepair) return false;
            return lastNightRepairIndex != nightIndex;
        }

        public bool TryUseNightRepair(int nightIndex, float currentLevelMaxDurability, out float repairAmount)
        {
            repairAmount = 0f;
            if (!CanUseNightRepair(nightIndex)) return false;

            repairAmount = currentLevelMaxDurability * CurrentAbility.NightRepairRatio;
            lastNightRepairIndex = nightIndex;
            Report("night-repair", $"수리공 야간 수리: 내구도 {repairAmount:F1} 회복 (밤 {nightIndex})");
            return true;
        }

        // 요리사
        public int GetCalculatedFoodCraftingCost(int originalCost)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return originalCost;
            int result = ability.CalculateFoodCraftingCost(originalCost);
            if (result != originalCost) Report("food", $"요리사 음식 재료 {originalCost} → {result}", 5f);
            return result;
        }

        // 간호사
        public float GetRescueRange(float baseRange)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseRange;
            float result = baseRange * ability.RescueRangeMultiplier;
            // 매 프레임 불리므로 기록은 10초에 한 번
            if (!Mathf.Approximately(result, baseRange))
                Report("rescue", $"간호사 구조 거리 {baseRange:0.#}m → {result:0.#}m", 10f);
            return result;
        }

        public float GetReviveInvincibleDuration()
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return 0f;
            float result = ability.ReviveInvincibleDuration;
            if (result > 0f) Report("revive", $"간호사 부활시킨 동료 무적 {result:0.#}초");
            return result;
        }

        public float GetAllyApproachSpeedMultiplier(float allyHpRatio, bool isApproaching)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return 1f;
            float result = 1f + ability.CalculateAllyApproachSpeedBonus(allyHpRatio, isApproaching);
            // 이동 틱마다 불리므로 같은 내용은 3초에 한 번만 기록
            if (result > 1f)
                Report("nurse-speed", $"간호사 다친 동료(HP {allyHpRatio * 100f:0}%)에게 접근 → 이동속도 x{result:0.##}", 3f);
            return result;
        }

        // 과학자 - 광물 변환 (낮에만, 공용 창고의 돌·원석 사용)
        public bool TryGetMineralConversion(ItemData inputItem, bool isDay,
            out string resultItemId, out int inputCount, out int outputCount)
        {
            resultItemId = null; inputCount = 0; outputCount = 0;
            var ability = CurrentAbility;
            if (!enabled || ability == null || !ability.CanConvertMinerals) return false;
            if (!isDay) return false;

            return ability.TryGetMineralConversion(inputItem, out resultItemId, out inputCount, out outputCount);
        }


    }
}