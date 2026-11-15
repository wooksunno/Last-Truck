using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 채집할 수 있는 대상(IGatherOutline) 가까이 가면 그 대상의 윤곽선을 굵게 그린다.
    ///
    ///  - "가까이" = 상호작용 범위 안에서 지금 잡힌 대상(PlayerInteract.CurrentInteractable). 이름은 띄우지 않는다.
    ///  - 캘 수 없는 상태(도구 등급 부족, 고갈 등)면 윤곽선이 없다 → 윤곽선이 보이면 "지금 캘 수 있다"는 뜻.
    ///    손에 든 도구를 바꾸면 바로 켜지거나 꺼진다.
    ///  - 윤곽선은 대상의 메쉬마다 뒷면만 그리는 부풀린 복사본(LastTruck/GatherOutline 셰이더)이고, 대상을 벗어나면 지운다.
    ///    한 번에 한 대상만 그리므로 맵에 돌/나무가 아무리 많아도 가볍다.
    /// 씬에 따로 둘 것이 없다 (게임이 시작되면 자동으로 만들어진다).
    /// </summary>
    public class GatherOutlineHighlighter : MonoBehaviour
    {
        private const int MaxMeshes = 24;
        private static readonly Color OutlineColor = new Color(1f, 0.92f, 0.35f, 1f);
        private const float OutlinePixels = 15f;

        private LastTruck.PlayerInteract _interact;
        private float _nextFindPlayer;
        private IGatherOutline _current;
        private GameObject _currentRoot;
        private readonly List<GameObject> _hulls = new List<GameObject>();
        private readonly List<Renderer> _hullRenderers = new List<Renderer>();
        private Material _material;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int WidthId = Shader.PropertyToID("_Width");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<GatherOutlineHighlighter>() != null) return;
            var go = new GameObject("GatherOutlineHighlighter");
            DontDestroyOnLoad(go);
            go.AddComponent<GatherOutlineHighlighter>();
        }

        private void OnDestroy() => Clear();

        private void LateUpdate()
        {
            IGatherOutline target = FindTarget();
            if (target == null || !target.CanGatherNow)
            {
                Clear();
                return;
            }

            GameObject root = target.OutlineRoot;
            if (root == null) { Clear(); return; }

            if (!ReferenceEquals(target, _current) || root != _currentRoot)
            {
                Clear();
                Build(target, root);
            }

            // 숨 쉬듯 살짝 밝아졌다 어두워진다
            float pulse = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 5f);
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in _hullRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, OutlineColor * pulse);
                r.SetPropertyBlock(block);
            }
        }

        private IGatherOutline FindTarget()
        {
            if (_interact == null && Time.unscaledTime >= _nextFindPlayer)
            {
                _nextFindPlayer = Time.unscaledTime + 0.5f;
                GameObject player = GatherRules.Player;
                _interact = player != null ? player.GetComponent<LastTruck.PlayerInteract>() : null;
            }
            if (_interact == null || !_interact.isActiveAndEnabled) return null;

            var comp = _interact.CurrentInteractable as Component;
            if (comp == null) return null;   // 파괴된 대상
            return comp as IGatherOutline;
        }

        private void Build(IGatherOutline target, GameObject root)
        {
            _current = target;
            _currentRoot = root;

            if (_material == null)
            {
                _material = Resources.Load<Material>("Materials/GatherOutline");
                if (_material == null)
                {
                    Shader shader = Shader.Find("LastTruck/GatherOutline");
                    if (shader == null) return;
                    _material = new Material(shader);
                }
            }

            int count = 0;
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (count >= MaxMeshes) break;
                if (!mr.enabled) continue;
                var filter = mr.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (mr.GetComponent<ParticleSystem>() != null) continue;

                var hull = new GameObject("GatherOutlineHull");
                hull.transform.SetParent(mr.transform, false);
                hull.layer = mr.gameObject.layer;
                hull.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var hr = hull.AddComponent<MeshRenderer>();
                hr.sharedMaterial = _material;
                hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hr.receiveShadows = false;

                var block = new MaterialPropertyBlock();
                block.SetVector(CenterId, filter.sharedMesh.bounds.center);
                block.SetFloat(WidthId, OutlinePixels);
                block.SetColor(ColorId, OutlineColor);
                hr.SetPropertyBlock(block);

                _hulls.Add(hull);
                _hullRenderers.Add(hr);
                count++;
            }
        }

        private void Clear()
        {
            foreach (GameObject hull in _hulls) if (hull != null) Destroy(hull);
            _hulls.Clear();
            _hullRenderers.Clear();
            _current = null;
            _currentRoot = null;
        }
    }
}
