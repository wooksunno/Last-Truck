using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 박격포 포탄 (보기용): 돌덩이가 포물선을 그리며 날아가 떨어진 자리에서 터진다.
    /// 피해 판정은 하지 않는다 - 싱글/호스트의 TruckTurret이 같은 비행 시간 뒤에 범위 피해를 준다.
    /// 포물선 모양/비행 시간은 MortarArc 한 곳에서 정해서 미리보기 점선, 포탄, 피해 시점이 모두 일치한다.
    /// </summary>
    public static class MortarArc
    {
        /// <summary>포물선 꼭대기 높이 (출발-도착 직선보다 얼마나 위로 솟는가).</summary>
        public static float ApexHeight(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0f;
            return Mathf.Clamp(d.magnitude * 0.32f, 2.5f, 11f);
        }

        /// <summary>날아가는 시간(초): 멀수록 오래 난다.</summary>
        public static float FlightTime(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0f;
            return Mathf.Clamp(0.7f + d.magnitude * 0.03f, 0.7f, 2.2f);
        }

        /// <summary>t(0~1) 지점의 위치.</summary>
        public static Vector3 Evaluate(Vector3 from, Vector3 to, float t)
        {
            float h = ApexHeight(from, to);
            return Vector3.Lerp(from, to, t) + Vector3.up * (4f * h * t * (1f - t));
        }
    }

    public class TurretMortarShell : MonoBehaviour
    {
        private static Material _rockMat, _fxMat;

        private Vector3 _from, _to;
        private float _flight, _age, _area;
        private Transform _rock;
        private Vector3 _spinAxis;
        private TrailRenderer _trail;

        public static void Launch(Vector3 from, Vector3 to, float area)
        {
            var go = new GameObject("MortarShell");
            go.AddComponent<TurretMortarShell>().Init(from, to, area);
        }

        private void Init(Vector3 from, Vector3 to, float area)
        {
            _from = from; _to = to; _area = area;
            _flight = MortarArc.FlightTime(from, to);
            transform.position = from;

            if (_rockMat == null)
            {
                _rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                Color c = new Color(0.42f, 0.38f, 0.34f);
                if (_rockMat.HasProperty("_BaseColor")) _rockMat.SetColor("_BaseColor", c);
                _rockMat.color = c;
            }
            if (_fxMat == null) _fxMat = new Material(Shader.Find("Sprites/Default"));

            // 울퉁불퉁한 돌덩이: 찌그러진 구 두 개를 겹친다
            _rock = new GameObject("Rock").transform;
            _rock.SetParent(transform, false);
            Prim(PrimitiveType.Sphere, _rock, Vector3.zero, new Vector3(0.55f, 0.45f, 0.5f), Quaternion.identity);
            Prim(PrimitiveType.Cube, _rock, new Vector3(0.08f, 0.05f, -0.04f), new Vector3(0.36f, 0.34f, 0.4f), Quaternion.Euler(25f, 40f, 15f));
            _spinAxis = Random.onUnitSphere;

            // 뒤로 남는 연기 꼬리
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = _fxMat;
            _trail.time = 0.35f;
            _trail.startWidth = 0.35f; _trail.endWidth = 0.05f;
            _trail.startColor = new Color(0.85f, 0.8f, 0.72f, 0.55f);
            _trail.endColor = new Color(0.6f, 0.55f, 0.5f, 0f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = _rockMat;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _flight);
            transform.position = MortarArc.Evaluate(_from, _to, t);
            _rock.Rotate(_spinAxis, 540f * Time.deltaTime, Space.World);
            if (t >= 1f) Explode();
        }

        private void Explode()
        {
            MortarBlast.Play(_to, _area, _fxMat);
            if (_trail != null) { _trail.transform.SetParent(null); _trail.autodestruct = true; _trail.emitting = false; }
            Destroy(_rock.gameObject);
            Destroy(this);
            Destroy(gameObject, 0.4f);
        }
    }

    /// <summary>착탄 폭발: 커지며 사라지는 불덩이 + 바닥을 따라 퍼지는 충격파 고리 + 튀는 돌 조각.</summary>
    public class MortarBlast : MonoBehaviour
    {
        private Material _mat;
        private Transform _ball;
        private Renderer _ballR;
        private LineRenderer _ring;
        private Transform[] _chunks;
        private Vector3[] _chunkVel;
        private float _area, _age;
        private MaterialPropertyBlock _mpb;
        private const float Life = 0.6f;

        public static void Play(Vector3 at, float area, Material mat)
        {
            var go = new GameObject("MortarBlast");
            go.transform.position = at;
            go.AddComponent<MortarBlast>().Init(area, mat);
        }

        private void Init(float area, Material mat)
        {
            _mat = mat; _area = Mathf.Max(1f, area);
            _mpb = new MaterialPropertyBlock();

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(transform, false);
            _ball = ball.transform;
            _ballR = ball.GetComponent<Renderer>();
            _ballR.sharedMaterial = mat;
            _ballR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ringGo = new GameObject("Shockwave");
            ringGo.transform.SetParent(transform, false);
            _ring = ringGo.AddComponent<LineRenderer>();
            _ring.sharedMaterial = mat;
            _ring.loop = true;
            _ring.useWorldSpace = true;
            _ring.positionCount = 40;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 돌 조각
            _chunks = new Transform[7];
            _chunkVel = new Vector3[_chunks.Length];
            for (int i = 0; i < _chunks.Length; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(c.GetComponent<Collider>());
                c.transform.SetParent(transform, false);
                c.transform.localScale = Vector3.one * Random.Range(0.12f, 0.24f);
                c.transform.rotation = Random.rotation;
                var r = c.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.SetPropertyBlock(Tint(new Color(0.45f, 0.4f, 0.35f, 1f)));
                _chunks[i] = c.transform;
                Vector2 dir = Random.insideUnitCircle.normalized;
                _chunkVel[i] = new Vector3(dir.x, 0f, dir.y) * Random.Range(3f, 6f) + Vector3.up * Random.Range(4f, 7f);
            }
            Apply(0f);

            // 가까이 떨어질수록 화면이 크게 흔들린다
            Camera cam = Camera.main;
            if (cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, transform.position);
                float shake = 0.35f * Mathf.Clamp01(1f - (d - 6f) / 40f);
                if (shake > 0.02f) Combat.CameraShake.Shake(shake);
            }
        }

        private MaterialPropertyBlock Tint(Color c)
        {
            var b = new MaterialPropertyBlock();
            b.SetColor("_Color", c);
            return b;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            Apply(t);
            for (int i = 0; i < _chunks.Length; i++)
            {
                _chunkVel[i] += Physics.gravity * Time.deltaTime;
                _chunks[i].position += _chunkVel[i] * Time.deltaTime;
                _chunks[i].Rotate(Vector3.one, 400f * Time.deltaTime);
            }
        }

        private void Apply(float t)
        {
            float e = 1f - (1f - t) * (1f - t);
            // 불덩이: 빠르게 커졌다가 흐려진다
            _ball.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.1f, e) * _area;
            Color fire = Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(0.6f, 0.25f, 0.1f), t);
            fire.a = 0.85f * (1f - t);
            _mpb.SetColor("_Color", fire);
            _ballR.SetPropertyBlock(_mpb);

            // 충격파 고리: 피해 범위 끝까지 퍼진다
            float radius = Mathf.Lerp(0.3f, 1f, e) * _area;
            Vector3 c = transform.position + Vector3.up * 0.15f;
            for (int i = 0; i < _ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / _ring.positionCount;
                _ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            _ring.widthMultiplier = Mathf.Lerp(0.5f, 0.08f, t);
            Color ring = new Color(1f, 0.8f, 0.55f, 0.8f * (1f - t));
            _ring.startColor = ring; _ring.endColor = ring;
        }
    }
}
