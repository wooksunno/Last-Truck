using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>UI Toolkit 화면들이 함께 쓰는 스킨 도우미 (Kenney 스프라이트를 9-slice 배경으로 입히기).</summary>
    public static class UiToolkitSkin
    {
        /// <summary>스프라이트를 9-slice 배경으로 입힌다. 모서리 두께는 scale배로 줄여서 보여 준다.</summary>
        public static void Slice(VisualElement e, Sprite sprite, float scale = 0.5f, Color? tint = null)
        {
            if (e == null || sprite == null) return;
            e.style.backgroundImage = new StyleBackground(sprite);
            if (tint.HasValue) e.style.unityBackgroundImageTintColor = tint.Value;
            e.style.unitySliceLeft = Mathf.RoundToInt(sprite.border.x);
            e.style.unitySliceBottom = Mathf.RoundToInt(sprite.border.y);
            e.style.unitySliceRight = Mathf.RoundToInt(sprite.border.z);
            e.style.unitySliceTop = Mathf.RoundToInt(sprite.border.w);
            e.style.unitySliceScale = scale;
        }
    }
}
