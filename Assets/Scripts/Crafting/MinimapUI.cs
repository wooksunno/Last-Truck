using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 화면 우측 상단 원형 미니맵. 플레이어 근처의 자원 노드를 실제 색상 그대로 점으로 표시하고,
    /// 트럭 방향을 가리키는 화살표를 항상 테두리에 표시한다. 카메라/렌더텍스처 없이 순수 UI로 만든다.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private float viewRadiusWorld = 60f;
        [SerializeField] private float refreshInterval = 0.4f;
        [SerializeField] private float minimapPixelRadius = 100f;

        private Transform _player;
        private Transform _truck;

        private RectTransform _dotLayer;
        private RectTransform _playerMarker;
        private RectTransform _truckArrow;
        private Image _truckArrowImage;
        private RectTransform _truckMarker;
        private Image _truckMarkerImage;

        private readonly List<Component> _trackedNodes = new List<Component>();
        private readonly List<Image> _dotPool = new List<Image>();

        private static Sprite _cachedCircleSprite;
        private static Sprite _cachedTriangleSprite;

        private float _refreshTimer;

        public void Initialize(Transform player, Transform truck)
        {
            _player = player;
            _truck = truck;

            if (_dotLayer == null)
                BuildUI();

            // CraftingSceneBootstrap은 실행 순서 -100으로 Awake()에서 미니맵을 초기화하므로,
            // MapGenerator.Start()가 자원 노드를 다 생성하기 전이다. 약간 지연 후 생성 완료 시점에 수집한다.
            CancelInvoke(nameof(CollectTrackedNodes));
            Invoke(nameof(CollectTrackedNodes), 0.2f);
        }

        private void CollectTrackedNodes()
        {
            _trackedNodes.Clear();

            var specialNodes = FindObjectsByType<SpecialResourceNode>(FindObjectsSortMode.None);
            foreach (var n in specialNodes)
                _trackedNodes.Add(n);

            var basicNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            foreach (var n in basicNodes)
                _trackedNodes.Add(n);
        }

        private void Update()
        {
            if (_player == null || _dotLayer == null)
                return;

            UpdatePlayerMarker();
            UpdateTruckArrow();

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = refreshInterval;
                RefreshMinerals();
            }
        }

        private void UpdatePlayerMarker()
        {
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_player.eulerAngles.y);
        }

private void UpdateTruckArrow()
        {
            if (_truck == null)
            {
                _truckArrow.gameObject.SetActive(false);
                _truckMarker.gameObject.SetActive(false);
                return;
            }

            Vector3 delta = _truck.position - _player.position;
            delta.y = 0f;
            float dist = delta.magnitude;

            if (dist <= viewRadiusWorld)
            {
                // 내 시야(미니맵 범위) 안에 트럭이 있으면 화살표 대신 지도 위 실제 위치에 트럭 아이콘을 표시한다.
                _truckArrow.gameObject.SetActive(false);
                _truckMarker.gameObject.SetActive(true);

                float scale = minimapPixelRadius / viewRadiusWorld;
                _truckMarker.anchoredPosition = new Vector2(delta.x, delta.z) * scale;
                return;
            }

            // 범위 밖이면 테두리에서 방향만 가리키는 화살표로 표시한다.
            _truckMarker.gameObject.SetActive(false);
            _truckArrow.gameObject.SetActive(true);

            if (delta.sqrMagnitude < 0.0001f)
            {
                _truckArrow.anchoredPosition = Vector2.zero;
                return;
            }

            float bearingDeg = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float bearingRad = bearingDeg * Mathf.Deg2Rad;

            float edgeRadius = minimapPixelRadius - 16f;
            _truckArrow.anchoredPosition = new Vector2(Mathf.Sin(bearingRad) * edgeRadius, Mathf.Cos(bearingRad) * edgeRadius);
            _truckArrow.localRotation = Quaternion.Euler(0f, 0f, -bearingDeg);
        }

        private void RefreshMinerals()
        {
            int used = 0;
            float scale = minimapPixelRadius / viewRadiusWorld;

            for (int i = 0; i < _trackedNodes.Count; i++)
            {
                Component node = _trackedNodes[i];
                if (node == null)
                    continue;

                Renderer renderer = node.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled)
                    continue;

                Vector3 delta = node.transform.position - _player.position;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist > viewRadiusWorld)
                    continue;

                Image dot = GetPooledDot(used);
                used++;

                Vector2 uiPos = new Vector2(delta.x, delta.z) * scale;
                dot.rectTransform.anchoredPosition = uiPos;

                Color c = renderer.sharedMaterial.HasProperty("_BaseColor")
                    ? renderer.sharedMaterial.GetColor("_BaseColor")
                    : renderer.sharedMaterial.color;
                dot.color = c;
                dot.gameObject.SetActive(true);
            }

            for (int i = used; i < _dotPool.Count; i++)
                _dotPool[i].gameObject.SetActive(false);
        }

        private Image GetPooledDot(int index)
        {
            if (index < _dotPool.Count)
                return _dotPool[index];

            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_dotLayer, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(9f, 9f);

            Image img = go.GetComponent<Image>();
            img.sprite = GetCircleSprite();

            _dotPool.Add(img);
            return img;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("MinimapCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            canvasGo.AddComponent<CanvasScaler>();

            var rootGo = new GameObject("MinimapRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-24f, -24f);
            rootRect.sizeDelta = new Vector2(minimapPixelRadius * 2f, minimapPixelRadius * 2f);

            // 테두리(배경보다 살짝 큰 원)
            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(rootGo.transform, false);
            RectTransform borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-4f, -4f);
            borderRect.offsetMax = new Vector2(4f, 4f);
            Image borderImg = borderGo.GetComponent<Image>();
            borderImg.sprite = GetCircleSprite();
            borderImg.color = new Color(0.8f, 0.75f, 0.5f, 0.9f);

            // 배경
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(rootGo.transform, false);
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = GetCircleSprite();
            bgImg.color = new Color(0.16f, 0.15f, 0.13f, 0.92f);

            // 원형으로 잘리는 컨텐츠 영역(점/화살표가 이 안에서만 보임)
            var maskGo = new GameObject("Masked", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(rootGo.transform, false);
            RectTransform maskRect = maskGo.GetComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = Vector2.zero;
            maskRect.offsetMax = Vector2.zero;
            Image maskImg = maskGo.GetComponent<Image>();
            maskImg.sprite = GetCircleSprite();
            maskImg.color = Color.white;
            Mask mask = maskGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var dotLayerGo = new GameObject("DotLayer", typeof(RectTransform));
            dotLayerGo.transform.SetParent(maskGo.transform, false);
            _dotLayer = dotLayerGo.GetComponent<RectTransform>();
            _dotLayer.anchorMin = new Vector2(0.5f, 0.5f);
            _dotLayer.anchorMax = new Vector2(0.5f, 0.5f);
            _dotLayer.pivot = new Vector2(0.5f, 0.5f);
            _dotLayer.anchoredPosition = Vector2.zero;
            _dotLayer.sizeDelta = Vector2.zero;

            // 트럭 방향 화살표 (항상 테두리에서 트럭 방향을 가리킴)
            var arrowGo = new GameObject("TruckArrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(_dotLayer, false);
            _truckArrow = arrowGo.GetComponent<RectTransform>();
            _truckArrow.sizeDelta = new Vector2(20f, 24f);
            _truckArrowImage = arrowGo.GetComponent<Image>();
            _truckArrowImage.sprite = GetTriangleSprite();
            _truckArrowImage.color = new Color(1f, 0.65f, 0.1f, 1f);

            // 트럭 위치 마커 (트럭이 미니맵 범위 안에 있을 때 실제 위치에 표시)
            var truckMarkerGo = new GameObject("TruckMarker", typeof(RectTransform), typeof(Image));
            truckMarkerGo.transform.SetParent(_dotLayer, false);
            _truckMarker = truckMarkerGo.GetComponent<RectTransform>();
            _truckMarker.sizeDelta = new Vector2(16f, 16f);
            _truckMarkerImage = truckMarkerGo.GetComponent<Image>();
            _truckMarkerImage.sprite = GetCircleSprite();
            _truckMarkerImage.color = new Color(1f, 0.65f, 0.1f, 1f);
            _truckMarker.gameObject.SetActive(false);

            // 플레이어 마커 (항상 중앙, 바라보는 방향으로 회전)
            var playerGo = new GameObject("PlayerMarker", typeof(RectTransform), typeof(Image));
            playerGo.transform.SetParent(_dotLayer, false);
            _playerMarker = playerGo.GetComponent<RectTransform>();
            _playerMarker.anchoredPosition = Vector2.zero;
            _playerMarker.sizeDelta = new Vector2(16f, 20f);
            Image playerImg = playerGo.GetComponent<Image>();
            playerImg.sprite = GetTriangleSprite();
            playerImg.color = new Color(0.3f, 0.85f, 1f, 1f);
        }

        private static Sprite GetCircleSprite()
        {
            if (_cachedCircleSprite != null)
                return _cachedCircleSprite;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedCircleSprite;
        }

        private static Sprite GetTriangleSprite()
        {
            if (_cachedTriangleSprite != null)
                return _cachedTriangleSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 apex = new Vector2(size * 0.5f, size * 0.95f);
            Vector2 baseLeft = new Vector2(size * 0.12f, size * 0.08f);
            Vector2 baseRight = new Vector2(size * 0.88f, size * 0.08f);

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = PointInTriangle(p, apex, baseLeft, baseRight);
                    pixels[y * size + x] = inside ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedTriangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedTriangleSprite;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);

            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
