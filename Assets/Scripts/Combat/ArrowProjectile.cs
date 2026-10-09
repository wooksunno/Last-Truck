using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 활의 화살·총의 총알 공용 발사체. 매 프레임 이동 구간을 Raycast로 검사해 빠른 속도에서도 관통 없이 맞힌다.
    /// 맞으면 Damageable에 피해를 주고, 화살은 맞은 곳에 잠시 꽂혀 있다가 사라진다(총알은 바로 사라짐).
    /// 모델은 피벗이 꼬리이고 로컬 +Z 방향이 앞(화살촉/탄두)이라고 가정한다.
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        [SerializeField] private float arrowLength = 0.764f;
        [SerializeField] private float gravity = 4f;
        [SerializeField] private float maxLifetime = 4f;
        [Tooltip("맞은 곳에 꽂혀 남는지(화살). 끄면 맞는 즉시 사라진다(총알)")]
        [SerializeField] private bool stickOnHit = true;
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

        [Header("적중 이펙트")]
        [Tooltip("맞았을 때 맞은 지점에 한 번 터지는 이펙트(총알용). 비워두면 없음")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private float hitVfxScale = 0.35f;

        private void SpawnHitVfx(RaycastHit hit)
        {
            if (hitVfxPrefab == null)
                return;

            GameObject fx = Instantiate(hitVfxPrefab, hit.point + hit.normal * 0.05f, Quaternion.LookRotation(hit.normal));
            fx.transform.localScale = Vector3.one * hitVfxScale;
            Transform darkBack = fx.transform.Find("Darkback");
            if (darkBack != null)
                darkBack.gameObject.SetActive(false);
            // 시작 버스트가 터진 뒤(0.08초) 방출을 멈춰 반복되지 않게 한다
            fx.AddComponent<DelayedStopEmit>().Init(0.08f);
            Destroy(fx, 1.2f);
        }

        private void OnHit(RaycastHit hit, Vector3 dir)
        {
            _stuck = true;
            SpawnHitVfx(hit);
            CameraShake.Shake(0.05f);

            if (!stickOnHit)
            {
                Damageable hitTarget = hit.collider.GetComponentInParent<Damageable>();
                if (hitTarget != null)
                    hitTarget.TakeDamage(_damage, hit.point);
                Destroy(gameObject);
                return;
            }

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

namespace Combat
{
    /// <summary>생성 후 지정한 시간이 지나면 하위 파티클의 방출만 멈춘다(남은 입자는 자연스럽게 사라짐).</summary>
    public class DelayedStopEmit : MonoBehaviour
    {
        private float _stopAt;

        public void Init(float seconds)
        {
            _stopAt = Time.time + seconds;
        }

        private void Update()
        {
            if (Time.time < _stopAt)
                return;

            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(this);
        }
    }
}
