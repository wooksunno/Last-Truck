using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 특정 구역(동굴 등)에 있는 동안 카메라의 최대 거리와 최대 높이를 제한한다.
    /// - 거리: 피벗(머리 높이) 기준으로 최대 거리를 넘지 않게 당긴다.
    /// - 높이: 캐릭터 발밑 기준으로 최대 높이를 넘을 때만 아래로 내린다. 아래로는 자유롭게 내릴 수 있다.
    /// 줌/회전 방식과 상관없이 최종 위치를 보정한다.
    ///
    /// 구역(CameraZone)은 매 프레임 "카메라가 따라가는 캐릭터가 지금 안에 있는가"로 판단한다.
    /// (들어감/나감 이벤트를 세지 않으므로 트럭 탑승, 순간이동, 캐릭터 교체, 다른 플레이어 등으로 줌이 남지 않는다)
    /// 코드에서 직접 거는 제한(EnterZone/ExitZone)도 그대로 쓸 수 있다.
    /// </summary>
    [AddComponentMenu("Cinemachine/Extensions/Camera Distance Limiter")]
    public class CameraDistanceLimiter : CinemachineExtension
    {
        public static CameraDistanceLimiter Instance { get; private set; }

        [Tooltip("거리 계산 기준점 높이 (머리 높이). 최대 높이가 아님! CaveCameraCollision과 같은 값")]
        [SerializeField] private float pivotHeight = 1.5f;
        [Tooltip("제한이 걸리거나 풀릴 때 걸리는 시간(초)")]
        [SerializeField] private float transitionTime = 0.4f;

        /// <summary>제한값을 부드럽게 걸고 푸는 공용 로직</summary>
        private class SmoothLimit
        {
            private readonly List<float> _manual = new();   // EnterZone/ExitZone으로 코드에서 건 제한
            private float _zoneLimit = float.PositiveInfinity; // 매 프레임 CameraZone에서 계산한 제한
            private float _current = float.PositiveInfinity;
            private float _velocity;

            public void Add(float v) => _manual.Add(v);
            public void Remove(float v) => _manual.Remove(v);
            public void Clear() => _manual.Clear();
            public void SetZoneLimit(float v) => _zoneLimit = v > 0f ? v : float.PositiveInfinity;

            private float Target
            {
                get
                {
                    float min = _zoneLimit;
                    foreach (float z in _manual)
                        if (z > 0f) min = Mathf.Min(min, z);
                    return min;
                }
            }

            /// <summary>현재 값(value)을 받아 이번 프레임에 적용할 제한값을 반환. 제한 없음이면 Infinity</summary>
            public float Evaluate(float value, float smoothTime, float deltaTime)
            {
                float target = Target;

                // 카메라 컷/첫 프레임(deltaTime < 0): 부드럽게 하지 않고 바로 맞춘다
                if (deltaTime < 0f)
                {
                    _current = target;
                    _velocity = 0f;
                    return _current;
                }
                // 일시정지 등으로 시간이 멈춘 프레임: 지금 값 유지
                if (deltaTime == 0f)
                    return float.IsInfinity(_current) ? target : _current;

                if (!float.IsInfinity(target))
                {
                    // 처음 걸릴 때는 현재 값에서 시작해서 부드럽게 당긴다
                    if (float.IsInfinity(_current) || float.IsNaN(_current)) { _current = value; _velocity = 0f; }
                    _current = Mathf.SmoothDamp(_current, target, ref _velocity, smoothTime, Mathf.Infinity, deltaTime);
                }
                else if (!float.IsInfinity(_current))
                {
                    // 풀릴 때: 제한값이 현재 값을 넘어서면 완전히 해제
                    _current = Mathf.SmoothDamp(_current, value + 1f, ref _velocity, smoothTime, Mathf.Infinity, deltaTime);
                    if (float.IsNaN(_current) || _current >= value)
                    {
                        _current = float.PositiveInfinity;
                        _velocity = 0f;
                    }
                }
                return _current;
            }
        }

        private readonly SmoothLimit _distance = new();
        private readonly SmoothLimit _height = new();

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        /// <param name="maxDistance">피벗 기준 최대 거리</param>
        /// <param name="maxHeight">발밑 기준 최대 높이 (0 이하이면 높이 제한 안 함)</param>
        public void EnterZone(float maxDistance, float maxHeight)
        {
            _distance.Add(maxDistance);
            if (maxHeight > 0f) _height.Add(maxHeight);
        }

        public void ExitZone(float maxDistance, float maxHeight)
        {
            _distance.Remove(maxDistance);
            if (maxHeight > 0f) _height.Remove(maxHeight);
        }

        /// <summary>리스폰/씬 이동 등으로 코드에서 건 제한을 모두 지울 때 사용 (CameraZone 구역은 자동으로 다시 계산된다)</summary>
        public void ClearZones()
        {
            _distance.Clear();
            _height.Clear();
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Body || vcam.Follow == null) return;

            Vector3 feet = vcam.Follow.position;
            Vector3 pivot = feet + Vector3.up * pivotHeight;

            // 0) 카메라가 따라가는 캐릭터가 들어가 있는 동굴 구역의 제한값 (구역 밖이면 Infinity)
            CameraZone.TryGetLimits(feet, out float zoneDistance, out float zoneHeight, out _);
            _distance.SetZoneLimit(zoneDistance);
            _height.SetZoneLimit(zoneHeight);

            // 1) 거리 제한
            Vector3 camPos = state.GetCorrectedPosition();
            Vector3 toCam = camPos - pivot;
            float dist = toCam.magnitude;
            if (dist > 0.001f)
            {
                float limit = _distance.Evaluate(dist, transitionTime, deltaTime);
                if (dist > limit)
                {
                    Vector3 limited = pivot + toCam / dist * limit;
                    state.PositionCorrection += limited - camPos;
                }
            }

            // 2) 높이 제한 (위로 넘칠 때만 아래로 내림)
            camPos = state.GetCorrectedPosition();
            float camHeight = camPos.y - feet.y;
            float maxH = _height.Evaluate(camHeight, transitionTime, deltaTime);
            if (camHeight > maxH)
            {
                state.PositionCorrection += Vector3.down * (camHeight - maxH);
            }
        }
    }
}
