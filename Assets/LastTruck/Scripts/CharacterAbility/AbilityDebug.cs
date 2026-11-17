using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 캐릭터 고유 능력 디버그 기록.
    ///  - 능력이 실제로 값을 바꿨을 때(CharacterAbilityController 등) Report를 부르면
    ///    콘솔에 "[Ability]" 로그가 남고, 화면 디버그 창(AbilityDebugOverlay, F9)에도 최근 기록이 보인다.
    ///  - 채집 시간/제작 재료처럼 UI가 매 프레임 묻는 값은 같은 내용이면 throttle초 동안 다시 기록하지 않는다 (콘솔 도배 방지).
    ///  - 콘솔 로그는 기본으로 꺼져 있다. 디버그 창(F9)에서 F10으로 켜면 "내 PC에만" 기억된다 (AbilityDebugPrefs).
    /// </summary>
    public static class AbilityDebug
    {
        public struct Entry
        {
            public float time;
            public string text;
        }

        /// <summary>콘솔에 로그를 남길지. 내 PC에 저장된 설정을 따른다 (기본 꺼짐, 팀원 PC에는 영향 없음).</summary>
        public static bool LogToConsole
        {
            get => Debug.isDebugBuild && AbilityDebugPrefs.ConsoleLog;
            set => AbilityDebugPrefs.ConsoleLog = value;
        }

        public const int MaxEntries = 16;

        private static readonly List<Entry> Recent = new List<Entry>();
        private static readonly Dictionary<string, KeyValuePair<string, float>> LastByKey = new Dictionary<string, KeyValuePair<string, float>>();

        /// <summary>최근 기록 (오래된 것 → 새 것).</summary>
        public static IReadOnlyList<Entry> Entries => Recent;

        /// <summary>
        /// 능력 발동 기록.
        /// who: 캐릭터 이름, key: 같은 종류의 기록을 묶는 이름, throttle: 같은 key + 같은 내용이면 이 시간(초) 안에는 다시 기록하지 않는다 (0이면 항상 기록).
        /// </summary>
        public static void Report(string who, string key, string message, float throttle = 0f)
        {
            if (string.IsNullOrEmpty(message)) return;

            string text = string.IsNullOrEmpty(who) ? message : $"[{who}] {message}";
            float now = Time.unscaledTime;
            string mapKey = (who ?? string.Empty) + "|" + (key ?? string.Empty);

            if (throttle > 0f && LastByKey.TryGetValue(mapKey, out KeyValuePair<string, float> previous) &&
                previous.Key == text && now - previous.Value < throttle)
                return;
            LastByKey[mapKey] = new KeyValuePair<string, float>(text, now);

            Recent.Add(new Entry { time = now, text = text });
            if (Recent.Count > MaxEntries) Recent.RemoveAt(0);

            if (LogToConsole) Debug.Log($"<color=#7FD7FF>[Ability]</color> {text}");
        }

        public static void Clear()
        {
            Recent.Clear();
            LastByKey.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Recent.Clear();
            LastByKey.Clear();
        }
    }

    /// <summary>
    /// 디버그 창/콘솔 로그 켜짐 여부를 "이 PC에만" 저장한다.
    ///  - 에디터: EditorPrefs (PC의 유니티 설정에 저장 - 프로젝트 파일이 아니라 깃허브로 공유되지 않음)
    ///  - 개발 빌드: PlayerPrefs (그 PC에만 저장)
    /// 기본값은 모두 꺼짐이라, 팀원이 프로젝트를 받아도 디버그 창이 뜨지 않는다.
    /// </summary>
    public static class AbilityDebugPrefs
    {
        private const string OverlayKey = "LastTruck.AbilityDebug.OverlayVisible";
        private const string ConsoleKey = "LastTruck.AbilityDebug.ConsoleLog";

        public static bool OverlayVisible
        {
            get => GetBool(OverlayKey);
            set => SetBool(OverlayKey, value);
        }

        public static bool ConsoleLog
        {
            get => GetBool(ConsoleKey);
            set => SetBool(ConsoleKey, value);
        }

        private static bool GetBool(string key)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(key, false);
#else
            return PlayerPrefs.GetInt(key, 0) == 1;
#endif
        }

        private static void SetBool(string key, bool value)
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(key, value);
#else
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
#endif
        }
    }

    /// <summary>
    /// 능력이 발동했을 때 내 화면에 짧게 띄우는 글자 (데미지 숫자와 같은 방식으로 떠오르며 사라진다).
    /// </summary>
    public static class AbilityFeedback
    {
        public static readonly Color ArmyColor = new Color(1f, 0.55f, 0.15f);
        public static readonly Color PoliceColor = new Color(1f, 0.9f, 0.25f);
        public static readonly Color BoxerColor = Color.white;

        /// <param name="sizeScale">글자 크기 배율 (1 = 기본)</param>
        public static void Popup(Vector3 worldPos, string text, Color color, float sizeScale = 1f)
        {
            if (string.IsNullOrEmpty(text)) return;
            Combat.DamagePopup.SpawnText(worldPos, text, color, sizeScale);
        }
    }
}
