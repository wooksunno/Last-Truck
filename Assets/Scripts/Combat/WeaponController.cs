using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CraftingSystem;

namespace Combat
{
    /// <summary>
    /// 인벤토리에서 무기를 선택한 상태로 좌클릭하면 무기 종류에 맞는 방식으로 공격한다.
    /// - Hitscan(AK-47/백금 저격소총): 클릭 시 마우스 방향으로 즉시 한 발.
    /// - MeleeArc(마체테): 클릭 시 주변(전방 부채꼴)에서 가장 가까운 적 최대 1명에게 근접 피해.
    /// - ContinuousCone(화염방사기): 누르고 있는 동안 전방 부채꼴 범위에 주기적으로 피해 + 불꽃 이펙트.
    /// - ChargeAndRelease(사냥용 활): 누르고 있으면 차징, 일정 시간 이상 차징한 뒤 떼면 화살 발사.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class WeaponController : MonoBehaviour
    {
        private enum WeaponBehavior
        {
            Hitscan,
            MeleeArc,
            ContinuousCone,
            ChargeAndRelease,
        }

        private struct WeaponStats
        {
            public WeaponBehavior behavior;
            public int damage;
            public float range;
            public float cooldown;
            public float coneAngle;
            public float chargeTime;
            public float minChargeFraction;
            public Color effectColor;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask hitMask = ~0;

        private PlayerInventory _inventory;
        private float _nextFireTime;

        private bool _pressVetoed;
        private bool _isCharging;
        private float _chargeStartTime;
        private bool _isFlameActive;
        private Coroutine _flameEffectRoutine;
        private string _activeWeaponId;

        private static readonly Dictionary<string, WeaponStats> Weapons = new Dictionary<string, WeaponStats>
        {
            {
                ItemIds.Ak47, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 15, range = 40f, cooldown = 0.15f,
                    effectColor = new Color(1f, 0.9f, 0.3f),
                }
            },
            {
                ItemIds.PlatinumSniperRifle, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 40, range = 80f, cooldown = 1.0f,
                    effectColor = new Color(0.4f, 0.85f, 1f),
                }
            },
            {
                ItemIds.Flamethrower, new WeaponStats
                {
                    behavior = WeaponBehavior.ContinuousCone, damage = 6, range = 6f, cooldown = 0.15f,
                    coneAngle = 50f, effectColor = new Color(1f, 0.5f, 0.15f),
                }
            },
            {
                ItemIds.HuntingBow, new WeaponStats
                {
                    behavior = WeaponBehavior.ChargeAndRelease, damage = 20, range = 30f,
                    chargeTime = 1.0f, minChargeFraction = 0.2f, effectColor = new Color(0.55f, 0.4f, 0.2f),
                }
            },
            {
                ItemIds.Machete, new WeaponStats
                {
                    behavior = WeaponBehavior.MeleeArc, damage = 25, range = 1.6f, cooldown = 0.4f,
                    coneAngle = 120f, effectColor = Color.white,
                }
            },
        };

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Update()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            bool blocked =
                (UnityEngine.EventSystems.EventSystem.current != null &&
                 UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) ||
                (GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen) ||
                targetCamera == null;

            ItemData selected = _inventory.SelectedItem;
            WeaponStats stats = default;
            bool hasWeapon = !blocked && selected != null && Weapons.TryGetValue(selected.itemID, out stats);

            if (!hasWeapon || selected.itemID != _activeWeaponId)
            {
                // 무기를 바꾸거나 내려놓으면 진행 중이던 차징/화염 상태를 정리한다.
                CancelOngoingActions();
                _activeWeaponId = hasWeapon ? selected.itemID : null;
                if (!hasWeapon)
                    return;
            }

            switch (stats.behavior)
            {
                case WeaponBehavior.Hitscan:
                    if (Input.GetMouseButtonDown(0))
                    {
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);
                        if (!_pressVetoed && Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + stats.cooldown;
                            FireHitscan(stats, stats.damage);
                        }
                    }
                    break;

                case WeaponBehavior.MeleeArc:
                    if (Input.GetMouseButtonDown(0))
                    {
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);
                        if (!_pressVetoed && Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + stats.cooldown;
                            FireMeleeArc(stats);
                        }
                    }
                    break;

                case WeaponBehavior.ContinuousCone:
                    if (Input.GetMouseButtonDown(0))
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);

                    if (!_pressVetoed && Input.GetMouseButton(0))
                    {
                        AimTowardMouse(stats.range);

                        if (!_isFlameActive)
                        {
                            _isFlameActive = true;
                            _flameEffectRoutine = StartCoroutine(FlameEffectRoutine(stats));
                        }

                        if (Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + stats.cooldown;
                            FireCone(stats);
                        }
                    }
                    else if (_isFlameActive && (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)))
                    {
                        StopFlameEffect();
                    }
                    break;

                case WeaponBehavior.ChargeAndRelease:
                    if (Input.GetMouseButtonDown(0))
                    {
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);
                        if (!_pressVetoed)
                        {
                            _isCharging = true;
                            _chargeStartTime = Time.time;
                        }
                    }
                    else if (Input.GetMouseButtonUp(0) && _isCharging)
                    {
                        _isCharging = false;
                        float held = Time.time - _chargeStartTime;
                        float fraction = Mathf.Clamp01(held / stats.chargeTime);
                        if (fraction >= stats.minChargeFraction)
                        {
                            int scaledDamage = Mathf.Max(1, Mathf.RoundToInt(stats.damage * Mathf.Lerp(0.4f, 1f, fraction)));
                            FireHitscan(stats, scaledDamage);
                        }
                    }
                    break;
            }
        }

        private void CancelOngoingActions()
        {
            _isCharging = false;
            StopFlameEffect();
        }

        private void StopFlameEffect()
        {
            _isFlameActive = false;
            if (_flameEffectRoutine != null)
            {
                StopCoroutine(_flameEffectRoutine);
                _flameEffectRoutine = null;
            }
        }

        private bool RaycastHitsWorldInteractable(float range)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out RaycastHit hit, range, hitMask) && IsWorldInteractable(hit.collider);
        }

/// <summary>
        /// 화염방사기를 쓸 때, 현재 바라보는 방향이 아니라 마우스로 클릭(드래그)한 방향을 바라보도록 회전한다.
        /// </summary>
        private void AimTowardMouse(float range)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 targetPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, range * 2f, hitMask))
            {
                targetPoint = hit.point;
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, transform.position);
                targetPoint = groundPlane.Raycast(ray, out float enter)
                    ? ray.GetPoint(enter)
                    : ray.origin + ray.direction * range;
            }

            Vector3 dir = targetPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }


        private void FireHitscan(WeaponStats stats, int damage)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 muzzle = transform.position + Vector3.up * 1.2f;
            Vector3 endPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, stats.range, hitMask))
            {
                endPoint = hit.point;
                Damageable target = hit.collider.GetComponentInParent<Damageable>();
                if (target != null)
                    target.TakeDamage(damage, hit.point);

                SpawnImpact(hit.point, stats.effectColor);
            }
            else
            {
                endPoint = ray.origin + ray.direction * stats.range;
            }

            SpawnTracer(muzzle, endPoint, stats.effectColor);
        }

        /// <summary>
        /// 마체테: 전방 부채꼴 안에서 가장 가까운 적 최대 1명에게만 피해를 준다.
        /// </summary>
        private void FireMeleeArc(WeaponStats stats)
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            Collider[] hits = Physics.OverlapSphere(origin, stats.range, hitMask);

            Damageable nearest = null;
            Vector3 nearestPoint = origin;
            float nearestDist = float.MaxValue;

            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null)
                    continue;

                Vector3 toTarget = col.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(transform.forward, toTarget.normalized) > stats.coneAngle * 0.5f)
                    continue;

                float dist = (col.transform.position - transform.position).sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = d;
                    nearestPoint = col.ClosestPoint(origin);
                }
            }

            if (nearest != null)
            {
                nearest.TakeDamage(stats.damage, nearestPoint);
                SpawnImpact(nearestPoint, stats.effectColor);
            }
        }

        /// <summary>
        /// 화염방사기: 전방 부채꼴 범위 안의 모든 적에게 동시에 피해를 준다.
        /// </summary>
        private void FireCone(WeaponStats stats)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Collider[] hits = Physics.OverlapSphere(origin, stats.range, hitMask);
            var damaged = new HashSet<Damageable>();

            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null || damaged.Contains(d))
                    continue;

                Vector3 toTarget = col.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f)
                    continue;
                if (Vector3.Angle(transform.forward, toTarget.normalized) > stats.coneAngle * 0.5f)
                    continue;

                d.TakeDamage(stats.damage, col.ClosestPoint(origin));
                damaged.Add(d);
            }
        }

        private static bool IsWorldInteractable(Collider collider)
        {
            return collider.GetComponentInParent<ProcessingFacility>() != null ||
                   collider.GetComponentInParent<TruckStation>() != null ||
                   collider.GetComponentInParent<ResourceNode>() != null ||
                   collider.GetComponentInParent<SpecialResourceNode>() != null;
        }

        private IEnumerator FlameEffectRoutine(WeaponStats stats)
        {
            var wait = new WaitForSeconds(0.03f);
            while (true)
            {
                Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
                float angleOffset = Random.Range(-stats.coneAngle * 0.4f, stats.coneAngle * 0.4f);
                Vector3 dir = Quaternion.Euler(0f, angleOffset, 0f) * transform.forward;
                float dist = Random.Range(stats.range * 0.3f, stats.range);
                Vector3 pos = origin + dir * dist + Vector3.up * Random.Range(-0.2f, 0.3f);
                SpawnFlameParticle(pos, stats.effectColor);
                yield return wait;
            }
        }

        private void SpawnFlameParticle(Vector3 pos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FlameParticle";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            float size = Random.Range(0.08f, 0.18f);
            go.transform.localScale = Vector3.one * size;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            Destroy(go, 0.25f);
        }

        private void SpawnTracer(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("WeaponTracer");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.05f;
            lr.endWidth = 0.02f;
            lr.numCapVertices = 4;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0.2f);
            Destroy(go, 0.08f);
        }

        private void SpawnImpact(Vector3 pos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WeaponImpact";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.12f;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            StartCoroutine(AnimateImpact(go));
        }

        private static IEnumerator AnimateImpact(GameObject go)
        {
            const float duration = 0.18f;
            float t = 0f;
            Vector3 baseScale = go.transform.localScale;

            while (t < duration)
            {
                t += Time.deltaTime;
                go.transform.localScale = baseScale * (1f + (t / duration) * 3f);
                yield return null;
            }

            Destroy(go);
        }
    }
}
