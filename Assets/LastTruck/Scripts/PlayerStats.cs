using System;
using UnityEngine;

namespace LastTruck
{
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public float currentHealth;

        [SerializeField] private Character character;
        [SerializeField] private Animator anim;

        public event Action<float, float> OnHealthChanged;
        public float MaxHealth => (character != null && character.StatsData != null) ? character.StatsData.MaxHealth : 100f;

        private void Start()
        {
            character = GetComponent<Character>();
            anim = GetComponentInChildren<Animator>();

            if (character != null && character.StatsData != null)
            {
                currentHealth = character.StatsData.MaxHealth;
            }

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        public void TakeDamage(float damage, GameObject attacker)
        {
            if (attacker != null && attacker.CompareTag("Player"))
            {
                Hit_Ani();
                return;
            }

            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            Hit_Ani();

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, MaxHealth);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        private void Hit_Ani()
        {
            if (anim != null)
            {
                anim.SetTrigger("doHit");
            }
        }

        private void Die()
        {
            if (anim != null)
            {
                anim.SetTrigger("doDie");
            }
        }

        // 체력바 확인용
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                float maxHP = MaxHealth;
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHP);
                OnHealthChanged?.Invoke(currentHealth, maxHP);
            }
        }
        #endif
    }
}