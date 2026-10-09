using System;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 동기화되는 체력. 플레이어와 몬스터 프리팹에 붙는다.
    ///
    /// 규칙
    ///  - 체력은 호스트만 바꾼다. 클라이언트가 때리면 "이만큼 맞았다"를 RPC로 호스트에게 요청하고,
    ///    호스트가 확인(사망 여부, 팀킬 여부, 값 범위) 후 적용한다.
    ///  - 피해가 적용되면 호스트가 모두에게 "맞았다" 효과를 보낸다 → 각자 화면에서 빨간색 깜빡임/데미지 숫자/피격 애니메이션.
    ///  - 플레이어끼리는 피해를 줄 수 없다 (팀킬 금지).
    ///  - 체력이 0이 되면 IsDead가 켜지고(모두에게 사망 연출), despawnDelay초 뒤 호스트가 오브젝트를 없앤다.
    ///
    /// 화면 반영은 같은 오브젝트의 INetworkHealthListener(PlayerStats, Damageable)가 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkHealth : NetworkBehaviour
    {
        #region 인스펙터

        [Tooltip("true면 플레이어 캐릭터 (다른 플레이어에게 피해를 받지 않는다).")]
        [SerializeField] private bool isPlayer;

        [Tooltip("사망 후 오브젝트가 사라지기까지 시간(초). 사망 애니메이션을 보여줄 시간.")]
        [SerializeField] private float despawnDelay = 3f;

        [Tooltip("최대 체력을 찾지 못했을 때 쓸 값.")]
        [SerializeField] private float fallbackMaxHealth = 100f;

        [Tooltip("한 번의 요청으로 받을 수 있는 최대 피해 (비정상 값 방어).")]
        [SerializeField] private float maxDamagePerHit = 1000f;

        #endregion

        #region 네트워크 상태

        [Networked, OnChangedRender(nameof(OnHealthRender))]
        public float Current { get; set; }

        [Networked, OnChangedRender(nameof(OnHealthRender))]
        public float Max { get; set; }

        [Networked, OnChangedRender(nameof(OnDeadRender))]
        public NetworkBool IsDead { get; set; }

        [Networked] private TickTimer DespawnTimer { get; set; }

        #endregion

        #region 이벤트 / 조회

        /// <summary>체력 변화 (모든 컴퓨터).</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>사망 (모든 컴퓨터, 한 번). 화면 연출용.</summary>
        public event Action<NetworkHealth> Died;

        /// <summary>사망 (호스트에서만, 체력이 0이 되는 순간 정확히 한 번). 킬 집계 등 게임 규칙용.</summary>
        public event Action<NetworkHealth> DiedOnHost;

        public bool IsPlayer => isPlayer;
        public bool IsAlive => Object != null && Object.IsValid && !IsDead;

        private INetworkHealthListener[] _listeners = new INetworkHealthListener[0];
        private bool _deathHandled;

        #endregion

        #region 생명주기

        private void Awake()
        {
            _listeners = GetComponents<INetworkHealthListener>();
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                float max = fallbackMaxHealth;
                foreach (INetworkHealthListener listener in _listeners)
                {
                    if (listener != null && listener.NetworkMaxHealth > 0f)
                    {
                        max = listener.NetworkMaxHealth;
                        break;
                    }
                }
                Max = max;
                Current = max;
                IsDead = false;
            }

            OnHealthRender();
            if (IsDead) OnDeadRender();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !IsDead) return;
            if (DespawnTimer.Expired(Runner))
            {
                DespawnTimer = TickTimer.None;
                Runner.Despawn(Object);
            }
        }

        #endregion

        #region 피해 / 회복 (요청 → 호스트 적용)

        /// <summary>
        /// 어디서든 부르는 피해 함수. 호스트면 바로 적용, 클라이언트면 호스트에게 요청한다.
        /// fromPlayer: 플레이어가 준 피해인가 (플레이어 대상이면 팀킬로 무시).
        /// </summary>
        public void RequestDamage(float amount, Vector3 hitPoint, bool fromPlayer)
        {
            if (Object == null || !Object.IsValid || amount <= 0f) return;
            if (isPlayer && fromPlayer) return; // 팀킬 금지 (요청 자체를 보내지 않음)

            if (HasStateAuthority) ApplyDamage(amount, hitPoint, fromPlayer);
            else RPC_RequestDamage(amount, hitPoint, fromPlayer);
        }

        public void RequestHeal(float amount)
        {
            if (Object == null || !Object.IsValid || amount <= 0f) return;
            if (HasStateAuthority) ApplyHeal(amount);
            else RPC_RequestHeal(amount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDamage(float amount, Vector3 hitPoint, NetworkBool fromPlayer)
        {
            ApplyDamage(amount, hitPoint, fromPlayer);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestHeal(float amount)
        {
            ApplyHeal(amount);
        }

        /// <summary>호스트 전용: 실제로 체력을 깎는다.</summary>
        public void ApplyDamage(float amount, Vector3 hitPoint, bool fromPlayer)
        {
            if (!HasStateAuthority || IsDead) return;
            if (isPlayer && fromPlayer) return; // 팀킬 금지
            if (NetworkGameState.IsGameOver) return;

            amount = Mathf.Clamp(amount, 0f, maxDamagePerHit);
            if (amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            RPC_PlayHit(amount, hitPoint);

            if (Current <= 0f)
            {
                IsDead = true;
                DespawnTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, despawnDelay));
                try { DiedOnHost?.Invoke(this); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void ApplyHeal(float amount)
        {
            if (!HasStateAuthority || IsDead) return;
            Current = Mathf.Min(Max, Current + Mathf.Clamp(amount, 0f, maxDamagePerHit));
        }

        #endregion

        #region 화면 반영 (모든 컴퓨터)

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayHit(float amount, Vector3 hitPoint)
        {
            foreach (INetworkHealthListener listener in _listeners)
            {
                try { listener?.OnNetworkHit(amount, hitPoint); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void OnHealthRender()
        {
            float current = Current;
            float max = Max;
            foreach (INetworkHealthListener listener in _listeners)
            {
                try { listener?.OnNetworkHealthChanged(current, max); }
                catch (Exception e) { Debug.LogException(e); }
            }
            HealthChanged?.Invoke(current, max);
        }

        private void OnDeadRender()
        {
            if (!IsDead || _deathHandled) return;
            _deathHandled = true;

            foreach (INetworkHealthListener listener in _listeners)
            {
                try { listener?.OnNetworkDeath(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            Died?.Invoke(this);
        }

        #endregion
    }
}
