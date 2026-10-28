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

        /// <summary>받는 피해 배율 (트럭 복합 장갑 파츠 = 0.75). TruckUpgradeState가 정한다.</summary>
        public float DamageTakenMultiplier { get; set; } = 1f;

        private void Awake()
        {
            // 트럭 파츠/강화(상태·들이받기·포탑)를 씬마다 따로 붙이지 않도록 트럭에 자동으로 붙인다
            if (GetComponent<CraftingSystem.TruckUpgradeState>() == null) gameObject.AddComponent<CraftingSystem.TruckUpgradeState>();
            if (GetComponent<CraftingSystem.TruckRam>() == null) gameObject.AddComponent<CraftingSystem.TruckRam>();
            if (GetComponent<CraftingSystem.TruckTurret>() == null) gameObject.AddComponent<CraftingSystem.TruckTurret>();
        }

        void Start()
        {
            curDurability = Mathf.Max(1f, maxDurability - startingDamage);
            OnDurabilityChanged?.Invoke(curDurability, maxDurability);
        }

        /// <summary>
        /// 최대 내구도를 바꾼다 (강화/장갑). 늘어난 만큼 현재 내구도도 같이 늘리고, 줄면 넘치는 만큼만 깎는다.
        /// </summary>
        public void SetMaxDurability(float value)
        {
            value = Mathf.Max(1f, value);
            if (Mathf.Approximately(value, maxDurability)) return;
            float delta = value - maxDurability;
            maxDurability = value;
            if (delta > 0f) curDurability += delta;
            curDurability = Mathf.Clamp(curDurability, 0f, maxDurability);
            OnDurabilityChanged?.Invoke(curDurability, maxDurability);
        }

public void Take_Damage(float amount)
        {
            amount *= DamageTakenMultiplier;   // 장갑 파츠
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
