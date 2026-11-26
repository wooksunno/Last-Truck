using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 화학 독가스 수류탄이 터진 자리에 남는 가스 구름. 반경 안의 Damageable에게 일정 간격으로 지속 피해를 준다.
    /// </summary>
    public class GasCloudEffect : MonoBehaviour
    {
        private float _radius;
        private float _duration;
        private float _tickInterval;
        private int _tickDamage;
        private LayerMask _hitMask;

        public void Setup(float radius, float duration, float tickInterval, int tickDamage, LayerMask hitMask)
        {
            _radius = radius;
            _duration = duration;
            _tickInterval = tickInterval;
            _tickDamage = tickDamage;
            _hitMask = hitMask;

            BuildVisual();
            StartCoroutine(Run());
        }

        private GameObject _smoke, _bubbles;

        private void BuildVisual()
        {
            // 진짜 연기: 초록빛 연기 구름(번져 오르며 흩어짐) + 보글거리는 독 방울
            float k = Mathf.Max(0.3f, _radius / 4.2f);   // 기준(배율 1)의 연기는 반경 약 9m라서 반경에 맞게 줄인다
            _smoke = CombatFx.Spawn("FX_Grenade_Smoke_01", transform.position, Quaternion.identity, k * 0.8f, 0f, transform);
            if (_smoke != null) CombatFx.Tint(_smoke, new Color(0.42f, 0.85f, 0.22f, 0.5f));
            _bubbles = CombatFx.Spawn("FX_Poison_Green_01", transform.position, Quaternion.identity, k * 1.2f, 0f, transform);
            if (_smoke != null || _bubbles != null) return;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "GasCloudVisual";
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * (_radius * 2f);

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.35f, 0.85f, 0.25f));
            visual.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private IEnumerator Run()
        {
            float elapsed = 0f;
            var wait = new WaitForSeconds(_tickInterval);

            while (elapsed < _duration)
            {
                yield return wait;
                elapsed += _tickInterval;
                Tick();
            }

            // 새로 뿜는 것만 멈추고, 남은 연기는 서서히 흩어지게 둔다
            ReleaseFx(_smoke); ReleaseFx(_bubbles);
            Destroy(gameObject);
        }

        private static void ReleaseFx(GameObject fx)
        {
            if (fx == null) return;
            fx.transform.SetParent(null, true);
            CombatFx.StopEmitting(fx, 8f);
        }

        private void Tick()
        {
            if (_tickDamage <= 0)
                return; // 멀티플레이 복제 구름(보여주기만)
            Collider[] hits = Physics.OverlapSphere(transform.position, _radius, _hitMask);
            var damaged = new HashSet<Damageable>();
            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null || damaged.Contains(d))
                    continue;
                d.TakeDamage(_tickDamage, col.ClosestPoint(transform.position));
                damaged.Add(d);
            }
        }
    }
}
