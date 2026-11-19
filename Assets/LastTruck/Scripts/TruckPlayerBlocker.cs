using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 플레이어가 트럭 몸체를 통과하지 못하게 막는 "벽" (TruckStabilizer가 자동으로 만든다).
    ///
    /// 배경: 캐릭터가 트럭 안에 겹치면 물리 엔진이 트럭을 튕겨 하늘로 날려 보내는 문제가 있어서,
    /// TruckStabilizer.IgnoreCharacter가 캐릭터와 트럭 충돌을 통째로 꺼 두었다 → 플레이어가 트럭을 그냥 통과했다.
    ///
    /// 해결: 트럭 몸체와 같은 모양의 충돌체를 "트럭과 따로 노는 키네마틱 물체"로 하나 더 만들어 트럭을 따라다니게 한다.
    ///  - 키네마틱이라 플레이어가 부딪혀도 이 벽은 밀리지 않고, 트럭 본체와는 충돌을 꺼 두어 트럭에 힘을 주지 않는다
    ///    → 플레이어는 막히고, 겹쳐도 트럭이 튕겨 나가지 않는다 (겹치면 플레이어가 밖으로 밀려난다).
    ///  - 트럭에 탄(앉은) 플레이어와는 부딪히지 않는다.
    ///  - "Ignore Raycast" 레이어라 총알/상호작용(E키) 레이캐스트에는 걸리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TruckPlayerBlocker : MonoBehaviour
    {
        private const int BlockerLayer = 2; // Ignore Raycast

        private Transform _truck;
        private Rigidbody _truckRb;
        private Rigidbody _rb;
        private readonly List<Collider> _blockers = new List<Collider>();
        private readonly Dictionary<Collider, bool> _ignored = new Dictionary<Collider, bool>();

        /// <summary>트럭 하나에 한 번만 만든다.</summary>
        public static TruckPlayerBlocker Create(Transform truck, IEnumerable<Collider> truckColliders)
        {
            if (truck == null) return null;
            var go = new GameObject(truck.name + "_PlayerBlocker");
            go.layer = BlockerLayer;
            var blocker = go.AddComponent<TruckPlayerBlocker>();
            blocker.Init(truck, truckColliders);
            return blocker;
        }

        private void Init(Transform truck, IEnumerable<Collider> truckColliders)
        {
            _truck = truck;
            _truckRb = truck.GetComponent<Rigidbody>();
            transform.SetPositionAndRotation(truck.position, truck.rotation);

            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var sources = new List<Collider>();
            foreach (Collider c in truckColliders)
            {
                if (c == null || c is WheelCollider || c.isTrigger) continue;
                sources.Add(c);
                Collider copy = Copy(c);
                if (copy != null) _blockers.Add(copy);
            }

            // 트럭 본체(바퀴 포함)와는 부딪히지 않는다 → 트럭에 힘을 주지 않는다
            foreach (Collider mine in _blockers)
                foreach (Collider theirs in truck.GetComponentsInChildren<Collider>(true))
                    if (theirs != null) Physics.IgnoreCollision(mine, theirs, true);
        }

        /// <summary>트럭 충돌체와 같은 모양/위치의 충돌체를 이 오브젝트 아래에 만든다.</summary>
        private Collider Copy(Collider source)
        {
            var child = new GameObject("Block_" + source.name);
            child.layer = BlockerLayer;
            child.transform.SetParent(transform, false);
            Transform st = source.transform;
            child.transform.localPosition = _truck.InverseTransformPoint(st.position);
            child.transform.localRotation = Quaternion.Inverse(_truck.rotation) * st.rotation;
            Vector3 ts = _truck.lossyScale, ss = st.lossyScale;
            child.transform.localScale = new Vector3(SafeDiv(ss.x, ts.x), SafeDiv(ss.y, ts.y), SafeDiv(ss.z, ts.z));

            switch (source)
            {
                case BoxCollider box:
                {
                    var c = child.AddComponent<BoxCollider>();
                    c.center = box.center; c.size = box.size;
                    return c;
                }
                case CapsuleCollider cap:
                {
                    var c = child.AddComponent<CapsuleCollider>();
                    c.center = cap.center; c.radius = cap.radius; c.height = cap.height; c.direction = cap.direction;
                    return c;
                }
                case SphereCollider sph:
                {
                    var c = child.AddComponent<SphereCollider>();
                    c.center = sph.center; c.radius = sph.radius;
                    return c;
                }
                case MeshCollider mesh when mesh.sharedMesh != null:
                {
                    var c = child.AddComponent<MeshCollider>();
                    c.sharedMesh = mesh.sharedMesh; c.convex = true;
                    return c;
                }
            }
            Destroy(child);
            return null;
        }

        private static float SafeDiv(float a, float b) => Mathf.Abs(b) < 1e-5f ? a : a / b;

        private void FixedUpdate()
        {
            if (_truck == null) { Destroy(gameObject); return; }

            // 호스트/싱글: 트럭 물리 위치, 멀티 클라이언트: 트럭이 키네마틱이라 Transform 위치
            bool usePhysics = _truckRb != null && !_truckRb.isKinematic;
            Vector3 pos = usePhysics ? _truckRb.position : _truck.position;
            Quaternion rot = usePhysics ? _truckRb.rotation : _truck.rotation;
            _rb.MovePosition(pos);
            _rb.MoveRotation(rot);

            UpdateSeatedIgnores();
        }

        /// <summary>트럭에 탄 플레이어와는 충돌을 끈다 (자리에 앉혀 둔 캐릭터가 벽에 밀려나지 않게).</summary>
        private void UpdateSeatedIgnores()
        {
            IReadOnlyList<GameObject> characters = TruckStabilizer.Characters;
            for (int i = 0; i < characters.Count; i++)
            {
                GameObject character = characters[i];
                if (character == null) continue;
                bool seated = IsSeated(character);
                foreach (Collider theirs in character.GetComponentsInChildren<Collider>(true))
                {
                    if (theirs == null) continue;
                    if (_ignored.TryGetValue(theirs, out bool was) && was == seated) continue;
                    _ignored[theirs] = seated;
                    foreach (Collider mine in _blockers)
                        if (mine != null) Physics.IgnoreCollision(mine, theirs, seated);
                }
            }
        }

        private static bool IsSeated(GameObject character)
        {
            var networkPlayer = character.GetComponent<LastTruck.Networking.NetworkPlayer>();
            if (networkPlayer != null) return networkPlayer.IsSeated;
            var move = character.GetComponent<PlayerMove>();
            return move != null && !move.enabled;   // 싱글: 운전석에 타면 조작 스크립트가 꺼진다
        }
    }
}
