using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    public class PlayerHPUI : MonoBehaviour
    {
        [Header("플레이어 Stats")]
        [SerializeField] private PlayerStats playerStats;

        [Header("UI")]
        [SerializeField] private Scrollbar hpScrollbar;

        private void Start()
        {
            if (playerStats == null)
            {
                playerStats = FindObjectOfType<PlayerStats>();
            }

            if (playerStats != null)
            {
                BindPlayer(playerStats);
            }
        }

        private void OnDisable()
        {
            if (playerStats != null)
            {
                playerStats.OnHealthChanged -= UpdateHealthUI;
            }
        }

        public void BindPlayer(PlayerStats targetPlayer)
        {
            if (playerStats != null)
            {
                playerStats.OnHealthChanged -= UpdateHealthUI;
            }

            playerStats = targetPlayer;

            if (playerStats != null)
            {
                playerStats.OnHealthChanged += UpdateHealthUI;
                UpdateHealthUI(playerStats.currentHealth, playerStats.MaxHealth);
            }
        }

        private void UpdateHealthUI(float currentHp, float maxHp)
        {
            if (maxHp <= 0f) return;

            float ratio = Mathf.Clamp01(currentHp / maxHp);

            if (hpScrollbar != null)
            {
                hpScrollbar.size = ratio;
            }
        }
    }
}