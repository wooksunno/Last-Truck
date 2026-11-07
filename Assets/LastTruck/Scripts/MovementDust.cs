using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 발밑 먼지: 달릴 때 발걸음마다 작은 먼지가 일고, 높은 데서 떨어져 착지하면 크게 퍼진다.
    /// 씬에 따로 둘 것이 없다 (처음 쓸 때 자동으로 만들어진다). 파티클 시스템 하나를 모두가 같이 쓴다.
    /// </summary>
    public class MovementDust : MonoBehaviour
    {
        private static MovementDust _instance;
        private ParticleSystem _ps;

        private static readonly Color DustColor = new Color(0.93f, 0.88f, 0.79f, 0.7f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private static MovementDust Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MovementDust");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MovementDust>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        /// <summary>달릴 때 발걸음 먼지. footPos = 발 위치, moveDir = 가는 방향 (먼지는 반대쪽으로 조금 흩날린다).</summary>
        public static void SprintPuff(Vector3 footPos, Vector3 moveDir)
        {
            Instance.Emit(footPos, -moveDir.normalized * 0.8f, 3, 0.4f, 0.7f);
        }

        /// <summary>착지 먼지. impactSpeed = 떨어지던 속도(m/s). 빠를수록 많이, 넓게 퍼진다.</summary>
        public static void Landing(Vector3 footPos, float impactSpeed)
        {
            float k = Mathf.Clamp01(Mathf.InverseLerp(3.5f, 16f, impactSpeed));
            int count = Mathf.RoundToInt(Mathf.Lerp(5f, 16f, k));
            Instance.Emit(footPos, Vector3.zero, count, Mathf.Lerp(0.5f, 0.85f, k), Mathf.Lerp(1.4f, 3.2f, k));
        }

        private void Emit(Vector3 pos, Vector3 drift, int count, float size, float spread)
        {
            if (_ps == null) return;

            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle;
                p.position = pos + new Vector3(ring.x, 0f, ring.y) * (0.15f + spread * 0.12f) + Vector3.up * 0.08f;
                Vector3 outward = new Vector3(ring.x, 0f, ring.y).normalized * spread * Random.Range(0.4f, 1f);
                p.velocity = outward + drift + Vector3.up * Random.Range(0.15f, 0.5f);
                p.startSize = size * Random.Range(0.7f, 1.3f);
                p.startLifetime = Random.Range(0.45f, 0.8f);
                p.startColor = DustColor;
                p.rotation = Random.Range(0f, 360f);
                _ps.Emit(p, 1);
            }
        }

        private void Build()
        {
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.gravityModifier = -0.04f;   // 먼지는 살짝 떠오른다
            main.startLifetime = 0.6f;
            main.startSize = 0.3f;

            var emission = _ps.emission;
            emission.enabled = false;

            var shape = _ps.shape;
            shape.enabled = false;

            // 갈수록 커지면서 옅어진다
            var size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(1f, 1.5f)));

            var color = _ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(fade);

            var rot = _ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var renderer = _ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                renderer.sharedMaterial = new Material(shader) { mainTexture = SoftCircle() };

            _ps.Play();   // 방출은 Emit()으로만 한다
        }

        private static Texture2D SoftCircle()
        {
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // 가장자리가 부드럽게
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
