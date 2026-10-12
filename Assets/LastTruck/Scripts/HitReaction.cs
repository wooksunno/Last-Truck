using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    /// <summary>
    /// 플레이어가 몬스터에게 맞았을 때의 연출.
    ///  - 잠깐 경직: 피격 모션이 나오는 동안 이동/공격 불가 (연속으로 맞아도 계속 묶이지 않게 경직 후 잠깐 면역)
    ///  - 몸이 빨갛게 번쩍(캐릭터 셰이더 _HitFlash), 피격 불꽃, 화면 가장자리 붉은 비네팅, 카메라 흔들림, 뒤로 밀려남
    /// PlayerStats가 맞을 때 HitReaction.Play로 부른다(컴포넌트는 없으면 자동으로 붙는다).
    /// 화면/카메라 연출과 경직은 내 캐릭터에만, 몸 번쩍임/불꽃은 모든 화면에서 보인다.
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        [Tooltip("피격 시 이동/공격이 막히는 시간(초). 피격 모션 길이와 비슷하게")]
        [SerializeField] private float stunSeconds = 0.6f;
        [Tooltip("경직이 끝난 뒤 다시 경직되지 않는 시간(초). 몬스터에게 계속 맞아도 영영 못 움직이는 일을 막는다")]
        [SerializeField] private float stunImmuneSeconds = 0.6f;
        [Tooltip("맞았을 때 공격자 반대쪽으로 밀려나는 속도(m/s, 싱글플레이)")]
        [SerializeField] private float knockbackSpeed = 7f;
        [Tooltip("카메라 흔들림 세기 (0~1)")]
        [SerializeField] private float shakeAmount = 0.6f;
        [SerializeField] private Color flashColor = new Color(1f, 0.12f, 0.08f, 1f);

        private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private static Texture2D _softCircle;
        private static Texture2D _vignette;
        private static Material _particleMaterial;

        private PlayerMove _move;
        private Combat.WeaponController _weapon;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private float _stunImmuneUntil;
        private Coroutine _flashRoutine;
        private Coroutine _overlayRoutine;
        private RawImage _overlay;
        private bool _isLocal = true;

        public static void Play(GameObject player, Vector3 attackerPosition, bool hasAttacker)
        {
            if (player == null)
                return;

            HitReaction reaction = player.GetComponent<HitReaction>();
            if (reaction == null)
                reaction = player.AddComponent<HitReaction>();
            reaction.React(attackerPosition, hasAttacker);
        }

        private void Awake()
        {
            _move = GetComponent<PlayerMove>();
            _weapon = GetComponent<Combat.WeaponController>();
            _block = new MaterialPropertyBlock();

            LastTruck.Networking.NetworkPlayer net = GetComponent<LastTruck.Networking.NetworkPlayer>();
            _isLocal = net == null || net.IsLocal;
        }

        private void OnDisable()
        {
            ClearFlash();
        }

        private void React(Vector3 attackerPosition, bool hasAttacker)
        {
            Vector3 chest = transform.position + Vector3.up * 1.1f;
            Vector3 away = transform.position - attackerPosition;
            away.y = 0f;
            bool hasDirection = hasAttacker && away.sqrMagnitude > 0.0001f;
            Vector3 awayDir = hasDirection ? away.normalized : -transform.forward;

            // 몸 번쩍임 + 피격 불꽃은 모두에게 보인다
            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
            SpawnHitParticles(chest - awayDir * 0.2f, -awayDir);

            if (!_isLocal)
                return;

            // 화면 연출
            Combat.CameraShake.Shake(shakeAmount);
            PlayOverlay();

            // 경직 + 밀려남 (직전에 경직이 끝났다면 잠깐 면역)
            if (Time.time >= _stunImmuneUntil && _move != null)
            {
                _stunImmuneUntil = Time.time + stunSeconds + stunImmuneSeconds;
                _move.Stun(stunSeconds);
                if (_weapon != null)
                    _weapon.InterruptAttack();
                if (hasDirection && !_move.IsNetworkControlled)
                    _move.Knockback(awayDir * knockbackSpeed);
            }
        }

        #region 몸 번쩍임

        private IEnumerator FlashRoutine()
        {
            if (_renderers == null)
                CacheRenderers();

            const float hold = 0.08f;
            const float fade = 0.28f;

            ApplyFlash(0.9f);
            yield return new WaitForSeconds(hold);

            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                float k = 1f - t / fade;
                ApplyFlash(0.9f * k * k);
                yield return null;
            }

            ClearFlash();
            _flashRoutine = null;
        }

        private void CacheRenderers()
        {
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer || r is MeshRenderer)
                    list.Add(r);
            }
            _renderers = list.ToArray();
        }

        private void ApplyFlash(float amount)
        {
            if (_renderers == null)
                return;

            foreach (Renderer r in _renderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(HitFlashId, amount);
                _block.SetColor(FlashColorId, flashColor);
                r.SetPropertyBlock(_block);
            }
        }

        private void ClearFlash()
        {
            if (_renderers == null)
                return;

            foreach (Renderer r in _renderers)
            {
                if (r != null)
                    r.SetPropertyBlock(null);
            }
        }

        #endregion

        #region 화면 가장자리 붉은 비네팅

        private void PlayOverlay()
        {
            EnsureOverlay();
            if (_overlayRoutine != null)
                StopCoroutine(_overlayRoutine);
            _overlayRoutine = StartCoroutine(OverlayRoutine());
        }

        private IEnumerator OverlayRoutine()
        {
            const float duration = 0.7f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / duration;
                _overlay.color = new Color(1f, 1f, 1f, 0.75f * k * Mathf.Sqrt(k));
                yield return null;
            }

            _overlay.color = new Color(1f, 1f, 1f, 0f);
            _overlayRoutine = null;
        }

        private void EnsureOverlay()
        {
            if (_overlay != null)
                return;

            var canvasObject = new GameObject("HitOverlay", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            var imageObject = new GameObject("Vignette", typeof(RectTransform));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _overlay = imageObject.AddComponent<RawImage>();
            _overlay.texture = GetVignetteTexture();
            _overlay.raycastTarget = false;
            _overlay.color = new Color(1f, 1f, 1f, 0f);
        }

        private static Texture2D GetVignetteTexture()
        {
            if (_vignette != null)
                return _vignette;

            const int size = 128;
            _vignette = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;           // 모서리에서 1
                    float t = Mathf.InverseLerp(0.3f, 1f, d);                      // 가운데 30%는 투명, 모서리로 갈수록 진해짐
                    float a = t * t * (3f - 2f * t);
                    a = Mathf.Pow(a, 1.5f) * 0.95f + 0.03f;                       // 전체에 아주 옅은 붉은 기운
                    _vignette.SetPixel(x, y, new Color(0.85f, 0.04f, 0.04f, Mathf.Clamp01(a)));
                }
            }
            _vignette.Apply();
            return _vignette;
        }

        #endregion

        #region 피격 불꽃

        private static void SpawnHitParticles(Vector3 position, Vector3 outwardDir)
        {
            // 튀는 불똥
            ParticleSystem sparks = CreateSystem("HitSparks", position, outwardDir);
            var main = sparks.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.1f, 0.05f), new Color(1f, 0.7f, 0.2f));
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });

            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.12f;

            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var render = sparks.GetComponent<ParticleSystemRenderer>();
            render.sharedMaterial = GetParticleMaterial();
            render.renderMode = ParticleSystemRenderMode.Stretch;
            render.lengthScale = 2.2f;
            render.velocityScale = 0.08f;
            sparks.Play();
            Destroy(sparks.gameObject, 1.5f);

            // 번쩍 퍼지는 붉은 섬광
            ParticleSystem flash = CreateSystem("HitFlashPuff", position, outwardDir);
            var fMain = flash.main;
            fMain.duration = 0.3f;
            fMain.loop = false;
            fMain.startLifetime = 0.18f;
            fMain.startSpeed = 0f;
            fMain.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.4f);
            fMain.startColor = new Color(1f, 0.25f, 0.18f, 0.85f);
            fMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var fEmission = flash.emission;
            fEmission.rateOverTime = 0f;
            fEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var fShape = flash.shape;
            fShape.enabled = false;

            var fSize = flash.sizeOverLifetime;
            fSize.enabled = true;
            fSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.2f));

            var fColor = flash.colorOverLifetime;
            fColor.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fColor.color = gradient;

            var fRender = flash.GetComponent<ParticleSystemRenderer>();
            fRender.sharedMaterial = GetParticleMaterial();
            flash.Play();
            Destroy(flash.gameObject, 1f);
        }

        private static ParticleSystem CreateSystem(string name, Vector3 position, Vector3 forward)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (forward.sqrMagnitude > 0.0001f)
                go.transform.rotation = Quaternion.LookRotation(forward);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // 설정을 바꾸기 전에 멈춘다
            return ps;
        }

        private static Material GetParticleMaterial()
        {
            if (_particleMaterial != null)
                return _particleMaterial;

            if (_softCircle == null)
            {
                const int size = 64;
                _softCircle = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        _softCircle.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }
                _softCircle.Apply();
            }

            _particleMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = _softCircle };
            return _particleMaterial;
        }

        #endregion
    }
}
