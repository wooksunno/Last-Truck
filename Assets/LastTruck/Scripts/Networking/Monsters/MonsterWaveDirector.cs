using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 밤(웨이브) 구성에 따라 다음에 스폰할 몬스터 종류를 고른다 (호스트).
    ///  - 이번 밤의 MonsterWaveData.weights 비율로 무작위 (빅 보이 칸은 무시).
    ///  - bossCount가 있으면 그 밤의 첫 스폰들을 빅 보이로 (모두에게 경고 문구).
    /// 웨이브 안내(제목/설명 배너 + 채팅)는 WaveAnnouncer가 밤이 시작될 때 각 컴퓨터에서 띄운다.
    /// </summary>
    public static class MonsterWaveDirector
    {
        #region 상태

        private static int _night = -1;
        private static int _bossesSpawned;

        /// <summary>지금 몇 번째 밤인가 (게임 관리자가 없으면 1).</summary>
        public static int CurrentNight => GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentCycle) : 1;

        #endregion

        #region 고르기

        public static MonsterKind HostPickKind()
        {
            int night = CurrentNight;
            if (night != _night)
            {
                _night = night;
                _bossesSpawned = 0;
            }

            MonsterCatalog catalog = MonsterCatalog.Instance;
            MonsterWaveData wave = catalog.GetWave(night);

            if (_bossesSpawned < wave.bossCount)
            {
                _bossesSpawned++;
                NetworkGameState.HostAnnounce($"{catalog.Get(MonsterKind.BigBoy).displayName}가 나타났습니다!", "거대한 좀비가 다가옵니다. 반원 공격 범위를 피하세요.");
                return MonsterKind.BigBoy;
            }

            float total = 0f;
            float[] weights = wave.weights ?? new float[0];
            for (int i = 0; i < weights.Length; i++)
            {
                if (i == (int)MonsterKind.BigBoy) continue;
                total += Mathf.Max(0f, weights[i]);
            }
            if (total <= 0f) return MonsterKind.Walker;

            float roll = Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (i == (int)MonsterKind.BigBoy) continue;
                roll -= Mathf.Max(0f, weights[i]);
                if (roll <= 0f) return (MonsterKind)i;
            }
            return MonsterKind.Walker;
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _night = -1;
            _bossesSpawned = 0;
        }
    }
}
