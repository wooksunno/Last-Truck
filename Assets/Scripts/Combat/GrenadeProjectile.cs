using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 던져진 화학 독가스 수류탄. 포물선으로 날아가다 무언가에 맞으면 즉시 폭발 피해를 주고,
    /// 그 자리에 독가스 구름(GasCloudEffect)을 남겨 지속 피해를 준다.
    /// </summary>
    public class GrenadeProjectile : MonoBehaviour
    {
        [SerializeField] private float gravity = 9f;
        [SerializeField] private float maxLifetime = 5f;

        private Vector3 _velocity;
        private int _blastDamage;
        private float _blastRadius;
        private float _gasDuration;
        private float _gasTickInterval;
        private int _gasTickDamage;
        private LayerMask _hitMask;
        private Transform _thrower;
        private bool _exploded;

        public float Gravity => gravity;

        public void Launch(Vector3 velocity, int blastDamage, float blastRadius,
            float gasDuration, float gasTickInterval, int gasTickDamage,
            LayerMask hitMask, Transform thrower)
        {
            _velocity = velocity;
            _blastDamage = blastDamage;
            _blastRadius = blastRadius;
            _gasDuration = gasDuration;
            _gasTickInterval = gasTickInterval;
            _gasTickDamage = gasTickDamage;
            _hitMask = hitMask;
            _thrower = thrower;

            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            if (_exploded)
                return;

            _velocity += Vector3.down * (gravity * Time.deltaTime);
            Vector3 step = _velocity * Time.deltaTime;
            float stepLength = step.magnitude;
            if (stepLength < 0.0001f)
                return;

            Vector3 dir = step / stepLength;
            if (Physics.SphereCast(transform.position, 0.1f, dir, out RaycastHit hit, stepLength, _hitMask, QueryTriggerInteraction.Ignore))
            {
                if (_thrower == null || !hit.collider.transform.IsChildOf(_thrower))
                {
                    Explode(hit.point);
                    return;
                }
            }

            transform.position += step;
        }

        private void Explode(Vector3 point)
        {
            _exploded = true;

            Collider[] hits = Physics.OverlapSphere(point, _blastRadius, _hitMask);
            var damaged = new HashSet<Damageable>();
            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null || damaged.Contains(d))
                    continue;
                d.TakeDamage(_blastDamage, col.ClosestPoint(point));
                damaged.Add(d);
            }

            if (_gasDuration > 0f && _gasTickDamage > 0)
            {
                GameObject cloudGO = new GameObject("GasCloud");
                cloudGO.transform.position = point;
                GasCloudEffect cloud = cloudGO.AddComponent<GasCloudEffect>();
                cloud.Setup(_blastRadius * 1.4f, _gasDuration, _gasTickInterval, _gasTickDamage, _hitMask);
            }

            Destroy(gameObject);
        }
    }
}
