using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 기절한 동료 살리기 (내 캐릭터에서만 동작, NetworkPlayer가 스폰될 때 자동으로 붙인다).
    ///  - 기절한 동료 옆(ReviveRange 안)에서 E를 누르고 있으면 호스트에게 "살리기 시작"을 알린다.
    ///  - 시간 계산/완료 판정은 호스트가 한다 (NetworkPlayer.ReviveTimer) → 진행도는 모두에게 같이 보인다.
    ///  - E를 떼거나 멀어지면 취소 (처음부터 다시). 살리는 동안은 다른 E 상호작용(PlayerInteract)을 잠깐 끈다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)] // PlayerInteract보다 먼저: 첫 E 프레임에 다른 상호작용(트럭 탑승 등)이 같이 일어나지 않게
    public class PlayerReviver : MonoBehaviour
    {
        #region 상태

        [SerializeField] private KeyCode reviveKey = KeyCode.E;

        private NetworkPlayer _self;
        private NetworkPlayer _requested;
        private PlayerInteract _interact;
        private bool _interactDisabledByMe;
        private float _nextResendTime;

        /// <summary>내 캐릭터의 Reviver (없으면 null).</summary>
        public static PlayerReviver Local { get; private set; }

        /// <summary>근처에 있는 기절한 동료 (살릴 수 있는 대상, 없으면 null).</summary>
        public NetworkPlayer NearbyDowned { get; private set; }

        /// <summary>지금 살리는 중인 동료 (없으면 null).</summary>
        public NetworkPlayer Reviving => _requested;

        #endregion

        #region 생명주기

        private void Awake()
        {
            _self = GetComponent<NetworkPlayer>();
            _interact = GetComponent<PlayerInteract>();
        }

        private void OnDisable()
        {
            StopReviving();
            if (Local == this) Local = null;
        }

        private void OnDestroy()
        {
            if (Local == this) Local = null;
        }

        private void Update()
        {
            if (_self == null || !_self.IsLocal) return;
            Local = this;

            bool canAct = _self.IsAlive && !_self.IsSeated && NetworkGameState.AllowPlayerControl;
            NearbyDowned = canAct ? FindNearbyDowned() : null;

            bool holding = NearbyDowned != null && Input.GetKey(reviveKey) && !IsBlockedByUI();
            if (!holding)
            {
                StopReviving();
                return;
            }

            if (_requested != NearbyDowned)
            {
                StopReviving();
                _requested = NearbyDowned;
                _requested.RequestRevive(true);
                _nextResendTime = Time.time + 0.25f;
                SetInteractEnabled(false);
            }
            else if (_requested.Reviver != _self.Owner && Time.time >= _nextResendTime)
            {
                // 호스트가 거절/취소했으면(위치 지연 등) 누르고 있는 동안 다시 요청한다
                _requested.RequestRevive(true);
                _nextResendTime = Time.time + 0.25f;
            }
        }

        #endregion

        #region 도우미

        private NetworkPlayer FindNearbyDowned()
        {
            NetworkPlayer best = null;
            float bestDistance = float.MaxValue;
            foreach (NetworkPlayer other in NetworkPlayer.All)
            {
                if (other == null || other == _self || !other.IsDowned || other.IsSeated) continue;
                // 이미 다른 사람이 살리는 중이면 건너뛴다
                if (other.Reviver != PlayerRef.None && other.Reviver != _self.Owner) continue;

                Vector3 delta = other.transform.position - transform.position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= other.ReviveRange && distance < bestDistance)
                {
                    best = other;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void StopReviving()
        {
            if (_requested != null)
            {
                if (_requested.Object != null && _requested.Object.IsValid) _requested.RequestRevive(false);
                _requested = null;
            }
            SetInteractEnabled(true);
        }

        private void SetInteractEnabled(bool enabled)
        {
            if (_interact == null) return;
            if (!enabled)
            {
                if (_interact.enabled)
                {
                    _interact.enabled = false;
                    _interactDisabledByMe = true;
                }
            }
            else if (_interactDisabledByMe)
            {
                _interactDisabledByMe = false;
                // 그사이 기절/탑승했으면 부활/하차할 때 켜지도록 NetworkPlayer에 맡긴다
                if (_self != null) _self.ReenableWhenAble(_interact);
                else _interact.enabled = true;
            }
        }

        private static bool IsBlockedByUI()
        {
            return CraftingSystem.GameUIController.Instance != null && CraftingSystem.GameUIController.Instance.IsPopupOpen;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
        }

        #endregion
    }
}
