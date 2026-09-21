using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace LastTruck
{
    public class PlayerAttack : MonoBehaviour
    {
        [Header("공격 설정")]
        public Transform attackPoint;       // 타격 중심점
        public float attackRange = 1.5f;    // 타격 반지름

        [Header("공격 쿨타임")]
        public float attackCooldown = 0.5f;
        private float lastAttackTime;

        public List<string> targetTags = new List<string> { "Player", "Enemy" };

        private Character character;
        private Animator anim;

        private void Start()
        {
            character = GetComponent<Character>();
            anim = GetComponentInChildren<Animator>();

            if (attackPoint == null)
            {
                attackPoint = transform;
            }
        }

        private void Update()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { return; }
            if (Input.GetMouseButtonDown(0) && Time.time >= lastAttackTime + attackCooldown)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
        }

        private void PerformAttack()
        {
            if (anim != null)
            {
                anim.SetTrigger("doAttack");
            }

            Collider[] hitColliders = Physics.OverlapSphere(attackPoint.position, attackRange);
            float damageToApply = (character != null && character.stats != null) ? character.stats.attackPower : 10f;

            foreach (var col in hitColliders)
            {
                if (col.gameObject == gameObject) continue;

                if (IsTargetTag(col.gameObject))
                {
                    if (col.TryGetComponent<IDamageable>(out var damageable))
                    {
                        damageable.TakeDamage(damageToApply, gameObject);
                    }
                }
            }
        }

        private bool IsTargetTag(GameObject obj)
        {
            foreach (string tag in targetTags)
            {
                if (obj.CompareTag(tag))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (attackPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(attackPoint.position, attackRange);
            }
        }
    }
}