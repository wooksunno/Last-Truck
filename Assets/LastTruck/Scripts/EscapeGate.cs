using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 탈출 게이트. 맵 끝에 서 있는 두 기둥 + 빛의 막. 7일째 낮부터 열린다 (닫혀 있을 땐 붉게, 열리면 초록빛으로 숨 쉰다).
    /// 트럭이 부품 3종을 싣고 이 구역(Radius) 안에 들어오면 탈출이다 (StoryDirector가 판정한다).
    /// 장면에 놓아 두는 물체라서 멀티플레이에서도 모두에게 같은 자리에 있다 (네트워크 동기화 불필요).
    /// </summary>
    public class EscapeGate : MonoBehaviour
    {
        public const float Radius = 9f;          // 트럭이 이 안에 들어오면 도착
        private const float Width = 10f;         // 기둥 사이 간격 (트럭이 지나갈 수 있게 넉넉하게)
        private const float Height = 8f;

        public static EscapeGate Instance { get; private set; }

        private Renderer _portal, _ring, _column;
        private Light _light;
        private Renderer[] _glowParts;
        private bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        private void Awake()
        {
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            Material dark = Lit(new Color(0.16f, 0.17f, 0.2f), Color.black);
            var glow = new System.Collections.Generic.List<Renderer>();

            // 기둥 2개 + 위 보
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * Width * 0.5f;
                Transform pillar = Prim(PrimitiveType.Cylinder, "Pillar", new Vector3(x, Height * 0.5f, 0f), new Vector3(1.1f, Height * 0.5f, 1.1f), dark, true);
                Transform cap = Prim(PrimitiveType.Sphere, "Lamp", new Vector3(x, Height + 0.4f, 0f), Vector3.one * 1.3f, Lit(Color.white, Color.white), false);
                glow.Add(cap.GetComponent<Renderer>());
                Transform band = Prim(PrimitiveType.Cylinder, "Band", new Vector3(x, 1.2f, 0f), new Vector3(1.35f, 0.18f, 1.35f), Lit(Color.white, Color.white), false);
                glow.Add(band.GetComponent<Renderer>());
            }
            Prim(PrimitiveType.Cube, "Beam", new Vector3(0f, Height + 0.2f, 0f), new Vector3(Width + 1.2f, 0.7f, 0.9f), dark, false);

            // 빛의 막 (양면)
            Transform portal = Prim(PrimitiveType.Quad, "Portal", new Vector3(0f, Height * 0.5f, 0f), new Vector3(Width - 1f, Height - 0.4f, 1f), Glow(new Color(1f, 0.3f, 0.25f, 0.3f)), false);
            _portal = portal.GetComponent<Renderer>();

            // 하늘로 솟는 빛기둥: 멀리서도 게이트 위치를 알 수 있게 한다 (큰 물체라 먼 거리 컬링에도 안 숨는다)
            Transform column = Prim(PrimitiveType.Cylinder, "Column", new Vector3(0f, 40f, 0f), new Vector3(3.2f, 40f, 3.2f), Glow(new Color(1f, 0.3f, 0.25f, 0.12f)), false);
            _column = column.GetComponent<Renderer>();

            // 바닥의 구역 표시 (트럭이 이 안에 들어오면 도착)
            Transform ring = Prim(PrimitiveType.Cylinder, "Zone", new Vector3(0f, 0.04f, 0f), new Vector3(Radius * 2f, 0.01f, Radius * 2f), Glow(new Color(1f, 0.3f, 0.25f, 0.12f)), false);
            _ring = ring.GetComponent<Renderer>();

            var lightGo = new GameObject("GateLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, Height * 0.5f, 0.5f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 22f;
            _light.intensity = 2.2f;

            _glowParts = glow.ToArray();
            Apply(false, 0f);
        }

        private bool _shown = true;

        private void Update()
        {
            // 모든 생존 조건을 돌파하기 전에는 게이트가 보이지 않는다 (탈출 작전은 나중에 드러난다)
            bool open = StoryDirector.GateIsOpen;
            if (open != _shown)
            {
                _shown = open;
                foreach (Transform child in transform) child.gameObject.SetActive(open);
            }
            if (open) Apply(true, Time.time);
        }

        private void Apply(bool open, float time)
        {
            Color c = open ? new Color(0.35f, 1f, 0.6f) : new Color(1f, 0.32f, 0.26f);
            float pulse = open ? 0.75f + 0.25f * Mathf.Sin(time * 2.4f) : 0.55f + 0.1f * Mathf.Sin(time * 1.1f);

            if (_portal != null)
            {
                Color pc = c; pc.a = open ? 0.18f + 0.14f * pulse : 0.3f;
                _portal.sharedMaterial.color = pc;
            }
            if (_column != null)
            {
                Color cc = c; cc.a = open ? 0.12f + 0.06f * pulse : 0.09f;
                _column.sharedMaterial.color = cc;
            }
            if (_ring != null)
            {
                Color rc = c; rc.a = open ? 0.12f + 0.08f * pulse : 0.1f;
                _ring.sharedMaterial.color = rc;
            }
            if (_light != null) { _light.color = c; _light.intensity = (open ? 2.6f : 1.4f) * pulse; }
            if (_glowParts != null)
            {
                foreach (Renderer r in _glowParts)
                {
                    if (r == null) continue;
                    Material m = r.sharedMaterial;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c * 0.6f);
                    m.color = c * 0.6f;
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * (1.2f + 1.6f * pulse));
                }
            }
        }

        // ---------------------------------------------------------------- 도형 / 재질

        private Transform Prim(PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) { var col = go.GetComponent<Collider>(); if (col != null) Destroy(col); }
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material Lit(Color baseColor, Color emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            m.color = baseColor;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        private static Material Glow(Color c)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            return new Material(shader) { color = c };
        }
    }
}
