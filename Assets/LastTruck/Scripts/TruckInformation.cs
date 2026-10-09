using UnityEngine;

namespace LastTruck
{
    public class TruckInformation : MonoBehaviour
    {
        [Header("트럭 내구도")]
        [SerializeField] private float maxDurability = 100f;
        private float curDurability;
        [Tooltip("게임 시작 시 깎아둘 내구도(수리 테스트용).")]
        [SerializeField] private float startingDamage = 60f;

        public event System.Action<float, float> OnDurabilityChanged;
        public float CurrentDurability => curDurability;
        public float MaxDurability => maxDurability;

        /// <summary>내구도가 0이 되어 부서졌는가 (멀티플레이 게임 오버 판정에 사용).</summary>
        public bool IsBroken { get; private set; }

        void Start()
        {
            curDurability = Mathf.Max(1f, maxDurability - startingDamage);
            OnDurabilityChanged?.Invoke(curDurability, maxDurability);
        }

public void Take_Damage(float amount)
        {
            curDurability -= amount;
            curDurability = Mathf.Clamp(curDurability, 0, maxDurability);
            OnDurabilityChanged?.Invoke(curDurability, maxDurability);

            if (curDurability <= 0)
            {
                Breakdown();
            }
        }

public void Repair(float amount)
        {
            curDurability += amount;
            curDurability = Mathf.Clamp(curDurability, 0, maxDurability);
            OnDurabilityChanged?.Invoke(curDurability, maxDurability);
        }

        /// <summary>멀티플레이 클라이언트: 호스트의 내구도 값을 반영한다 (몬스터 공격은 호스트에서만 계산).</summary>
        public void ApplyNetworkDurability(float value)
        {
            float previous = curDurability;
            curDurability = Mathf.Clamp(value, 0, maxDurability);
            if (!Mathf.Approximately(previous, curDurability))
                OnDurabilityChanged?.Invoke(curDurability, maxDurability);
            if (curDurability <= 0 && !IsBroken)
            {
                Breakdown();
            }
        }

        private void Breakdown()
        {
            IsBroken = true;
            GetComponent<TruckMove>().enabled = false;
        }
    }
}
