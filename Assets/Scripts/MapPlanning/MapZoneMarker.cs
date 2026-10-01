using UnityEngine;

namespace MapPlanning
{
    /// <summary>
    /// 실제 동굴 에셋이 들어오기 전, 맵의 한 구역(방/자원지대)이 대략 어디에 얼마나 넓게
    /// 놓일지를 미리 표시해두는 자리표시자. 실제 게임 로직과는 무관한 순수 기획용 마커.
    /// </summary>
    [ExecuteAlways]
    public class MapZoneMarker : MonoBehaviour
    {
        /// <summary>이 구역의 지형이 어떤 모양으로 깎일지. MapTerrainCarver가 참조한다.</summary>
        public enum TerrainShape
        {
            Flat,       // 평지: 노이즈 없이 평평하게
            Mountain,   // 고산지역: 봉우리처럼 솟아오름
            Pit,        // 채석장/광산: 구덩이처럼 깊게 파임
            Desert,     // 사막: 평지보다 살짝 낮고 은은한 듄(모래언덕) 굴곡
        }

        public string zoneName = "구역";
        public Color color = Color.white;
        public float radius = 8f;

        [Header("지형 모양")]
        public TerrainShape shape = TerrainShape.Flat;
        [Tooltip("0=주변 암반과 거의 같음, 1=그 모양(산/구덩이 등)이 가장 강하게 적용됨")]
        [Range(0f, 1f)] public float shapeIntensity = 1f;

        [Header("접근성")]
        [Tooltip("트럭으로 도달 가능한 구역인지. false면 통로(곡선)와 일부러 연결하지 않아 도보로만 갈 수 있게 설계한다 " +
                 "— 대신 그런 구역엔 더 좋은 보상(희귀 자원)을 배치하는 게 의도.")]
        public bool truckAccessible = true;

        [TextArea] public string notes;

        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            DrawFlatWireCircle(transform.position, radius);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.01f, new Vector3(radius * 1.6f, 0.02f, radius * 1.6f));

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            string accessTag = truckAccessible ? "" : " [도보전용]";
            UnityEditor.Handles.Label(transform.position + Vector3.up * (radius * 0.2f + 3f), $"{zoneName} [{shape}]{accessTag}");
#endif
        }

        private void DrawFlatWireCircle(Vector3 center, float r)
        {
            const int segments = 40;
            Vector3 prev = center + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
