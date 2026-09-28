using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 활에서 발사된 화살. 매 프레임 이동 구간을 Raycast로 검사해 빠른 속도에서도 관통 없이 맞힌다.
    /// 맞으면 Damageable에 피해를 주고, 맞은 곳에 잠시 꽂혀 있다가 사라진다.
    /// 화살 모델은 피벗이 꼬리(오늬)이고 로컬 +Z 방향이 화살촉이라고 가정한다.
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        [SerializeField] private float arrowLength = 0.764f;
        [SerializeField] private float gravity = 4f;
        [SerializeField] private float maxLifetime = 4f;
        [SerializeField] private float stuckLifetime = 2f;

        private Vector3 _velocity;
        private int _damage;
        private float _maxDistance;
        private float _travelled;
        private LayerMask _hitMask;
        private Transform _shooter;
        private bool _stuck;

        public float Gravity => gravity;

        public void Launch(Vector3 velocity, int damage, float maxDistance, LayerMask hitMask, Transform shooter)
        {
            _velocity = velocity;
            _damage = damage;
            _maxDistance = maxDistance;
            _hitMask = hitMask;
            _shooter = shooter;

            if (_velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_velocity);

            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            if (_stuck)
                return;

            _velocity += Vector3.down * (gravity * Time.deltaTime);
            Vector3 step = _velocity * Time.deltaTime;
            float stepLength = step.magnitude;
            if (stepLength < 0.0001f)
                return;

            Vector3 dir = step / stepLength;
            Vector3 tip = transform.position + transform.forward * arrowLength;
            RaycastHit[] hits = Physics.RaycastAll(tip, dir, stepLength, _hitMask, QueryTriggerInteraction.Ignore);
            if (TryGetFirstValidHit(hits, out RaycastHit hit))
            {
                OnHit(hit, dir);
                return;
            }

            transform.position += step;
            transform.rotation = Quaternion.LookRotation(dir);

            _travelled += stepLength;
            if (_travelled >= _maxDistance)
                Destroy(gameObject);
        }

        private bool TryGetFirstValidHit(RaycastHit[] hits, out RaycastHit best)
        {
            best = default;
            float bestDist = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                // 쏜 사람 자신의 콜라이더는 무시한다.
                if (_shooter != null && h.collider.transform.IsChildOf(_shooter))
                    continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                }
            }
            return bestDist < float.MaxValue;
        }

        private void OnHit(RaycastHit hit, Vector3 dir)
        {
            _stuck = true;

            // 화살이 살짝 박힌 모습으로 멈춘다. 대상이 파괴되면 화살도 같이 사라진다.
            transform.rotation = Quaternion.LookRotation(dir);
            transform.position = hit.point - dir * (arrowLength - 0.15f);
            transform.SetParent(hit.collider.transform, true);

            Damageable target = hit.collider.GetComponentInParent<Damageable>();
            if (target != null)
                target.TakeDamage(_damage, hit.point);

            Destroy(gameObject, stuckLifetime);
        }
    }
}
