using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 우리 게임의 낮/밤 진행(GameManager)을 COZY 시각(하루 비율 0~1)으로 바꾸는 계산기. CozyWeatherBridge가 매 프레임 부른다.
    ///
    ///  - "밤 정도"(NightFactor, 0 낮 ~ 1 밤)는 AtmosphereController와 같은 규칙: 해질녘 진행률을 바로 따라 올라가고,
    ///    밤이 끝나면 dawnSeconds에 걸쳐 내려온다(동틀녘).
    ///  - 시각: 낮 08:00 → 16:30, 해질녘 → 20:00, 밤 20:00 → 02:00(멈춤), 동틀녘 05:00 → 08:00.
    ///    밤 길이는 생존 조건에 따라 달라서, 밤은 시작부터 지난 시간으로 천천히(secondsPerNightHour초에 1시간) 흐르다 02:00에서 멈춘다.
    ///    02:00 → 05:00은 건너뛴다(동틀녘 몇 초 안에 새벽이 오게).
    /// </summary>
    public class CozyDayClock
    {
        private const float DayStartHour = 8f;
        private const float DuskStartHour = 16.5f;
        private const float NightStartHour = 20f;
        private const float NightMaxHours = 6f;      // 20:00 + 6시간 = 02:00
        private const float DawnStartHour = 5f;

        private readonly float _dawnSeconds;
        private readonly float _secondsPerNightHour;
        private float _nightElapsed;

        public float NightFactor { get; private set; }
        public float Hours { get; private set; }

        public CozyDayClock(float dawnSeconds = 10f, float secondsPerNightHour = 30f)
        {
            _dawnSeconds = dawnSeconds;
            _secondsPerNightHour = secondsPerNightHour;
            Hours = DayStartHour;
        }

        /// <summary>한 프레임 진행. COZY용 하루 비율(0~1)을 돌려준다.</summary>
        public float Step(bool hasGame, bool isNight, float duskProgress, float dayElapsed01, float dt)
        {
            if (!hasGame)
            {
                NightFactor = 0f;
                _nightElapsed = 0f;
                Hours = DayStartHour;
                return Hours / 24f;
            }

            float target = isNight ? 1f : Mathf.Clamp01(duskProgress);
            bool dawn = target < NightFactor;
            NightFactor = dawn
                ? Mathf.MoveTowards(NightFactor, target, _dawnSeconds > 0f ? dt / _dawnSeconds : 1f)
                : target;

            if (isNight)
            {
                _nightElapsed += dt;
                float nightHours = _secondsPerNightHour > 0f ? _nightElapsed / _secondsPerNightHour : NightMaxHours;
                Hours = NightStartHour + Mathf.Min(nightHours, NightMaxHours);
            }
            else
            {
                _nightElapsed = 0f;
                float day = Mathf.Lerp(DayStartHour, DuskStartHour, Mathf.Clamp01(dayElapsed01));
                Hours = Mathf.Lerp(day, dawn ? DawnStartHour : NightStartHour, NightFactor);
            }

            return (Hours % 24f) / 24f;
        }
    }
}
