using Unity.Cinemachine;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 카메라를 아래로 끝까지 내리면(궤도 세로 각도가 0도 아래) 하늘을 올려다볼 수 있게 한다.
    ///  - 궤도가 땅 밑으로 들어가지 않게 카메라를 지면 위(clearance)로 올린다.
    ///  - 각도가 낮을수록 시선을 위로 더 들어 올려(최대 maxExtraPitch도) 플레이어 위로 하늘이 보이게 한다.
    /// CinemachineOrbitalFollow가 있는 가상 카메라에 붙인다 (VerticalAxis.Range 최소값을 음수로 두어야 한다).
    /// </summary>
    [AddComponentMenu("Cinemachine/Extensions/Camera Sky Look")]
    public class CameraSkyLook : CinemachineExtension
    {
        [Tooltip("세로 각도가 이 값보다 낮아지면 시선을 들기 시작한다 (도).")]
        [SerializeField] private float startAngle = 5f;
        [Tooltip("이 각도에서 시선이 가장 많이 들린다 (도, 궤도 최소 각도와 맞춘다).")]
        [SerializeField] private float fullAngle = -35f;
        [Tooltip("가장 낮은 각도에서 추가로 위를 보는 각도 (도).")]
        [SerializeField] private float maxExtraPitch = 32f;
        [Tooltip("카메라가 지면에서 떨어져 있어야 하는 높이 (m).")]
        [SerializeField] private float clearance = 0.45f;

        private CinemachineOrbitalFollow _orbital;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private float Low01(CinemachineVirtualCameraBase vcam)
        {
            if (_orbital == null) _orbital = vcam.GetComponent<CinemachineOrbitalFollow>();
            if (_orbital == null) return 0f;
            float v = _orbital.VerticalAxis.Value;
            return Mathf.Clamp01((startAngle - v) / Mathf.Max(0.01f, startAngle - fullAngle));
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (vcam.Follow == null) return;

            if (stage == CinemachineCore.Stage.Body)
            {
                // 지면 아래로 내려가지 않게: 카메라 바로 위에서 아래로 쏴서 땅 높이를 찾는다
                Vector3 cam = state.GetCorrectedPosition();
                float top = Mathf.Max(cam.y, vcam.Follow.position.y) + 3f;
                Vector3 from = new Vector3(cam.x, top, cam.z);
                int n = Physics.RaycastNonAlloc(from, Vector3.down, _hits, top - cam.y + 6f, ~0, QueryTriggerInteraction.Ignore);
                float ground = float.MinValue;
                Transform followRoot = vcam.Follow.root;
                for (int i = 0; i < n; i++)
                {
                    Collider c = _hits[i].collider;
                    if (c.transform.IsChildOf(followRoot)) continue;           // 플레이어 자신
                    if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;   // 굴러다니는 물체/트럭
                    if (c.GetComponentInParent<Combat.Damageable>() != null) continue;   // 몬스터
                    // 카메라보다 3m 넘게 위에 있는 지붕/나뭇가지는 땅이 아니다
                    if (_hits[i].point.y > vcam.Follow.position.y + 1.2f) continue;
                    if (_hits[i].point.y > ground) ground = _hits[i].point.y;
                }
                if (ground > float.MinValue && cam.y < ground + clearance)
                    state.PositionCorrection += Vector3.up * (ground + clearance - cam.y);
            }
            else if (stage == CinemachineCore.Stage.Aim)
            {
                // 낮게 내려갈수록 시선을 위로 든다
                float t = Low01(vcam);
                if (t <= 0f) return;
                t = t * t * (3f - 2f * t);
                Quaternion q = state.GetCorrectedOrientation();
                Quaternion lifted = Quaternion.AngleAxis(-maxExtraPitch * t, q * Vector3.right) * q;
                state.OrientationCorrection = state.OrientationCorrection * (Quaternion.Inverse(q) * lifted);
            }
        }
    }
}
