using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 이 컴포넌트가 있는 씬에서만 FPS를 모니터 주사율(알 수 없으면 60)로 제한한다.
    /// 제한이 없으면 GPU가 1초에 수백 장을 그리느라 팬이 크게 돈다.
    /// 씬을 떠나면(파괴되면) 원래 값으로 되돌려서 다른 씬에는 영향을 주지 않는다.
    /// 참고: vSync(QualitySettings.vSyncCount)가 켜진 품질 단계에서는 Unity가 이 값을 무시하고 vSync를 따른다.
    /// </summary>
    public class FrameRateLimiter : MonoBehaviour
    {
        private int _previous;

        // 에디터에서 이 씬을 플레이하다 멈추면 OnDisable이 되돌린 뒤에도 Unity가 값을 제한된 값으로 다시 바꿔 놓는다.
        // 그러면 다음에 다른 씬을 플레이할 때도 제한이 남으므로, 게임(플레이)이 시작될 때마다 먼저 '제한 없음'으로 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOnStart()
        {
            Application.targetFrameRate = -1;
        }

        private void OnEnable()
        {
            _previous = Application.targetFrameRate;
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = hz > 0 ? hz : 60;
        }

        private void OnDisable()
        {
            Application.targetFrameRate = _previous;
        }
    }
}
