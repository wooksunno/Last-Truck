using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 트럭에 탄 캐릭터를 좌석에 "앉은 모습"으로 보이게 한다.
    ///
    ///  - 이 프로젝트의 캐릭터 애니메이션에는 앉는 모션이 없어서, 애니메이터가 평소 자세를 만든 뒤(LateUpdate)
    ///    다리를 앞으로 접고(허벅지 앞 / 종아리 아래) 팔을 앞으로 뻗는(운전자는 핸들을 잡은 모습) 자세를 덧씌운다.
    ///  - 캐릭터의 위치(뿌리)는 "엉덩이가 놓일 자리"(트럭의 SeatPose_N)이고, 엉덩이 뼈를 서 있을 때의 엉덩이 높이만큼 내려서 엉덩이가 좌석에 닿게 한다.
    ///  - 싱글플레이는 TruckInteractable이 Sit/Stand를 부르고, 멀티플레이는 NetworkPlayer가 매 프레임 Apply로 상태를 넘긴다.
    ///  - 앉아 있는 동안 몬스터가 캐릭터를 노리지 않게 태그를 잠깐 뗀다 (몬스터는 트럭을 공격한다). Sit(disableControls)이면
    ///    조작 스크립트(이동/공격/상호작용 등)도 꺼 두고 내릴 때 켠다.
    /// </summary>
    [DefaultExecutionOrder(500)]   // 애니메이터와 다른 LateUpdate(무기 자세 등)가 끝난 뒤에 덮어쓴다
    public class SeatedPose : MonoBehaviour
    {
        [Header("앉은 자세 (도)")]
        [SerializeField] private float thighFlex = 88f;        // 허벅지를 앞으로 드는 각도
        [SerializeField] private float driverArmRaise = 55f;   // 운전자: 윗팔을 앞으로
        [SerializeField] private float driverForearmRaise = 38f;
        [SerializeField] private float passengerArmRaise = 25f;  // 동승자: 손을 무릎 쪽에
        [SerializeField] private float passengerForearmRaise = 55f;
        [SerializeField] private float blendSeconds = 0.18f;
        [Tooltip("앉았을 때 몸 크기 배율 (머리가 큰 캐릭터가 운전실 지붕을 뚫지 않게 조금 줄인다)")]
        [Range(0.5f, 1f)] [SerializeField] private float seatedScale = 0.68f;

        // 싱글플레이에서 앉는 동안 끄는 조작 스크립트 (PlayerStats가 기절할 때 끄는 것과 같다)
        private static readonly string[] ControlTypeNames =
        {
            "PlayerMove", "PlayerInteract", "PlayerInventory", "WeaponController", "PlayerAttack",
            "EatController", "TrapController", "PouchController",
        };

        private Animator _anim;
        private float _hipHeight = -1f;

        private bool _seated;
        private bool _driver;
        private float _blend;
        private bool _bodyHidden;
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();

        private bool _controlsDisabled;
        private readonly List<Behaviour> _disabledControls = new List<Behaviour>();
        private string _savedTag;

        public bool IsSeated => _seated;

        public static SeatedPose Ensure(GameObject player)
        {
            var pose = player.GetComponent<SeatedPose>();
            return pose != null ? pose : player.AddComponent<SeatedPose>();
        }

        // ---------------------------------------------------------------- 상태

        /// <summary>싱글플레이: 앉는다. disableControls면 조작 스크립트를 끄고 몬스터가 노리지 못하게 태그를 뗀다.</summary>
        public void Sit(bool driver, bool disableControls)
        {
            _seated = true;
            _driver = driver;
            if (disableControls) DisableControls();
            ResetLocomotion();

            // 보간(Interpolate)이 켜진 리지드바디는 부모(트럭)가 움직여도 예전 위치로 끌려가서 좌석에서 벗어난다 → 앉아 있는 동안만 끈다
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && !_rbInterpolationSaved)
            {
                _rbInterpolationSaved = true;
                _savedInterpolation = rb.interpolation;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        private bool _rbInterpolationSaved;
        private RigidbodyInterpolation _savedInterpolation;

        /// <summary>달리기/점프 상태 변수를 꺼서 애니메이터가 서 있는 평소 자세로 돌아가게 한다 (그 위에 앉은 자세를 덧씌운다).</summary>
        private void ResetLocomotion()
        {
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null) return;
            foreach (AnimatorControllerParameter p in _anim.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                if (p.name == "isRun" || p.name == "isWalk" || p.name == "isSprint" || p.name == "isJumping" || p.name == "isFalling")
                    _anim.SetBool(p.name, false);
            }
            _anim.speed = 1f;
        }

        /// <summary>싱글플레이: 일어선다 (끈 조작 스크립트/태그를 되돌린다).</summary>
        public void Stand()
        {
            _seated = false;
            SetBodyVisible(true);
            RestoreControls();

            if (_rbInterpolationSaved)
            {
                _rbInterpolationSaved = false;
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null) rb.interpolation = _savedInterpolation;
            }
        }

        /// <summary>멀티플레이: 지금 상태를 알린다 (매 프레임 불러도 된다). seated가 아니면 일어선 것.</summary>
        public void Apply(bool seated, bool driver)
        {
            _seated = seated;
            _driver = driver;
        }

        // ---------------------------------------------------------------- 포탑 사수

        [Header("포탑 사수 자세 (도)")]
        [SerializeField] private float gunnerArmRaise = 62f;      // 윗팔을 앞으로 (손잡이를 잡은 모습)
        [SerializeField] private float gunnerForearmRaise = 28f;
        [SerializeField] private float gunnerLean = 10f;          // 상체를 포신 쪽으로 살짝 숙인다
        [SerializeField] private float gunnerKnee = 14f;          // 무릎을 살짝 굽혀 버틴다

        private bool _gunner;
        private float _gunnerBlend;

        /// <summary>
        /// 멀티플레이: 포탑 위에 서 있는가 (매 프레임 불러도 된다).
        /// 켜져 있으면 매 프레임 포탑 받침 위 자리로 몸을 붙이고(호스트 틱마다 순간이동하던 떨림/미끄러짐 제거),
        /// 차렷 자세 대신 손잡이를 잡고 포신 쪽으로 몸을 숙인 자세를 덧씌운다.
        /// </summary>
        public void ApplyGunner(bool gunner)
        {
            if (gunner && !_gunner) ResetLocomotion();
            _gunner = gunner;
        }

        private void SnapToTurret()
        {
            CraftingSystem.TruckTurret turret = CraftingSystem.TruckTurret.Instance;
            if (turret == null) return;
            Vector3 pos; Quaternion rot;
            if (turret.TryGetStandPose(out pos, out rot)) transform.SetPositionAndRotation(pos, rot);
        }

        private void ApplyGunnerBones(float w)
        {
            Vector3 right = transform.right;
            Transform spine = Bone(HumanBodyBones.Spine);
            if (spine != null) spine.rotation = Quaternion.AngleAxis(gunnerLean * w, right) * spine.rotation;

            Raise(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), right, gunnerArmRaise * w, gunnerForearmRaise * w);
            Raise(Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), right, gunnerArmRaise * w, gunnerForearmRaise * w);

            // 무릎: 허벅지는 앞으로, 종아리는 뒤로 같은 만큼 → 발은 그대로 두고 살짝 굽힌다
            float k = gunnerKnee * w;
            Bend(Bone(HumanBodyBones.LeftUpperLeg), Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.LeftFoot), right, k);
            Bend(Bone(HumanBodyBones.RightUpperLeg), Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), right, k);
        }

        private static void Bend(Transform upper, Transform lower, Transform foot, Vector3 right, float degrees)
        {
            if (upper == null || lower == null) return;
            Quaternion footWorld = foot != null ? foot.rotation : Quaternion.identity;
            upper.rotation = Quaternion.AngleAxis(-degrees, right) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis(degrees * 2f, right) * lower.rotation;
            if (foot != null) foot.rotation = footWorld;
        }

        /// <summary>1인칭 포탑 등에서 몸을 잠깐 숨긴다 / 다시 보인다.</summary>
        public void SetBodyVisible(bool visible)
        {
            if (visible)
            {
                if (!_bodyHidden) return;
                _bodyHidden = false;
                foreach (Renderer r in _hiddenRenderers) if (r != null) r.enabled = true;
                _hiddenRenderers.Clear();
                return;
            }

            if (_bodyHidden) return;
            _bodyHidden = true;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled) { r.enabled = false; _hiddenRenderers.Add(r); }
        }

        private void DisableControls()
        {
            if (_controlsDisabled) return;
            _controlsDisabled = true;

            foreach (MonoBehaviour b in GetComponents<MonoBehaviour>())
            {
                if (b == null || b == this || !b.enabled) continue;
                if (System.Array.IndexOf(ControlTypeNames, b.GetType().Name) < 0) continue;
                b.enabled = false;
                _disabledControls.Add(b);
            }

            _savedTag = gameObject.tag;
            gameObject.tag = "Untagged";
        }

        private void RestoreControls()
        {
            if (!_controlsDisabled) return;
            _controlsDisabled = false;

            foreach (Behaviour b in _disabledControls) if (b != null) b.enabled = true;
            _disabledControls.Clear();
            if (_savedTag != null) { gameObject.tag = _savedTag; _savedTag = null; }
        }

        // ---------------------------------------------------------------- 자세

        private void LateUpdate()
        {
            if (_gunner) SnapToTurret();
            _gunnerBlend = blendSeconds > 0f ? Mathf.MoveTowards(_gunnerBlend, _gunner ? 1f : 0f, Time.deltaTime / blendSeconds) : (_gunner ? 1f : 0f);
            if (_gunnerBlend > 0f)
            {
                if (_anim == null) _anim = GetComponentInChildren<Animator>();
                if (_anim != null && _anim.isHuman)
                {
                    ApplyGunnerBones(_gunnerBlend);
                }
            }

            float target = _seated ? 1f : 0f;
            _blend = blendSeconds > 0f ? Mathf.MoveTowards(_blend, target, Time.deltaTime / blendSeconds) : target;
            if (_blend <= 0f)
            {
                RestoreVisual();
                return;
            }

            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null || !_anim.isHuman) return;

            Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;

            // 서 있을 때 뿌리에서 엉덩이까지의 높이 (한 번 재 둔다: 앉기 시작하기 전 자세 기준)
            if (_hipHeight < 0f) _hipHeight = Mathf.Max(0.05f, Vector3.Dot(hips.position - transform.position, transform.up));

            // 엉덩이(와 그 아래로 달린 몸 전체)를 엉덩이 높이만큼 내려서 엉덩이가 좌석(뿌리 위치)에 닿게 한다.
            // 애니메이터가 매 프레임 엉덩이를 새로 쓰므로 누적되지 않는다. (캐릭터 뿌리는 NetworkPlayerMovement 등이 정하므로 건드리지 않는다)
            hips.position -= transform.up * (_hipHeight * _blend);

            // 앉은 몸을 살짝 줄여서 (머리 큰 캐릭터가) 운전실 지붕에 닿지 않게 한다. 기준 크기를 기억해 두고 절대값으로 쓴다 (누적되지 않게)
            if (!_hipsScaleSaved) { _hipsScaleSaved = true; _hipsBaseScale = hips.localScale; }
            hips.localScale = _hipsBaseScale * Mathf.Lerp(1f, seatedScale, _blend);

            ApplyBones(_blend);
        }

        private bool _hipsScaleSaved;
        private Vector3 _hipsBaseScale = Vector3.one;

        private void RestoreVisual()
        {
            if (!_hipsScaleSaved || _anim == null) return;
            Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) hips.localScale = _hipsBaseScale;
        }

        private void ApplyBones(float w)
        {
            Vector3 right = transform.right;

            Transform lUp = Bone(HumanBodyBones.LeftUpperLeg), lLow = Bone(HumanBodyBones.LeftLowerLeg), lFoot = Bone(HumanBodyBones.LeftFoot);
            Transform rUp = Bone(HumanBodyBones.RightUpperLeg), rLow = Bone(HumanBodyBones.RightLowerLeg), rFoot = Bone(HumanBodyBones.RightFoot);

            // 허벅지를 앞으로 들되, 종아리/발은 원래 방향(아래)을 유지한다 → 무릎이 직각으로 굽는다
            Quaternion thigh = Quaternion.AngleAxis(-thighFlex * w, right);
            Flex(lUp, lLow, lFoot, thigh);
            Flex(rUp, rLow, rFoot, thigh);

            float armRaise = _driver ? driverArmRaise : passengerArmRaise;
            float foreRaise = _driver ? driverForearmRaise : passengerForearmRaise;
            Raise(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), right, armRaise * w, foreRaise * w);
            Raise(Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), right, armRaise * w, foreRaise * w);
        }

        private Transform Bone(HumanBodyBones bone) => _anim.GetBoneTransform(bone);

        private static void Flex(Transform upper, Transform lower, Transform foot, Quaternion rotation)
        {
            if (upper == null) return;
            Quaternion lowerWorld = lower != null ? lower.rotation : Quaternion.identity;
            Quaternion footWorld = foot != null ? foot.rotation : Quaternion.identity;
            upper.rotation = rotation * upper.rotation;
            if (lower != null) lower.rotation = lowerWorld;
            if (foot != null) foot.rotation = footWorld;
        }

        private static void Raise(Transform upper, Transform lower, Vector3 right, float upperDegrees, float lowerDegrees)
        {
            if (upper == null) return;
            upper.rotation = Quaternion.AngleAxis(-upperDegrees, right) * upper.rotation;
            if (lower != null) lower.rotation = Quaternion.AngleAxis(-lowerDegrees, right) * lower.rotation;
        }

        private void OnDisable()
        {
            RestoreVisual();
            SetBodyVisible(true);
        }
    }
}
