using System;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 플레이어 체력 / 피격 / 사망.
    ///
    /// 멀티플레이: 같은 오브젝트에 NetworkHealth가 있으면 체력은 호스트가 관리한다.
    ///  - TakeDamage / Heal은 호스트에게 요청만 하고, 결과(HP바, 피격/사망 애니메이션)는
    ///    NetworkHealth가 모든 컴퓨터에서 이 스크립트의 OnNetwork... 함수로 알려준다.
    ///  - 플레이어끼리는 피해를 줄 수 없다 (팀킬 금지).
    /// </summary>
    public class PlayerStats : MonoBehaviour, IDamageable, LastTruck.Networking.INetworkHealthListener
    {
        #region 필드

        public float currentHealth;

        [SerializeField] private Character character;
        [SerializeField] private Animator anim;

        public event Action<float, float> OnHealthChanged;
        public float MaxHealth
        {
            get
            {
                if (character == null) character = GetComponent<Character>();
                return (character != null && character.StatsData != null) ? character.StatsData.MaxHealth : 100f;
            }
        }

        private LastTruck.Networking.NetworkHealth _networkHealth;

        /// <summary>멀티플레이에서 체력을 호스트가 관리하는 캐릭터인가.</summary>
        private bool IsNetworked => _networkHealth != null && _networkHealth.Object != null && _networkHealth.Object.IsValid;

        #endregion

        #region 생명주기

        private void Awake()
        {
            _networkHealth = GetComponent<LastTruck.Networking.NetworkHealth>();
        }

        private void Start()
        {
            character = GetComponent<Character>();
            anim = GetComponentInChildren<Animator>();

            if (IsNetworked) return; // 멀티플레이: 체력은 NetworkHealth가 알려준다.

            if (character != null && character.StatsData != null)
            {
                currentHealth = character.StatsData.MaxHealth;
            }

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        #endregion

        #region 피해 / 회복

        public void TakeDamage(float damage, GameObject attacker)
        {
            bool fromPlayer = attacker != null && attacker.CompareTag("Player");

            if (IsNetworked)
            {
                // 팀킬 금지: 다른 플레이어의 공격은 피격 연출도 없이 무시한다.
                if (fromPlayer) return;
                _networkHealth.RequestDamage(damage, transform.position + Vector3.up, false);
                return;
            }

            if (fromPlayer)
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
            if (IsNetworked)
            {
                _networkHealth.RequestHeal(amount);
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, MaxHealth);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        #endregion

        #region 애니메이션

        private void Hit_Ani()
        {
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("doHit");
            }
        }

        private void Die()
        {
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("doDie");
            }
        }

        #endregion

        #region 멀티플레이 (NetworkHealth가 호출)

        float LastTruck.Networking.INetworkHealthListener.NetworkMaxHealth => MaxHealth;

        void LastTruck.Networking.INetworkHealthListener.OnNetworkHealthChanged(float current, float max)
        {
            currentHealth = current;
            OnHealthChanged?.Invoke(current, max);
        }

        void LastTruck.Networking.INetworkHealthListener.OnNetworkHit(float amount, Vector3 hitPoint)
        {
            Hit_Ani();
        }

        void LastTruck.Networking.INetworkHealthListener.OnNetworkDeath()
        {
            Die();
        }

        #endregion

        #region 에디터

        // 체력바 확인용
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && !IsNetworked)
            {
                float maxHP = MaxHealth;
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHP);
                OnHealthChanged?.Invoke(currentHealth, maxHP);
            }
        }
#endif

        #endregion
    }
}
