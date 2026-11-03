using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    /// <summary>
    /// 글자 대신 쓰는 UI 그림(Resources/UI/Icons/&lt;이름&gt;.png)을 불러온다.
    /// 이름: heart bolt truck sun moon crate hammer gear book upgrade shield speed bulb sword crosshair
    ///       turret rate burst engine bumper plate lock plus check close grid sortname sortcount star
    /// 그림이 없으면 null을 돌려주므로, 호출하는 쪽은 글자로 되돌아가면 된다.
    /// </summary>
    public static class UiIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out Sprite s) && s != null) return s;
            s = Resources.Load<Sprite>("UI/Icons/" + name);
            Cache[name] = s;
            return s;
        }

        /// <summary>uGUI: parent 아래에 아이콘 이미지 오브젝트를 만들어 돌려준다 (그림이 없으면 null).</summary>
        public static Image Make(Transform parent, string name, Vector2 size, Color? tint = null)
        {
            Sprite sprite = Get(name);
            if (sprite == null) return null;

            var go = new GameObject("Icon_" + name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint ?? Color.white;
            return img;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }
    }
}
