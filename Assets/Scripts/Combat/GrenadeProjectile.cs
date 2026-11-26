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

        /// <summary>터진 자리에 남는 것: 독가스 구름 / 불길(화염병) / 신호탄 불빛.</summary>
        public enum AreaKind { Gas = 0, Fire = 1, Flare = 2 }
        private AreaKind _area = AreaKind.Gas;

        public void SetArea(AreaKind area) => _area = area;

        // 멀티플레이: 다른 사람이 던진 수류탄을 보여주기만 하는 복제본 (피해 없음, 가스 구름도 모양만).
        private bool _visualOnly;

        public void LaunchVisual(Vector3 velocity, float blastRadius, float gasDuration, LayerMask hitMask, Transform thrower)
        {
            _visualOnly = true;
            Launch(velocity, 0, blastRadius, gasDuration, 1f, 0, hitMask, thrower);
        }

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

        // 터지는 순간의 이펙트: 파편 수류탄 = 폭발, 화염병 = 불덩이, 독가스 = 뻥 터지는 초록 연기, 돌 = 돌가루
        private void SpawnBlastFx(Vector3 point)
        {
            Vector3 p = point + Vector3.up * 0.1f;
            if (_area == AreaKind.Flare) return;
            if (_area == AreaKind.Fire)
            {
                CombatFx.Spawn("FX_Fire_Explosion_01", p, Quaternion.identity, Mathf.Max(0.6f, _blastRadius / 2.6f), 6f);
            }
            else if (_gasDuration > 0f)
            {
                GameObject puff = CombatFx.Spawn("FX_Grenade_Explosive_01", p, Quaternion.identity, 0.55f, 5f);
                if (puff != null) CombatFx.Tint(puff, new Color(0.5f, 0.9f, 0.25f, 0.9f));
            }
            else if (_blastRadius > 1.5f)
            {
                CombatFx.Spawn("FX_Grenade_Explosive_01", p, Quaternion.identity, Mathf.Max(1f, _blastRadius / 1.6f), 6f);
                Camera cam = Camera.main;
                if (cam != null && (cam.transform.position - point).sqrMagnitude < 18f * 18f) CameraShake.Shake(0.18f);
            }
            else
            {
                CombatFx.Spawn("FX_Impact_Stone_01", p, Quaternion.identity, 0.7f, 3f);
            }
        }

        private void Explode(Vector3 point)
        {
            _exploded = true;

            Collider[] hits = _visualOnly || _blastDamage <= 0 ? new Collider[0] : Physics.OverlapSphere(point, _blastRadius, _hitMask);
            var damaged = new HashSet<Damageable>();
            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null || damaged.Contains(d))
                    continue;
                d.TakeDamage(_blastDamage, col.ClosestPoint(point));
                damaged.Add(d);
            }

            SpawnBlastFx(point);

            if (_area == AreaKind.Flare)
            {
                FlareLight.Spawn(point, _gasDuration);
            }
            else if (_area == AreaKind.Fire && _gasDuration > 0f)
            {
                var fireGO = new GameObject("FireArea");
                fireGO.transform.position = point;
                fireGO.AddComponent<FireAreaEffect>().Setup(_blastRadius, _gasDuration, _gasTickInterval, _visualOnly ? 0 : _gasTickDamage, _hitMask);
            }
            else if (_gasDuration > 0f && (_gasTickDamage > 0 || _visualOnly))
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
