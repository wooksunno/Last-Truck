using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 포탑 레이저 한 줄기 연출: 굵고 흐린 빛 + 가는 밝은 심지, 맞은 곳 섬광. 짧게 번쩍였다가 가늘어지며 사라진다.
    /// TruckTurret.ShowShot이 만든다 (판정과 상관없는 보기용).
    /// </summary>
    public class TurretLaserFx : MonoBehaviour
    {
        private LineRenderer _glow, _core;
        private Transform _flash;
        private Renderer _flashRenderer;
        private MaterialPropertyBlock _mpb;
        private Color _glowColor, _coreColor;
        private float _width, _life, _flashSize, _age;

        public void Init(Material mat, Vector3 from, Vector3 to, Color glow, Color core, float width, float life, float flashSize)
        {
            _glowColor = glow; _coreColor = core; _width = width; _life = Mathf.Max(0.04f, life); _flashSize = flashSize;
            _glow = MakeLine("Glow", mat, from, to);
            _core = MakeLine("Core", mat, from, to);

            var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(f.GetComponent<Collider>());
            f.transform.SetParent(transform, false);
            f.transform.position = to;
            _flash = f.transform;
            _flashRenderer = f.GetComponent<Renderer>();
            _flashRenderer.sharedMaterial = mat;
            _flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mpb = new MaterialPropertyBlock();

            Apply(0f);
        }

        private LineRenderer MakeLine(string name, Material mat, Vector3 from, Vector3 to)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f) { Destroy(gameObject); return; }
            Apply(t);
        }

        private void Apply(float t)
        {
            float fade = 1f - t * t;                 // 처음엔 밝게 유지하다 끝에서 빨리 꺼진다
            float w = _width * Mathf.Lerp(1.15f, 0.25f, t);

            Color g = _glowColor; g.a = 0.45f * fade;
            _glow.startWidth = w * 2.6f; _glow.endWidth = w * 2.2f;
            _glow.startColor = g; _glow.endColor = g;

            Color c = _coreColor; c.a = fade;
            _core.startWidth = w * 0.7f; _core.endWidth = w * 0.55f;
            _core.startColor = c; _core.endColor = c;

            if (_flash != null)
            {
                _flash.localScale = Vector3.one * _flashSize * Mathf.Lerp(0.6f, 1.2f, t);
                Color fc = Color.Lerp(_coreColor, _glowColor, t); fc.a = 0.8f * fade;
                _mpb.SetColor("_Color", fc);
                _mpb.SetColor("_BaseColor", fc);
                _flashRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
