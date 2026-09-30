using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LastTruck
{
    public class StatWindowController : MonoBehaviour
    {
        [Header("차트 연결")]
        [SerializeField] private RadarChartUI radarChart;

        [Header("폰트 및 텍스트 연결")]
        [SerializeField] private Font uiFont;
        [SerializeField] private Text[] statLabels;

        [Header("애니메이션 설정")]
        [SerializeField] private float animationDuration = 0.6f;
        [SerializeField] private Ease easeType = Ease.OutBack;

        [Header("최대 스탯 기준치")]
        public float maxHP = 500f;
        public float maxAttack = 100f;
        public float maxMoveSpeed = 10f;
        public float maxAttackSpeed = 3f;
        public float maxGatherSpeed = 5f;

        private Tween statTween;

        private void Awake()
        {
            ApplyFontToLabels();
        }

        private void OnEnable()
        {
            AnimateStatChart();
        }

        private void OnDisable()
        {
            statTween?.Kill();
        }

        private void ApplyFontToLabels()
        {
            if (uiFont == null) return;

            if (statLabels != null && statLabels.Length > 0)
            {
                foreach (var label in statLabels)
                {
                    if (label != null) label.font = uiFont;
                }
            }
            else
            {
                Text[] allTexts = GetComponentsInChildren<Text>(true);
                foreach (var textComp in allTexts)
                {
                    textComp.font = uiFont;
                }
            }
        }

        public void AnimateStatChart()
        {
            if (radarChart == null) return;

            float targetHP = 350f;
            float targetAtk = 80f;
            float targetMove = 7f;
            float targetAtkSpeed = 2f;
            float targetGather = 4f;

            UpdateLabelTexts(targetHP, targetAtk, targetMove, targetAtkSpeed, targetGather);

            RadarChartUI.StatValues targetStats = new RadarChartUI.StatValues(
                targetHP / maxHP,
                targetAtk / maxAttack,
                targetMove / maxMoveSpeed,
                targetAtkSpeed / maxAttackSpeed,
                targetGather / maxGatherSpeed
            );

            radarChart.SetStatValues(new RadarChartUI.StatValues(0, 0, 0, 0, 0));

            statTween?.Kill();
            statTween = DOVirtual.Float(0f, 1f, animationDuration, progress =>
            {
                RadarChartUI.StatValues animatedStats = new RadarChartUI.StatValues(
                    Mathf.Lerp(0, targetStats.hp, progress),
                    Mathf.Lerp(0, targetStats.attack, progress),
                    Mathf.Lerp(0, targetStats.moveSpeed, progress),
                    Mathf.Lerp(0, targetStats.attackSpeed, progress),
                    Mathf.Lerp(0, targetStats.gatherSpeed, progress)
                );

                radarChart.SetStatValues(animatedStats);
            }).SetEase(easeType);
        }

        private void UpdateLabelTexts(float hp, float atk, float move, float atkSpd, float gatherSpd)
        {
            if (statLabels != null && statLabels.Length >= 5)
            {
                if (statLabels[0] != null) statLabels[0].text = $"체력\n{hp}/{maxHP}";
                if (statLabels[1] != null) statLabels[1].text = $"공격력\n{atk}";
                if (statLabels[2] != null) statLabels[2].text = $"이동속도\n{move}";
                if (statLabels[3] != null) statLabels[3].text = $"공격속도\n{atkSpd}";
                if (statLabels[4] != null) statLabels[4].text = $"채집속도\n{gatherSpd}";
            }
        }
    }
}