using UnityEngine;

/// <summary>
/// 동굴 장식용 은은한 숨쉬기 효과: 빛은 세기가 일렁이고, 선(에너지 줄기)은 굵기가 맥박처럼 변한다.
/// (맵 꾸미기 스크립트가 붙여 둔다. 게임 플레이에는 영향이 없다.)
/// </summary>
public class CaveGlowPulse : MonoBehaviour
{
    [SerializeField] private float speed = 2.2f;
    [SerializeField, Range(0f, 0.9f)] private float amount = 0.25f;
    [SerializeField] private float phase;

    private Light _light;
    private LineRenderer _line;
    private float _baseIntensity, _baseWidth;

    public void Configure(float pulseSpeed, float pulseAmount, float pulsePhase)
    {
        speed = pulseSpeed; amount = pulseAmount; phase = pulsePhase;
    }

    private void Awake()
    {
        _light = GetComponent<Light>();
        _line = GetComponent<LineRenderer>();
        if (_light != null) _baseIntensity = _light.intensity;
        if (_line != null) _baseWidth = _line.widthMultiplier;
    }

    private void Update()
    {
        float k = 1f + amount * Mathf.Sin(Time.time * speed + phase) + amount * 0.4f * Mathf.Sin(Time.time * speed * 2.7f + phase * 1.7f);
        if (_light != null) _light.intensity = _baseIntensity * k;
        if (_line != null) _line.widthMultiplier = _baseWidth * Mathf.Max(0.3f, k);
    }
}
