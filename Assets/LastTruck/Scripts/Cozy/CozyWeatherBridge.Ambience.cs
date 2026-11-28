using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// COZY 환경 연출(나비, 반딧불이, 오로라 등)을 COZY 무작위 대신 우리 시간·날씨에 맞춰 고른다.
    ///  - 맑은 낮: clearDayAmbience 중 하나, 맑은 밤: clearNightAmbience 중 하나 (회차·낮밤으로 정해서 모든 화면이 같다)
    ///  - 안개 밤: fogNightAmbience (도깨비불), 그 외(안개 낮, 비, 폭풍우, 눈): quietAmbience (연출 없음 — 날씨 효과만)
    /// 밤 판정은 연결 스크립트의 밤 정도(0.5 이상)를 쓴다. 노을·동틀녘 중간에 바뀐다.
    /// </summary>
    public partial class CozyWeatherBridge
    {
        [Header("환경 연출 (시간·날씨별)")]
        [SerializeField] private AmbienceProfile[] clearDayAmbience;
        [SerializeField] private AmbienceProfile[] clearNightAmbience;
        [SerializeField] private AmbienceProfile fogNightAmbience;
        [SerializeField] private AmbienceProfile quietAmbience;
        [SerializeField] private float ambienceTransitionSeconds = 5f;

        private CozyAmbienceModule _ambienceModule;
        private bool _ambienceApplied;

        /// <summary>지금 규칙으로 고른 환경 연출 (테스트·표시용).</summary>
        public AmbienceProfile WantedAmbience { get; private set; }

        private void UpdateAmbience(int cycle, WeatherKind kind)
        {
            if (_ambienceModule == null && Cozy != null) _ambienceModule = Cozy.GetComponentInChildren<CozyAmbienceModule>(true);
            if (_ambienceModule == null || quietAmbience == null) return;

            bool night = _clock.NightFactor >= 0.5f;
            AmbienceProfile want = quietAmbience;
            if (kind == WeatherKind.Clear) want = Pick(night ? clearNightAmbience : clearDayAmbience, cycle, night) ?? quietAmbience;
            else if (kind == WeatherKind.Fog && night && fogNightAmbience != null) want = fogNightAmbience;
            WantedAmbience = want;

            if (_ambienceModule.currentAmbienceProfile != want)   // COZY가 제 타이머로 바꿔도 다시 우리 것으로
                _ambienceModule.SetAmbience(want, _ambienceApplied ? ambienceTransitionSeconds : 0f);
            _ambienceApplied = true;
        }

        private static AmbienceProfile Pick(AmbienceProfile[] list, int cycle, bool night)
        {
            if (list == null || list.Length == 0) return null;
            uint h = (uint)Mathf.Max(cycle, 0) * 2654435761u ^ (night ? 0x9E3779B1u : 0x85EBCA6Bu);
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
            return list[(int)(h % (uint)list.Length)];
        }
    }
}
