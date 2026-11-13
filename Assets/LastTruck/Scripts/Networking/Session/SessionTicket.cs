using System;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 접속할 때 호스트에게 보내는 "입장권"(ConnectionToken)과 빌드 버전 / 기기 고유 ID.
    ///
    ///  - 버전: 다른 빌드끼리는 같은 방에 들어가지 못한다 (방 목록에서도 같은 버전 방만 보인다).
    ///  - 기기 ID: 처음 실행할 때 만든 GUID를 저장해 두고 계속 쓴다. 닉네임은 겹칠 수 있어서,
    ///    끊겼다가 다시 들어온 사람(재접속)이나 방장이 바뀐 뒤 다시 붙는 사람을 이 값으로 알아본다.
    ///  - 비밀번호: 비공개 방이면 같이 보낸다 (예전처럼 호스트가 OnConnectRequest에서 확인).
    ///
    /// 형식: "LT1\n버전\n기기ID\n비밀번호" (UTF-8). 예전 형식(비밀번호만)도 읽을 수 있다.
    /// </summary>
    public static class SessionTicket
    {
        #region 버전

        /// <summary>네트워크 구조가 바뀌면 올린다 (같은 Application.version이라도 서로 못 들어오게).</summary>
        public const int NetProtocol = 13;

        /// <summary>방 목록/접속에서 비교하는 버전 문자열.</summary>
        public static string GameVersion => $"{Application.version}/n{NetProtocol}";

        #endregion

        #region 기기 ID

        private const string DeviceIdPrefKey = "LastTruck.DeviceId";
        private static string _deviceId;

        /// <summary>이 컴퓨터(설치)의 고유 ID. 한 번 만들면 PlayerPrefs에 저장해서 다음 실행에도 같다.</summary>
        public static string DeviceId
        {
            get
            {
                if (!string.IsNullOrEmpty(_deviceId)) return _deviceId;
                string saved = PlayerPrefs.GetString(DeviceIdPrefKey, string.Empty);
                if (string.IsNullOrEmpty(saved) || saved.Length > 40)
                {
                    saved = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(DeviceIdPrefKey, saved);
                    PlayerPrefs.Save();
                }
                // 같은 컴퓨터에서 게임을 여러 개 띄우면(에디터 + 빌드, ParrelSync 복제, 빌드 2개) PlayerPrefs가 같아
                // 모두 같은 ID가 된다 → 실행 순서대로 슬롯 번호를 붙여 서로 구분한다 (하나만 띄우면 번호 없음 = 항상 같은 ID).
                int slot = AcquireInstanceSlot();
                _deviceId = slot > 0 ? $"{saved}-{slot}" : saved;
                return _deviceId;
            }
        }

        private static Mutex _instanceMutex; // 프로세스가 끝날 때까지 잡고 있는다 (같은 슬롯을 다른 실행이 못 쓰게)
        private static int _instanceSlot;    // 플레이를 다시 눌러도(정적 값 초기화) 같은 번호를 쓴다

        /// <summary>이 컴퓨터에서 몇 번째로 실행된 게임인가 (0 = 처음).</summary>
        private static int AcquireInstanceSlot()
        {
            if (_instanceMutex != null) return _instanceSlot;
            for (int slot = 0; slot < 8; slot++)
            {
                try
                {
                    var mutex = new Mutex(false, $"LastTruck.Instance.{slot}", out bool createdNew);
                    if (createdNew)
                    {
                        _instanceMutex = mutex;
                        _instanceSlot = slot;
                        return slot;
                    }
                    mutex.Dispose();
                }
                catch (Exception)
                {
                    return 0; // 지원 안 되는 환경이면 구분하지 않는다
                }
            }
            return 0;
        }

        #endregion

        #region 입장권 만들기 / 읽기

        private const string Header = "LT1";

        public static byte[] Build(string password)
        {
            string text = Header + "\n" + GameVersion + "\n" + DeviceId + "\n" + (password ?? string.Empty);
            return Encoding.UTF8.GetBytes(text);
        }

        public struct Info
        {
            public string Version;   // 예전 형식이면 null
            public string DeviceId;  // 예전 형식이면 null
            public string Password;  // 없으면 빈 문자열
        }

        public static Info Parse(byte[] token)
        {
            var info = new Info { Password = string.Empty };
            if (token == null || token.Length == 0) return info;

            string text;
            try { text = Encoding.UTF8.GetString(token); }
            catch { return info; }

            if (!text.StartsWith(Header + "\n", StringComparison.Ordinal))
            {
                info.Password = text; // 예전 형식: 비밀번호만
                return info;
            }

            string[] parts = text.Split('\n');
            info.Version = parts.Length > 1 ? parts[1] : null;
            info.DeviceId = parts.Length > 2 ? parts[2] : null;
            info.Password = parts.Length > 3 ? parts[3] : string.Empty;
            return info;
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _deviceId = null;
        }
    }
}
