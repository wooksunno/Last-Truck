using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 날씨 키트 (개발/테스트용). F5를 누를 때마다 자동 → 맑음 → 안개 → 비 → 폭풍우 → 눈 → 자동 순서로 바뀐다.
    /// 바뀐 날씨는 알림(HudToast)으로 보여 준다. 눈은 눈 연출이 있는 씬(WeatherDirector.SnowAvailable)에서만 순서에 들어간다.
    /// 개발 빌드(에디터 포함)에서만 동작한다. 정식 빌드에서는 Debug.isDebugBuild가 꺼져 있어 키가 먹지 않는다.
    /// 고른 날씨는 내 화면에만 적용된다 (멀티플레이에서 다른 사람에게 보내지 않는다).
    /// </summary>
    public class WeatherKitHud
    {
        private const KeyCode CycleKey = KeyCode.F5;
        private static readonly WeatherKind?[] Order = { null, WeatherKind.Clear, WeatherKind.Fog, WeatherKind.Rain, WeatherKind.Storm, WeatherKind.Snow };

        public void Tick()
        {
            if (!Debug.isDebugBuild || !Input.GetKeyDown(CycleKey)) return;

            int i = System.Array.IndexOf(Order, WeatherDirector.Override);
            WeatherKind? next = Order[(i + 1) % Order.Length];
            if (next == WeatherKind.Snow && !WeatherDirector.SnowAvailable) next = null;   // 눈이 없는 씬이면 건너뛰고 자동으로
            WeatherDirector.SetOverride(next);   // 알림은 SetOverride가 띄운다
        }
    }
}
