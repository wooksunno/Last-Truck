using UnityEngine;

namespace LastTruck
{
    public class Character : MonoBehaviour
    {
        [Header("Data Reference")]
        [SerializeField] private CharacterStatsData statsData;
        public CharacterStatsData StatsData => statsData;

        public float CurrentHealth { get; private set; }
        public float CurrentMoveSpeed { get; private set; }

        private float lastAbilityUsedTime;

        private void Awake()
        {
            Init();
        }

        // 스탯 초기화
        public void Init()
        {
            if (statsData == null) { return; }

            CurrentHealth = statsData.MaxHealth;
            CurrentMoveSpeed = statsData.ScaledMoveSpeed;
        }

        // 데미지
        public void Take_Damage(float damage)
        {
            CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        // 고유 능력
        public void Use_UniqueAbility()
        {
            if (statsData == null || statsData.UniqueAbility == null) return;

            if (Time.time >= lastAbilityUsedTime + statsData.UniqueAbility.cooldown)
            {
                statsData.UniqueAbility.Execute(this);
                lastAbilityUsedTime = Time.time;
            }
        }

        private void Die()
        {
        }
    }
}