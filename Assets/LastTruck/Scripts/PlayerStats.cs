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
    ///
    /// 체력 0 = 사망이 아니라 "기절" (멀티: 동료가 살릴 수 있음 / 싱글: 살려줄 사람이 없어 게임 오버).
    /// 맞으면 머리 위 데미지 숫자(모두에게), 내 캐릭터면 화면 가장자리 붉게 + 카메라 흔들림 (PlayerStatusHud).
    /// </summary>
    public class PlayerStats : MonoBehaviour, IDamageable, LastTruck.Networking.INetworkHealthListener, LastTruck.Networking.INetworkReviveListener
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
        private bool _downedOffline;

        /// <summary>싱글플레이에서 내 캐릭터 (멀티플레이에서는 null).</summary>
        public static PlayerStats OfflineLocal { get; private set; }

        /// <summary>싱글플레이에서 기절했는가 (멀티플레이는 NetworkPlayer.IsDowned).</summary>
        public bool IsDownedOffline => _downedOffline;

        /// <summary>싱글플레이에서 기절할 때 끄는 조작 스크립트 이름.</summary>
        private static readonly string[] OfflineControlTypes =
        {
            "PlayerMove", "PlayerInteract", "PlayerInventory", "WeaponController", "PlayerAttack",
            "EatController", "TrapController", "PouchController",
        };

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

            if (GetComponent<LastTruck.Networking.NetworkPlayer>() == null)
            {
                OfflineLocal = this;
                LastTruck.Networking.PlayerStatusHud.Ensure();
            }

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

            if (_downedOffline) return;

            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            Hit_Ani();
            LastTruck.Networking.PlayerStatusHud.ShowDamageNumber(transform, damage);
            LastTruck.Networking.PlayerStatusHud.PlayLocalHit();

            if (currentHealth <= 0)
            {
                GoDownOffline();
            }
        }

        public void Heal(float amount)
        {
            if (IsNetworked)
            {
                _networkHealth.RequestHeal(amount);
                return;
            }

            if (_downedOffline) return;

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

        /// <summary>싱글플레이 기절: 살려줄 동료가 없으므로 쓰러진 채로 조작이 멈춘다 (게임 오버).</summary>
        private void GoDownOffline()
        {
            if (_downedOffline) return;
            _downedOffline = true;
            Die();

            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour == this) continue;
                if (System.Array.IndexOf(OfflineControlTypes, behaviour.GetType().Name) >= 0) behaviour.enabled = false;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = Vector3.zero;
#else
                body.velocity = Vector3.zero;
#endif
            }
            Debug.Log("[PlayerStats] 기절했습니다 (싱글플레이: 살려줄 동료가 없어 게임 오버).");
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
            LastTruck.Networking.PlayerStatusHud.ShowDamageNumber(transform, amount);

            var networkPlayer = GetComponent<LastTruck.Networking.NetworkPlayer>();
            if (networkPlayer != null && networkPlayer.IsLocal) LastTruck.Networking.PlayerStatusHud.PlayLocalHit();
        }

        void LastTruck.Networking.INetworkReviveListener.OnNetworkRevived()
        {
            // 쓰러진 애니메이션에서 기본 상태로 되돌린다.
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0f);
            }
        }

        void LastTruck.Networking.INetworkHealthListener.OnNetworkDeath()
        {
            Die();
        }

        #endregion

        private void OnDestroy()
        {
            if (OfflineLocal == this) OfflineLocal = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OfflineLocal = null;
        }

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
