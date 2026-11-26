using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 화염병이 깨진 자리에 남는 불길. 반경 안의 적(Damageable)에게 일정 간격으로 불 피해를 준다 (플레이어는 태우지 않는다).
    /// 보이는 모습: 그을린 바닥 + 흔들리는 불꽃 덩어리 + 깜빡이는 주황 불빛. 끝나기 1초 전부터 사그라든다.
    /// tickDamage가 0이면 멀티플레이 복제본(보여주기만).
    /// </summary>
    public class FireAreaEffect : MonoBehaviour
    {
        private static Material _fxMat, _scorchMat;

        private float _radius, _duration, _tickInterval, _age, _nextTick;
        private int _tickDamage;
        private LayerMask _hitMask;
        private Transform[] _flames;
        private Vector3[] _flameBase;
        private float[] _flameSeed;
        private Renderer[] _flameR;
        private Light _light;
        private List<GameObject> _fireFx;
        private bool _fxStopped;
        private MaterialPropertyBlock _mpb;

        public void Setup(float radius, float duration, float tickInterval, int tickDamage, LayerMask hitMask)
        {
            _radius = Mathf.Max(0.5f, radius);
            _duration = duration;
            _tickInterval = Mathf.Max(0.1f, tickInterval);
            _tickDamage = tickDamage;
            _hitMask = hitMask;
            _nextTick = _tickInterval * 0.5f;
            _mpb = new MaterialPropertyBlock();
            Build();
        }

        private void Build()
        {
            if (_fxMat == null) _fxMat = new Material(Shader.Find("Sprites/Default"));
            if (_scorchMat == null)
            {
                // 반투명한 그을음 (불투명하면 바닥에 구멍이 난 것처럼 보인다)
                _scorchMat = new Material(Shader.Find("Sprites/Default"));
                _scorchMat.color = new Color(0.12f, 0.07f, 0.04f, 0.5f);
            }

            // 그을린 바닥
            var scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(scorch.GetComponent<Collider>());
            scorch.name = "Scorch";
            scorch.transform.SetParent(transform, false);
            scorch.transform.localPosition = Vector3.up * 0.03f;
            scorch.transform.localScale = new Vector3(_radius * 1.6f, 0.01f, _radius * 1.6f);
            var sr = scorch.GetComponent<Renderer>();
            sr.sharedMaterial = _scorchMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 진짜 불꽃: 반경 안에 불 이펙트를 여러 군데 피운다 (이펙트가 없으면 아래 둥근 도형 불꽃으로 대신)
            _fireFx = new List<GameObject>();
            int clusters = Mathf.Clamp(Mathf.RoundToInt(_radius * 1.6f), 3, 8);
            for (int i = 0; i < clusters; i++)
            {
                Vector2 q = i == 0 ? Vector2.zero : Random.insideUnitCircle * _radius * 0.8f;
                GameObject fx = CombatFx.Spawn(i % 3 == 0 ? "FX_Fire_Big_01" : "FX_Fire_Small_01", transform.position + new Vector3(q.x, 0f, q.y),
                    Quaternion.identity, Mathf.Clamp(_radius / 3.2f, 0.45f, 1.1f), 0f, transform);
                if (fx != null) _fireFx.Add(fx);
            }
            bool realFire = _fireFx.Count > 0;

            int n = realFire ? 0 : Mathf.Clamp(Mathf.RoundToInt(_radius * 6f), 8, 18);
            _flames = new Transform[n];
            _flameBase = new Vector3[n];
            _flameSeed = new float[n];
            _flameR = new Renderer[n];
            for (int i = 0; i < n; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(f.GetComponent<Collider>());
                f.name = "Flame";
                f.transform.SetParent(transform, false);
                Vector2 p = Random.insideUnitCircle * _radius * 0.85f;
                _flameBase[i] = new Vector3(p.x, 0f, p.y);
                _flameSeed[i] = Random.value * 10f;
                _flames[i] = f.transform;
                _flameR[i] = f.GetComponent<Renderer>();
                _flameR[i].sharedMaterial = _fxMat;
                _flameR[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 1f;
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.55f, 0.2f);
            _light.range = _radius * 4f;
            _light.intensity = 3f;
            _light.shadows = LightShadows.None;

            Update();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _duration) { Destroy(gameObject); return; }

            if (!_fxStopped && _duration - _age < 1.5f && _fireFx != null)
            {
                // 끝나기 직전: 새 불꽃을 멈추고 남은 불꽃이 사그라들게 한다
                _fxStopped = true;
                foreach (GameObject fx in _fireFx) if (fx != null) { fx.transform.SetParent(null, true); CombatFx.StopEmitting(fx, 4f); }
            }
            float fade = Mathf.Clamp01(_duration - _age);                 // 마지막 1초 동안 사그라든다
            float grow = Mathf.Clamp01(_age / 0.25f);                     // 처음엔 확 번진다
            float life = fade * grow;

            for (int i = 0; i < _flames.Length; i++)
            {
                float t = Time.time * 7f + _flameSeed[i];
                float flick = 0.75f + 0.25f * Mathf.Sin(t) + 0.15f * Mathf.Sin(t * 2.3f + 1f);
                float h = (0.9f + 0.8f * Mathf.PerlinNoise(_flameSeed[i], Time.time * 2f)) * flick * life;
                float w = 0.5f * life * (0.8f + 0.2f * flick);
                _flames[i].localScale = new Vector3(w, h, w);
                _flames[i].localPosition = _flameBase[i] + Vector3.up * (h * 0.45f);
                Color c = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.05f), 0.5f + 0.5f * Mathf.Sin(t * 0.7f));
                c.a = 0.85f * life;
                _mpb.SetColor("_Color", c);
                _flameR[i].SetPropertyBlock(_mpb);
            }
            _light.intensity = (2.6f + Mathf.PerlinNoise(Time.time * 8f, 0.3f) * 1.6f) * life;

            if (_tickDamage > 0 && _age >= _nextTick)
            {
                _nextTick += _tickInterval;
                Burn();
            }
        }

        private void Burn()
        {
            var done = new HashSet<Damageable>();
            foreach (Collider col in Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, _radius, _hitMask, QueryTriggerInteraction.Ignore))
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null || !done.Add(d)) continue;
                if (col.GetComponentInParent<LastTruck.PlayerMove>() != null || col.GetComponentInParent<LastTruck.Networking.NetworkPlayer>() != null) continue;
                d.TakeDamage(_tickDamage, col.ClosestPoint(transform.position));
            }
        }
    }
}
