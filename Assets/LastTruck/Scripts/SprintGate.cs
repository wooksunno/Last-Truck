using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 달리기 "연출"용 상태 필터. Shift를 눌렀다 뗐다 연타해도 달리기 모션/카메라 효과가 매 프레임 뒤집히지 않게 한다.
    ///
    /// - 켜질 때: Shift를 누르면 거의 바로 켠다 (방금 꺼졌다면 OnCooldown초는 기다린다).
    /// - 꺼질 때: Shift를 놓은 채로 OffDelay초가 지나야 끈다. 연타처럼 짧게 뗐다 누르는 동안은 계속 달리기 모션을 유지한다.
    /// - 한 번 바뀐 뒤 MinHold초 안에는 다시 바뀌지 않는다.
    /// - 실제 이동 속도/스태미나는 건드리지 않는다 (연타로 스태미나를 아끼는 꼼수가 되지 않게). 모션/카메라 쪽만 이 값을 쓴다.
    /// - StepAnimSpeed: 달릴 때 빨라지는 애니메이션 재생 속도를 즉시 바꾸지 않고 부드럽게 따라가게 하는 값.
    /// </summary>
    public struct SprintGate
    {
        public const float OffDelay = 0.2f;
        public const float OnCooldown = 0.12f;
        public const float MinHold = 0.25f;
        public const float AnimSpeedFast = 1.7f;
        private const float AnimSpeedChangePerSecond = 3.5f;

        private bool _on;
        private float _changedAt;     // Time.unscaledTime 기준 마지막으로 바뀐 시각 (기본값 0 = 오래전)
        private float _lastWantAt;    // 마지막으로 달리고 싶어 하던 시각
        private float _animSpeed;     // 0이면 아직 초기화 전 (1로 시작)

        public bool Active => _on;

        /// <summary>want: 지금 달리고 싶은가. 필터를 거친 달리기 상태를 돌려준다.</summary>
        public bool Update(bool want)
        {
            float now = Time.unscaledTime;
            if (want)
            {
                _lastWantAt = now;
                if (!_on && now - _changedAt >= OnCooldown)
                {
                    _on = true;
                    _changedAt = now;
                }
            }
            else if (_on && now - _lastWantAt >= OffDelay && now - _changedAt >= MinHold)
            {
                _on = false;
                _changedAt = now;
            }
            return _on;
        }

        /// <summary>달릴 때 애니메이션 재생 속도 (1 ~ AnimSpeedFast 사이를 부드럽게).</summary>
        public float StepAnimSpeed(bool sprintAnim, float dt)
        {
            if (_animSpeed <= 0f) _animSpeed = 1f;
            float target = sprintAnim ? AnimSpeedFast : 1f;
            _animSpeed = Mathf.MoveTowards(_animSpeed, target, AnimSpeedChangePerSecond * dt);
            return _animSpeed;
        }

        public void Reset()
        {
            _on = false;
            _changedAt = 0f;
            _lastWantAt = 0f;
            _animSpeed = 1f;
        }
    }
}
