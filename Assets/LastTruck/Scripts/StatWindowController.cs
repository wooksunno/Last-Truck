using UnityEngine;
using UnityEngine.UI; // 기본 UI Text 사용을 위해 추가
using DG.Tweening;

namespace LastTruck
{
    public class StatWindowController : MonoBehaviour
    {
        [Header("차트 연결")]
        [SerializeField] private RadarChartUI radarChart;

        [Header("폰트 및 텍스트 연결")]
        [SerializeField] private Font uiFont; // 지정할 폰트 에셋
        [SerializeField] private Text[] statLabels; // 스탯 라벨 텍스트 5개 (체력, 공격력 등)

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
            // 스탯창이 로드될 때 폰트 일괄 적용
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

        // uiFont 변수에 할당된 폰트를 라벨들에 한 번에 적용하는 메서드
        private void ApplyFontToLabels()
        {
            if (uiFont == null) return;

            // 1. 인스펙터에 수동으로 연결한 statLabels 배열이 있는 경우
            if (statLabels != null && statLabels.Length > 0)
            {
                foreach (var label in statLabels)
                {
                    if (label != null) label.font = uiFont;
                }
            }
            // 2. 배열을 비워둔 경우, 스탯창 자식에 있는 모든 Text 컴포넌트를 찾아서 폰트 자동 적용
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

            // [예시 수치] 실제 게임 구현 시 플레이어 스탯을 전달받도록 연결
            float targetHP = 350f;
            float targetAtk = 80f;
            float targetMove = 7f;
            float targetAtkSpeed = 2f;
            float targetGather = 4f;

            // 라벨 텍스트 내용 갱신
            UpdateLabelTexts(targetHP, targetAtk, targetMove, targetAtkSpeed, targetGather);

            // 비율 계산 (0~1 범위)
            RadarChartUI.StatValues targetStats = new RadarChartUI.StatValues(
                targetHP / maxHP,
                targetAtk / maxAttack,
                targetMove / maxMoveSpeed,
                targetAtkSpeed / maxAttackSpeed,
                targetGather / maxGatherSpeed
            );

            // 0에서부터 시작
            radarChart.SetStatValues(new RadarChartUI.StatValues(0, 0, 0, 0, 0));

            // DOTween으로 스무스하게 뻗어나가는 연출
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
            // statLabels 배열 순서: [0] 체력, [1] 공격력, [2] 이동속도, [3] 공격속도, [4] 채집속도
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