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
        private bool _handledByWeapons;

        private void Start()
        {
            // 무기 시스템(Combat.WeaponController)이 붙은 캐릭터는 좌클릭 공격(주먹/무기)을 그쪽에서 처리한다.
            // 이 스크립트까지 켜져 있으면 피해가 두 번 들어가고, 애니메이터의 doAttack(점프 모션)이 재생되어 캐릭터가 뜬 것처럼 보인다.
            _handledByWeapons = GetComponent<Combat.WeaponController>() != null;
            if (_handledByWeapons)
            {
                enabled = false;
                return;
            }

            character = GetComponent<Character>();
            anim = GetComponentInChildren<Animator>();

            if (attackPoint == null)
            {
                attackPoint = transform;
            }
        }

        private void Update()
        {
            if (_handledByWeapons) return; // 다른 스크립트가 다시 켜도 동작하지 않게
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
            float damageToApply = (character != null && character.StatsData != null) ? character.StatsData.AttackPower : 10f;

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