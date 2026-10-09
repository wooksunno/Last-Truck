using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 게임 씬에서 플레이어들이 처음 나타날 위치.
    ///
    /// Demo_01에는 싱글플레이용 캐릭터(Army)가 미리 놓여 있는데, 멀티플레이로 들어오면
    /// CraftingSceneBootstrap이 그 캐릭터를 지우면서 "그 자리"를 여기에 기록한다.
    /// 호스트는 그 주변 원 위에 참가자들을 나란히 스폰한다 (씬을 따로 고칠 필요 없음).
    /// </summary>
    public static class PlayerSpawnArea
    {
        private static bool _hasOrigin;
        private static Vector3 _origin;
        private static Quaternion _rotation = Quaternion.identity;

        public static bool HasOrigin => _hasOrigin;

        public static void SetOrigin(Vector3 position, Quaternion rotation)
        {
            if (_hasOrigin) return; // 여러 개 있으면 첫 번째 것만 쓴다.
            _hasOrigin = true;
            _origin = position;
            _rotation = rotation;
        }

        public static void Clear()
        {
            _hasOrigin = false;
            _origin = Vector3.zero;
            _rotation = Quaternion.identity;
        }

        /// <summary>
        /// slot번째 플레이어의 스폰 위치. 중심점 주변 원 위에 배치하고, 위에서 아래로 레이를 쏴서 바닥 높이에 맞춘다.
        /// </summary>
        public static void GetSpawnPose(int slot, Vector3 fallbackOrigin, float radius, out Vector3 position, out Quaternion rotation)
        {
            Vector3 center = _hasOrigin ? _origin : fallbackOrigin;
            rotation = _hasOrigin ? _rotation : Quaternion.identity;

            // 중심점 둘레에 90도 간격으로 배치 (5번째부터는 45도 돌려서) → 서로 겹치지 않게.
            float angle = slot * 90f + (slot / 4) * 45f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.right * radius;
            position = center + offset;

            Vector3 rayStart = position + Vector3.up * 30f;
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                // 가장 높은 바닥이 아니라 "원래 높이에서 가장 가까운" 바닥을 고른다 (나무 꼭대기 등에 올라가지 않게).
                if (bestY == float.NegativeInfinity || Mathf.Abs(hit.point.y - center.y) < Mathf.Abs(bestY - center.y))
                {
                    bestY = hit.point.y;
                }
            }

            position.y = bestY != float.NegativeInfinity ? bestY + 0.1f : center.y + 0.5f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }
    }
}
