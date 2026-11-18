using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// F3: 네트워크 상태 창 (모두 사용 가능).
    /// 역할(방장/참가자), 핑, 틱, 인원(나간 사람 포함), 몬스터 수, AOI, 방장 변경 상태, 최근 접속 기록을 보여준다.
    /// </summary>
    public class NetworkStatsHud : MonoBehaviour
    {
        #region 상태

        private bool _visible;
        private float _nextRefresh;
        private readonly StringBuilder _text = new StringBuilder(512);
        private GUIStyle _style;

        private const float RefreshInterval = 0.5f;

        #endregion

        #region 유니티

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) _visible = !_visible;
            if (!_visible || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Rebuild();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13,
                    richText = true,
                    wordWrap = false,
                    padding = new RectOffset(10, 10, 8, 8),
                };
            }
            GUIContent content = new GUIContent(_text.ToString());
            Vector2 size = _style.CalcSize(content);
            GUI.Box(new Rect(10f, 10f, Mathf.Max(260f, size.x), size.y), content, _style);
        }

        #endregion

        #region 내용

        private void Rebuild()
        {
            _text.Clear();
            GameLauncher launcher = GameLauncher.Instance;
            NetworkRunner runner = launcher != null ? launcher.Runner : null;

            _text.AppendLine("<b>네트워크 (F3)</b>");
            _text.AppendLine($"버전: {SessionTicket.GameVersion}");

            if (runner == null || !runner.IsRunning)
            {
                _text.AppendLine(launcher != null && launcher.IsMigrating ? "<color=yellow>방장 변경 중...</color>" : "연결 안 됨");
                AppendLog();
                return;
            }

            string role = runner.IsServer ? "방장" : "참가자";
            _text.AppendLine($"방: {runner.SessionInfo?.Name}   역할: {role}");

            if (!runner.IsServer)
            {
                double rtt = runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0;
                _text.AppendLine($"핑: {rtt:0} ms");
            }
            float tickRate = runner.DeltaTime > 0f ? 1f / runner.DeltaTime : 0f;
            _text.AppendLine($"틱: {tickRate:0} Hz   현재 틱: {runner.Tick.Raw}");

            int present = NetworkPlayer.All.Count;
            int total = NetworkPlayer.AllIncludingDeparted.Count;
            int connected = 0;
            foreach (PlayerRef _ in runner.ActivePlayers) connected++;
            _text.AppendLine($"접속: {connected}명   캐릭터: {present}/{total} (나머지는 재접속 대기)");

            int monsters = FindObjectsByType<NetworkMonster>(FindObjectsSortMode.None).Length;
            _text.AppendLine($"몬스터(이 컴퓨터에 보이는 수): {monsters}");
            _text.AppendLine($"AOI: {(NetworkInterest.Enabled(runner) ? "켜짐" : "꺼짐")}");

            if (NetworkPause.IsPaused) _text.AppendLine("<color=yellow>일시정지 (방장 변경)</color>");
            if (launcher.IsMigrating) _text.AppendLine("<color=yellow>방장 변경 진행 중</color>");

            AppendLog();
        }

        private void AppendLog()
        {
            IReadOnlyList<string> log = GameLauncher.RecentLog;
            if (log.Count == 0) return;
            _text.AppendLine();
            _text.AppendLine("<b>최근 기록</b>");
            for (int i = log.Count - 1; i >= 0 && i >= log.Count - 8; i--) _text.AppendLine(log[i]);
        }

        #endregion
    }
}
