using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 몬스터 특수 공격의 "보이기만 하는" 연출 (각 컴퓨터에서 따로 만든다, 피해 판정은 호스트가 따로 한다).
    ///  - 투사체: 빛나는 구슬 + 꼬리 (스나이퍼/타겟터: 곧게, 오염체: 포물선)
    ///  - 오염 지대: 바닥의 초록 원 (지속 시간 동안 일렁이다 사라짐)
    ///  - 폭발: 커지며 사라지는 주황 구 + 빛 (붐버)
    /// 에셋 없이 코드로 만든다 (Sprites/Default 셰이더: 빌드에 항상 포함됨).
    /// </summary>
    public static class MonsterEffects
    {
        #region 재질

        private static Shader _shader;

        private static Material NewMaterial(Color color, Texture texture = null)
        {
            if (_shader == null) _shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(_shader) { color = color };
            if (texture != null) material.mainTexture = texture;
            return material;
        }

        private static GameObject NewPrimitive(PrimitiveType type, string name, Color color, Texture texture = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = NewMaterial(color, texture);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.AddComponent<EffectMaterialOwner>();
            return go;
        }

        #endregion

        #region 공개 함수

        public static readonly Color SniperColor = new Color(0.55f, 0.75f, 1f, 1f);
        public static readonly Color TargeterColor = new Color(1f, 0.6f, 0.2f, 1f);
        public static readonly Color PoisonColor = new Color(0.45f, 1f, 0.3f, 1f);

        /// <summary>from에서 to까지 날아가는 투사체. arcHeight > 0이면 포물선.</summary>
        public static void Projectile(Vector3 from, Vector3 to, Color color, float speed, float arcHeight)
        {
            GameObject go = NewPrimitive(PrimitiveType.Sphere, "MonsterProjectile", color);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.32f;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0f;
            trail.sharedMaterial = NewMaterial(new Color(color.r, color.g, color.b, 0.6f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var mover = go.AddComponent<ProjectileMover>();
            mover.Begin(from, to, Mathf.Max(1f, speed), arcHeight);
        }

        /// <summary>바닥 오염 지대 (반경 radius, duration초).</summary>
        public static void Pool(Vector3 center, float radius, float duration)
        {
            Texture texture = InGameHud.CircleSprite != null ? InGameHud.CircleSprite.texture : null;
            GameObject go = NewPrimitive(PrimitiveType.Quad, "PollutionPool", new Color(PoisonColor.r, PoisonColor.g, PoisonColor.b, 0.5f), texture);
            go.transform.position = center + Vector3.up * 0.06f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * radius * 2f;
            go.AddComponent<PoolFader>().Begin(duration, radius);
        }

        /// <summary>폭발 연출 (반경 radius).</summary>
        public static void Explosion(Vector3 center, float radius)
        {
            GameObject go = NewPrimitive(PrimitiveType.Sphere, "MonsterExplosion", new Color(1f, 0.55f, 0.15f, 0.8f));
            go.transform.position = center + Vector3.up * 0.5f;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = radius * 3f;
            light.intensity = 6f;
            go.AddComponent<ExplosionFader>().Begin(radius);
        }

        #endregion

        #region 연출 컴포넌트

        /// <summary>코드로 만든 재질을 오브젝트와 같이 지운다.</summary>
        private class EffectMaterialOwner : MonoBehaviour
        {
            private void OnDestroy()
            {
                foreach (Renderer renderer in GetComponents<Renderer>())
                {
                    if (renderer != null && renderer.sharedMaterial != null) Object.Destroy(renderer.sharedMaterial);
                }
            }
        }

        private class ProjectileMover : MonoBehaviour
        {
            private Vector3 _from;
            private Vector3 _to;
            private float _duration;
            private float _arc;
            private float _t;

            public void Begin(Vector3 from, Vector3 to, float speed, float arc)
            {
                _from = from;
                _to = to;
                _arc = arc;
                _duration = Mathf.Max(0.05f, Vector3.Distance(from, to) / speed);
            }

            private void Update()
            {
                _t += Time.deltaTime / _duration;
                float t = Mathf.Clamp01(_t);
                Vector3 position = Vector3.Lerp(_from, _to, t);
                position.y += _arc * 4f * t * (1f - t);
                transform.position = position;
                if (_t >= 1f) Destroy(gameObject, 0.05f);
            }
        }

        private class PoolFader : MonoBehaviour
        {
            private float _duration;
            private float _age;
            private float _size;
            private Material _material;
            private Color _color;

            public void Begin(float duration, float radius)
            {
                _duration = Mathf.Max(0.2f, duration);
                _size = radius * 2f;
                _material = GetComponent<Renderer>().sharedMaterial;
                _color = _material.color;
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float grow = Mathf.Clamp01(_age / 0.2f);
                float wobble = 1f + Mathf.Sin(_age * 6f) * 0.04f;
                transform.localScale = Vector3.one * _size * grow * wobble;
                float fade = Mathf.Clamp01((_duration - _age) / 0.4f);
                _material.color = new Color(_color.r, _color.g, _color.b, _color.a * fade);
                if (_age >= _duration) Destroy(gameObject);
            }
        }

        private class ExplosionFader : MonoBehaviour
        {
            private const float Duration = 0.45f;
            private float _age;
            private float _radius;
            private Material _material;
            private Light _light;

            public void Begin(float radius)
            {
                _radius = radius;
                _material = GetComponent<Renderer>().sharedMaterial;
                _light = GetComponent<Light>();
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float t = Mathf.Clamp01(_age / Duration);
                transform.localScale = Vector3.one * Mathf.Lerp(0.5f, _radius * 2f, Mathf.Sqrt(t));
                Color color = _material.color;
                color.a = 0.8f * (1f - t);
                _material.color = color;
                if (_light != null) _light.intensity = 6f * (1f - t);
                if (_age >= Duration) Destroy(gameObject);
            }
        }

        #endregion
    }
}
