using System.Collections;
using UnityEngine;
using Combat;

namespace CraftingSystem
{
    /// <summary>
    /// 설치된 유인 미끼 트랩. Damageable을 가진 대상이 접촉하면 피해를 주고 일정 시간 속박한 뒤 사라진다.
    /// </summary>
    public class TrapPlaced : MonoBehaviour
    {
        private int _damage = 15;
        private float _rootSeconds = 2f;
        private float _triggerRadius = 0.6f;
        private Transform _owner;
        private bool _triggered;

        public void Setup(int damage, float rootSeconds, float triggerRadius, Transform owner)
        {
            _damage = damage;
            _rootSeconds = rootSeconds;
            _triggerRadius = triggerRadius;
            _owner = owner;
        }

        private void Update()
        {
            if (_triggered)
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, _triggerRadius);
            foreach (Collider col in hits)
            {
                if (_owner != null && col.transform.IsChildOf(_owner))
                    continue;

                Damageable target = col.GetComponentInParent<Damageable>();
                if (target == null)
                    continue;

                Trigger(target);
                break;
            }
        }

        private void Trigger(Damageable target)
        {
            _triggered = true;
            target.TakeDamage(_damage, target.transform.position);
            target.Root(_rootSeconds);
            Debug.Log($"[TrapPlaced] {target.name}이(가) 트랩을 밟았습니다. 피해 {_damage}, {_rootSeconds}초 속박.");

            StartCoroutine(TriggeredPulseAndDestroy());
        }

        private IEnumerator TriggeredPulseAndDestroy()
        {
            const float duration = 0.25f;
            float t = 0f;
            Vector3 baseScale = transform.localScale;

            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = baseScale * (1f + (t / duration) * 0.5f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
