using UnityEngine;

namespace LastTruck
{
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public float currentHealth;

        
        [SerializeField] private Character character;
        [SerializeField] private Animator anim;

        private void Start()
        {
            character = GetComponent<Character>();
            anim = GetComponentInChildren<Animator>();

            if (character != null && character.stats != null)
            {
                currentHealth = character.stats.maxHealth;
            }
        }

        public void TakeDamage(float damage, GameObject attacker)
        {
            if (attacker != null && attacker.CompareTag("Player"))
            {
                Hit_Ani();
                return;
            }

            currentHealth -= damage;
            float maxHP = (character != null && character.stats != null) ? character.stats.maxHealth : 100f;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHP);

            Hit_Ani();

            if (currentHealth <= 0)
            {
                Die();
            }
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
    }
}