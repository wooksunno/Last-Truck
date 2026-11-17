using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 경찰 표식 연출 (경찰 고유 능력). 표식이 걸린 몬스터의 몸 가장자리가 노랗게 맥동하며 빛난다.
    /// 표식이 있는 적을 때려 추가 피해가 나면 잠깐 하얗게 번쩍인다.
    /// 표식은 때린 경찰 화면에서만 계산되므로 연출도 경찰 화면에만 보인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PoliceMarkVisual : RimHighlightVisual
    {
        private static readonly Color MarkColor = new Color(1f, 0.82f, 0.18f, 1f);
        private static readonly Color FlashColor = new Color(1f, 0.97f, 0.75f, 1f);
        private const float NormalIntensity = 1.6f;
        private const float FlashIntensity = 4.5f;
        private const float FlashSeconds = 0.18f;

        private float _flashUntil;

        /// <summary>표식 연출 시작/연장 (until: 표식이 끝나는 Time.time).</summary>
        public static void Show(Component target, float until)
        {
            PoliceMarkVisual visual = Find<PoliceMarkVisual>(target, true);
            if (visual != null) visual.Begin(until);
        }

        /// <summary>표식 추가 피해가 들어갔을 때 잠깐 번쩍.</summary>
        public static void Flash(Component target)
        {
            PoliceMarkVisual visual = Find<PoliceMarkVisual>(target, false);
            if (visual != null && visual.Attached) visual._flashUntil = Time.time + FlashSeconds;
        }

        /// <summary>표식 연출 즉시 끄기 (몬스터 사망 등).</summary>
        public static void Hide(Component target)
        {
            PoliceMarkVisual visual = Find<PoliceMarkVisual>(target, false);
            if (visual != null) visual.Stop();
        }

        protected override void Configure(Material material)
        {
            material.SetColor(RimColorId, MarkColor);
            material.SetFloat(IntensityId, NormalIntensity);
            material.SetFloat(RimPowerId, 2.2f);
            material.SetFloat(FillId, 0.08f);
            material.SetFloat(PulseSpeedId, 6f);
            material.SetFloat(PulseMinId, 0.55f);
        }

        protected override void Animate(Material material)
        {
            bool flashing = Time.time < _flashUntil;
            material.SetFloat(IntensityId, flashing ? FlashIntensity : NormalIntensity);
            material.SetColor(RimColorId, flashing ? FlashColor : MarkColor);
        }
    }
}
