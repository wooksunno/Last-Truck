using CraftingSystem;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 멀티플레이 게임 씬 전용 화면 요소를 한 곳에서 만든다 (NetworkGameState.Spawned가 부름, 씬이 바뀌면 같이 사라짐).
    ///  - InGameMenu: ESC → 게임 나가기 확인
    ///  - TeammateNameplates: 동료 머리 위 이름표 + HP바
    ///  - MinimapTeammateMarkers: 미니맵에 동료 위치 점
    /// </summary>
    public static class InGameHud
    {
        private const string RootName = "MultiplayerHud";

        public static void Ensure()
        {
            if (!GameLauncher.IsOnlineSession) return;
            if (GameObject.Find(RootName) != null) return;

            var root = new GameObject(RootName);
            root.AddComponent<InGameMenu>();
            root.AddComponent<TeammateNameplates>();
            root.AddComponent<MinimapTeammateMarkers>();
        }

        #region 공용 도우미

        private static Sprite _circleSprite;

        /// <summary>코드로 만든 동그라미 스프라이트 (미니맵 점 등).</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite != null) return _circleSprite;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var center = new Vector2(size / 2f, size / 2f);
                float radius = size / 2f - 1f;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 1f));
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                _circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return _circleSprite;
            }
        }

        /// <summary>화면 위에 그리는 캔버스 (픽셀 좌표).</summary>
        public static Canvas CreateOverlayCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            return canvas;
        }

        #endregion
    }
}
