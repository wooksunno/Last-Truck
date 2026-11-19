using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>
    /// 내 캐릭터가 쓰러져 있는 동안 화면의 모든 UI(HUD, 창, 알림)를 숨긴다. 살아나면 원래대로 돌려 놓는다.
    /// PlayerStatusHud가 매 프레임 Apply를 불러 준다. "기절했습니다" 안내(PlayerStatusHud 자신의 캔버스)만 남긴다.
    /// 월드 공간 UI(머리 위 숫자, 이름표)는 건드리지 않는다.
    /// </summary>
    public static class DownedUiHider
    {
        private static readonly List<Canvas> HiddenCanvases = new List<Canvas>();
        private static readonly List<UIDocument> HiddenDocuments = new List<UIDocument>();
        private static float _nextScan;

        public static void Apply(bool downed, Transform keepRoot)
        {
            if (!downed)
            {
                if (HiddenCanvases.Count > 0 || HiddenDocuments.Count > 0) Restore();
                return;
            }

            // 새로 생기는 UI(알림, 창)도 잡아야 해서 조금씩 자주 훑는다
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.2f;

            CraftingSystem.GameUIController gui = Object.FindFirstObjectByType<CraftingSystem.GameUIController>();
            if (gui != null && gui.IsPopupOpen) gui.ClosePopup();   // 열려 있던 트럭/제작/가공 창은 닫는다

            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == null || !c.isRootCanvas || !c.enabled || c.renderMode == RenderMode.WorldSpace) continue;
                if (keepRoot != null && c.transform.IsChildOf(keepRoot)) continue;
                c.enabled = false;
                HiddenCanvases.Add(c);
            }

            foreach (UIDocument d in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (d == null || d.rootVisualElement == null) continue;
                if (d.rootVisualElement.resolvedStyle.display == DisplayStyle.None) continue;
                d.rootVisualElement.style.display = DisplayStyle.None;
                HiddenDocuments.Add(d);
            }
        }

        private static void Restore()
        {
            foreach (Canvas c in HiddenCanvases)
                if (c != null) c.enabled = true;
            HiddenCanvases.Clear();

            foreach (UIDocument d in HiddenDocuments)
                if (d != null && d.rootVisualElement != null) d.rootVisualElement.style.display = StyleKeyword.Null;
            HiddenDocuments.Clear();
            _nextScan = 0f;
        }
    }
}
