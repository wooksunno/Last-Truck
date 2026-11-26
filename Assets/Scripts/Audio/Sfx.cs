using System.Collections.Generic;
using UnityEngine;

namespace LastTruck.Audio
{
    /// <summary>
    /// 효과음 재생기. Resources/Audio/SFX/&lt;묶음&gt;/ 안의 소리 중 하나를 골라(연속으로 같은 것 X) 살짝 다른 높이로 낸다.
    ///  - Sfx.Play("ui_click")            : 화면 소리 (UI)
    ///  - Sfx.PlayAt("hit_flesh", pos)     : 그 자리에서 나는 소리 (멀면 작게, 아주 멀면 안 냄)
    /// 같은 묶음이 한 프레임에 여러 번 불려도 겹쳐서 귀가 아프지 않게 묶음마다 최소 간격을 둔다.
    /// 소리는 Kenney 오디오 팩(CC0). 음량은 Sfx.Volume (PlayerPrefs "sfx_volume", 기본 0.8).
    /// </summary>
    public static class Sfx
    {
        private const string Folder = "Audio/SFX/";
        private const int PoolSize = 24;
        private const float MaxDistance = 38f;

        // 묶음별 기본 크기 (팩마다 녹음 크기가 달라서 귀로 맞춘 값이 아니라 대략 맞춘 값)
        private static readonly Dictionary<string, float> BaseVolume = new Dictionary<string, float>
        {
            { "ui_click", 0.55f }, { "ui_switch", 0.35f }, { "ui_open", 0.55f },
            { "quest_open", 0.7f }, { "quest_flip", 0.65f }, { "quest_close", 0.55f },
            { "pickup", 0.55f }, { "craft", 0.5f }, { "coins", 0.6f }, { "notify_done", 0.7f },
            { "equip_blade", 0.45f }, { "equip_tool", 0.45f },
            { "swing_blade", 0.7f }, { "swing_light", 0.45f },
            { "hit_flesh", 0.6f }, { "hit_heavy", 0.85f }, { "kill", 0.8f },
            { "gather_rock", 0.8f }, { "gather_wood", 0.7f }, { "gather_plant", 0.8f },
            { "truck_hit", 0.7f }, { "truck_repair", 0.55f },
            { "footstep", 0.28f }, { "run_loop", 0.45f }, { "bell", 0.75f }, { "zombie", 0.7f },
        };

        // 파일 하나하나의 크기 보정 (같은 묶음 안에서 유난히 작은 녹음). 불러올 때 소리 자체를 키운다.
        //  실측 최대치: grass 0.08 / done_c 0.26 / notify_b 0.75
        private static readonly Dictionary<string, float> ClipGain = new Dictionary<string, float>
        {
            { "grass", 8f }, { "done_c", 3f }, { "notify_b", 1.3f },
        };

        private static readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, int> _last = new Dictionary<string, int>();
        private static readonly Dictionary<string, float> _lastTime = new Dictionary<string, float>();
        private static AudioSource[] _pool;
        private static int _next;
        private static GameObject _host;
        private static float _volume = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _clips.Clear(); _last.Clear(); _lastTime.Clear();
            _pool = null; _host = null; _next = 0; _volume = -1f;
            _loop = null; _loopGroup = null; _loopTarget = 0f; _loopLevel = 0f;
        }

        public static float Volume
        {
            get
            {
                if (_volume < 0f)
                {
                    try { _volume = PlayerPrefs.GetFloat("sfx_volume", 0.8f); } catch { _volume = 0.8f; }
                }
                return _volume;
            }
            set
            {
                _volume = Mathf.Clamp01(value);
                try { PlayerPrefs.SetFloat("sfx_volume", _volume); } catch { }
            }
        }

        /// <summary>화면 소리 (UI, 내 캐릭터 소리 등 거리와 상관없는 것).</summary>
        public static void Play(string group, float volume = 1f, float pitch = 1f, float minGap = 0.04f)
        {
            Emit(group, Vector3.zero, false, volume, pitch, minGap);
        }

        /// <summary>그 자리에서 나는 소리. 듣는 사람(카메라)에서 멀수록 작아지고, 38m 넘으면 내지 않는다.</summary>
        public static void PlayAt(string group, Vector3 position, float volume = 1f, float pitch = 1f, float minGap = 0.04f)
        {
            Emit(group, position, true, volume, pitch, minGap);
        }

        private static void Emit(string group, Vector3 pos, bool spatial, float volume, float pitch, float minGap)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(group)) return;
            float master = Volume;
            if (master <= 0.001f || volume <= 0.001f) return;

            if (spatial)
            {
                AudioListener listener = Listener();
                if (listener != null && (listener.transform.position - pos).sqrMagnitude > MaxDistance * MaxDistance) return;
            }

            float now = Time.unscaledTime;
            if (_lastTime.TryGetValue(group, out float t) && now - t < minGap) return;

            AudioClip clip = Pick(group);
            if (clip == null) return;
            _lastTime[group] = now;

            AudioSource src = NextSource();
            if (src == null) return;
            src.transform.position = pos;
            src.spatialBlend = spatial ? 1f : 0f;
            src.clip = clip;
            src.pitch = pitch * Random.Range(0.93f, 1.07f);
            float baseVol = BaseVolume.TryGetValue(group, out float b) ? b : 0.6f;
            src.volume = Mathf.Clamp01(master * baseVol * volume);
            src.Play();
        }

        private static AudioListener _listener;
        private static float _listenerCheck;

        private static AudioListener Listener()
        {
            if (_listener != null && _listener.isActiveAndEnabled) return _listener;
            if (Time.unscaledTime < _listenerCheck) return null;
            _listenerCheck = Time.unscaledTime + 1f;
            _listener = Object.FindFirstObjectByType<AudioListener>();
            return _listener;
        }

        private static AudioClip Pick(string group)
        {
            if (!_clips.TryGetValue(group, out AudioClip[] list))
            {
                list = Resources.LoadAll<AudioClip>(Folder + group);
                for (int k = 0; k < list.Length; k++) list[k] = Prepare(list[k]);
                _clips[group] = list;
                if (list.Length == 0) Debug.LogWarning("[Sfx] 소리 묶음이 비어 있어요: " + group);
            }
            if (list.Length == 0) return null;
            if (list.Length == 1) return list[0];

            int prev = _last.TryGetValue(group, out int p) ? p : -1;
            int i;
            if (prev < 0) i = Random.Range(0, list.Length);
            else { i = Random.Range(0, list.Length - 1); if (i >= prev) i++; }   // 방금 낸 소리는 건너뛴다
            _last[group] = i;
            return list[i];
        }

        /// <summary>
        /// 짧은 소리를 불러올 때 한 번만 손질한다:
        ///  - 앞쪽 무음을 잘라낸다 (예: 칼 공격 파일은 1.1초 조용하다가 소리가 나서, 휘두른 뒤 한참 늦게 들렸다)
        ///  - ClipGain 에 있는 파일은 소리 자체를 키운다 (최대 0.95에서 멈춤)
        /// 원본 에셋은 건드리지 않고 메모리 안에서 새 클립을 만든다.
        /// </summary>
        private static AudioClip Prepare(AudioClip clip)
        {
            if (clip == null || clip.loadType != AudioClipLoadType.DecompressOnLoad || clip.length > 8f) return clip;
            try
            {
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                int ch = clip.channels, hz = clip.frequency;
                var data = new float[clip.samples * ch];
                if (!clip.GetData(data, 0)) return clip;

                int first = 0;
                while (first < data.Length && Mathf.Abs(data[first]) < 0.03f) first++;
                first = Mathf.Max(0, first - hz * ch / 100);   // 소리 시작 10ms 앞에서 자른다
                first -= first % ch;
                float gain = ClipGain.TryGetValue(clip.name, out float g) ? g : 1f;
                if (first < hz * ch / 50 && Mathf.Approximately(gain, 1f)) return clip;   // 20ms 미만 무음 + 보정 없음 → 그대로

                int len = data.Length - first;
                if (len <= ch) return clip;
                var cut = new float[len];
                for (int i = 0; i < len; i++) cut[i] = Mathf.Clamp(data[first + i] * gain, -0.95f, 0.95f);
                AudioClip made = AudioClip.Create(clip.name, len / ch, ch, hz, false);
                made.SetData(cut, 0);
                return made;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Sfx] 소리 손질 실패 " + clip.name + ": " + e.Message);
                return clip;
            }
        }

        // ───────── 반복 소리 (달리기처럼 켜고 끄는 소리) ─────────

        private static AudioSource _loop;
        private static string _loopGroup;
        private static float _loopTarget, _loopLevel;

        /// <summary>on이면 그 묶음을 반복 재생, off면 서서히 줄여 멈춘다. 매 프레임 불러도 된다.</summary>
        public static void Loop(string group, bool on)
        {
            if (!Application.isPlaying) return;
            if (on && (_loop == null || _loopGroup != group))
            {
                AudioClip clip = Pick(group);
                if (clip == null) return;
                if (_loop == null)
                {
                    NextSource();   // 풀/호스트 준비
                    var go = new GameObject("Loop");
                    go.transform.SetParent(_host.transform, false);
                    _loop = go.AddComponent<AudioSource>();
                    _loop.playOnAwake = false;
                    _loop.loop = true;
                    _loop.spatialBlend = 0f;
                    _loop.volume = 0f;
                }
                _loop.clip = clip;
                _loopGroup = group;
                _loopLevel = 0f;
            }
            if (_loop == null) return;

            _loopTarget = on ? 1f : 0f;
            _loopLevel = Mathf.MoveTowards(_loopLevel, _loopTarget, Time.unscaledDeltaTime / (on ? 0.15f : 0.3f));
            float baseVol = BaseVolume.TryGetValue(_loopGroup, out float b) ? b : 0.5f;
            _loop.volume = Volume * baseVol * _loopLevel;
            if (_loopLevel > 0.001f && !_loop.isPlaying) _loop.Play();
            else if (_loopLevel <= 0.001f && _loop.isPlaying) _loop.Pause();
        }

        private static AudioSource NextSource()
        {
            if (_pool == null || _host == null)
            {
                _host = new GameObject("Sfx");
                Object.DontDestroyOnLoad(_host);
                _pool = new AudioSource[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    var go = new GameObject("Voice" + i);
                    go.transform.SetParent(_host.transform, false);
                    AudioSource s = go.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.rolloffMode = AudioRolloffMode.Linear;
                    s.minDistance = 4f;
                    s.maxDistance = MaxDistance;
                    s.dopplerLevel = 0f;
                    s.priority = 96;
                    _pool[i] = s;
                }
            }

            // 쉬고 있는 목소리를 먼저 쓰고, 다 바쁘면 가장 오래된 것을 끊고 쓴다
            for (int k = 0; k < PoolSize; k++)
            {
                AudioSource s = _pool[(_next + k) % PoolSize];
                if (s != null && !s.isPlaying) { _next = (_next + k + 1) % PoolSize; return s; }
            }
            AudioSource oldest = _pool[_next];
            _next = (_next + 1) % PoolSize;
            return oldest;
        }
    }
}
