using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 내구도가 낮아지면 문과 지붕이 투명해지고, 수리되면 다시 원래대로 보인다.
    /// 원본 머티리얼은 흰색 _BaseColor + _BaseMap 텍스처로 실제 색을 낸다.
    ///
    /// 지붕은 차체(본네트/적재함/펜더 포함) 메쉬와 한 덩어리라 따로 뗄 수 없어서, 높이(worldY) 기준으로
    /// clip()하는 셰이더(Custom/TruckRoofClip, Opaque+ZWrite On)를 쓴다 — 알파블렌딩이 아니라 완전히
    /// 잘라내는 방식이라 차체 전체의 깊이 정렬은 그대로 유지된다.
    ///
    /// 문은 자체 메쉬라서 알파 페이드가 가능한 별도 셰이더(Custom/TruckDoorFade, Transparent+ZWrite Off)를 쓴다.
    /// 이 둘을 하나로 합쳐 차체까지 Transparent로 만들면 깊이 정렬이 깨져 트럭 전체가 이상하게 보이므로 분리했다.
    /// </summary>
    public class TruckDamageVisuals : MonoBehaviour
    {
        private class FadePart
        {
            public Material[] materials;
            public float detachBelow;
        }

        [Tooltip("이 worldY 이상은 지붕으로 간주해 클립한다. 문 상단(약 1.89) 바로 위로 잡는다.")]
        [SerializeField] private float roofClipWorldY = 1.86f;
        [Tooltip("지붕이 사라지기 시작하는 내구도 비율.")]
        [SerializeField] private float roofDetachBelow = 0.35f;
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private float goneAlpha = 0.08f;

        private readonly List<FadePart> _doors = new List<FadePart>();
        private LastTruck.TruckInformation _info;
        private Renderer _bodyRenderer;
        private Material _roofMaterial;
        private int _bodyMaterialIndex = -1;
        private bool _roofGone;
        private Coroutine _doorFadeRoutine;
        private static Shader _roofClipShader;
        private static Shader _doorFadeShader;

        private void Awake()
        {
            _info = GetComponent<LastTruck.TruckInformation>();
            _bodyRenderer = GetComponent<Renderer>();
            _roofClipShader = Shader.Find("Custom/TruckRoofClip");
            _doorFadeShader = Shader.Find("Custom/TruckDoorFade");

            AddDoor("Pick Up_7 L Door", 0.75f);
            AddDoor("Pick Up_7 R Door", 0.5f);
            SetupRoofMaterial();

            if (_info != null)
                _info.OnDurabilityChanged += OnDurabilityChanged;
        }

        private void OnDestroy()
        {
            if (_info != null)
                _info.OnDurabilityChanged -= OnDurabilityChanged;
        }

        private static void CopyBaseAppearance(Material dest, Material source)
        {
            if (source.HasProperty("_BaseMap"))
            {
                dest.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                dest.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                dest.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            }
            dest.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white);
        }

        private void AddDoor(string childName, float detachBelow)
        {
            if (_doorFadeShader == null)
                return;

            Transform t = transform.Find(childName);
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null)
                return;

            Material[] original = r.sharedMaterials;
            var fadeMats = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] == null) continue;
                var mat = new Material(_doorFadeShader);
                CopyBaseAppearance(mat, original[i]);
                mat.SetFloat("_FadeAlpha", 1f);
                fadeMats[i] = mat;
            }

            r.materials = fadeMats;
            _doors.Add(new FadePart { materials = fadeMats, detachBelow = detachBelow });
        }

        private void SetupRoofMaterial()
        {
            if (_bodyRenderer == null || _roofClipShader == null)
                return;

            Material[] shared = _bodyRenderer.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] != null && shared[i].name.Replace(" (Instance)", "") == "Color")
                {
                    _bodyMaterialIndex = i;
                    break;
                }
            }

            if (_bodyMaterialIndex < 0)
                return;

            _roofMaterial = new Material(_roofClipShader);
            CopyBaseAppearance(_roofMaterial, shared[_bodyMaterialIndex]);
            _roofMaterial.SetFloat("_ClipHeight", roofClipWorldY);
            _roofMaterial.SetFloat("_ClipEnabled", 0f);

            Material[] mats = _bodyRenderer.materials;
            mats[_bodyMaterialIndex] = _roofMaterial;
            _bodyRenderer.materials = mats;
        }

        private void OnDurabilityChanged(float current, float max)
        {
            float fraction = max > 0f ? current / max : 0f;

            if (_doorFadeRoutine != null)
                StopCoroutine(_doorFadeRoutine);
            _doorFadeRoutine = StartCoroutine(FadeDoors(fraction));

            bool roofShouldBeGone = fraction < roofDetachBelow;
            if (roofShouldBeGone != _roofGone)
            {
                _roofGone = roofShouldBeGone;
                if (_roofMaterial != null)
                    _roofMaterial.SetFloat("_ClipEnabled", _roofGone ? 1f : 0f);
            }
        }

        private IEnumerator FadeDoors(float fraction)
        {
            var targets = new List<(Material mat, float from, float to)>();
            foreach (FadePart door in _doors)
            {
                bool gone = fraction < door.detachBelow;
                float to = gone ? goneAlpha : 1f;
                foreach (Material mat in door.materials)
                {
                    if (mat == null) continue;
                    float from = mat.GetFloat("_FadeAlpha");
                    targets.Add((mat, from, to));
                }
            }

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeDuration);
                foreach (var (mat, from, to) in targets)
                    mat.SetFloat("_FadeAlpha", Mathf.Lerp(from, to, k));
                yield return null;
            }

            foreach (var (mat, from, to) in targets)
                mat.SetFloat("_FadeAlpha", to);
        }
    }
}
