using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 몬스터 몸 가장자리를 빛나게 하는 림라이트 연출의 공통 부분 (경찰 표식, 복서 기절에서 쓴다).
    ///
    /// 몬스터 원래 머티리얼은 바꾸지 않고, 렌더러에 림 머티리얼을 하나 "덧붙였다가" 끝나면 떼어낸다
    /// (피격 빨간 깜빡임 등 기존 연출과 겹치지 않음). 종류별 컴포넌트(PoliceMarkVisual, StunRimVisual)는
    /// 각자 자기 머티리얼을 붙이므로 둘이 동시에 걸려도 문제없다.
    /// 필요할 때 코드에서 자동으로 붙이므로 프리팹에 따로 넣을 필요는 없다.
    /// </summary>
    public abstract class RimHighlightVisual : MonoBehaviour
    {
        private const string ShaderName = "LastTruck/PoliceMarkRim";

        private static Shader _shader;
        private static bool _shaderMissingLogged;

        protected static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        protected static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        protected static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        protected static readonly int FillId = Shader.PropertyToID("_Fill");
        protected static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        protected static readonly int PulseMinId = Shader.PropertyToID("_PulseMin");
        protected static readonly int FresnelId = Shader.PropertyToID("_Fresnel");

        protected Material RimMaterial { get; private set; }
        private readonly List<Renderer> _targets = new List<Renderer>();
        private float _until;
        private bool _attached;

        protected bool Attached => _attached;
        protected float TimeLeft => _until - Time.time;

        /// <summary>림 머티리얼 초기 값 (색, 세기, 맥동 등)을 정한다.</summary>
        protected abstract void Configure(Material material);

        /// <summary>매 프레임 추가 연출 (번쩍임 등). 기본은 아무것도 안 함.</summary>
        protected virtual void Animate(Material material) { }

        protected static T Find<T>(Component target, bool create) where T : RimHighlightVisual
        {
            if (target == null) return null;
            T visual = target.GetComponent<T>();
            if (visual == null && create) visual = target.gameObject.AddComponent<T>();
            return visual;
        }

        /// <summary>연출 시작/연장 (until: 끝나는 Time.time).</summary>
        protected void Begin(float until)
        {
            _until = Mathf.Max(_until, until);
            if (!_attached) Attach();
            enabled = _attached;
        }

        /// <summary>즉시 끄기 (몬스터 사망 등).</summary>
        public void Stop()
        {
            _until = 0f;
            Detach();
            enabled = false;
        }

        private void Attach()
        {
            if (_shader == null) _shader = Shader.Find(ShaderName);
            if (_shader == null)
            {
                if (!_shaderMissingLogged)
                {
                    _shaderMissingLogged = true;
                    Debug.LogWarning($"[RimHighlightVisual] 셰이더 '{ShaderName}'를 찾지 못해 림라이트 연출을 끕니다. (Assets/Resources/Shaders/PoliceMarkRim.shader 확인)");
                }
                return;
            }

            RimMaterial = new Material(_shader) { name = GetType().Name + " (Instance)", hideFlags = HideFlags.DontSave };
            RimMaterial.SetFloat(FresnelId, 1f);
            Configure(RimMaterial);

            // 몸 렌더러(메시/스킨 메시)에만 덧붙인다. 비활성 몸(좀비 변형)도 같이 붙여 둬서 켜져도 보이게 한다.
            _targets.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                if (r.GetComponent<TextMesh>() != null) continue;   // 데미지 숫자 등 글자는 제외

                var materials = new List<Material>(r.sharedMaterials);
                if (materials.Contains(RimMaterial)) continue;
                materials.Add(RimMaterial);
                r.sharedMaterials = materials.ToArray();
                _targets.Add(r);
            }

            _attached = true;
        }

        protected virtual void Update()
        {
            if (!_attached) { enabled = false; return; }

            if (Time.time >= _until)
            {
                Detach();
                enabled = false;
                return;
            }

            Animate(RimMaterial);
        }

        private void Detach()
        {
            if (!_attached) return;
            _attached = false;

            foreach (Renderer r in _targets)
            {
                if (r == null) continue;
                var materials = new List<Material>(r.sharedMaterials);
                if (materials.RemoveAll(m => m == RimMaterial) > 0)
                    r.sharedMaterials = materials.ToArray();
            }
            _targets.Clear();

            if (RimMaterial != null) Destroy(RimMaterial);
            RimMaterial = null;
        }

        protected virtual void OnDestroy()
        {
            Detach();
        }
    }
}
