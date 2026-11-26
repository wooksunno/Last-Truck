using System.Collections.Generic;
using UnityEngine;

namespace LastTruck.Audio
{
    /// <summary>
    /// 배경음악 감독. 씬에 아무것도 놓지 않아도 게임 시작 시 저절로 생긴다 (Resources/Audio/BGM 의 mp3 사용).
    ///  - 메뉴/대기실/게임 시작 대기 → 로비 곡
    ///  - 낮 → 낮 곡 4개를 섞어서 차례로 (같은 곡 연속 X)
    ///  - 밤 → 밤 곡 2개를 번갈아
    ///  - 게임 오버 → 서서히 꺼짐
    /// 낮↔밤이 바뀌면 크로스페이드로 자연스럽게 넘어가고, 곡이 끝나기 직전에 다음 곡과 겹쳐 넘어간다.
    /// 곡마다 녹음 크기가 달라서 TrackGain 으로 들리는 크기를 맞춘다.
    /// 음량은 BgmDirector.Volume (PlayerPrefs "bgm_volume", 기본 0.45).
    /// </summary>
    public class BgmDirector : MonoBehaviour
    {
        private const string Folder = "Audio/BGM/";
        private static readonly string[] LobbyTracks = { "lobby_bgm_B" };
        private static readonly string[] DayTracks = { "day_bgm_v2_A", "day_bgm_A", "day_bgm_v2_B", "day_bgm_B" };
        private static readonly string[] NightTracks = { "night_bgm_v2_A", "night_bgm_v2_B" };

        // 곡별 크기 보정 (1 = 그대로). 곡 전체 평균 음량(RMS)을 재서 모두 약 -19dB로 맞췄다.
        //  실측: day_A -15.3 / day_B -16.3 / day_v2_A -18.2 / day_v2_B -18.5 / lobby_B -17.8 / night_v2_A -21.0 / night_v2_B -19.2 dB
        private static readonly Dictionary<string, float> TrackGain = new Dictionary<string, float>
        {
            { "day_bgm_A", 0.65f }, { "day_bgm_B", 0.73f }, { "day_bgm_v2_A", 0.91f }, { "day_bgm_v2_B", 0.94f },
            { "lobby_bgm_B", 0.87f }, { "night_bgm_v2_A", 1.26f }, { "night_bgm_v2_B", 1.02f },
        };

        private const float MoodFade = 3.5f;    // 낮↔밤 전환
        private const float TrackFade = 4f;     // 같은 분위기 안에서 다음 곡으로
        private const float SilentFade = 2.5f;  // 게임 오버

        private enum Mood { None, Lobby, Day, Night, Silent }

        private static BgmDirector _instance;
        private static float _volume = -1f;

        public static float Volume
        {
            get
            {
                if (_volume < 0f)
                {
                    try { _volume = PlayerPrefs.GetFloat("bgm_volume", 0.45f); } catch { _volume = 0.45f; }
                }
                return _volume;
            }
            set
            {
                _volume = Mathf.Clamp01(value);
                try { PlayerPrefs.SetFloat("bgm_volume", _volume); } catch { }
            }
        }

        private AudioSource _a, _b;        // _a = 지금 곡, _b = 사라지는 곡
        private float _aGain = 1f, _bGain = 1f;
        private float _fadeIn = 1f, _fadeOut;  // 0→1 진행
        private float _fadeDur = 1f;
        private Mood _mood = Mood.None;
        private string _current;
        private readonly Dictionary<Mood, List<string>> _queue = new Dictionary<Mood, List<string>>();
        private float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("BgmDirector");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BgmDirector>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _volume = -1f; }

        private void Awake()
        {
            _a = MakeSource();
            _b = MakeSource();
        }

        private AudioSource MakeSource()
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            s.priority = 0;
            s.ignoreListenerPause = true;
            s.volume = 0f;
            return s;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 0.5f;
                Mood want = DecideMood();
                if (want != _mood)
                {
                    bool first = _mood == Mood.None;
                    _mood = want;
                    if (want == Mood.Silent) FadeToSilence();
                    else PlayNext(first ? 1.5f : MoodFade);
                }
            }

            // 곡이 끝나기 직전이면 같은 분위기의 다음 곡으로 겹쳐 넘어간다
            if (_mood != Mood.Silent && _mood != Mood.None && _a.clip != null && _a.isPlaying &&
                _fadeIn >= 1f && _a.time >= _a.clip.length - TrackFade)
                PlayNext(TrackFade);
            // 재생이 어떤 이유로 멈췄으면 다시 이어 준다
            else if (_mood != Mood.Silent && _mood != Mood.None && _a.clip != null && !_a.isPlaying && _fadeIn >= 1f)
                PlayNext(1.5f);

            _fadeIn = Mathf.MoveTowards(_fadeIn, 1f, dt / _fadeDur);
            _fadeOut = Mathf.MoveTowards(_fadeOut, 1f, dt / _fadeDur);

            float vol = Volume;
            _a.volume = vol * _aGain * Smooth(_fadeIn);
            _b.volume = vol * _bGain * (1f - Smooth(_fadeOut));
            if (_fadeOut >= 1f && _b.isPlaying) _b.Stop();
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static Mood DecideMood()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.IsWaitingForNetworkStart) return Mood.Lobby;
            if (LastTruck.Networking.NetworkGameState.IsGameOver) return Mood.Silent;
            return gm.CurrentPhase == GameManager.GamePhase.Night ? Mood.Night : Mood.Day;
        }

        private string[] TracksOf(Mood m)
        {
            switch (m)
            {
                case Mood.Day: return DayTracks;
                case Mood.Night: return NightTracks;
                default: return LobbyTracks;
            }
        }

        // 섞은 순서대로 하나씩 꺼낸다. 다 쓰면 다시 섞되, 방금 곡이 바로 또 나오지 않게.
        private string NextName(Mood m)
        {
            string[] all = TracksOf(m);
            if (all.Length == 1) return all[0];
            if (!_queue.TryGetValue(m, out List<string> q)) { q = new List<string>(); _queue[m] = q; }
            if (q.Count == 0)
            {
                q.AddRange(all);
                for (int i = q.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (q[i], q[j]) = (q[j], q[i]); }
                if (q[0] == _current) { q.RemoveAt(0); q.Add(_current); }
            }
            string n = q[0];
            q.RemoveAt(0);
            return n;
        }

        private void PlayNext(float fade)
        {
            string name = NextName(_mood);
            AudioClip clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) { Debug.LogWarning("[BGM] 곡을 찾을 수 없어요: " + name); return; }

            // 지금 곡을 사라지는 쪽으로 보내고, 새 곡을 앞에 세운다
            (_a, _b) = (_b, _a);
            _bGain = _aGain * Smooth(_fadeIn);   // 페이드 도중이었으면 그 크기에서부터 줄어든다
            _fadeOut = 0f;

            _a.Stop();
            _a.clip = clip;
            _a.time = 0f;
            _a.Play();
            _aGain = TrackGain.TryGetValue(name, out float g) ? g : 1f;
            _fadeIn = 0f;
            _fadeDur = Mathf.Max(0.1f, fade);
            _current = name;
        }

        private void FadeToSilence()
        {
            (_a, _b) = (_b, _a);
            _bGain = _aGain * Smooth(_fadeIn);
            _fadeOut = 0f;
            _a.Stop();
            _a.clip = null;
            _aGain = 0f;
            _fadeIn = 1f;
            _fadeDur = SilentFade;
            _current = null;
        }
    }
}
