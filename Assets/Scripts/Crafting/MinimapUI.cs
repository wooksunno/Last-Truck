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

        [Header("에디터에서 미리 그려둔 UI (비워두면 코드로 생성)")]
        [SerializeField] private RectTransform authoredDotLayer;
        [SerializeField] private RectTransform authoredPlayerMarker;
        [SerializeField] private RectTransform authoredTruckArrow;
        [SerializeField] private RectTransform authoredTruckMarker;
        [SerializeField] private Sprite dotSprite;

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

        // UI Toolkit 미니맵 (있으면 이쪽으로 그리고 uGUI 미니맵은 숨긴다)
        private LastTruck.MinimapView _view;
        /// <summary>UI Toolkit 미니맵 (만들지 못했으면 null → 예전 uGUI 미니맵).</summary>
        public LastTruck.MinimapView ToolkitView => _view;

        #region 외부 표시용 (멀티플레이 동료 마커 - LastTruck.Networking.MinimapTeammateMarkers)

        /// <summary>점/마커를 올리는 레이어 (중앙 = 내 캐릭터). UI가 아직 없으면 null.</summary>
        public RectTransform DotLayer => _dotLayer;
        /// <summary>미니맵 중심이 되는 내 캐릭터.</summary>
        public Transform PlayerTransform => _player;
        /// <summary>미니맵이 보여주는 월드 반경(m).</summary>
        public float ViewRadiusWorld => viewRadiusWorld;
        /// <summary>미니맵 반지름(픽셀).</summary>
        public float PixelRadius => minimapPixelRadius;

        #endregion

        public void Initialize(Transform player, Transform truck)
        {
            _player = player;
            _truck = truck;

            if (_dotLayer == null)
            {
                if (authoredDotLayer != null && authoredPlayerMarker != null && authoredTruckArrow != null && authoredTruckMarker != null)
                {
                    _dotLayer = authoredDotLayer;
                    _playerMarker = authoredPlayerMarker;
                    _truckArrow = authoredTruckArrow;
                    _truckMarker = authoredTruckMarker;
                    _truckMarker.gameObject.SetActive(false);
                }
                else
                {
                    BuildUI();
                }
            }

            if (_view == null)
            {
                _view = LastTruck.MinimapView.Create(minimapPixelRadius);
                if (_view != null && _dotLayer != null)
                {
                    Canvas canvas = _dotLayer.GetComponentInParent<Canvas>();
                    if (canvas != null) canvas.enabled = false;
                }
            }

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
            if (_view != null) { _view.SetPlayerHeading(_player.eulerAngles.y); return; }
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_player.eulerAngles.y);
        }

private void UpdateTruckArrow()
        {
            if (_view != null)
            {
                if (_truck == null) { _view.SetTruck(false, false, Vector2.zero, 0f); return; }
                Vector3 d = _truck.position - _player.position;
                d.y = 0f;
                bool inside = d.magnitude <= viewRadiusWorld;
                _view.SetTruck(true, inside, new Vector2(d.x, d.z) * (minimapPixelRadius / viewRadiusWorld), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                return;
            }

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

                // 대부분의 자원 노드는 모델(렌더러)이 자식에 있다 (예전에는 자기 자신만 봐서 점이 거의 안 찍혔다)
                Renderer renderer = node.GetComponent<Renderer>();
                if (renderer == null) renderer = node.GetComponentInChildren<Renderer>();
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (node is SpecialResourceNode special && special.IsDepleted)
                    continue;

                Vector3 delta = node.transform.position - _player.position;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist > viewRadiusWorld)
                    continue;

                Vector2 uiPos = new Vector2(delta.x, delta.z) * scale;
                Color c = DotColor(node, renderer);

                if (_view != null)
                {
                    _view.SetDot(used, uiPos, c);
                    used++;
                    continue;
                }

                Image dot = GetPooledDot(used);
                used++;
                dot.rectTransform.anchoredPosition = uiPos;
                dot.color = c;
                dot.gameObject.SetActive(true);
            }

            if (_view != null)
            {
                _view.HideDotsFrom(used);
                return;
            }
            for (int i = used; i < _dotPool.Count; i++)
                _dotPool[i].gameObject.SetActive(false);
        }

        /// <summary>점 색: 자원 종류별로 정해 둔 색 (모르는 종류는 재질 색).</summary>
        private static Color DotColor(Component node, Renderer renderer)
        {
            string id = node is SpecialResourceNode sn && sn.Item != null ? sn.Item.itemID : null;
            switch (id)
            {
                case ItemIds.CopperOre: return new Color(1f, 0.52f, 0.28f);
                case ItemIds.IronOre: return new Color(0.80f, 0.82f, 0.88f);
                case ItemIds.PlatinumOre: return new Color(1f, 0.85f, 0.25f);
                case ItemIds.Diamond: return new Color(0.35f, 0.75f, 1f);
                case ItemIds.Oil: return new Color(0.55f, 0.35f, 0.75f);
                case ItemIds.Herb: return new Color(0.45f, 0.90f, 0.40f);
                case ItemIds.Stone: return new Color(0.62f, 0.60f, 0.58f);
                case ItemIds.Wood: return new Color(0.70f, 0.48f, 0.26f);
            }
            Material m = renderer.sharedMaterial;
            Color c = m == null ? Color.white : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color);
            c.a = 1f;
            return c;
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
            img.sprite = dotSprite != null ? dotSprite : GetCircleSprite();
            img.raycastTarget = false;

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
