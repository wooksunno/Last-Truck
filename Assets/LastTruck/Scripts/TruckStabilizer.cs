using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 트럭이 가볍게 날아가거나 통통 튀지 않게 잡아 주는 물리 보정 (트럭 Rigidbody에 붙는다, TruckMove가 없으면 자동 추가).
    ///
    ///  - 무게: Rigidbody 질량을 올리고, 서스펜션 스프링/모터·브레이크 힘도 같은 비율로 올려 주행감(가속/높이)은 그대로 둔다.
    ///  - 무게 중심: 바퀴 높이 근처로 낮춰서 잘 넘어지거나 들썩이지 않게 한다.
    ///  - 안티롤바: 좌우 바퀴 서스펜션 차이만큼 반대로 눌러 커브에서 덜 기울게 한다.
    ///  - 튐 억제: 위로 솟는 속도와 회전 속도를 제한하고, 공중에서는 중력을 조금 더 준다.
    ///  - 몬스터와는 물리 충돌하지 않는다 (몬스터 충돌체에 바퀴가 올라타 튀던 문제). 공격 판정은 거리로 하므로 영향 없음.
    ///
    /// 운전 중이 아닐 때(멀티에서 TruckMove가 꺼져 있을 때)도 동작한다. 클라이언트(키네마틱)에서는 아무것도 하지 않는다.
    /// </summary>
    [DefaultExecutionOrder(100)] // TruckMove.Start(무게 중심 설정) 뒤에 덮어쓴다
    [DisallowMultipleComponent]
    public class TruckStabilizer : MonoBehaviour
    {
        #region 인스펙터

        [Header("무게")]
        [Tooltip("트럭 질량(kg). 무거울수록 부딪혀도 덜 밀린다.")]
        public float mass = 2500f;
        [Tooltip("질량을 바꾼 비율만큼 서스펜션 스프링/댐퍼와 모터·브레이크 힘도 같이 올린다 (주행감 유지).")]
        public bool scaleForcesWithMass = true;

        [Header("무게 중심")]
        [Tooltip("무게 중심을 바퀴 중심 높이 근처로 낮춘다.")]
        public bool lowerCenterOfMass = true;
        [Tooltip("바퀴 중심 높이 기준으로 무게 중심을 얼마나 위에 둘지 (m, 음수면 더 아래).")]
        public float centerOfMassHeight = 0.1f;

        [Header("서스펜션")]
        [Tooltip("서스펜션 거리가 이보다 짧으면 늘린다 (너무 짧으면 바닥을 치며 튄다).")]
        public float minSuspensionDistance = 0.3f;

        [Header("흔들림 / 튐 억제")]
        [Tooltip("안티롤바 힘. 좌우로 덜 기울게 한다 (0이면 끔).")]
        public float antiRollForce = 15000f;
        [Tooltip("회전 감쇠. 부딪힌 뒤 빙글 도는 것을 줄인다.")]
        public float angularDamping = 1.5f;
        [Tooltip("최대 회전 속도 (rad/s).")]
        public float maxAngularVelocity = 3f;
        [Tooltip("위로 솟는 속도 상한 (m/s). 부딪혔을 때 하늘로 날아가지 않게.")]
        public float maxUpwardSpeed = 2.5f;
        [Tooltip("바퀴가 모두 땅에서 떨어졌을 때 추가 중력 배율 (1이면 추가 없음).")]
        public float airGravityMultiplier = 1.8f;

        [Header("충돌")]
        [Tooltip("몬스터와 물리 충돌하지 않는다 (몬스터에 올라타 튀거나 밀려나지 않게).")]
        public bool ignoreMonsterCollision = true;

        #endregion

        #region 상태

        private static readonly List<TruckStabilizer> Active = new List<TruckStabilizer>();
        private static readonly List<GameObject> IgnoredObjects = new List<GameObject>();

        private Rigidbody _rb;
        private TruckMove _move;
        private Collider[] _truckColliders = new Collider[0];
        private WheelCollider[] _wheels = new WheelCollider[0];
        private readonly List<WheelCollider[]> _axles = new List<WheelCollider[]>(); // [왼쪽, 오른쪽]
        private bool _applied;

        #endregion

        #region 생명주기

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _move = GetComponent<TruckMove>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Start()
        {
            ApplySetup();
        }

        private void ApplySetup()
        {
            if (_applied || _rb == null) return;
            _applied = true;

            _truckColliders = GetComponentsInChildren<Collider>(true);
            _wheels = GetComponentsInChildren<WheelCollider>(true);
            BuildAxles();

            // 무게 + 힘 비율
            float ratio = 1f;
            if (mass > 0f && _rb.mass > 0f)
            {
                ratio = mass / _rb.mass;
                _rb.mass = mass;
            }

            if (scaleForcesWithMass && Mathf.Abs(ratio - 1f) > 0.001f)
            {
                foreach (WheelCollider wheel in _wheels)
                {
                    JointSpring spring = wheel.suspensionSpring;
                    spring.spring *= ratio;
                    spring.damper *= ratio;
                    wheel.suspensionSpring = spring;
                }
                if (_move != null)
                {
                    _move.maxAcceleration *= ratio;
                    _move.brakeAcceleration *= ratio;
                    _move.decelerationForce *= ratio;
                    _move.downForce *= ratio;
                }
            }

            foreach (WheelCollider wheel in _wheels)
            {
                if (wheel.suspensionDistance < minSuspensionDistance) wheel.suspensionDistance = minSuspensionDistance;
            }

            // 무게 중심: 바퀴들의 중앙, 바퀴 중심 높이 + centerOfMassHeight
            if (lowerCenterOfMass && _wheels.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (WheelCollider wheel in _wheels) sum += transform.InverseTransformPoint(wheel.transform.TransformPoint(wheel.center));
                Vector3 center = sum / _wheels.Length;
                // (트럭 모델의 원점이 차체 중앙이 아닐 수 있어서 x/z도 바퀴 중앙을 쓴다)
                _rb.centerOfMass = new Vector3(center.x, center.y + centerOfMassHeight, center.z);
            }

            _rb.angularDamping = Mathf.Max(_rb.angularDamping, angularDamping);
            _rb.maxAngularVelocity = maxAngularVelocity;
            if (_rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 이 트럭이 생기기 전에 등록된 몬스터들
            IgnoredObjects.RemoveAll(o => o == null);
            foreach (GameObject go in IgnoredObjects) IgnoreWith(go);
        }

        /// <summary>앞/뒤 바퀴를 좌우 한 쌍씩 묶는다 (안티롤바용).</summary>
        private void BuildAxles()
        {
            _axles.Clear();
            List<WheelCollider> remaining = new List<WheelCollider>(_wheels);
            while (remaining.Count >= 2)
            {
                WheelCollider a = remaining[0];
                remaining.RemoveAt(0);
                float az = transform.InverseTransformPoint(a.transform.position).z;

                int best = -1;
                float bestDiff = float.MaxValue;
                for (int i = 0; i < remaining.Count; i++)
                {
                    float diff = Mathf.Abs(transform.InverseTransformPoint(remaining[i].transform.position).z - az);
                    if (diff < bestDiff) { bestDiff = diff; best = i; }
                }
                WheelCollider b = remaining[best];
                remaining.RemoveAt(best);

                bool aIsLeft = transform.InverseTransformPoint(a.transform.position).x < transform.InverseTransformPoint(b.transform.position).x;
                _axles.Add(aIsLeft ? new[] { a, b } : new[] { b, a });
            }
        }

        #endregion

        #region 물리 보정

        private void FixedUpdate()
        {
            if (_rb == null || _rb.isKinematic) return;
            if (!_applied) ApplySetup();

            bool anyGrounded = false;
            foreach (WheelCollider[] axle in _axles)
            {
                anyGrounded |= ApplyAntiRoll(axle[0], axle[1]);
            }
            if (_axles.Count == 0)
            {
                foreach (WheelCollider wheel in _wheels) anyGrounded |= wheel.isGrounded;
            }

            if (!anyGrounded && airGravityMultiplier > 1f)
                _rb.AddForce(Physics.gravity * (airGravityMultiplier - 1f), ForceMode.Acceleration);

            // 트럭 위쪽 방향으로 튀어 오르는 속도만 자른다 (오르막을 달리는 속도는 트럭 앞 방향이라 영향 없음).
            Vector3 velocity = _rb.linearVelocity;
            bool changed = false;
            float up = Vector3.Dot(velocity, transform.up);
            if (up > maxUpwardSpeed)
            {
                velocity -= transform.up * (up - maxUpwardSpeed);
                changed = true;
            }
            if (!anyGrounded && velocity.y > maxUpwardSpeed)
            {
                velocity.y = maxUpwardSpeed;
                changed = true;
            }
            if (changed) _rb.linearVelocity = velocity;
        }

        /// <summary>좌우 서스펜션이 눌린 정도의 차이만큼 반대쪽을 눌러 준다. 하나라도 땅에 닿아 있으면 true.</summary>
        private bool ApplyAntiRoll(WheelCollider left, WheelCollider right)
        {
            float travelLeft = 1f;
            float travelRight = 1f;
            bool groundedLeft = left.GetGroundHit(out WheelHit hitLeft);
            bool groundedRight = right.GetGroundHit(out WheelHit hitRight);

            if (groundedLeft)
                travelLeft = (-left.transform.InverseTransformPoint(hitLeft.point).y - left.radius) / Mathf.Max(0.01f, left.suspensionDistance);
            if (groundedRight)
                travelRight = (-right.transform.InverseTransformPoint(hitRight.point).y - right.radius) / Mathf.Max(0.01f, right.suspensionDistance);

            if (antiRollForce > 0f)
            {
                float force = (travelLeft - travelRight) * antiRollForce;
                if (groundedLeft) _rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
                if (groundedRight) _rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
            }
            return groundedLeft || groundedRight;
        }

        #endregion

        #region 몬스터 충돌 무시

        /// <summary>몬스터가 생길 때 부른다. 트럭(바퀴 포함)과 이 오브젝트의 충돌을 끈다.</summary>
        public static void IgnoreMonster(GameObject monster)
        {
            if (monster == null) return;
            if (!IgnoredObjects.Contains(monster)) IgnoredObjects.Add(monster);
            foreach (TruckStabilizer truck in Active)
            {
                if (truck != null && truck._applied) truck.IgnoreWith(monster);
            }
        }

        private void IgnoreWith(GameObject other)
        {
            if (!ignoreMonsterCollision || other == null) return;
            Collider[] others = other.GetComponentsInChildren<Collider>(true);
            foreach (Collider mine in _truckColliders)
            {
                if (mine == null) continue;
                foreach (Collider theirs in others)
                {
                    if (theirs != null) Physics.IgnoreCollision(mine, theirs, true);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            IgnoredObjects.Clear();
        }

        #endregion
    }
}
