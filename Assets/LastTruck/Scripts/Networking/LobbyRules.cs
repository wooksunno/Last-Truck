namespace LastTruck.Networking
{
    /// <summary>
    /// 멀티플레이 로비에서 팀이 합의한 규칙 값을 한곳에 모아둔 상수 모음.
    /// 숫자를 바꾸고 싶으면 여기만 고치면 된다 (UI 생성 에디터 스크립트도 이 값을 읽는다).
    /// </summary>
    public static class LobbyRules
    {
        // ---- 인원 ----
        public const int MinPlayers = 1;
        public const int MaxPlayersLimit = 4;
        public const int DefaultMaxPlayers = 4;

        // ---- 닉네임 (중복 허용) ----
        public const string NicknamePrefix = "player";
        public const int NicknameMinLength = 2;
        public const int NicknameMaxLength = 16;

        // ---- 방 제목 ----
        public const string DefaultTitleSuffix = "'s room";
        public const int TitleMaxLength = 24;

        // ---- 비밀번호 ----
        public const bool DefaultPrivate = true;       // 세션 생성 기본값 = 비공개
        public const int GeneratedPasswordLength = 8;   // 자동 생성 비밀번호 자릿수
        public const int PasswordMaxLength = 16;

        // ---- 네트워크 ----
        /// <summary>로비 접속 / 방 생성 / 방 참가 요청이 이 시간 안에 끝나지 않으면 실패 처리한다.</summary>
        public const float OperationTimeoutSeconds = 15f;

        // ---- 세션 프로퍼티 키 (로비의 모든 사람이 읽을 수 있으므로 비밀번호 자체는 절대 넣지 않는다) ----
        public const string PropTitle = "title";
        public const string PropHasPassword = "pw"; // 0 = 공개, 1 = 비공개

        public static string DefaultRoomTitle(string nickname) => nickname + DefaultTitleSuffix;

        /// <summary>
        /// 사용자가 입력한 글자(닉네임/방 제목)를 TMP 리치 텍스트 태그로 해석하지 않고 그대로 보여주기 위한 감싸기.
        /// </summary>
        public static string SafeText(string value)
        {
            string text = value ?? string.Empty;
            text = System.Text.RegularExpressions.Regex.Replace(text, "</?noparse>", string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return "<noparse>" + text + "</noparse>";
        }
    }
}
