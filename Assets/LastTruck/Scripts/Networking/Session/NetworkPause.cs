using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastTruck.Networking
{
    /// <summary>
    /// 게임 전체 일시정지 (방장이 바뀌는 동안 / 방장 변경 뒤 다른 사람이 다시 연결될 때까지).
    ///
    ///  - Time.timeScale = 0: 낮/밤 시간, 물리(트럭), 애니메이션, 각자 컴퓨터의 타이머(채집/먹기 등)가 모두 멈춘다.
    ///    Fusion은 실제 시간(Unscaled)으로 돌아가서 재접속은 계속 진행된다.
    ///  - 소리 정지, UI 클릭 막기(EventSystem 끄기), 화면에 안내 문구.
    ///  - 이동/조작 입력은 NetworkGameState.AllowPlayerControl이, 몬스터는 NetworkGameState.IsHeld가 막는다.
    ///  - 여러 이유(reason)가 겹칠 수 있다. 모든 이유가 끝나야 풀린다.
    /// </summary>
    public static class NetworkPause
    {
        #region 상태

        public const string MigrationReason = "migration";
        public const string HoldReason = "hold";

        private static readonly HashSet<string> Reasons = new HashSet<string>();
        private static float _savedTimeScale = 1f;
        private static EventSystem _disabledEventSystem;

        public static bool IsPaused => Reasons.Count > 0;

        #endregion

        #region 시작 / 끝

        public static void Begin(string reason, string message)
        {
            if (string.IsNullOrEmpty(reason)) reason = MigrationReason;
            bool wasPaused = IsPaused;
            Reasons.Add(reason);
            if (!wasPaused)
            {
                _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                AudioListener.pause = true;

                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null && eventSystem.enabled)
                {
                    eventSystem.enabled = false;
                    _disabledEventSystem = eventSystem;
                }
            }
            SystemUI.ShowLoading(message);
        }

        public static void End(string reason)
        {
            if (!Reasons.Remove(reason ?? MigrationReason)) return;
            if (!IsPaused) Resume();
        }

        /// <summary>모든 일시정지를 푼다 (방 목록으로 돌아갈 때).</summary>
        public static void EndAll()
        {
            if (!IsPaused) return;
            Reasons.Clear();
            Resume();
        }

        private static void Resume()
        {
            Time.timeScale = _savedTimeScale;
            AudioListener.pause = false;
            if (_disabledEventSystem != null) _disabledEventSystem.enabled = true;
            _disabledEventSystem = null;
            SystemUI.HideLoading();
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Reasons.Clear();
            _savedTimeScale = 1f;
            _disabledEventSystem = null;
        }
    }
}
