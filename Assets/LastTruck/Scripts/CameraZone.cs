using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 동굴 길 같은 구역. 카메라가 따라가는 캐릭터가 이 콜라이더 안에 있는 동안
    ///  - 카메라 최대 거리/최대 높이를 제한하고 (CameraDistanceLimiter)
    ///  - 가리는 물체를 반투명하게 만드는 기능을 끈다 (CameraOcclusionFader) → 동굴 벽이 투명해져 바깥 세상이 보이는 것을 막는다.
    ///
    /// 들어감/나감 이벤트(OnTriggerEnter/Exit)에 기대지 않고, 매 프레임 "지금 캐릭터가 안에 있는가"를 직접 확인한다.
    /// 그래서 트럭 탑승, 기절, 순간이동, 캐릭터 교체, 멀티플레이(다른 플레이어가 지나감) 등으로 상태가 꼬여
    /// 카메라가 줌된 채로 남는 일이 없다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CameraZone : MonoBehaviour
    {
        [Tooltip("피벗(머리) 기준 카메라 최대 거리(m)")]
        [SerializeField] private float maxCameraDistance = 3f;
        [Tooltip("캐릭터 발밑 기준 카메라 최대 높이(m). 0이면 높이 제한 없음")]
        [SerializeField] private float maxCameraHeight = 2.5f;
        [Tooltip("이 구역 안에 있는 동안 가리는 물체(동굴 벽 등)를 반투명하게 만들지 않는다")]
        [SerializeField] private bool blockOcclusionFade = true;

        private static readonly List<CameraZone> _active = new List<CameraZone>();
        private Collider[] _colliders;

        public float MaxCameraDistance => maxCameraDistance;
        public float MaxCameraHeight => maxCameraHeight;
        public bool BlockOcclusionFade => blockOcclusionFade;

        private void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void OnEnable()
        {
            _colliders = GetComponents<Collider>();
            if (!_active.Contains(this)) _active.Add(this);
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        /// <summary>발 위치(feet)의 캐릭터가 이 구역 안에 있는가. 발이 바닥에 살짝 묻혀도 잡히도록 몸 높이 두 곳을 본다.</summary>
        public bool Contains(Vector3 feet)
        {
            if (_colliders == null) return false;
            foreach (Collider c in _colliders)
            {
                if (c == null || !c.enabled) continue;
                if (IsInside(c, feet + Vector3.up * 0.3f) || IsInside(c, feet + Vector3.up * 1.0f))
                    return true;
            }
            return false;
        }

        private static bool IsInside(Collider c, Vector3 p)
        {
            if (!c.bounds.Contains(p)) return false;
            // 오목한 메쉬 콜라이더는 정확한 판정이 안 돼서 상자 범위로만 본다
            if (c is MeshCollider mesh && !mesh.convex) return true;
            if (c is TerrainCollider) return false;
            return (c.ClosestPoint(p) - p).sqrMagnitude < 0.0001f;
        }

        /// <summary>
        /// 캐릭터가 들어가 있는 모든 구역 중 가장 엄격한 제한값을 돌려준다.
        /// 들어가 있는 구역이 없으면 false (제한 없음 = Infinity).
        /// </summary>
        public static bool TryGetLimits(Vector3 feet, out float maxDistance, out float maxHeight, out bool blockFade)
        {
            maxDistance = float.PositiveInfinity;
            maxHeight = float.PositiveInfinity;
            blockFade = false;
            bool any = false;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                CameraZone zone = _active[i];
                if (zone == null) { _active.RemoveAt(i); continue; }
                if (!zone.Contains(feet)) continue;

                any = true;
                if (zone.maxCameraDistance > 0f) maxDistance = Mathf.Min(maxDistance, zone.maxCameraDistance);
                if (zone.maxCameraHeight > 0f) maxHeight = Mathf.Min(maxHeight, zone.maxCameraHeight);
                blockFade |= zone.blockOcclusionFade;
            }
            return any;
        }

        /// <summary>이 위치의 캐릭터가 "반투명 금지" 구역 안에 있는가.</summary>
        public static bool BlocksFadeAt(Vector3 feet)
        {
            return TryGetLimits(feet, out _, out _, out bool block) && block;
        }
    }
}
