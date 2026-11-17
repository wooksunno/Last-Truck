using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 구역 이름 표시(ZoneBanner)용 표식. 광산 동굴이나 독버섯 군락처럼 위치가 지도 생성 규칙으로 정해지는 구역에 달아 둔다.
    ///  - radius > 0 : 이 오브젝트 위치를 중심으로 한 원 안
    ///  - radius = 0 : 아래 자식 렌더러/충돌체 전체를 감싸는 범위 안 (동굴)
    ///  - maxY가 0이 아니면 그 높이보다 아래에 있을 때만 (지하 동굴: 땅 위를 걸을 땐 뜨지 않게)
    /// ZoneBanner가 게임 시작 때 모든 표식을 찾아 쓴다.
    /// </summary>
    public class ZoneMarker : MonoBehaviour
    {
        public string zoneName = "구역";
        [TextArea] public string hint = "";
        public float radius = 0f;
        public float maxY = 0f;

        private Bounds _bounds;
        private bool _hasBounds;

        public bool Contains(Vector3 p)
        {
            if (maxY != 0f && p.y > maxY) return false;
            if (radius > 0f)
            {
                Vector3 c = transform.position;
                float dx = p.x - c.x, dz = p.z - c.z;
                return dx * dx + dz * dz <= radius * radius;
            }
            if (!_hasBounds)
            {
                bool first = true;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { _bounds = r.bounds; first = false; } else _bounds.Encapsulate(r.bounds);
                }
                _hasBounds = !first;
            }
            if (!_hasBounds) return false;
            return p.x >= _bounds.min.x && p.x <= _bounds.max.x && p.z >= _bounds.min.z && p.z <= _bounds.max.z;
        }
    }
}
