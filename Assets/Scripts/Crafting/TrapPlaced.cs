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

        // ---------------------------------------------------------------- 페로몬 유인
        // 독성 유인 페로몬을 붙인 덫: 주변 몬스터가 (플레이어가 바로 곁에 있지 않은 한) 덫으로 몰려와서 걸린다.
        // 판정은 싱글/호스트에서만 (몬스터 AI가 거기서 돌기 때문). 몬스터 AI(MonsterAI / NetworkMonster)가 FindLureFor / TryCatch를 부른다.
        public const float LureRadius = 18f;       // 이 안의 몬스터가 끌려온다
        public const float CatchRadius = 2.4f;     // 몬스터가 이 안까지 오면 덫이 잡는다 (몬스터가 멈춰 서는 거리가 달라도 걸리게)
        private const float LureSeconds = 45f;     // 페로몬 냄새가 가시기까지 (그 뒤에는 평범한 덫)

        private static readonly System.Collections.Generic.List<TrapPlaced> LureTraps = new System.Collections.Generic.List<TrapPlaced>();
        private bool _lure;
        private float _lureEndsAt;

        public bool IsLuring => _lure && !_triggered && !VisualOnly && Time.time < _lureEndsAt;

        /// <summary>페로몬을 붙인 덫으로 만든다 (모양에 초록 기운도 붙인다). 판정은 싱글/호스트에서만 의미가 있다.</summary>
        public void EnableLure()
        {
            if (_lure) return;
            _lure = true;
            _lureEndsAt = Time.time + LureSeconds;
            LureTraps.Add(this);
        }

        private void OnDestroy() { LureTraps.Remove(this); }

        /// <summary>이 위치의 몬스터를 끌어당기는 페로몬 덫 (가장 가까운 것). 없으면 null.</summary>
        public static TrapPlaced FindLureFor(Vector3 position)
        {
            TrapPlaced best = null;
            float bestDist = LureRadius;
            for (int i = 0; i < LureTraps.Count; i++)
            {
                TrapPlaced t = LureTraps[i];
                if (t == null || !t.IsLuring) continue;
                float d = Horizontal(position, t.transform.position);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        public bool Reached(Vector3 position) => Horizontal(position, transform.position) <= CatchRadius;

        /// <summary>덫에 닿은 몬스터를 잡는다 (피해 + 속박). 덫이 이미 터졌으면 false.</summary>
        public bool TryCatch(Damageable monster)
        {
            if (_triggered || VisualOnly || monster == null) return false;
            Trigger(monster);
            return true;
        }

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLureStatics() => LureTraps.Clear();

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
