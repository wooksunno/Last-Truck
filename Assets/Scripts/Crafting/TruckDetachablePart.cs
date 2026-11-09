using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 몸체에서 떨어져 나가는 부품 하나 (문/사이드미러/범퍼/적재함 아치/뒷문 등).
    /// 이 컴포넌트가 붙은 오브젝트에 MeshFilter + MeshRenderer가 있어야 한다.
    /// 트럭 내구도 비율이 detachBelow 아래로 내려가면 TruckDamageVisuals가 부품을 숨기고 진짜로 굴러 떨어지는 파편을 만든다.
    /// 수리해서 다시 detachBelow 위로 올라가면 부품이 다시 붙는다.
    /// </summary>
    public class TruckDetachablePart : MonoBehaviour
    {
        [Tooltip("트럭 내구도가 이 비율(0~1) 아래로 떨어지면 부품이 떨어진다.")]
        [Range(0f, 1f)] public float detachBelow = 0.5f;

        [Tooltip("떨어진 파편의 무게(kg).")]
        public float debrisMass = 20f;

        [Tooltip("떨어질 때 튕겨 나가는 속도 (트럭 기준 좌표: x 오른쪽, y 위, z 앞) m/s. 트럭이 달리던 속도는 따로 더해진다.")]
        public Vector3 kickLocal = new Vector3(2f, 2.5f, 0f);

        [Tooltip("떨어질 때 빙글 도는 정도 (rad/s).")]
        public float spin = 4f;
    }
}
