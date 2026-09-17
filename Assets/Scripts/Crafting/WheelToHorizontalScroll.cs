using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 마우스 휠(수직 입력)을 가로 스크롤(ScrollRect)로 변환한다.
    /// </summary>
    public class WheelToHorizontalScroll : MonoBehaviour, IScrollHandler
    {
        public ScrollRect scrollRect;
        [SerializeField] private float sensitivity = 0.15f;

        public void OnScroll(PointerEventData eventData)
        {
            if (scrollRect == null)
                return;

            float delta = Mathf.Abs(eventData.scrollDelta.y) > Mathf.Abs(eventData.scrollDelta.x)
                ? eventData.scrollDelta.y
                : eventData.scrollDelta.x;

            scrollRect.horizontalNormalizedPosition =
                Mathf.Clamp01(scrollRect.horizontalNormalizedPosition - delta * sensitivity);
        }
    }
}
