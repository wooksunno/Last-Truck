using UnityEngine;

namespace LastTruck
{
    public interface IDamageable
    {
        void TakeDamage(float damage, GameObject attacker);
    }
}