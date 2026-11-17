using System.Text;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 고유 능력 디버그 창 (에디터 / Development Build에서만 자동으로 생긴다).
    ///  - F9: 창 켜기/끄기, 창이 켜져 있을 때 F10: 콘솔 [Ability] 로그 켜기/끄기, F11: 기록 지우기
    ///  - 켜짐/꺼짐은 "이 PC에만" 기억된다 (AbilityDebugPrefs). 기본은 꺼짐이라 팀원 PC에서는 F9를 누르기 전까지 보이지 않는다.
    ///  - 내 캐릭터 / 고유 능력 / 능력 설명(현재 수치) / 실시간 상태(군인 공격 횟수 등) / 최근 발동 기록을 보여준다.
    /// 정식 빌드(Development Build 체크 해제)에서는 만들어지지 않는다.
    /// </summary>
    public class AbilityDebugOverlay : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F9;
        [SerializeField] private KeyCode consoleKey = KeyCode.F10;
        [SerializeField] private KeyCode clearKey = KeyCode.F11;

        private bool _visible;
        private CharacterAbilityController _local;
        private float _nextLookupTime;
        private GUIStyle _box;
        private GUIStyle _label;
        private readonly StringBuilder _sb = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!Debug.isDebugBuild) return;
            if (FindFirstObjectByType<AbilityDebugOverlay>() != null) return;

            var go = new GameObject("AbilityDebugOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<AbilityDebugOverlay>();
        }

        private void Awake()
        {
            _visible = AbilityDebugPrefs.OverlayVisible;   // 이 PC에 저장된 마지막 상태
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _visible = !_visible;
                AbilityDebugPrefs.OverlayVisible = _visible;   // 다음 플레이에도 이 PC에서는 같은 상태로 시작
            }
            if (!_visible) return;

            if (Input.GetKeyDown(consoleKey)) AbilityDebug.LogToConsole = !AbilityDebug.LogToConsole;
            if (Input.GetKeyDown(clearKey)) AbilityDebug.Clear();

            // 내 캐릭터는 스폰/부활로 바뀔 수 있어서 1초마다 다시 찾는다 (FindFirstObjectByType이 비싸서 매 프레임 찾지 않음)
            if (_local == null || Time.unscaledTime >= _nextLookupTime)
            {
                _local = Networking.LocalPlayerLookup.AbilityController;
                _nextLookupTime = Time.unscaledTime + 1f;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            _sb.Clear();
            _sb.Append("<b>고유 능력 디버그</b>  <color=#9AA3B2>(F9 닫기 · F10 콘솔 로그 ")
               .Append(AbilityDebug.LogToConsole ? "켜짐" : "꺼짐").Append(" · F11 기록 지우기)</color>\n");

            CharacterAbility ability = _local != null ? _local.CurrentAbility : null;
            string characterName = _local != null ? _local.CharacterName : null;

            if (_local == null)
            {
                _sb.Append("<color=#FF8080>내 캐릭터에서 CharacterAbilityController를 찾지 못함</color>\n");
            }
            else
            {
                _sb.Append("캐릭터: <b>").Append(characterName).Append("</b>");
                if (!_local.enabled) _sb.Append("  <color=#FF8080>(컨트롤러 꺼짐 - 능력 미적용)</color>");
                _sb.Append('\n');

                if (ability == null)
                {
                    _sb.Append("<color=#FF8080>고유 능력 없음 (CharacterData의 Unique Ability가 비어 있음)</color>\n");
                }
                else
                {
                    _sb.Append("능력: <b><color=#7FD7FF>").Append(ability.AbilityTitle).Append("</color></b>  <color=#9AA3B2>(")
                       .Append(ability.name).Append(")</color>\n");
                    string description = ability.GetDescription();
                    if (!string.IsNullOrEmpty(description)) _sb.Append(description).Append('\n');
                    string status = ability.GetDebugStatus();
                    if (!string.IsNullOrEmpty(status)) _sb.Append("<color=#FFD27F>상태: ").Append(status).Append("</color>\n");
                }
            }

            _sb.Append("\n<b>최근 발동</b>\n");
            var entries = AbilityDebug.Entries;
            if (entries.Count == 0) _sb.Append("<color=#9AA3B2>(아직 없음 - 능력이 값을 바꾸면 여기에 표시)</color>\n");
            float now = Time.unscaledTime;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                float age = now - entries[i].time;
                _sb.Append("<color=#9AA3B2>").Append(age.ToString("0.0")).Append("초 전</color>  ").Append(entries[i].text).Append('\n');
            }

            const float width = 460f;
            var content = new GUIContent(_sb.ToString());
            float height = _label.CalcHeight(content, width - 20f) + 20f;
            var rect = new Rect(12f, Screen.height - height - 12f, width, height);
            GUI.Box(rect, GUIContent.none, _box);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, width - 20f, height - 20f), content, _label);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _box = new GUIStyle(GUI.skin.box);
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.08f, 0.82f));
            background.Apply();
            _box.normal.background = background;

            _label = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 13 };
            _label.normal.textColor = Color.white;
            Font font = Networking.InGameFonts.LegacyFont;   // 한글이 선명한 게임 UI 폰트 (없으면 기본 폰트)
            if (font != null) _label.font = font;
        }
    }
}
