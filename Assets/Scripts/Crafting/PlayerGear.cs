using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어 장비 강화 (한 판 동안 유지되는 성장 수치).
    ///
    ///   무기 강화 : "무기 강화 키트"를 손에 들고 왼쪽 클릭을 꾹 누르면 1단계 오른다. 단계마다 내가 주는 피해 +8% (최대 10단계 = +80%).
    ///
    /// 키트는 성장 임무(ProgressMissions) 보상으로 받거나 트럭에서 조립한다.
    /// 피해 보정은 Damageable.TakeDamage(플레이어가 직접 때린 피해)에서 적용한다. 포탑/덫 피해에는 적용되지 않는다.
    /// 멀티플레이: 피해는 때린 사람 컴퓨터에서 계산해서 호스트로 보내므로, 각자 자기 강화 단계가 적용된다.
    /// </summary>
    public static class PlayerGear
    {
        public const int MaxWeaponLevel = 10;
        public const float DamagePerLevel = 0.08f;

        public static int WeaponLevel { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { WeaponLevel = 0; }

        public static bool CanUpgradeWeapon => WeaponLevel < MaxWeaponLevel;

        /// <summary>내가 주는 피해 배율 (1 = 강화 없음).</summary>
        public static float DamageMultiplier => 1f + DamagePerLevel * WeaponLevel;

        public static int ScaleDamage(int amount)
        {
            if (WeaponLevel <= 0 || amount <= 0) return amount;
            return Mathf.Max(amount, Mathf.RoundToInt(amount * DamageMultiplier));
        }

        /// <summary>무기 강화 1단계. 이미 최대면 false.</summary>
        public static bool UpgradeWeapon()
        {
            if (!CanUpgradeWeapon) return false;
            WeaponLevel++;
            return true;
        }

        public static string WeaponSummary =>
            WeaponLevel <= 0 ? "무기 강화 없음" : "무기 강화 Lv." + WeaponLevel + " (피해 +" + Mathf.RoundToInt(DamagePerLevel * WeaponLevel * 100f) + "%)";
    }
}
