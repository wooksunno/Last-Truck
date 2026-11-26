using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 펌프 화면의 "단면 장면": 땅을 세로로 잘라 보여 준다 (코드로 만든다, 에셋 없음).
    ///  - 위: 하늘과 모래 언덕, 끄덕이는 펌프잭(진짜 펌프잭의 움직임을 그대로 따라 한다)
    ///  - 아래: 모래/점토/암반 지층, 맨 아래 검은 기름층. 잘린 유정 관 속으로 흡입 막대가 오르내리고,
    ///    막대가 올라갈 때마다 기름 방울이 관을 타고 "쭉쭉" 빨려 올라온다. 기름층에서는 기포가 관 입구로 모여든다.
    ///  - 오른쪽: 배관을 따라 흘러간 기름이 잘린 저장 탱크에 떨어져 차오른다 (탱크 양 = 진짜 펌프잭 탱크).
    ///  - 탱크가 가득 차면 펌프가 멈추고 기름 흐름도 멈춘다.
    /// LastTruck.ProcessDiorama가 띄우고 전용 카메라로 찍는다 (ShowCustom).
    /// </summary>
    public class OilPumpDiorama : LastTruck.ProcessDioramaStage
    {
        public const string Key = "OilPump";

        /// <summary>만들어 둔 단면 장면 (하나만 만든다).</summary>
        public static OilPumpDiorama Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; }

        private OilPumpjack _model;
        private OilPumpjack _source;
        private Transform _rod, _plunger, _tankFill, _tankSurface;
        private readonly List<Transform> _wellBlobs = new List<Transform>();
        private readonly List<Transform> _lineBlobs = new List<Transform>();
        private readonly List<Transform> _bubbles = new List<Transform>();
        private readonly List<Transform> _drips = new List<Transform>();
        private readonly List<Vector3> _bubbleFrom = new List<Vector3>();
        private Vector3[] _path;
        private float[] _pathLen;
        private float _pathTotal;
        private float _flow;        // 관 속 기름 흐름 위치 (오를 때 많이 늘어난다)
        private float _lineFlow;
        private float _lastD;
        private float _fillShown;

        // 배치 (장면 좌표: 카메라가 +x에서 -x를 본다. 화면 오른쪽 = +z, 땅 표면 y = 0)
        private const float WellZ = 0.9f;
        private const float WellBottom = -8.2f;
        private const float TankZ = 4.55f, TankHalf = 0.95f, TankH = 2.7f;
        private const float FaceX = -0.7f;                 // 잘린 땅의 앞면

        private static readonly Color Oil = OilWellRig.OilColor;
        private static readonly Color OilShine = new Color(0.22f, 0.2f, 0.19f);

        /// <summary>진짜 펌프잭을 따라 하게 한다.</summary>
        public void SetSource(OilPumpjack source)
        {
            _source = source;
            if (_model != null) _model.Mirror(source);
        }

        public static LastTruck.ProcessDioramaStage Build(Transform parent)
        {
            var go = new GameObject("Stage_OilPump");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<OilPumpDiorama>();
            s.BuildScene();
            Current = s;
            return s;
        }

        private Transform P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null) => OilWellRig.Part(t, transform, n, pos, scale, c, euler);

        private void BuildScene()
        {
            camPosition = new Vector3(33f, -2.85f, 0.45f);
            camTarget = new Vector3(0f, -2.85f, 0.45f);
            camFov = 34f;
            clearColor = new Color(0.16f, 0.12f, 0.1f);
            ambient = new Color(0.56f, 0.53f, 0.5f);

            // 해 (이 장면을 찍을 때만 켜진다)
            var keyGo = new GameObject("KeyLight");
            keyGo.transform.SetParent(transform, false);
            keyGo.transform.localRotation = Quaternion.Euler(38f, -115f, 0f);
            keyLight = keyGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.92f, 0.8f);
            keyLight.intensity = 1.05f;
            keyLight.shadows = LightShadows.None;

            // 하늘 (세로 그라데이션 판) + 먼 모래 언덕
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(sky.GetComponent<Collider>());
            sky.name = "Sky";
            sky.transform.SetParent(transform, false);
            sky.transform.localPosition = new Vector3(-7f, 4f, 0.45f);
            sky.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            sky.transform.localScale = new Vector3(34f, 10f, 1f);
            var skyTex = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                Color c = Color.Lerp(new Color(0.99f, 0.86f, 0.62f), new Color(0.4f, 0.64f, 0.92f), Mathf.SmoothStep(0f, 1f, y / 63f));
                skyTex.SetPixel(0, y, c); skyTex.SetPixel(1, y, c);
            }
            skyTex.Apply();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var skyMat = new Material(unlit != null ? unlit : Shader.Find("Unlit/Texture"));
            if (skyMat.HasProperty("_BaseMap")) skyMat.SetTexture("_BaseMap", skyTex); else skyMat.mainTexture = skyTex;
            sky.GetComponent<Renderer>().sharedMaterial = skyMat;
            Color dune = new Color(0.9f, 0.74f, 0.5f);
            for (int i = 0; i < 6; i++)
                P(PrimitiveType.Sphere, "Dune", new Vector3(-5.5f, -0.2f, -9f + i * 3.6f), new Vector3(1f, 1.4f + (i % 3) * 0.5f, 5.5f), Color.Lerp(dune, new Color(0.95f, 0.8f, 0.6f), (i % 2) * 0.5f));

            // 지층 (앞면이 잘린 땅)
            float[] tops = { 0f, -1.0f, -3.2f, -6.0f };
            float[] bottoms = { -1.0f, -3.2f, -6.0f, -11f };
            Color[] cols = { new Color(0.88f, 0.72f, 0.47f), new Color(0.64f, 0.44f, 0.28f), new Color(0.47f, 0.4f, 0.35f), new Color(0.32f, 0.27f, 0.25f) };
            for (int i = 0; i < 4; i++)
            {
                float h = tops[i] - bottoms[i];
                P(PrimitiveType.Cube, "Stratum", new Vector3((FaceX - 6f) * 0.5f, (tops[i] + bottoms[i]) * 0.5f, 0.45f), new Vector3(FaceX + 6f, h, 24f), cols[i]);
                // 지층 경계의 울퉁불퉁한 띠
                for (int k = 0; k < 14; k++)
                    P(PrimitiveType.Cube, "Edge", new Vector3(FaceX + 0.02f, tops[i] - 0.05f, -8f + k * 1.25f + Random.Range(-0.2f, 0.2f)),
                        new Vector3(0.04f, Random.Range(0.12f, 0.3f), Random.Range(0.6f, 1.2f)), cols[i] * 0.86f, new Vector3(Random.Range(-15f, 15f), 0f, 0f));
                // 자갈/돌 조각
                for (int k = 0; k < 10; k++)
                {
                    float y = Random.Range(bottoms[i] + 0.25f, tops[i] - 0.25f);
                    float sz = Random.Range(0.12f, 0.32f) * (i + 1) * 0.55f;
                    P(PrimitiveType.Cube, "Pebble", new Vector3(FaceX + 0.03f, y, Random.Range(-5.5f, 6.4f)), new Vector3(0.04f, sz, sz * Random.Range(0.8f, 1.5f)),
                        Color.Lerp(cols[i], k % 2 == 0 ? Color.white : Color.black, 0.18f), new Vector3(Random.Range(0f, 90f), 0f, 0f));
                }
            }
            // 앞쪽 지표 (펌프잭/탱크가 놓이는 땅): 잘린 앞면 위에 얇게
            P(PrimitiveType.Cube, "GroundLip", new Vector3(0.65f, -0.15f, 0.45f), new Vector3(2.7f, 0.3f, 24f), cols[0] * 1.03f);

            // 기름층: 다공성 사암 띠 + 검은 기름 렌즈 + 윤기
            P(PrimitiveType.Sphere, "Sandstone", new Vector3(FaceX + 0.02f, -8.6f, 1.2f), new Vector3(0.05f, 2.9f, 10.5f), new Color(0.55f, 0.45f, 0.32f));
            P(PrimitiveType.Sphere, "OilPool", new Vector3(FaceX + 0.05f, -8.7f, 1.1f), new Vector3(0.05f, 1.9f, 8.6f), Oil);
            P(PrimitiveType.Sphere, "OilPool2", new Vector3(FaceX + 0.06f, -8.9f, -1.6f), new Vector3(0.05f, 1.2f, 3.4f), Oil);
            for (int k = 0; k < 5; k++)
                P(PrimitiveType.Cube, "Shine", new Vector3(FaceX + 0.08f, -8.05f - k * 0.05f, -1.6f + k * 1.3f), new Vector3(0.02f, 0.05f, 0.7f), OilShine);
            for (int k = 0; k < 10; k++)   // 사암 알갱이
                P(PrimitiveType.Sphere, "Grain", new Vector3(FaceX + 0.07f, -8.6f + Random.Range(-1.3f, 1.3f), Random.Range(-3.8f, 5.6f)), Vector3.one * 0.1f, new Color(0.72f, 0.6f, 0.42f));

            // 잘린 유정 관: 어두운 안쪽 + 강철 벽 두 개 + 아래 흡입 펌프 통
            Color steel = new Color(0.55f, 0.58f, 0.62f);
            float wellTop = -0.05f, wellH = wellTop - WellBottom;
            P(PrimitiveType.Cube, "WellInside", new Vector3(-0.32f, (wellTop + WellBottom) * 0.5f, WellZ), new Vector3(0.05f, wellH, 0.52f), new Color(0.14f, 0.12f, 0.12f));
            foreach (float s in new[] { -1f, 1f })
            {
                P(PrimitiveType.Cube, "WellWall", new Vector3(0f, (wellTop + WellBottom) * 0.5f, WellZ + s * 0.29f), new Vector3(0.12f, wellH, 0.07f), steel);
                P(PrimitiveType.Cube, "Cement", new Vector3(-0.1f, (wellTop + WellBottom) * 0.5f, WellZ + s * 0.39f), new Vector3(0.1f, wellH, 0.12f), new Color(0.62f, 0.6f, 0.56f));
                P(PrimitiveType.Cube, "Barrel", new Vector3(0.02f, WellBottom + 0.55f, WellZ + s * 0.24f), new Vector3(0.1f, 1.1f, 0.06f), new Color(0.3f, 0.32f, 0.36f));
                for (int k = 0; k < 3; k++)   // 관 아래 구멍(기름이 들어오는 곳)
                    P(PrimitiveType.Cube, "Perf", new Vector3(0.07f, WellBottom + 0.15f + k * 0.22f, WellZ + s * 0.29f), new Vector3(0.04f, 0.09f, 0.09f), Oil);
            }
            P(PrimitiveType.Sphere, "StandingValve", new Vector3(0.02f, WellBottom + 0.12f, WellZ), Vector3.one * 0.2f, new Color(0.7f, 0.2f, 0.14f));
            _rod = P(PrimitiveType.Cylinder, "SuckerRod", Vector3.zero, new Vector3(0.07f, 1f, 0.07f), new Color(0.78f, 0.8f, 0.83f));
            _plunger = P(PrimitiveType.Cube, "Plunger", Vector3.zero, new Vector3(0.14f, 0.5f, 0.38f), new Color(0.72f, 0.5f, 0.2f));

            // 관 속 기름 방울 (빨려 올라온다)
            for (int i = 0; i < 26; i++)
                _wellBlobs.Add(P(PrimitiveType.Sphere, "OilBlob", Vector3.zero, new Vector3(0.12f, 0.34f, 0.34f), Oil));
            // 기름층에서 관 입구로 모여드는 기포
            for (int i = 0; i < 10; i++)
            {
                _bubbles.Add(P(PrimitiveType.Sphere, "Bubble", Vector3.zero, Vector3.one * 0.14f, OilShine));
                _bubbleFrom.Add(new Vector3(FaceX + 0.1f, -8.7f + Random.Range(-0.9f, 0.7f), WellZ + Random.Range(-3.8f, 4.2f)));
            }

            // 펌프잭 모형 (유정 바로 위)
            _model = OilPumpjack.CreateDisplay(transform, new Vector3(0f, 0f, WellZ));
            if (_source != null) _model.Mirror(_source);

            // 배관: 유정 머리 → 위로 → 탱크 위까지 (잘린 관: 어두운 안쪽 + 위아래 벽)
            _path = new[]
            {
                new Vector3(0.05f, 0.5f, WellZ + 0.25f), new Vector3(0.05f, 0.5f, 2.15f), new Vector3(0.05f, 3.25f, 2.15f),
                new Vector3(0.05f, 3.25f, TankZ - 0.1f), new Vector3(0.05f, TankH + 0.35f, TankZ - 0.1f),
            };
            _pathLen = new float[_path.Length];
            for (int i = 1; i < _path.Length; i++) { _pathLen[i] = _pathLen[i - 1] + Vector3.Distance(_path[i - 1], _path[i]); }
            _pathTotal = _pathLen[_path.Length - 1];
            for (int i = 1; i < _path.Length; i++)
            {
                Vector3 a = _path[i - 1], b = _path[i];
                bool vertical = Mathf.Abs(a.y - b.y) > 0.01f;
                Vector3 mid = (a + b) * 0.5f;
                float len = Vector3.Distance(a, b) + 0.3f;
                P(PrimitiveType.Cube, "PipeInside", mid + new Vector3(-0.22f, 0f, 0f), vertical ? new Vector3(0.04f, len, 0.3f) : new Vector3(0.04f, 0.3f, len), new Color(0.14f, 0.12f, 0.12f));
                foreach (float s in new[] { -1f, 1f })
                    P(PrimitiveType.Cube, "PipeWall", mid + (vertical ? new Vector3(0f, 0f, s * 0.18f) : new Vector3(0f, s * 0.18f, 0f)),
                        vertical ? new Vector3(0.1f, len, 0.06f) : new Vector3(0.1f, 0.06f, len), steel);
            }
            for (int i = 0; i < 12; i++)
                _lineBlobs.Add(P(PrimitiveType.Sphere, "LineBlob", Vector3.zero, new Vector3(0.1f, 0.22f, 0.22f), Oil));

            // 잘린 저장 탱크: 뒷벽 + 옆벽 + 바닥 + 앞 테두리 + 눈금, 안에 기름이 찬다
            Color tankRed = new Color(0.55f, 0.18f, 0.12f);
            P(PrimitiveType.Cube, "TankBack", new Vector3(-0.85f, TankH * 0.5f, TankZ), new Vector3(0.08f, TankH, TankHalf * 2f), tankRed * 0.7f);
            foreach (float s in new[] { -1f, 1f })
                P(PrimitiveType.Cube, "TankSide", new Vector3(0f, TankH * 0.5f, TankZ + s * TankHalf), new Vector3(1.8f, TankH, 0.1f), tankRed);
            P(PrimitiveType.Cube, "TankFloor", new Vector3(0f, 0.04f, TankZ), new Vector3(1.8f, 0.08f, TankHalf * 2f), tankRed * 0.8f);
            P(PrimitiveType.Cube, "TankRoof", new Vector3(-0.35f, TankH + 0.03f, TankZ), new Vector3(1.1f, 0.06f, TankHalf * 2f + 0.1f), tankRed * 0.9f);
            P(PrimitiveType.Cube, "TankRimFront", new Vector3(0.88f, TankH - 0.05f, TankZ), new Vector3(0.06f, 0.1f, TankHalf * 2f + 0.1f), tankRed);
            P(PrimitiveType.Cube, "TankRimBottom", new Vector3(0.88f, 0.12f, TankZ), new Vector3(0.06f, 0.16f, TankHalf * 2f + 0.1f), tankRed);
            for (int k = 1; k < 4; k++)
                P(PrimitiveType.Cube, "Tick", new Vector3(-0.8f, 0.1f + (TankH - 0.2f) * k / 4f, TankZ - TankHalf + 0.25f), new Vector3(0.02f, 0.05f, 0.3f), new Color(0.95f, 0.9f, 0.8f));
            _tankFill = P(PrimitiveType.Cube, "TankOil", new Vector3(0f, 0.1f, TankZ), new Vector3(1.6f, 0.01f, TankHalf * 2f - 0.12f), Oil);
            _tankSurface = P(PrimitiveType.Cube, "TankOilShine", new Vector3(0.1f, 0.1f, TankZ), new Vector3(1.5f, 0.025f, TankHalf * 2f - 0.2f), OilShine);
            for (int i = 0; i < 3; i++)
                _drips.Add(P(PrimitiveType.Sphere, "Drip", Vector3.zero, new Vector3(0.1f, 0.2f, 0.2f), Oil));
        }

        private Vector3 OnPath(float d)
        {
            d = Mathf.Repeat(d, _pathTotal);
            for (int i = 1; i < _path.Length; i++)
                if (d <= _pathLen[i]) return Vector3.Lerp(_path[i - 1], _path[i], Mathf.InverseLerp(_pathLen[i - 1], _pathLen[i], d));
            return _path[_path.Length - 1];
        }

        /// <summary>state: 1 일하는 중, 2 탱크 가득. progress: 탱크 양(0~1).</summary>
        public override void Animate(int state, float progress, float age, SpriteRenderer item, SpriteRenderer output, float dt)
        {
            if (_model == null) return;
            bool working = state != 2;
            float phase = _model.Phase;

            // 흡입 막대: 말머리가 내려가면(sin>0) 막대도 내려간다
            float d = -Mathf.Sin(phase) * 0.5f;
            float up = Mathf.Max(0f, (d - _lastD) / Mathf.Max(dt, 0.0001f));   // 올라가는 속도
            _lastD = d;
            float rodTop = -0.1f + d, rodBottom = WellBottom + 0.55f + d;
            _rod.localPosition = new Vector3(0.04f, (rodTop + rodBottom) * 0.5f, WellZ);
            _rod.localScale = new Vector3(0.07f, (rodTop - rodBottom) * 0.5f, 0.07f);
            _plunger.localPosition = new Vector3(0.04f, rodBottom, WellZ);

            // 관 속 기름: 막대가 올라갈 때 "쭉" 빨려 오른다 (천천히 스며드는 흐름 + 오를 때 크게)
            float surge = working ? 0.05f + up * 0.16f : 0f;
            _flow += dt * surge;
            float span = -0.15f - (WellBottom + 0.2f);
            for (int i = 0; i < _wellBlobs.Count; i++)
            {
                float f = Mathf.Repeat(_flow + i / (float)_wellBlobs.Count, 1f);
                float y = WellBottom + 0.2f + f * span;
                float wob = Mathf.Sin(Time.time * 6f + i * 1.7f);
                var b = _wellBlobs[i];
                b.localPosition = new Vector3(0.06f, y, WellZ + wob * 0.06f);
                float stretch = 1f + Mathf.Min(1.2f, up * 0.9f);   // 빨려 오를 때 위아래로 길쭉해진다
                b.localScale = new Vector3(0.12f, 0.36f * stretch, 0.34f + wob * 0.04f);
                b.gameObject.SetActive(working || f < 0.15f);
            }

            // 기름층의 기포가 관 입구로 모여든다
            for (int i = 0; i < _bubbles.Count; i++)
            {
                float f = Mathf.Repeat(Time.time * (working ? 0.18f : 0.03f) + i / (float)_bubbles.Count, 1f);
                Vector3 to = new Vector3(0.05f, WellBottom + 0.2f, WellZ);
                Vector3 from = _bubbleFrom[i];
                Vector3 p = Vector3.Lerp(from, to, f * f);
                p.y += Mathf.Sin(f * Mathf.PI) * 0.35f;
                _bubbles[i].localPosition = p;
                _bubbles[i].localScale = Vector3.one * (0.1f + 0.08f * Mathf.Sin(f * Mathf.PI));
            }

            // 배관을 따라 탱크로
            _lineFlow += dt * (working ? 0.6f + up * 1.2f : 0f);
            for (int i = 0; i < _lineBlobs.Count; i++)
            {
                float dist = _lineFlow + i * (_pathTotal / _lineBlobs.Count);
                _lineBlobs[i].localPosition = OnPath(dist);
                _lineBlobs[i].gameObject.SetActive(working);
            }

            // 탱크: 진짜 탱크 양만큼 차오른다 (부드럽게)
            _fillShown = Mathf.MoveTowards(_fillShown, Mathf.Clamp01(progress), dt * 0.6f);
            float h = Mathf.Max(0.02f, _fillShown * (TankH - 0.25f));
            _tankFill.localPosition = new Vector3(0f, 0.08f + h * 0.5f, TankZ);
            _tankFill.localScale = new Vector3(1.6f, h, TankHalf * 2f - 0.12f);
            float wave = Mathf.Sin(Time.time * 3f) * 0.02f;
            _tankSurface.localPosition = new Vector3(0.1f, 0.08f + h + 0.005f + wave, TankZ);

            // 배관 끝에서 탱크로 떨어지는 방울
            for (int i = 0; i < _drips.Count; i++)
            {
                float f = Mathf.Repeat(Time.time * 1.4f + i / (float)_drips.Count, 1f);
                float top = TankH + 0.2f, bottom = 0.08f + h;
                _drips[i].localPosition = new Vector3(0.05f, Mathf.Lerp(top, bottom, f * f), TankZ - 0.1f);
                _drips[i].gameObject.SetActive(working && bottom < top - 0.1f);
            }
        }
    }
}
