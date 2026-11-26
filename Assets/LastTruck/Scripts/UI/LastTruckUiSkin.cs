using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 게임 UI가 함께 쓰는 스킨(Kenney "UI Pack - Adventure" 스프라이트 + 글꼴 묶음).
    /// uGUI(트럭 팝업, 인벤토리)와 UI Toolkit(HUD)이 둘 다 이 한 곳에서 스프라이트를 가져다 쓴다.
    /// 에셋은 Resources/UI/LastTruckUiSkin.asset 이고, 에디터 메뉴 LastTruck/UI/UI 스킨 만들기 로 (다시) 만든다.
    /// 어떤 스프라이트가 없어도 (null) 각 UI는 원래 모양으로 돌아가도록 짜여 있다.
    /// </summary>
    public class LastTruckUiSkin : ScriptableObject
    {
        public const string ResourcePath = "UI/LastTruckUiSkin";

        [Header("패널 (9-slice)")]
        public Sprite panelBrown;
        public Sprite panelBrownDark;
        public Sprite panelGrey;
        public Sprite panelGreyDark;
        public Sprite panelGreyBolts;
        public Sprite panelGreyBoltsDark;

        [Header("버튼 (9-slice)")]
        public Sprite buttonBrown;
        public Sprite buttonGrey;
        public Sprite buttonRed;
        public Sprite buttonBrownClose;
        public Sprite buttonRedClose;

        [Header("그 밖")]
        public Sprite hexBrown;
        public Sprite hexBrownDark;
        public Sprite roundBrown;
        public Sprite roundBrownDark;
        public Sprite roundGreyDark;
        public Sprite bannerModern;
        public Sprite minimapRingBrown;

        [Header("투명 HUD 그늘 (원신풍: 판 없이 글자 뒤에 깔리는 부드러운 그늘)")]
        [Tooltip("왼쪽이 진하고 오른쪽으로 갈수록 사라지는 그늘 (왼쪽 임무 추적기).")]
        public Sprite hudFade;
        [Tooltip("가운데가 진하고 양 끝으로 사라지는 띠 (무전 자막, 달성 알림, 토스트).")]
        public Sprite hudBand;

        [Header("글꼴 (한글 지원)")]
        public Font font;

        [Header("테마")]
        [Tooltip("원신풍 폴리곤 스킨(남색 판 + 금테 + 크림 버튼)이 적용되어 있는가. 메뉴 LastTruck/UI/원신풍 폴리곤 스킨 적용 이 켠다.\n" +
                 "켜져 있으면 예전 나무 판용 갈색 색조(곱하기 색)를 밝기만 남기고 바꿔서, 남색 판이 갈색으로 탁해지지 않게 한다.")]
        public bool polyTheme;

        /// <summary>
        /// 화면 코드가 주는 색조(나무 판 기준 갈색 배율)를 지금 테마에 맞게 바꾼다.
        ///  - 판/육각/원: 색은 버리고 밝기만 (선택된 칸처럼 밝은 금빛 색조 = 가장 밝게)
        ///  - 버튼: 의도 색(초록/빨강 등)은 살리되 크림색이 탁해지지 않게 흰색 쪽으로 당긴다
        /// </summary>
        public Color ThemeTint(Sprite sprite, Color tint)
        {
            if (!polyTheme || sprite == null) return tint;
            bool button = sprite == buttonBrown || sprite == buttonGrey || sprite == buttonRed || sprite == buttonBrownClose || sprite == buttonRedClose;
            if (button)
                return new Color(Mathf.Lerp(1f, tint.r, 0.35f), Mathf.Lerp(1f, tint.g, 0.35f), Mathf.Lerp(1f, tint.b, 0.35f), tint.a);

            bool warmBright = tint.r > 0.9f && tint.g > 0.7f;
            float lum = tint.r * 0.3f + tint.g * 0.59f + tint.b * 0.11f;
            float k = warmBright ? 1f : Mathf.Lerp(0.64f, 0.9f, Mathf.InverseLerp(0.3f, 0.65f, lum));
            return new Color(k, k, k, tint.a);
        }

        private static LastTruckUiSkin _instance;
        private static bool _searched;

        /// <summary>스킨 에셋 (없으면 null → 호출하는 쪽이 원래 모양을 쓴다).</summary>
        public static LastTruckUiSkin Get()
        {
            if (_instance == null && !_searched)
            {
                _searched = true;
                _instance = Resources.Load<LastTruckUiSkin>(ResourcePath);
            }
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _searched = false;
        }
    }
}
