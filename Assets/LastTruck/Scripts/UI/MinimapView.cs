using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 오른쪽 위 원형 미니맵 (UI Toolkit). 위치 계산은 CraftingSystem.MinimapUI가 하고, 이 화면은 받은 값으로 그리기만 한다.
    ///  - 좌표는 미니맵 중심 기준 픽셀 (오른쪽 +x, 위쪽 +y). 둥근 판 밖은 잘린다.
    ///  - 자원 점, 내 캐릭터 화살표(바라보는 방향), 트럭(범위 안: 아이콘 / 밖: 테두리 화살표), 동료 점.
    /// </summary>
    public class MinimapView : MonoBehaviour
    {
        private const string PanelSettingsPath = "UI/LastTruckHudPanelSettings";
        private const string UssPath = "UI/LastTruckMinimap";

        private VisualElement _frame, _map, _dots, _player, _truckArrow, _truckMarker;
        private readonly List<VisualElement> _dotPool = new List<VisualElement>();
        private readonly Dictionary<int, VisualElement> _markers = new Dictionary<int, VisualElement>();
        private float _radius;

        public float Radius => _radius;

        public static MinimapView Create(float pixelRadius)
        {
            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            PanelSettings settings = Resources.Load<PanelSettings>(PanelSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (skin == null || settings == null || uss == null) return null;

            var go = new GameObject("UIDocument_Minimap");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<MinimapView>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.sortingOrder = 2;
            view.Build(doc.rootVisualElement, uss, skin, pixelRadius);
            return view;
        }

        private void Build(VisualElement root, StyleSheet uss, LastTruckUiSkin skin, float radius)
        {
            _radius = radius;
            root.styleSheets.Add(uss);
            root.AddToClassList("lt-root");
            root.pickingMode = PickingMode.Ignore;

            float size = radius * 2f;
            _frame = new VisualElement { pickingMode = PickingMode.Ignore };
            _frame.AddToClassList("mm-frame");
            _frame.style.width = size + 28f;
            _frame.style.height = size + 28f;
            root.Add(_frame);

            var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            shadow.AddToClassList("mm-shadow");
            _frame.Add(shadow);

            // 둥근 지도 (밖은 잘린다)
            _map = new VisualElement { pickingMode = PickingMode.Ignore };
            _map.AddToClassList("mm-map");
            _map.style.width = size;
            _map.style.height = size;
            _map.style.borderTopLeftRadius = _map.style.borderTopRightRadius = _map.style.borderBottomLeftRadius = _map.style.borderBottomRightRadius = radius;
            _frame.Add(_map);

            // 거리 고리 (반경 1/2)
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("mm-range");
            ring.style.width = radius; ring.style.height = radius;
            ring.style.left = radius * 0.5f; ring.style.top = radius * 0.5f;
            ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = radius * 0.5f;
            _map.Add(ring);

            _dots = new VisualElement { pickingMode = PickingMode.Ignore };
            _dots.AddToClassList("mm-layer");
            _map.Add(_dots);

            _truckMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            _truckMarker.AddToClassList("mm-truck");
            Sprite truck = UiIcons.Get("truck");
            if (truck != null) _truckMarker.style.backgroundImage = new StyleBackground(truck);
            else _truckMarker.AddToClassList("mm-truck--dot");
            _map.Add(_truckMarker);

            _truckArrow = new Triangle(new Color(1f, 0.65f, 0.1f)) { pickingMode = PickingMode.Ignore };
            _truckArrow.AddToClassList("mm-truck-arrow");
            _map.Add(_truckArrow);

            _player = new Triangle(new Color(0.3f, 0.85f, 1f)) { pickingMode = PickingMode.Ignore };
            _player.AddToClassList("mm-player");
            _map.Add(_player);

            // 테두리 고리 + 북쪽 표시
            var frameRing = new VisualElement { pickingMode = PickingMode.Ignore };
            frameRing.AddToClassList("mm-ring");
            frameRing.AddToClassList("mm-ring--plain");   // 시계 판과 같은 금색 고리 (Kenney 고리 그림은 파란 화살표가 있어 어울리지 않았다)
            _frame.Add(frameRing);

            var north = new Label("N") { pickingMode = PickingMode.Ignore };
            north.AddToClassList("mm-north");
            if (LastTruckHud.SharedFont.HasValue) north.style.unityFontDefinition = LastTruckHud.SharedFont.Value;
            _frame.Add(north);
        }

        public void SetVisible(bool visible)
        {
            if (_frame != null) _frame.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Place(VisualElement e, Vector2 pos, float size)
        {
            e.style.left = _radius + pos.x - size * 0.5f;
            e.style.top = _radius - pos.y - size * 0.5f;
        }

        /// <summary>내 캐릭터가 바라보는 방향 (월드 Y 회전, 도).</summary>
        public void SetPlayerHeading(float yawDeg)
        {
            if (_player == null) return;
            Place(_player, Vector2.zero, 18f);
            _player.style.rotate = new Rotate(new Angle(yawDeg, AngleUnit.Degree));
        }

        /// <summary>트럭: inside면 지도 위 위치에 아이콘, 아니면 테두리에서 방향(bearing, 도)을 가리키는 화살표.</summary>
        public void SetTruck(bool exists, bool inside, Vector2 pos, float bearingDeg)
        {
            if (_truckMarker == null) return;
            _truckMarker.style.display = exists && inside ? DisplayStyle.Flex : DisplayStyle.None;
            _truckArrow.style.display = exists && !inside ? DisplayStyle.Flex : DisplayStyle.None;
            if (!exists) return;
            if (inside) Place(_truckMarker, pos, 26f);
            else
            {
                float r = _radius - 14f, b = bearingDeg * Mathf.Deg2Rad;
                Place(_truckArrow, new Vector2(Mathf.Sin(b) * r, Mathf.Cos(b) * r), 22f);
                _truckArrow.style.rotate = new Rotate(new Angle(bearingDeg, AngleUnit.Degree));
            }
        }

        /// <summary>자원 점 i번을 놓는다. 다 놓은 뒤 HideDotsFrom(개수)로 남은 점을 숨긴다.</summary>
        public void SetDot(int i, Vector2 pos, Color color)
        {
            while (_dotPool.Count <= i)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                d.AddToClassList("mm-dot");
                _dots.Add(d);
                _dotPool.Add(d);
            }
            VisualElement dot = _dotPool[i];
            dot.style.display = DisplayStyle.Flex;
            dot.style.backgroundColor = color;
            Place(dot, pos, 9f);
        }

        public void HideDotsFrom(int count)
        {
            for (int i = count; i < _dotPool.Count; i++)
                _dotPool[i].style.display = DisplayStyle.None;
        }

        /// <summary>동료 같은 추가 표시 (key별로 하나). 범위 밖은 부르는 쪽에서 테두리에 붙여서 넘긴다.</summary>
        public void SetMarker(int key, Vector2 pos, Color color, bool visible)
        {
            if (!_markers.TryGetValue(key, out VisualElement m))
            {
                m = new VisualElement { pickingMode = PickingMode.Ignore };
                m.AddToClassList("mm-mate");
                _map.Add(m);
                _markers[key] = m;
            }
            m.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            m.style.backgroundColor = color;
            Place(m, pos, 13f);
            m.BringToFront();
            _player.BringToFront();
        }

        public void RemoveMarker(int key)
        {
            if (_markers.TryGetValue(key, out VisualElement m))
            {
                m.RemoveFromHierarchy();
                _markers.Remove(key);
            }
        }

        /// <summary>위쪽을 가리키는 삼각형 (회전으로 방향을 나타낸다).</summary>
        private class Triangle : VisualElement
        {
            private readonly Color _color;

            public Triangle(Color color)
            {
                _color = color;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext ctx)
            {
                Rect r = contentRect;
                var p = ctx.painter2D;
                p.fillColor = _color;
                p.strokeColor = new Color(0.08f, 0.06f, 0.04f, 0.9f);
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(new Vector2(r.width * 0.5f, 0f));
                p.LineTo(new Vector2(r.width, r.height));
                p.LineTo(new Vector2(r.width * 0.5f, r.height * 0.72f));
                p.LineTo(new Vector2(0f, r.height));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }
    }
}
