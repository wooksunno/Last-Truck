using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 미니맵(MinimapUI)에 동료 위치를 초록 점으로 표시한다.
    ///  - 미니맵 범위 밖이면 테두리에 붙여서 방향만 보여준다.
    ///  - 죽은 동료는 표시하지 않는다. 트럭에 탄 동료는 트럭 자리에 보인다.
    /// </summary>
    public class MinimapTeammateMarkers : MonoBehaviour
    {
        [SerializeField] private float markerSize = 12f;
        [SerializeField] private Color markerColor = new Color(0.4f, 1f, 0.45f, 1f);

        private MinimapUI _minimap;
        private readonly Dictionary<NetworkPlayer, Image> _markers = new Dictionary<NetworkPlayer, Image>();
        private readonly List<NetworkPlayer> _removeBuffer = new List<NetworkPlayer>();

        private void LateUpdate()
        {
            if (_minimap == null) _minimap = FindFirstObjectByType<MinimapUI>();
            if (_minimap == null || _minimap.DotLayer == null || _minimap.PlayerTransform == null) return;

            Transform center = _minimap.PlayerTransform;
            float scale = _minimap.PixelRadius / Mathf.Max(1f, _minimap.ViewRadiusWorld);
            float edge = _minimap.PixelRadius - markerSize;

            foreach (NetworkPlayer player in NetworkPlayer.All)
            {
                if (player == null || player.IsLocal) continue;
                if (!_markers.TryGetValue(player, out Image marker))
                {
                    marker = CreateMarker();
                    _markers[player] = marker;
                }

                bool visible = player.IsAlive;
                if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
                if (!visible) continue;

                Vector3 delta = player.transform.position - center.position;
                var position = new Vector2(delta.x, delta.z) * scale;
                if (position.magnitude > edge) position = position.normalized * edge; // 범위 밖: 테두리에 붙인다
                marker.rectTransform.anchoredPosition = position;
                marker.transform.SetAsLastSibling(); // 자원 점보다 위에
            }

            _removeBuffer.Clear();
            foreach (KeyValuePair<NetworkPlayer, Image> pair in _markers)
            {
                if (pair.Key == null || pair.Key.Object == null || !pair.Key.Object.IsValid) _removeBuffer.Add(pair.Key);
            }
            foreach (NetworkPlayer player in _removeBuffer)
            {
                if (_markers[player] != null) Destroy(_markers[player].gameObject);
                _markers.Remove(player);
            }
        }

        private Image CreateMarker()
        {
            var go = new GameObject("TeammateMarker", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_minimap.DotLayer, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(markerSize, markerSize);
            Image image = go.GetComponent<Image>();
            image.sprite = InGameHud.CircleSprite;
            image.color = markerColor;
            image.raycastTarget = false;
            return image;
        }
    }
}
