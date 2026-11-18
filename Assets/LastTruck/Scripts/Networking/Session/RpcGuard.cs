using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 호스트가 받는 요청 RPC의 기본 검증.
    ///  - 횟수 제한: 같은 사람이 같은 요청을 1초에 너무 많이 보내면 넘친 만큼 버린다(도배/매크로 방지).
    ///  - 거리 확인: 보낸 사람 캐릭터(트럭에 탔으면 트럭)와 대상 위치가 너무 멀면 버린다.
    /// 호스트 자신이 보낸 요청은 검사하지 않는다.
    /// </summary>
    public static class RpcGuard
    {
        #region 횟수 제한

        private struct Window
        {
            public float Start;
            public int Count;
        }

        private static readonly Dictionary<(int, string), Window> Windows = new Dictionary<(int, string), Window>();
        private static NetworkRunner _runner;
        private static float _lastWarnTime = -10f;

        /// <summary>
        /// 이 요청을 처리해도 되는가. maxPerSecond를 넘으면 false.
        /// </summary>
        public static bool Allow(NetworkRunner runner, PlayerRef source, string key, int maxPerSecond = 15)
        {
            if (runner == null || IsLocal(runner, source)) return true;
            if (_runner != runner)
            {
                _runner = runner;
                Windows.Clear();
            }

            float now = Time.realtimeSinceStartup;
            (int, string) id = (source.RawEncoded, key);
            Windows.TryGetValue(id, out Window window);
            if (now - window.Start >= 1f)
            {
                window.Start = now;
                window.Count = 0;
            }
            window.Count++;
            Windows[id] = window;

            if (window.Count <= maxPerSecond) return true;
            Warn($"요청 과다: {source} {key} ({window.Count}/초)");
            return false;
        }

        #endregion

        #region 거리 확인

        /// <summary>보낸 사람이 target에서 maxDistance 안에 있는가. 캐릭터를 못 찾으면 통과시킨다.</summary>
        public static bool Near(NetworkRunner runner, PlayerRef source, Vector3 target, float maxDistance)
        {
            if (runner == null || IsLocal(runner, source)) return true;
            NetworkPlayer character = NetworkPlayer.Find(source);
            if (character == null) return true;

            Vector3 from = character.transform.position;
            if (character.IsSeated && NetworkTruck.TruckTransform != null) from = NetworkTruck.TruckTransform.position;
            if ((from - target).sqrMagnitude <= maxDistance * maxDistance) return true;

            Warn($"거리 초과: {source} {Vector3.Distance(from, target):0.#}m");
            return false;
        }

        #endregion

        #region 내부

        private static bool IsLocal(NetworkRunner runner, PlayerRef source)
        {
            return source == PlayerRef.None || source == runner.LocalPlayer;
        }

        private static void Warn(string message)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastWarnTime < 2f) return;
            _lastWarnTime = now;
            Debug.LogWarning("[RpcGuard] " + message);
            GameLauncher.LogSession("검증: " + message);
        }

        #endregion
    }
}
