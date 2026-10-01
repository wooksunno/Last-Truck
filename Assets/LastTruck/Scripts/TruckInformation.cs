using UnityEngine;

namespace LastTruck
{
    public class TruckInformation : MonoBehaviour
    {
        [Header("트럭 내구도")]
        [SerializeField] private float maxDurability = 100f;
        private float curDurability;

        public float CurrentDurability => curDurability;
        public float MaxDurability => maxDurability;

        /// <summary>내구도가 0이 되어 부서졌는가 (멀티플레이 게임 오버 판정에 사용).</summary>
        public bool IsBroken { get; private set; }

        void Start()
        {
            curDurability = maxDurability;
        }

        public void Take_Damage(float amount)
        {
            curDurability -= amount;
            curDurability = Mathf.Clamp(curDurability, 0, maxDurability);

            if (curDurability <= 0)
            {
                Breakdown();
            }
        }

        public void Repair(float amount)
        {
            curDurability += amount;
            curDurability = Mathf.Clamp(curDurability, 0, maxDurability);
        }

        /// <summary>멀티플레이 클라이언트: 호스트의 내구도 값을 반영한다 (몬스터 공격은 호스트에서만 계산).</summary>
        public void ApplyNetworkDurability(float value)
        {
            curDurability = Mathf.Clamp(value, 0, maxDurability);
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
