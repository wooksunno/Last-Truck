using UnityEngine;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어 인벤토리 HUD의 슬롯 1칸. 에디터에서 미리 그려둔 오브젝트이며,
    /// 런타임에는 생성/삭제 없이 아이콘·수량·선택 표시만 갱신한다(캔버스 리빌드 최소화).
    /// </summary>
    public class InventorySlotView : MonoBehaviour,
        UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IEndDragHandler, UnityEngine.EventSystems.IDropHandler
    {
        // ---- 드래그: 다른 칸에 놓으면 교환/이동/합치기, 인벤토리 밖(빈 화면)에 놓으면 버리기 ----
        /// <summary>(드래그한 칸, 놓은 칸). 인벤토리가 연결해 준다.</summary>
        public System.Action<int, int> OnSwapRequested;
        /// <summary>인벤토리 밖에 놓았을 때 (드래그한 칸).</summary>
        public System.Action<int> OnDropOutRequested;

        private GameObject _ghost;
        private bool _dropHandled;

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e)
        {
            _dropHandled = false;
            if (_item == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Canvas root = canvas.rootCanvas;

            _ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            _ghost.transform.SetParent(root.transform, false);
            _ghost.transform.SetAsLastSibling();
            var rt = (RectTransform)_ghost.transform;
            rt.sizeDelta = new Vector2(64f, 64f);
            var img = _ghost.GetComponent<Image>();
            img.sprite = _item.icon;
            img.color = _item.icon != null ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 0.85f, 0.4f, 0.85f);
            img.raycastTarget = false;
            img.preserveAspect = true;
            rt.position = e.position;
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_ghost != null)
                _ghost.transform.position = e.position;
        }

        public void OnDrop(UnityEngine.EventSystems.PointerEventData e)
        {
            // 다른 칸 위에 놓였다: 놓인 칸이 "받는 쪽"으로 불린다
            InventorySlotView from = e.pointerDrag != null ? e.pointerDrag.GetComponent<InventorySlotView>() : null;
            if (from == null || from == this) return;

            from._dropHandled = true;
            OnSwapRequested?.Invoke(from._index, _index);
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }

            if (_dropHandled || _item == null) return;

            // 어떤 UI 위에도 놓이지 않았으면(빈 화면) 버린다
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!overUi)
                OnDropOutRequested?.Invoke(_index);
        }

        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image glow;
        [SerializeField] private Image icon;
        [SerializeField] private Text letter;       // 아이콘이 없는 아이템의 대체 글자
        [SerializeField] private Text count;
        [SerializeField] private Text key;
        [SerializeField] private Button button;

        [Header("색")]
        [SerializeField] private Color borderNormal = new Color(1f, 1f, 1f, 0.14f);
        [SerializeField] private Color borderSelected = new Color(1f, 0.80f, 0.30f, 1f);
        [SerializeField] private Color bgNormal = new Color(0.08f, 0.065f, 0.055f, 0.92f);
        [SerializeField] private Color bgFilled = new Color(0.14f, 0.11f, 0.09f, 0.95f);
        [SerializeField] private Color bgSelected = new Color(0.27f, 0.20f, 0.10f, 0.97f);
        [SerializeField] private float selectedScale = 1.07f;

        private int _index;
        private ItemData _item;
        private int _count = -1;
        private bool _selected;
        private bool _inited;

        public Button Button => button;
        public ItemData Item => _item;

        public void Setup(int index, UnityEngine.Events.UnityAction<int> onClick)
        {
            _index = index;
            if (key != null) key.text = (index + 1).ToString();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick(_index));
            }
            _inited = false;
        }

        /// <summary>바뀐 값만 반영한다.</summary>
        public void Set(ItemData item, int amount, bool selected)
        {
            bool itemChanged = !_inited || item != _item;
            if (itemChanged)
            {
                _item = item;
                bool has = item != null;
                if (icon != null)
                {
                    bool showIcon = has && item.icon != null;
                    icon.sprite = showIcon ? item.icon : null;
                    icon.enabled = showIcon;
                }
                if (letter != null)
                {
                    bool showLetter = has && item.icon == null;
                    letter.enabled = showLetter;
                    if (showLetter) letter.text = FirstLetters(item.itemName);
                }
            }

            if (!_inited || amount != _count || itemChanged)
            {
                _count = amount;
                if (count != null)
                {
                    bool show = item != null && amount > 1;
                    count.enabled = show;
                    if (show) count.text = amount.ToString();
                }
            }

            if (!_inited || selected != _selected || itemChanged)
            {
                _selected = selected;
                if (border != null) border.color = selected ? borderSelected : borderNormal;
                if (glow != null) glow.enabled = selected;
                if (background != null) background.color = selected ? bgSelected : (item != null ? bgFilled : bgNormal);
                transform.localScale = selected ? Vector3.one * selectedScale : Vector3.one;
            }

            _inited = true;
        }

        private static string FirstLetters(string s)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            return s.Length <= 2 ? s : s.Substring(0, 2);
        }
    }
}
