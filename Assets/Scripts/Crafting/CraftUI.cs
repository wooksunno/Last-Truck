using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 팝업(도감/제작/가공)이 함께 쓰는 UI 부품과 팔레트.
    /// GameUIController가 시작할 때 폰트/스프라이트를 한 번 넣어 준다(Init).
    /// </summary>
    public static class CraftUI
    {
        public static Font Font;
        public static Sprite Round, Ring, Tab, Tri;

        // ---------- 팔레트 ----------
        public static readonly Color PanelCol = new Color(0.10f, 0.08f, 0.07f, 0.92f);
        public static readonly Color CardCol = new Color(0.20f, 0.16f, 0.13f, 1f);
        public static readonly Color CardHover = new Color(1.18f, 1.12f, 1.05f, 1f);
        public static readonly Color Amber = new Color(1f, 0.80f, 0.30f, 1f);
        public static readonly Color Cream = new Color(1f, 0.95f, 0.82f, 1f);
        public static readonly Color Dim = new Color(1f, 0.95f, 0.82f, 0.55f);
        public static readonly Color Green = new Color(0.42f, 0.80f, 0.45f, 1f);
        public static readonly Color Red = new Color(0.94f, 0.42f, 0.38f, 1f);
        public static readonly Color BtnBrown = new Color(0.30f, 0.24f, 0.19f, 1f);
        public static readonly Color BtnAmber = new Color(0.78f, 0.56f, 0.20f, 1f);
        public static readonly Color BtnGreen = new Color(0.30f, 0.58f, 0.36f, 1f);
        public static readonly Color BtnOff = new Color(0.25f, 0.23f, 0.22f, 1f);
        public static readonly Color Parchment = new Color(0.89f, 0.86f, 0.79f, 1f);

        public static void Init(Font font, Sprite round, Sprite ring, Sprite tab, Sprite tri)
        {
            Font = font; Round = round; Ring = ring; Tab = tab; Tri = tri;
        }

        // ---------- 기본 생성 ----------
        public static RectTransform Mk(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        public static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced = true, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color;
            img.type = sprite != null && sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Txt(RectTransform rt, string text, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static RectTransform Panel(RectTransform parent, string name, Color color, bool raycast = false)
        {
            var rt = Mk(name, parent);
            Img(rt, Round, color, true, raycast);
            return rt;
        }

        /// <summary>아이콘(없으면 흐린 회색 사각형 대신 이름 첫 글자).</summary>
        public static void ItemIcon(RectTransform slot, ItemData item, float alpha = 1f)
        {
            if (item != null && item.icon != null)
            {
                var im = Img(slot, item.icon, new Color(1, 1, 1, alpha), false);
                im.preserveAspect = true;
            }
            else
            {
                var t = Txt(slot, item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName.Substring(0, Mathf.Min(2, item.itemName.Length)) : "", 22, FontStyle.Bold, new Color(Cream.r, Cream.g, Cream.b, alpha), TextAnchor.MiddleCenter);
            }
        }

        // ---------- 버튼 ----------
        public static Button MkButton(RectTransform parent, string name, string label, Color color, int fontSize, UnityAction onClick, bool interactable = true)
        {
            var rt = Mk(name, parent);
            var img = Img(rt, Round, interactable ? color : BtnOff, true, true);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = CardHover; cb.pressedColor = new Color(0.8f, 0.78f, 0.75f, 1f); cb.disabledColor = Color.white; b.colors = cb;
            b.interactable = interactable;
            if (onClick != null) b.onClick.AddListener(onClick);
            var lt = Mk("Label", rt); Stretch(lt);
            var t = Txt(lt, label, fontSize, FontStyle.Bold, interactable ? Color.white : new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);
            return b;
        }

        /// <summary>폴더 인덱스 탭(위쪽만 둥근 색상 탭). 선택된 탭은 더 크고 밝다.</summary>
        public static Button FolderTab(RectTransform parent, string label, Color color, bool selected, float width, UnityAction onClick)
        {
            var rt = Mk("Tab_" + label, parent);
            float h = selected ? 46f : 38f;
            rt.sizeDelta = new Vector2(width, h);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width; le.preferredHeight = h; le.minHeight = h;
            Color fill = selected ? color : new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.95f);
            var img = Img(rt, Tab != null ? Tab : Round, fill, true, true);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = CardHover; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);
            var lt = Mk("Label", rt); Stretch(lt, 4, 0, 4, 2);
            Txt(lt, label, selected ? 19 : 17, FontStyle.Bold, selected ? new Color(0.18f, 0.12f, 0.06f, 1f) : Cream, TextAnchor.MiddleCenter);
            return b;
        }

        // ---------- 스크롤 ----------
        public static ScrollRect NewScroll(RectTransform parent, bool horizontal, bool vertical, float l, float b, float r, float t, out RectTransform content)
        {
            var sv = Mk("Scroll", parent); Stretch(sv, l, b, r, t);
            var view = Mk("Viewport", sv); Stretch(view);
            view.gameObject.AddComponent<RectMask2D>();
            var vi = view.gameObject.AddComponent<Image>(); vi.color = new Color(1, 1, 1, 0.004f); vi.raycastTarget = true;   // 빈 곳에서도 드래그/휠 입력
            content = Mk("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var sr = sv.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = content;
            sr.horizontal = horizontal; sr.vertical = vertical;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 28f;
            return sr;
        }

        /// <summary>쉬프트를 누르면 휠이 좌우 이동, 아니면 상하 이동(그래프 팬용).</summary>
        public class WheelPan : MonoBehaviour, UnityEngine.EventSystems.IScrollHandler
        {
            public ScrollRect target;
            public void OnScroll(UnityEngine.EventSystems.PointerEventData e)
            {
                if (target == null) return;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    e.scrollDelta = new Vector2(e.scrollDelta.y, 0f);
                target.OnScroll(e);
            }
        }
    }
}
