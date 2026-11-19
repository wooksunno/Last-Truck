using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 설정 창 [소리] / [화면] 탭의 값. PlayerPrefs에 저장하고, 게임을 켤 때(GameLauncher.Start) 한 번 적용한다.
///  - 소리: 전체(AudioListener.volume) / 효과음 / 배경음. 효과음·배경음은 AudioChannel 컴포넌트가 붙은 AudioSource에 곱해진다.
///    씬의 AudioSource에 AudioChannel이 없으면 씬을 불러올 때 자동으로 붙인다 (반복 재생 + 2D 소리 = 배경음, 나머지 = 효과음).
///  - 화면: 해상도 / 화면 모드 / 품질 / FPS 제한 / 수직 동기화.
///    저장된 화면 설정이 없으면 예전처럼 800x800 창 모드로 시작한다 (한 PC에서 여러 개 띄워 테스트하기 편하게).
/// </summary>
public static class GameSettings
{
    #region 값

    [Serializable]
    public struct Values
    {
        public float master;     // 0~1
        public float sfx;        // 0~1
        public float bgm;        // 0~1
        public int width;
        public int height;
        public FullScreenMode mode;
        public int quality;      // QualitySettings 번호
        public int fpsLimit;     // 0 = 무제한
        public bool vsync;
    }

    public const int DefaultWidth = 800;
    public const int DefaultHeight = 800;
    public static readonly int[] FpsOptions = { 30, 60, 120, 144, 240, 0 };

    private const string P = "LastTruck.Settings.";
    private static Values? _current;

    /// <summary>값이 바뀌어 적용됐을 때 (볼륨 미리듣기 포함).</summary>
    public static event Action Changed;

    public static Values Current
    {
        get
        {
            if (!_current.HasValue) _current = Load();
            return _current.Value;
        }
    }

    public static float SfxVolume => Current.master > 0f ? Current.sfx : 0f;
    public static float BgmVolume => Current.master > 0f ? Current.bgm : 0f;

    public static Values Defaults()
    {
        return new Values
        {
            master = 1f,
            sfx = 0.8f,
            bgm = 0.6f,
            width = DefaultWidth,
            height = DefaultHeight,
            mode = FullScreenMode.Windowed,
            quality = QualitySettings.GetQualityLevel(),
            fpsLimit = 60,
            vsync = false,
        };
    }

    private static Values Load()
    {
        Values d = Defaults();
        return new Values
        {
            master = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "Master", d.master)),
            sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "Sfx", d.sfx)),
            bgm = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "Bgm", d.bgm)),
            width = Mathf.Max(320, PlayerPrefs.GetInt(P + "Width", d.width)),
            height = Mathf.Max(240, PlayerPrefs.GetInt(P + "Height", d.height)),
            mode = (FullScreenMode)Mathf.Clamp(PlayerPrefs.GetInt(P + "Mode", (int)d.mode), 0, 3),
            quality = Mathf.Clamp(PlayerPrefs.GetInt(P + "Quality", d.quality), 0, Mathf.Max(0, QualitySettings.names.Length - 1)),
            fpsLimit = Mathf.Max(0, PlayerPrefs.GetInt(P + "Fps", d.fpsLimit)),
            vsync = PlayerPrefs.GetInt(P + "VSync", d.vsync ? 1 : 0) != 0,
        };
    }

    private static void Save(Values v)
    {
        PlayerPrefs.SetFloat(P + "Master", v.master);
        PlayerPrefs.SetFloat(P + "Sfx", v.sfx);
        PlayerPrefs.SetFloat(P + "Bgm", v.bgm);
        PlayerPrefs.SetInt(P + "Width", v.width);
        PlayerPrefs.SetInt(P + "Height", v.height);
        PlayerPrefs.SetInt(P + "Mode", (int)v.mode);
        PlayerPrefs.SetInt(P + "Quality", v.quality);
        PlayerPrefs.SetInt(P + "Fps", v.fpsLimit);
        PlayerPrefs.SetInt(P + "VSync", v.vsync ? 1 : 0);
        PlayerPrefs.Save();
    }

    #endregion

    #region 적용

    /// <summary>게임 시작 때 한 번: 저장된 값을 전부 적용한다.</summary>
    public static void ApplyAll()
    {
        Values v = Current;
        ApplyAudio(v);
        ApplyDisplay(v);
        EnsureAudioHook();
    }

    /// <summary>설정 창 [적용]: 저장 + 적용.</summary>
    public static void Commit(Values v)
    {
        Values before = Current;
        _current = v;
        Save(v);
        ApplyAudio(v);
        bool displayChanged = before.width != v.width || before.height != v.height || before.mode != v.mode
                              || before.quality != v.quality || before.fpsLimit != v.fpsLimit || before.vsync != v.vsync;
        if (displayChanged) ApplyDisplay(v);
        Changed?.Invoke();
    }

    /// <summary>설정 창에서 슬라이더를 움직이는 동안 저장하지 않고 소리만 미리 들려준다.</summary>
    public static void PreviewAudio(float master, float sfx, float bgm)
    {
        Values v = Current;
        v.master = master;
        v.sfx = sfx;
        v.bgm = bgm;
        _preview = v;
        ApplyAudio(v);
    }

    /// <summary>미리듣기를 취소하고 저장된 볼륨으로 되돌린다.</summary>
    public static void CancelPreview()
    {
        _preview = null;
        ApplyAudio(Current);
    }

    private static Values? _preview;

    /// <summary>AudioChannel이 읽는 지금 볼륨 (미리듣기 중이면 미리듣기 값).</summary>
    public static float ChannelVolume(AudioChannel.Kind kind)
    {
        Values v = _preview ?? Current;
        return kind == AudioChannel.Kind.Bgm ? v.bgm : v.sfx;
    }

    private static void ApplyAudio(Values v)
    {
        AudioListener.volume = Mathf.Clamp01(v.master);
        AudioChannel.RefreshAll();
    }

    private static void ApplyDisplay(Values v)
    {
        if (QualitySettings.names.Length > 0 && v.quality != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(Mathf.Clamp(v.quality, 0, QualitySettings.names.Length - 1), true);
        QualitySettings.vSyncCount = v.vsync ? 1 : 0;
        Application.targetFrameRate = v.vsync || v.fpsLimit <= 0 ? -1 : v.fpsLimit;
        // (에디터 Game 뷰에서는 해상도/화면 모드가 바뀌지 않는다 - 빌드에서만 적용된다)
        Screen.SetResolution(v.width, v.height, v.mode);
    }

    #endregion

    #region 화면 선택지

    /// <summary>고를 수 있는 해상도 (모니터가 지원하는 해상도, 중복 주사율 제거 + 기본 800x800 + 지금 값).</summary>
    public static List<Vector2Int> ResolutionOptions()
    {
        var list = new List<Vector2Int>();
        void Add(int w, int h)
        {
            var r = new Vector2Int(w, h);
            if (w >= 640 && h >= 480 && !list.Contains(r)) list.Add(r);
        }
        foreach (Resolution r in Screen.resolutions) Add(r.width, r.height);
        Add(1280, 720);
        Add(1600, 900);
        Add(1920, 1080);
        Add(DefaultWidth, DefaultHeight);
        Add(Current.width, Current.height);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    public static readonly FullScreenMode[] ModeOptions =
    {
        FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed,
    };

    public static string ModeName(FullScreenMode mode)
    {
        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen: return "전체 화면";
            case FullScreenMode.FullScreenWindow: return "테두리 없는 창";
            case FullScreenMode.MaximizedWindow: return "최대화 창";
            default: return "창 모드";
        }
    }

    public static string QualityName(int index)
    {
        string[] names = QualitySettings.names;
        if (index < 0 || index >= names.Length) return "-";
        switch (names[index])
        {
            case "Very Low": return "매우 낮음";
            case "Low": return "낮음";
            case "Medium": return "중간";
            case "High": return "높음";
            case "Very High": return "매우 높음";
            case "Ultra": return "최고";
            default: return names[index];
        }
    }

    public static string FpsName(int fps) => fps <= 0 ? "무제한" : fps + " FPS";

    #endregion

    #region 씬 AudioSource 자동 분류

    private static bool _hooked;

    private static void EnsureAudioHook()
    {
        if (_hooked) return;
        _hooked = true;
        SceneManager.sceneLoaded += (scene, mode) => TagSceneAudio(scene);
        for (int i = 0; i < SceneManager.sceneCount; i++) TagSceneAudio(SceneManager.GetSceneAt(i));
    }

    private static void TagSceneAudio(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
            {
                if (source == null || source.GetComponent<AudioChannel>() != null) continue;
                var channel = source.gameObject.AddComponent<AudioChannel>();
                channel.Setup(source, source.loop && source.spatialBlend < 0.5f ? AudioChannel.Kind.Bgm : AudioChannel.Kind.Sfx);
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        _preview = null;
        _hooked = false;
        Changed = null;
    }

    #endregion
}
