using UnityEngine;

namespace LastTruck
{
    public class TruckInformation : MonoBehaviour
    {
        [Header("트럭 내구도")]
        [SerializeField] private float maxDurability = 100f;
        private float curDurability;

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

        private void Breakdown()
        {
            GetComponent<TruckMove>().enabled = false;
        }
    }
}
