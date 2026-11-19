using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// GameLauncher 중 새 메뉴 화면(LobbyToolkitView)이 쓰는 부분.
    ///  - 방 코드: 방을 만들 때 6자리 코드(헷갈리는 0/O/1/I 제외)를 세션 프로퍼티 "code"로 붙인다. 방 목록에서 코드로 방을 찾는다.
    ///  - 방 이름 바꾸기: 방장만. 세션 프로퍼티 "title"을 바꾸면 방 안의 모두 + 방 목록에 같이 바뀐다.
    ///  - 방 비밀번호: 방장은 만들 때 정한 것, 참가자는 들어올 때 입력한 것 (대기실에서 복사 버튼으로 공유).
    ///  - 재접속: 남은 시간 / 포기.
    ///  - 로비 조용히 접속: 타이틀 화면에서는 로딩 창 없이 뒤에서 로비에 붙어 둔다.
    /// </summary>
    public partial class GameLauncher
    {
        #region 방 코드

        private const string RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>지금 방 목록에 없는 새 코드를 만든다.</summary>
        private string GenerateRoomCode()
        {
            var used = new HashSet<string>();
            foreach (SessionInfo s in _sessions) used.Add(ReadCode(s));
            var chars = new char[LobbyRules.RoomCodeLength];
            for (int tries = 0; tries < 50; tries++)
            {
                for (int i = 0; i < chars.Length; i++) chars[i] = RoomCodeChars[UnityEngine.Random.Range(0, RoomCodeChars.Length)];
                string code = new string(chars);
                if (!used.Contains(code)) return code;
            }
            return new string(chars);
        }

        /// <summary>방 목록 항목의 방 코드 (없으면 빈 문자열 = 예전 버전 방).</summary>
        public static string ReadCode(SessionInfo session)
        {
            if (session != null && session.Properties != null
                && session.Properties.TryGetValue(LobbyRules.PropCode, out SessionProperty prop) && prop.IsString)
            {
                return (string)prop.PropertyValue;
            }
            return string.Empty;
        }

        /// <summary>입력한 코드를 대문자 + 허용 글자만 남긴 형태로 바꾼다.</summary>
        public static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            var sb = new System.Text.StringBuilder(LobbyRules.RoomCodeLength);
            foreach (char c in code.ToUpperInvariant())
            {
                if (RoomCodeChars.IndexOf(c) >= 0) sb.Append(c);   // 코드에 없는 글자(0/O/1/I, 기호)는 버린다
                if (sb.Length >= LobbyRules.RoomCodeLength) break;
            }
            return sb.ToString();
        }

        /// <summary>방 목록에서 코드가 같은 방을 찾는다.</summary>
        public SessionInfo FindSessionByCode(string code)
        {
            code = NormalizeCode(code);
            if (code.Length != LobbyRules.RoomCodeLength) return null;
            foreach (SessionInfo s in _sessions)
                if (s != null && ReadCode(s) == code) return s;
            return null;
        }

        /// <summary>지금 들어와 있는 방의 코드.</summary>
        public string RoomCode
        {
            get
            {
                if (_runner == null || !_runner.IsRunning) return string.Empty;
                string code = ReadCode(_runner.SessionInfo);
                if (!string.IsNullOrEmpty(code)) _roomCode = code;
                return _roomCode ?? string.Empty;
            }
        }

        private string _roomCode;

        #endregion

        #region 방 이름 / 비밀번호

        /// <summary>
        /// 대기실에 보이는 방 이름. 방장은 직접 바꾼 값을 바로, 참가자는 세션 프로퍼티(방장이 바꾸면 같이 바뀜)를 읽는다.
        /// </summary>
        public string RoomTitle
        {
            get
            {
                if (!IsHost && _runner != null && _runner.IsRunning && _runner.SessionInfo != null
                    && _runner.SessionInfo.Properties != null
                    && _runner.SessionInfo.Properties.TryGetValue(LobbyRules.PropTitle, out SessionProperty prop) && prop.IsString)
                {
                    string live = (string)prop.PropertyValue;
                    if (!string.IsNullOrEmpty(live) && live != _roomTitle)
                    {
                        _roomTitle = live;
                        _lastSessionTitle = live;   // 재접속 안내에도 바뀐 이름을 쓴다
                    }
                }
                return _roomTitle;
            }
        }

        /// <summary>방 이름 바꾸기 (방장, 대기실에서만). 실패하면 false와 이유.</summary>
        public bool RenameRoom(string title, out string error)
        {
            error = null;
            if (!IsHost || _state != LauncherState.InRoom)
            {
                error = "방장만 대기실에서 방 이름을 바꿀 수 있어요.";
                return false;
            }
            title = (title ?? string.Empty).Trim();
            if (title.Length == 0)
            {
                error = "방 이름을 입력해 주세요.";
                return false;
            }
            if (title.Length > LobbyRules.TitleMaxLength) title = title.Substring(0, LobbyRules.TitleMaxLength);
            if (title == _roomTitle) return true;

            try
            {
                _runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
                {
                    [LobbyRules.PropTitle] = title,
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                error = "방 이름을 바꾸지 못했어요.";
                return false;
            }
            _roomTitle = title;
            _lastSessionTitle = title;
            LogSession($"방 이름 변경: {title}");
            return true;
        }

        /// <summary>대기실에서 보여 줄 비밀번호 (비공개 방만): 방장 = 정한 비밀번호, 참가자 = 들어올 때 입력한 비밀번호.</summary>
        public string RoomPassword
        {
            get
            {
                if (!_roomIsPrivate) return null;
                if (IsHost) return _hostPassword;
                return _lastSessionPassword;
            }
        }

        #endregion

        #region 재접속 정보

        /// <summary>재접속할 수 있는 게임이 있는가 (3분 안).</summary>
        public bool HasReconnect => CanReconnect;

        /// <summary>재접속 남은 시간(초). 없으면 0.</summary>
        public float ReconnectSecondsLeft => CanReconnect ? Mathf.Max(0f, _reconnectDeadline - Time.realtimeSinceStartup) : 0f;

        /// <summary>재접속할 방 이름.</summary>
        public string ReconnectTitle => _lastSessionTitle ?? string.Empty;

        /// <summary>재접속 포기: 다른 방에 들어갈 수 있게 된다.</summary>
        public void AbandonReconnect()
        {
            if (!CanReconnect) return;
            LogSession($"재접속 포기: {_lastSessionTitle}");
            _reconnectDeadline = 0f;
            SessionListUpdated?.Invoke(_sessions);
        }

        #endregion

        #region 로비 접속 (조용히)

        private bool _quietLobbyConnect;

        /// <summary>
        /// 타이틀 화면에서: 로딩 창 없이 로비에 접속해 둔다. 실패해도 알림을 띄우지 않는다 (플레이를 누르면 다시 시도).
        /// </summary>
        public void EnterLobbyQuietly()
        {
            if (_state != LauncherState.Offline) return;
            _quietLobbyConnect = true;
            EnterLobby();
        }

        /// <summary>새 메뉴가 로비 접속 시점을 정한다 (GameLauncher.Start에서 바로 접속하지 않는다).</summary>
        public static bool MenuControlsLobbyConnect { get; set; }

        #endregion
    }
}
