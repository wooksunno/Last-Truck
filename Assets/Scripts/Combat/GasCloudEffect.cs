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

        private void BuildVisual()
        {
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

            Destroy(gameObject);
        }

        private void Tick()
        {
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
