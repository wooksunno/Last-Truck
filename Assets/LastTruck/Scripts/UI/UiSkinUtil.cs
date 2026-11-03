using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    /// <summary>
    /// 기존 uGUI 화면(트럭 팝업, 인벤토리 핫바 등)에 UI 스킨(Kenney 스프라이트)을 입히는 도우미.
    /// 스킨 에셋이 없으면 아무것도 하지 않아서 화면은 예전 모양 그대로다 (모든 함수가 "입혔는가"를 돌려준다).
    /// Image.color는 스프라이트 색에 곱해지므로, 여기서 쓰는 색은 "밝기 배율"에 가깝다.
    /// </summary>
    public static class UiSkinUtil
    {
        // 슬롯 배경 배율 (Kenney 갈색 패널 × 이 색)
        public static readonly Color SlotNormal = new Color(0.42f, 0.35f, 0.31f, 0.95f);
        public static readonly Color SlotFilled = new Color(0.66f, 0.56f, 0.48f, 1f);
        public static readonly Color SlotSelected = new Color(1.00f, 0.86f, 0.52f, 1f);

        public static LastTruckUiSkin Skin => LastTruckUiSkin.Get();

        public static bool SetSliced(Image img, Sprite sprite, Color tint, float ppuMultiplier = 1.6f)
        {
            if (img == null || sprite == null) return false;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = tint;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
            return true;
        }

        /// <summary>팝업 창 틀: 어두운 나무 판. 예전의 금색 테두리 링은 숨긴다.</summary>
        public static bool SkinWindow(GameObject root)
        {
            LastTruckUiSkin s = Skin;
            if (s == null || root == null) return false;
            var img = root.GetComponent<Image>();
            if (!SetSliced(img, s.panelBrownDark, new Color(0.60f, 0.50f, 0.44f, 1f), 1.7f)) return false;

            Transform border = root.transform.Find("Border");
            if (border != null) border.gameObject.SetActive(false);
            return true;
        }

        /// <summary>핫바 판: 어두운 나무 판. 링 테두리는 숨긴다.</summary>
        public static bool SkinHotbarPanel(GameObject root)
        {
            LastTruckUiSkin s = Skin;
            if (s == null || root == null) return false;
            var img = root.GetComponent<Image>();
            if (!SetSliced(img, s.panelBrownDark, new Color(0.58f, 0.48f, 0.42f, 1f), 1.7f)) return false;

            Transform border = root.transform.Find("Border");
            if (border != null) border.gameObject.SetActive(false);
            return true;
        }

        /// <summary>슬롯 배경 스프라이트를 입힌다 (색은 슬롯 스크립트가 상태에 따라 SlotNormal/Filled/Selected로 정한다).</summary>
        public static bool SkinSlotBackground(Image img)
        {
            LastTruckUiSkin s = Skin;
            if (s == null) return false;
            if (img == null || s.panelBrownDark == null) return false;
            img.sprite = s.panelBrownDark;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.4f;
            return true;
        }

        /// <summary>닫기(X) 버튼: 빨간 단추 스프라이트(X가 그려져 있다). 글자 X는 숨긴다.</summary>
        public static bool SkinCloseButton(Button button)
        {
            LastTruckUiSkin s = Skin;
            if (s == null || button == null || s.buttonRedClose == null) return false;
            var img = button.GetComponent<Image>();
            if (img == null) return false;

            // X가 그려진 2:1 단추 그림이라 늘리면 찌그러진다: 늘리지 않고 비율을 지킨다
            img.sprite = s.buttonRedClose;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(78f, 39f);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.gameObject.SetActive(false);
            return true;
        }

        /// <summary>일반 버튼: 의도한 색(빨강/초록/그 밖) 따라 나무/강철/빨간 단추를 고른다.</summary>
        public static bool SkinButton(Image img, Color intent, bool interactable = true)
        {
            LastTruckUiSkin s = Skin;
            if (s == null || img == null) return false;

            Sprite sprite;
            Color tint;
            if (!interactable)
            {
                sprite = s.buttonGrey;
                tint = new Color(0.62f, 0.62f, 0.66f, 1f);
            }
            else if (intent.r > intent.g * 1.9f && intent.r > intent.b * 1.9f)   // 주황(0.91,0.57,0.23)은 빨강이 아니라 나무 단추
            {
                sprite = s.buttonRed;
                tint = new Color(0.95f, 0.85f, 0.85f, 1f);
            }
            else if (intent.g > intent.r * 1.12f && intent.g > intent.b * 1.12f)
            {
                sprite = s.buttonBrown;   // 크림색 단추 × 초록
                tint = new Color(0.50f, 0.85f, 0.58f, 1f);
            }
            else
            {
                sprite = s.buttonBrown;
                tint = new Color(0.86f, 0.70f, 0.52f, 1f);
            }
            return SetSliced(img, sprite, tint, 2.2f);
        }

        /// <summary>분류/정렬 칩: 선택됐으면 나무(accent 색을 살짝 섞음), 아니면 어두운 강철.</summary>
        public static bool SkinChip(Image img, bool selected, Color accent)
        {
            LastTruckUiSkin s = Skin;
            if (s == null || img == null) return false;
            if (selected)
                return SetSliced(img, s.buttonBrown, Color.Lerp(new Color(1f, 0.92f, 0.78f, 1f), accent, 0.45f), 2.2f);
            return SetSliced(img, s.buttonGrey, new Color(0.46f, 0.47f, 0.52f, 1f), 2.2f);
        }
    }
}
