using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 밤이 시작되면 각 컴퓨터에서 웨이브 배너(제목 + 설명)와 채팅 경고를 띄운다.
    /// 밤 번호와 웨이브 표(MonsterCatalog)는 모두 같으므로 네트워크로 보내지 않는다.
    /// (GameManager.OnNightStarted는 클라이언트에서도 호스트 값을 따라 불린다)
    /// </summary>
    public class WaveAnnouncer : MonoBehaviour
    {
        private GameManager _subscribed;

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == _subscribed) return;
            Unsubscribe();
            _subscribed = gm;
            if (gm == null) return;
            gm.OnNightStarted += HandleNightStarted;
            gm.OnDayStarted += HandleDayStarted;
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_subscribed == null) return;
            _subscribed.OnNightStarted -= HandleNightStarted;
            _subscribed.OnDayStarted -= HandleDayStarted;
            _subscribed = null;
        }

        private void HandleNightStarted(int quota)
        {
            int night = MonsterWaveDirector.CurrentNight;
            MonsterWaveData wave = MonsterCatalog.Instance.GetWave(night);
            WaveBanner.Show($"{night}번째 밤 · {wave.title}", wave.description);
            ChatHud.AddLocal(string.Empty, $"{night}번째 밤이 시작됐습니다: {wave.title} (좀비 {quota}마리)", ChatKind.Warning);
        }

        private void HandleDayStarted(int cycle)
        {
            if (cycle <= 1) return;
            ChatHud.AddLocal(string.Empty, $"{cycle}일차 아침이 밝았습니다. 밤이 오기 전에 준비하세요.", ChatKind.System);
        }
    }
}
