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
            private readonly List<float> _zones = new();
            private float _target = float.PositiveInfinity;
            private float _current = float.PositiveInfinity;
            private float _velocity;

            public void Add(float v) { _zones.Add(v); Refresh(); }
            public void Remove(float v) { _zones.Remove(v); Refresh(); }
            public void Clear() { _zones.Clear(); Refresh(); }

            private void Refresh()
            {
                float min = float.PositiveInfinity;
                foreach (var z in _zones) min = Mathf.Min(min, z);
                _target = min;
            }

            /// <summary>현재 값(value)을 받아 이번 프레임에 적용할 제한값을 반환. 제한 없음이면 Infinity</summary>
            public float Evaluate(float value, float smoothTime, float deltaTime)
            {
                float dt = Mathf.Max(deltaTime, 0f);

                if (!float.IsInfinity(_target))
                {
                    // 처음 걸릴 때는 현재 값에서 시작해서 부드럽게 당긴다
                    if (float.IsInfinity(_current)) _current = value;
                    _current = Mathf.SmoothDamp(_current, _target, ref _velocity, smoothTime, Mathf.Infinity, dt);
                }
                else if (!float.IsInfinity(_current))
                {
                    // 풀릴 때: 제한값이 현재 값을 넘어서면 완전히 해제
                    _current = Mathf.SmoothDamp(_current, value + 1f, ref _velocity, smoothTime, Mathf.Infinity, dt);
                    if (_current >= value)
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

        /// <summary>리스폰/씬 이동 등으로 구역 상태를 초기화할 때 사용</summary>
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
