using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 복서 기절 연출. 기절한 몬스터의 외곽선이 보라색으로 깜빡깜빡 빛난다 (기절이 끝나면 사라짐).
    /// 기절을 건 복서 화면에서 보인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StunRimVisual : RimHighlightVisual
    {
        private static readonly Color StunColor = new Color(0.72f, 0.22f, 1f, 1f);   // 보라색
        private const float BlinkPerSecond = 3f;   // 초당 깜빡임 횟수

        /// <summary>기절 연출 시작/연장 (duration: 기절 시간 초).</summary>
        public static void Show(Component target, float duration)
        {
            if (duration <= 0f) return;
            StunRimVisual visual = Find<StunRimVisual>(target, true);
            if (visual != null) visual.Begin(Time.time + duration);
        }

        /// <summary>기절 연출 즉시 끄기 (몬스터 사망 등).</summary>
        public static void Hide(Component target)
        {
            StunRimVisual visual = Find<StunRimVisual>(target, false);
            if (visual != null) visual.Stop();
        }

        protected override void Configure(Material material)
        {
            // 가장자리만 얇고 진하게 = 외곽선 느낌. 맥동 대신 Animate에서 켜졌다 꺼졌다 한다.
            material.SetColor(RimColorId, StunColor);
            material.SetFloat(IntensityId, 2.6f);
            material.SetFloat(RimPowerId, 3.2f);
            material.SetFloat(FillId, 0f);
            material.SetFloat(PulseSpeedId, 0f);
            material.SetFloat(PulseMinId, 1f);
        }

        protected override void Animate(Material material)
        {
            // 또렷한 깜빡임: 70% 켜짐 / 30% 꺼짐
            float phase = Mathf.Repeat(Time.time * BlinkPerSecond, 1f);
            material.SetFloat(IntensityId, phase < 0.7f ? 2.6f : 0f);
        }
    }
}
