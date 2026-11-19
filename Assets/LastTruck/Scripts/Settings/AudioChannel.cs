using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AudioSource에 붙여서 설정의 효과음/배경음 볼륨을 곱해 주는 컴포넌트.
/// 직접 붙여도 되고, 안 붙인 AudioSource는 GameSettings가 씬을 불러올 때 자동으로 붙인다.
/// 볼륨은 이 컴포넌트가 처음 본 AudioSource.volume을 기준값으로 기억하고 (기준값 × 채널 볼륨)으로 맞춘다.
/// </summary>
[DisallowMultipleComponent]
public class AudioChannel : MonoBehaviour
{
    public enum Kind { Sfx, Bgm }

    [SerializeField] private Kind kind = Kind.Sfx;
    [SerializeField] private AudioSource source;
    [Tooltip("채널 볼륨을 곱하기 전 원래 볼륨 (-1 = 처음 본 AudioSource.volume)")]
    [SerializeField] private float baseVolume = -1f;

    private static readonly List<AudioChannel> Active = new List<AudioChannel>();

    public Kind Channel => kind;

    public void Setup(AudioSource target, Kind channel)
    {
        source = target;
        kind = channel;
        baseVolume = target != null ? target.volume : 1f;
        Refresh();
    }

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (baseVolume < 0f) baseVolume = source != null ? source.volume : 1f;
    }

    private void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        Refresh();
    }

    private void OnDisable() => Active.Remove(this);

    public void Refresh()
    {
        if (source == null) return;
        source.volume = Mathf.Max(0f, baseVolume) * GameSettings.ChannelVolume(kind);
    }

    public static void RefreshAll()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i] == null) Active.RemoveAt(i);
            else Active[i].Refresh();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Active.Clear();
}
