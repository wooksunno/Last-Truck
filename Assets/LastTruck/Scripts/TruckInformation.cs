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

        private void Breakdown()
        {
            GetComponent<TruckMove>().enabled = false;
        }
    }
}
