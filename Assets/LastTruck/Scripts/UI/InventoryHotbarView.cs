using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 우하단 플레이어 인벤토리(핫바)를 UI Toolkit으로 그린다. PlayerInventoryHUD가 만들고 값을 넘겨 준다.
    ///  - 칸 클릭: 손에 들기 (트럭/파우치 창이 열려 있으면 그쪽으로 옮기기 - 판단은 GameUIController)
    ///  - 칸을 끌어 다른 칸에 놓기: 교환/합치기, 인벤토리 밖 빈 화면에 놓기: 버리기
    ///  - 도구는 칸 아래 내구도 막대, 20% 이하면 깜빡인다
    /// 스킨/패널 설정이 없으면 만들지 않고 false를 돌려준다 (그러면 예전 uGUI 핫바가 그대로 보인다).
    /// </summary>
    public class InventoryHotbarView : MonoBehaviour
    {
        private const string PanelSettingsPath = "UI/LastTruckHudPanelSettings";
        private const string UssPath = "UI/LastTruckInventory";
        private const float DragThreshold = 8f;

        private class SlotEl
        {
            public VisualElement root, icon, durBack, durFill;
            public Label letter, count, key;
            public ItemData item;
            public int amount = -1;
            public bool selected;
            public float durability = -1f;
            public bool inited;
        }

        private readonly List<SlotEl> _slots = new List<SlotEl>();
        private VisualElement _panel, _slotRow, _ghost;
        private Label _usedLabel, _heldLabel;
        private LastTruckUiSkin _skin;
        private StyleFontDefinition _font;
        private FontAsset _ownFont;

        private Action<int> _onClick;
        private Action<int, int> _onSwap;
        private Action<int> _onDropOut;

        // 끌기 상태
        private int _pressIndex = -1;
        private Vector2 _pressPos;
        private bool _dragging;
        private VisualElement _captureEl;

        /// <summary>핫바를 만든다. 스킨이 없으면 null.</summary>
        public static InventoryHotbarView Create(int slotCount, Action<int> onClick, Action<int, int> onSwap, Action<int> onDropOut)
        {
            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            PanelSettings settings = Resources.Load<PanelSettings>(PanelSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (skin == null || settings == null || uss == null || skin.panelBrownDark == null)
                return null;

            var go = new GameObject("UIDocument_InventoryHotbar");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<InventoryHotbarView>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.sortingOrder = 5;
            view._skin = skin;
            view._onClick = onClick;
            view._onSwap = onSwap;
            view._onDropOut = onDropOut;
            view.Build(doc.rootVisualElement, uss, slotCount);
            return view;
        }

        private void OnDestroy()
        {
            if (_ownFont != null) Destroy(_ownFont);
        }

        // ------------------------------------------------------------------ 만들기

        private void Build(VisualElement root, StyleSheet uss, int slotCount)
        {
            root.styleSheets.Add(uss);
            root.AddToClassList("lt-root");
            root.pickingMode = PickingMode.Ignore;

            if (LastTruckHud.SharedFont.HasValue)
                _font = LastTruckHud.SharedFont.Value;
            else if (_skin.font != null)
            {
                _ownFont = FontAsset.CreateFontAsset(_skin.font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                _font = _ownFont != null ? new StyleFontDefinition(FontDefinition.FromSDFFont(_ownFont)) : new StyleFontDefinition(_skin.font);
            }

            _panel = new VisualElement();
            _panel.AddToClassList("inv-panel");
            UiToolkitSkin.Slice(_panel, _skin.panelBrownDark, 0.5f, new Color(0.62f, 0.51f, 0.45f, 1f));
            root.Add(_panel);

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("inv-header");
            header.Add(MakeLabel("인벤토리", "inv-title"));
            _heldLabel = MakeLabel("", "inv-held");
            header.Add(_heldLabel);
            _usedLabel = MakeLabel("0/0", "inv-used");
            header.Add(_usedLabel);
            _panel.Add(header);

            _slotRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _slotRow.AddToClassList("inv-row");
            _panel.Add(_slotRow);

            for (int i = 0; i < slotCount; i++)
                _slotRow.Add(MakeSlot(i));

            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.AddToClassList("inv-ghost");
            _ghost.style.display = DisplayStyle.None;
            root.Add(_ghost);

            // 곧 부서질 도구의 내구도 막대 깜빡임
            root.schedule.Execute(BlinkDurability).Every(50);
        }

        private Label MakeLabel(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            l.AddToClassList(cls);
            l.style.unityFontDefinition = _font;
            return l;
        }

        private VisualElement MakeSlot(int index)
        {
            var s = new SlotEl();
            s.root = new VisualElement();
            s.root.AddToClassList("inv-slot");
            UiToolkitSkin.Slice(s.root, _skin.panelBrownDark, 0.35f, UiSkinUtil.SlotNormal);

            s.icon = new VisualElement { pickingMode = PickingMode.Ignore };
            s.icon.AddToClassList("inv-icon");
            s.root.Add(s.icon);

            s.letter = MakeLabel("", "inv-letter");
            s.root.Add(s.letter);
            s.count = MakeLabel("", "inv-count");
            s.root.Add(s.count);
            s.key = MakeLabel((index + 1).ToString(), "inv-key");
            s.root.Add(s.key);

            s.durBack = new VisualElement { pickingMode = PickingMode.Ignore };
            s.durBack.AddToClassList("inv-dur");
            s.durFill = new VisualElement { pickingMode = PickingMode.Ignore };
            s.durFill.AddToClassList("inv-dur-fill");
            s.durBack.Add(s.durFill);
            s.durBack.style.display = DisplayStyle.None;
            s.root.Add(s.durBack);

            int captured = index;
            s.root.RegisterCallback<PointerDownEvent>(e => OnPointerDown(e, captured));
            s.root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            s.root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            s.root.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());

            _slots.Add(s);
            return s.root;
        }

        // ------------------------------------------------------------------ 값 반영

        /// <summary>칸 하나의 내용을 반영한다 (바뀐 값만 다시 그린다).</summary>
        public void SetSlot(int index, bool visible, ItemData item, int amount, bool selected, float durability01)
        {
            if (!IsBuilt || index < 0 || index >= _slots.Count) return;
            SlotEl s = _slots[index];
            DisplayStyle d = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (s.root.style.display != d) s.root.style.display = d;
            if (!visible) return;

            bool itemChanged = !s.inited || item != s.item;
            if (itemChanged)
            {
                s.item = item;
                bool hasIcon = item != null && item.icon != null;
                s.icon.style.backgroundImage = hasIcon ? new StyleBackground(item.icon) : new StyleBackground(StyleKeyword.None);
                s.icon.style.display = hasIcon ? DisplayStyle.Flex : DisplayStyle.None;
                bool showLetter = item != null && item.icon == null;
                s.letter.text = showLetter ? FirstLetters(item.itemName) : "";
                s.letter.style.display = showLetter ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (itemChanged || amount != s.amount)
            {
                s.amount = amount;
                bool showCount = item != null && amount > 1;
                s.count.text = showCount ? amount.ToString() : "";
                s.count.style.display = showCount ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (itemChanged || selected != s.selected || !s.inited)
            {
                s.selected = selected;
                s.root.EnableInClassList("inv-slot--selected", selected);
                Color tint = selected ? UiSkinUtil.SlotSelected : (item != null ? UiSkinUtil.SlotFilled : UiSkinUtil.SlotNormal);
                s.root.style.unityBackgroundImageTintColor = tint;
            }

            bool showDur = item != null && item.HasDurability;
            s.durBack.style.display = showDur ? DisplayStyle.Flex : DisplayStyle.None;
            if (showDur)
            {
                float r = Mathf.Clamp01(durability01);
                if (!Mathf.Approximately(r, s.durability))
                {
                    s.durability = r;
                    s.durFill.style.width = Length.Percent(r * 100f);
                    s.durFill.style.backgroundColor = r > 0.5f
                        ? Color.Lerp(new Color(0.95f, 0.8f, 0.25f), new Color(0.45f, 0.85f, 0.35f), (r - 0.5f) * 2f)
                        : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.8f, 0.25f), r * 2f);
                }
            }
            else s.durability = -1f;

            s.inited = true;
        }

        /// <summary>화면이 만들어져 있는가 (스크립트를 다시 불러온 뒤에는 요소가 사라져 있을 수 있다).</summary>
        public bool IsBuilt => _panel != null;

        public void SetSummary(int used, int capacity, string heldName)
        {
            if (!IsBuilt) return;
            _usedLabel.text = used + "/" + capacity;
            _heldLabel.text = heldName;
        }

        public void SetVisible(bool visible)
        {
            if (!IsBuilt) return;
            _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BlinkDurability()
        {
            float a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
            foreach (SlotEl s in _slots)
            {
                if (s.durability < 0f) continue;
                s.durFill.style.opacity = s.durability <= 0.2f ? a : 1f;
            }
        }

        private static string FirstLetters(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Length <= 2 ? name : name.Substring(0, 2);
        }

        // ------------------------------------------------------------------ 클릭 / 끌기

        private void OnPointerDown(PointerDownEvent e, int index)
        {
            if (e.button != 0) return;
            _pressIndex = index;
            _pressPos = e.position;
            _dragging = false;
            _captureEl = _slots[index].root;
            _captureEl.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (_pressIndex < 0 || _captureEl == null || !_captureEl.HasPointerCapture(e.pointerId)) return;
            Vector2 pos = e.position;
            if (!_dragging)
            {
                SlotEl s = _slots[_pressIndex];
                if (s.item == null || (pos - _pressPos).sqrMagnitude < DragThreshold * DragThreshold) return;
                _dragging = true;
                _ghost.style.backgroundImage = s.item.icon != null ? new StyleBackground(s.item.icon) : new StyleBackground(StyleKeyword.None);
                _ghost.style.display = DisplayStyle.Flex;
                s.root.AddToClassList("inv-slot--dragging");
            }
            _ghost.style.left = pos.x - 36f;
            _ghost.style.top = pos.y - 36f;
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (_pressIndex < 0 || _captureEl == null || !_captureEl.HasPointerCapture(e.pointerId)) return;
            int from = _pressIndex;
            bool wasDragging = _dragging;
            Vector2 pos = e.position;
            _captureEl.ReleasePointer(e.pointerId);   // CaptureOut에서 상태를 정리한다

            if (!wasDragging)
            {
                _onClick?.Invoke(from);
                return;
            }

            int to = SlotAt(pos);
            if (to >= 0)
            {
                if (to != from) _onSwap?.Invoke(from, to);
            }
            else if (!_panel.worldBound.Contains(pos) && !IsOverOtherUi())
            {
                _onDropOut?.Invoke(from);
            }
        }

        private void CancelDrag()
        {
            if (_pressIndex >= 0 && _pressIndex < _slots.Count)
                _slots[_pressIndex].root.RemoveFromClassList("inv-slot--dragging");
            _ghost.style.display = DisplayStyle.None;
            _pressIndex = -1;
            _dragging = false;
            _captureEl = null;
        }

        private int SlotAt(Vector2 panelPos)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                VisualElement r = _slots[i].root;
                if (r.resolvedStyle.display == DisplayStyle.None) continue;
                if (r.worldBound.Contains(panelPos)) return i;
            }
            return -1;
        }

        /// <summary>끝낸 자리가 트럭 창 같은 다른 uGUI 위면 버리지 않는다.</summary>
        private static bool IsOverOtherUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits)
                if (h.gameObject != null && h.gameObject.GetComponentInParent<Canvas>() != null)
                    return true;
            return false;
        }
    }
}
