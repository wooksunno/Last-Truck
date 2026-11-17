using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 신호탄이 떨어진 자리: 바닥에서 타는 붉은 불꽃 + 넓게 비추는 붉은 빛 + 위로 피어오르는 연기.
    /// 피해는 없다. 밤길/동굴에서 길을 표시하거나 주변을 밝히는 용도. duration초 뒤 사그라든다.
    /// </summary>
    public class FlareLight : MonoBehaviour
    {
        private static Material _fxMat, _stickMat;

        private float _duration, _age, _nextPuff;
        private Light _light;
        private Transform _core;
        private Renderer _coreR;
        private MaterialPropertyBlock _mpb;

        public static void Spawn(Vector3 at, float duration)
        {
            var go = new GameObject("SignalFlare");
            go.transform.position = at;
            go.AddComponent<FlareLight>().Init(Mathf.Max(3f, duration));
        }

        private void Init(float duration)
        {
            _duration = duration;
            _mpb = new MaterialPropertyBlock();
            if (_fxMat == null) _fxMat = new Material(Shader.Find("Sprites/Default"));
            if (_stickMat == null)
            {
                _stickMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                Color c = new Color(0.75f, 0.12f, 0.1f);
                if (_stickMat.HasProperty("_BaseColor")) _stickMat.SetColor("_BaseColor", c);
                _stickMat.color = c;
            }

            // 바닥에 비스듬히 누운 신호탄 막대
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(transform, false);
            stick.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            stick.transform.localRotation = Quaternion.Euler(80f, Random.Range(0f, 360f), 0f);
            stick.transform.localScale = new Vector3(0.07f, 0.14f, 0.07f);
            stick.GetComponent<Renderer>().sharedMaterial = _stickMat;

            // 타는 끝: 밝게 깜빡이는 불덩이
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(transform, false);
            core.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            _core = core.transform;
            _coreR = core.GetComponent<Renderer>();
            _coreR.sharedMaterial = _fxMat;
            _coreR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("FlareLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.8f;
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.25f, 0.2f);
            _light.range = 16f;
            _light.intensity = 5f;
            _light.shadows = LightShadows.None;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _duration) { Destroy(gameObject); return; }

            float fade = Mathf.Clamp01((_duration - _age) / 2f);   // 마지막 2초 동안 꺼져 간다
            float flick = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 12f, 0.7f);
            _light.intensity = 5f * flick * fade;
            _core.localScale = Vector3.one * (0.22f + 0.08f * flick) * Mathf.Max(0.2f, fade);
            Color c = Color.Lerp(new Color(1f, 0.85f, 0.8f), new Color(1f, 0.25f, 0.2f), 0.4f);
            c.a = fade;
            _mpb.SetColor("_Color", c);
            _coreR.SetPropertyBlock(_mpb);

            // 연기: 붉게 물든 연기 덩어리가 천천히 피어오른다 (멀리서도 위치가 보인다)
            if (fade > 0.3f && Time.time >= _nextPuff)
            {
                _nextPuff = Time.time + 0.18f;
                FlarePuff.Spawn(transform.position + Vector3.up * 0.2f, _fxMat);
            }
        }
    }

    /// <summary>신호탄 연기 한 덩어리: 위로 떠오르며 커지고 옅어진다.</summary>
    public class FlarePuff : MonoBehaviour
    {
        private const float Life = 2.6f;
        private float _age;
        private Vector3 _vel;
        private Renderer _r;
        private MaterialPropertyBlock _mpb;

        public static void Spawn(Vector3 at, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "FlarePuff";
            go.transform.position = at;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<FlarePuff>();
            p._r = r;
            p._mpb = new MaterialPropertyBlock();
            Vector2 drift = Random.insideUnitCircle * 0.25f;
            p._vel = new Vector3(drift.x, 1.4f + Random.value * 0.5f, drift.y);
            p.Update();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            transform.position += _vel * Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.3f, t);
            Color c = Color.Lerp(new Color(1f, 0.45f, 0.4f), new Color(0.6f, 0.5f, 0.5f), t);
            c.a = 0.45f * (1f - t);
            _mpb.SetColor("_Color", c);
            _r.SetPropertyBlock(_mpb);
        }
    }
}
