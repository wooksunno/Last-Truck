using Unity.Cinemachine;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 철도 레일/자갈길처럼 평평하지 않은 곳을 걸을 때 캐릭터의 높이(Y)가 잘게 튀면서 카메라가 같이 흔들리는 것을 막는다.
    /// 시네머신이 따라가는 대상을 부드러운 '중간 대상'으로 바꿔 두고, 가로(X/Z)는 그대로, 세로(Y)만 천천히 따라가게 한다.
    /// 트럭 탑승/하차처럼 다른 스크립트가 TrackingTarget을 바꿔도 알아서 새 대상을 이어받는다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(CinemachineCamera))]
    public class CameraTargetSmoother : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera cam;
        [Tooltip("높이(Y)를 따라가는 데 걸리는 시간(초). 클수록 부드럽지만 언덕에서 카메라가 늦게 올라온다")]
        [SerializeField] private float verticalSmoothTime = 0.22f;
        [Tooltip("이보다 큰 높이 차이(텔레포트, 탑승 등)는 부드럽게 따라가지 않고 즉시 맞춘다(m)")]
        [SerializeField] private float snapDistance = 3f;

        private Transform _proxy;
        private Transform _real;
        private float _velocityY;

        private void Awake()
        {
            if (cam == null)
                cam = GetComponent<CinemachineCamera>();

            var go = new GameObject("CameraTargetProxy");
            _proxy = go.transform;
        }

        private void LateUpdate()
        {
            if (cam == null || _proxy == null)
                return;

            Transform current = cam.Target.TrackingTarget;
            if (current != _proxy)
            {
                // 다른 스크립트(탑승/하차 등)가 대상을 바꿨다 → 새 대상을 기억하고 위치를 즉시 맞춘다
                _real = current;
                _velocityY = 0f;
                if (_real != null)
                    _proxy.position = _real.position;
                cam.Target.TrackingTarget = _proxy;
            }

            if (_real == null)
                return;

            Vector3 p = _real.position;
            float y = _proxy.position.y;
            if (Mathf.Abs(p.y - y) > snapDistance)
            {
                y = p.y;
                _velocityY = 0f;
            }
            else
            {
                y = Mathf.SmoothDamp(y, p.y, ref _velocityY, verticalSmoothTime);
            }

            _proxy.position = new Vector3(p.x, y, p.z);
        }

        private void OnDisable()
        {
            // 이 컴포넌트를 끄면 원래 대상을 시네머신에 돌려준다
            if (cam != null && _proxy != null && cam.Target.TrackingTarget == _proxy && _real != null)
                cam.Target.TrackingTarget = _real;
            _real = null;
        }

        private void OnDestroy()
        {
            if (_proxy != null)
                Destroy(_proxy.gameObject);
        }
    }
}
