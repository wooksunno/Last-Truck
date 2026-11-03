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

        [Header("글꼴 (한글 지원)")]
        public Font font;

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
