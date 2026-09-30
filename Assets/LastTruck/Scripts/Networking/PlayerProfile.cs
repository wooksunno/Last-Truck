using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 로컬 플레이어의 닉네임을 관리한다.
    /// - 처음 실행하면 "player" + 4자리 숫자(예: player0427)를 랜덤으로 만든다.
    /// - 사용자가 닉네임 칸에서 직접 바꾼 경우에만 PlayerPrefs에 저장해서 다음 실행에도 유지한다.
    ///   (같은 PC에서 여러 개 띄워서 테스트할 때 전부 같은 이름이 되지 않도록, 자동 생성 이름은 저장하지 않는다.)
    /// - 닉네임 중복은 허용한다. 내부적으로는 PlayerRef로 사람을 구분하므로 이름이 같아도 로직은 꼬이지 않는다.
    /// </summary>
    public static class PlayerProfile
    {
        private const string PrefKey = "LastTruck.Nickname";

        private static string _nickname;

        public static string Nickname
        {
            get
            {
                if (string.IsNullOrEmpty(_nickname))
                {
                    string saved = PlayerPrefs.GetString(PrefKey, string.Empty);
                    _nickname = Validate(saved, out _) ? saved : GenerateRandomNickname();
                }
                return _nickname;
            }
        }

        public static string GenerateRandomNickname()
        {
            return LobbyRules.NicknamePrefix + UnityEngine.Random.Range(0, 10000).ToString("D4");
        }

        /// <summary>닉네임 입력칸에서 사용자가 이름을 바꿨을 때 호출. 실패하면 false와 이유를 돌려준다.</summary>
        public static bool TrySetNickname(string value, out string error)
        {
            string trimmed = Clean(value);
            if (!Validate(trimmed, out error)) return false;

            _nickname = trimmed;
            PlayerPrefs.SetString(PrefKey, trimmed);
            PlayerPrefs.Save();
            return true;
        }

        public static string Clean(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        /// <summary>네트워크로 받은 닉네임을 안전한 길이로 자른다 (호스트가 RPC로 받을 때 사용).</summary>
        public static string ClampForNetwork(string value)
        {
            string trimmed = Clean(value);
            if (trimmed.Length > LobbyRules.NicknameMaxLength)
            {
                trimmed = trimmed.Substring(0, LobbyRules.NicknameMaxLength);
            }
            return string.IsNullOrEmpty(trimmed) ? "player" : trimmed;
        }

        private static bool Validate(string value, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "닉네임을 입력해 주세요.";
                return false;
            }
            if (value.Length < LobbyRules.NicknameMinLength || value.Length > LobbyRules.NicknameMaxLength)
            {
                error = $"닉네임은 {LobbyRules.NicknameMinLength}~{LobbyRules.NicknameMaxLength}자로 입력해 주세요.";
                return false;
            }
            error = null;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _nickname = null;
        }
    }
}
