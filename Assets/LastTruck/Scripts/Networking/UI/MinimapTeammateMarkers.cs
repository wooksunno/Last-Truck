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

            LastTruck.MinimapView view = _minimap.ToolkitView;
            foreach (NetworkPlayer player in NetworkPlayer.All)
            {
                if (player == null || player.IsLocal) continue;

                // UI Toolkit 미니맵이면 그쪽에 표시한다
                if (view != null)
                {
                    Vector3 d = player.transform.position - center.position;
                    Vector2 p = new Vector2(d.x, d.z) * scale;
                    if (p.magnitude > edge) p = p.normalized * edge;
                    int key = player.GetInstanceID();
                    if (!_viewKeys.Contains(key)) _viewKeys.Add(key);
                    view.SetMarker(key, p, player.IsDowned ? DownedMarkerColor : markerColor, !player.IsSeated);
                    continue;
                }

                if (!_markers.TryGetValue(player, out Image marker))
                {
                    marker = CreateMarker();
                    _markers[player] = marker;
                }

                // 기절한 동료도 표시한다 (빨간 점 → 살리러 갈 위치). 트럭에 탄 동료는 숨긴다.
                bool visible = !player.IsSeated;
                if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
                if (!visible) continue;
                marker.color = player.IsDowned ? DownedMarkerColor : markerColor;

                Vector3 delta = player.transform.position - center.position;
                var position = new Vector2(delta.x, delta.z) * scale;
                if (position.magnitude > edge) position = position.normalized * edge; // 범위 밖: 테두리에 붙인다
                marker.rectTransform.anchoredPosition = position;
                marker.transform.SetAsLastSibling(); // 자원 점보다 위에
            }

            // 나간 동료의 UI Toolkit 표시 지우기
            if (view != null)
            {
                _liveKeys.Clear();
                foreach (NetworkPlayer player in NetworkPlayer.All)
                    if (player != null && player.Object != null && player.Object.IsValid) _liveKeys.Add(player.GetInstanceID());
                for (int i = _viewKeys.Count - 1; i >= 0; i--)
                {
                    if (_liveKeys.Contains(_viewKeys[i])) continue;
                    view.RemoveMarker(_viewKeys[i]);
                    _viewKeys.RemoveAt(i);
                }
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

        private readonly List<int> _viewKeys = new List<int>();
        private readonly HashSet<int> _liveKeys = new HashSet<int>();

        private static readonly Color DownedMarkerColor = new Color(1f, 0.25f, 0.2f);

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
