using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 새 구역(북쪽/동쪽 확장 땅)에 들어가면 화면에 구역 이름을 띄운다 (오픈월드 탐험 느낌).
    /// 구역 범위는 OpenWorldExpansionBuilder가 땅을 깐 범위와 같다. 같은 구역은 나갔다가 들어와도 40초 안에는 다시 띄우지 않는다.
    /// 씬에 따로 둘 것 없이 게임이 시작되면 자동으로 만들어진다. 내 캐릭터 기준(멀티플레이도 각자 화면).
    /// </summary>
    public class ZoneBanner : MonoBehaviour
    {
        private struct Zone
        {
            public string name, hint;
            public float x0, x1, z0, z1;
            public bool Contains(Vector3 p) => p.x >= x0 && p.x <= x1 && p.z >= z0 && p.z <= z1;
        }

        // 순서가 우선순위 (겹치면 앞의 것)
        private static readonly Zone[] Zones =
        {
            new Zone { name = "안개 늪", hint = "독약초와 기름이 나는 축축한 늪. 호수 건너로 폐허 도시가 보인다", x0 = 375f, x1 = 650f, z0 = -360f, z1 = 95f },
            new Zone { name = "바위 고원", hint = "구리·철·금 광맥이 드러난 바위 땅", x0 = 165f, x1 = 650f, z0 = 95f, z1 = 310f },
            new Zone { name = "푸른 초원", hint = "나무와 약초가 많은 들판", x0 = -125f, x1 = 165f, z0 = 115f, z1 = 310f },
        };

        private static ZoneBanner _instance;
        private int _current = -1;
        private readonly float[] _lastShown = new float[Zones.Length];
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("ZoneBanner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ZoneBanner>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private void Awake()
        {
            for (int i = 0; i < _lastShown.Length; i++) _lastShown[i] = -999f;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;

            GameObject player = CraftingSystem.GatherRules.Player;
            if (player == null) { _current = -1; return; }

            int zone = -1;
            Vector3 p = player.transform.position;
            for (int i = 0; i < Zones.Length; i++)
                if (Zones[i].Contains(p)) { zone = i; break; }

            if (zone == _current) return;
            _current = zone;
            if (zone < 0) return;
            if (Time.unscaledTime - _lastShown[zone] < 40f) return;
            _lastShown[zone] = Time.unscaledTime;
            HudToast.Show("[ " + Zones[zone].name + " ]   " + Zones[zone].hint, 4f);
        }
    }
}
