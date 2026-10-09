using UnityEngine;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어 인벤토리 HUD의 슬롯 1칸. 에디터에서 미리 그려둔 오브젝트이며,
    /// 런타임에는 생성/삭제 없이 아이콘·수량·선택 표시만 갱신한다(캔버스 리빌드 최소화).
    /// </summary>
    public class InventorySlotView : MonoBehaviour
    {
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
