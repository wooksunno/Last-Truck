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

        // 멀티플레이 (LastTruck.Networking.NetworkGathering)
        //  - 호스트의 덫만 밟았는지 판정한다. 클라이언트의 덫은 보여주기만 하다가 호스트가 알려주면 터진다.
        public int NetworkId { get; set; }
        public bool VisualOnly { get; set; }

        /// <summary>호스트: 덫이 발동한 순간.</summary>
        public static event System.Action<TrapPlaced> Triggered;

        /// <summary>클라이언트: 호스트가 알려준 발동 연출만 한다.</summary>
        public void PlayTriggered()
        {
            if (_triggered)
                return;
            _triggered = true;
            StartCoroutine(TriggeredPulseAndDestroy());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Triggered = null;

        public void Setup(int damage, float rootSeconds, float triggerRadius, Transform owner)
        {
            _damage = damage;
            _rootSeconds = rootSeconds;
            _triggerRadius = triggerRadius;
            _owner = owner;
        }

        private void Update()
        {
            if (_triggered || VisualOnly)
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
            // 능력 보너스(군인/경찰)는 덫을 설치한 사람 기준 (멀티플레이에서 호스트가 판정해도 설치자의 능력).
            LastTruck.CharacterAbilityController ownerAbility =
                _owner != null ? _owner.GetComponentInParent<LastTruck.CharacterAbilityController>() : null;
            target.TakeDamage(_damage, target.transform.position, ownerAbility);
            target.Root(_rootSeconds);
            Triggered?.Invoke(this);
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
