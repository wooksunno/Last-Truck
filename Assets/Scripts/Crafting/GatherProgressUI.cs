using UnityEngine;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// E키를 꾹 눌러 채집할 때 화면 중앙 아래쪽에 원형(Radial) 진행률을 표시한다.
    /// LastTruck.PlayerInteract의 IsHolding/HoldProgress01 값을 매 프레임 읽어 반영한다.
    /// </summary>
    public class GatherProgressUI : MonoBehaviour
    {
        private LastTruck.PlayerInteract _playerInteract;
        private Image _fillImage;
        private GameObject _root;

        private static Sprite _cachedCircleSprite;

        public void Initialize(LastTruck.PlayerInteract playerInteract)
        {
            _playerInteract = playerInteract;
            if (_root == null)
                BuildUI();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("GatherProgressCanvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGO.AddComponent<CanvasScaler>();

            _root = new GameObject("GatherProgressRoot");
            _root.transform.SetParent(canvasGO.transform, false);
            RectTransform rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, -90f);
            rootRect.sizeDelta = new Vector2(56f, 56f);

            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(_root.transform, false);
            RectTransform bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgGO.AddComponent<Image>();
            bgImage.sprite = GetCircleSprite();
            bgImage.color = new Color(0f, 0f, 0f, 0.45f);

            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(_root.transform, false);
            RectTransform fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);
            _fillImage = fillGO.AddComponent<Image>();
            _fillImage.sprite = GetCircleSprite();
            _fillImage.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Radial360;
            _fillImage.fillOrigin = (int)Image.Origin360.Top;
            _fillImage.fillClockwise = true;
            _fillImage.fillAmount = 0f;

            _root.SetActive(false);
        }

        private void Update()
        {
            if (_playerInteract == null || _root == null)
                return;

            bool holding = _playerInteract.IsHolding;
            if (_root.activeSelf != holding)
                _root.SetActive(holding);

            if (holding)
                _fillImage.fillAmount = _playerInteract.HoldProgress01;
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
    }
}
