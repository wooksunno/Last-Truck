using System.Collections.Generic;
using UnityEngine;

namespace MapPlanning
{
    /// <summary>
    /// 맵의 전체적인 큰 틀(동굴 통로의 흐름)을 잡기 위한 곡선 도구.
    /// 자식으로 있는 컨트롤 포인트들을 Scene 뷰에서 직접 드래그해서 움직이면
    /// Catmull-Rom 보간으로 부드러운 통로 곡선이 실시간으로 그려진다.
    /// 실제 에셋(동굴 모델)이 아직 없을 때, 통로가 어떤 모양으로 휘고 어디에 방(구역)이
    /// 놓일지 미리 잡아보기 위한 순수 에디터 시각화 도구다 (런타임 로직 없음).
    /// </summary>
    [ExecuteAlways]
    public class MapLayoutCurve : MonoBehaviour
    {
        [Tooltip("곡선이 지나갈 컨트롤 포인트들. 순서대로 연결된다.")]
        [SerializeField] private List<Transform> controlPoints = new List<Transform>();

        [Tooltip("두 컨트롤 포인트 사이를 몇 개의 선분으로 잘게 나눠 그릴지 (부드러움 정도)")]
        [SerializeField] private int segmentsPerSpan = 16;

        [SerializeField] private Color curveColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private float pathWidthGizmo = 6f;

        public IReadOnlyList<Transform> ControlPoints => controlPoints;

        /// <summary>0~1 사이 t값으로 곡선 위의 월드 좌표를 샘플링한다. 구역 마커를 곡선 위에 올릴 때 사용.</summary>
        public Vector3 SamplePoint(float t)
        {
            if (controlPoints == null || controlPoints.Count == 0)
                return transform.position;
            if (controlPoints.Count == 1)
                return controlPoints[0].position;

            t = Mathf.Clamp01(t);
            int spanCount = controlPoints.Count - 1;
            float scaled = t * spanCount;
            int span = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, spanCount - 1);
            float localT = scaled - span;

            Vector3 p0 = GetPoint(span - 1);
            Vector3 p1 = GetPoint(span);
            Vector3 p2 = GetPoint(span + 1);
            Vector3 p3 = GetPoint(span + 2);
            return CatmullRom(p0, p1, p2, p3, localT);
        }

        private Vector3 GetPoint(int index)
        {
            index = Mathf.Clamp(index, 0, controlPoints.Count - 1);
            return controlPoints[index].position;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private void OnDrawGizmos()
        {
            if (controlPoints == null || controlPoints.Count < 2)
                return;

            int totalSegments = (controlPoints.Count - 1) * segmentsPerSpan;
            var centerLine = new Vector3[totalSegments + 1];
            var leftLine = new Vector3[totalSegments + 1];
            var rightLine = new Vector3[totalSegments + 1];

            Vector3 prev = SamplePoint(0f);
            centerLine[0] = prev;
            for (int i = 1; i <= totalSegments; i++)
            {
                float t = (float)i / totalSegments;
                Vector3 cur = SamplePoint(t);
                centerLine[i] = cur;

                Vector3 dir = (cur - prev).normalized;
                Vector3 side = Vector3.Cross(dir, Vector3.up) * (pathWidthGizmo * 0.5f);
                leftLine[i] = cur + side;
                rightLine[i] = cur - side;
                if (i == 1)
                {
                    leftLine[0] = prev + side;
                    rightLine[0] = prev - side;
                }
                prev = cur;
            }

#if UNITY_EDITOR
            // 확대/축소해도 항상 눈에 띄도록 두꺼운 선(Handles)으로 그린다. Gizmos.DrawLine은 1px 고정이라
            // 넓게 줌아웃한 Top-down 뷰에서는 잘 안 보인다.
            UnityEditor.Handles.color = curveColor;
            UnityEditor.Handles.DrawAAPolyLine(6f, centerLine);
            UnityEditor.Handles.color = new Color(curveColor.r, curveColor.g, curveColor.b, 0.35f);
            UnityEditor.Handles.DrawAAPolyLine(2f, leftLine);
            UnityEditor.Handles.DrawAAPolyLine(2f, rightLine);
#else
            Gizmos.color = curveColor;
            for (int i = 1; i <= totalSegments; i++)
                Gizmos.DrawLine(centerLine[i - 1], centerLine[i]);
#endif

            Gizmos.color = Color.white;
            for (int i = 0; i < controlPoints.Count; i++)
            {
                if (controlPoints[i] == null) continue;
                Gizmos.DrawWireSphere(controlPoints[i].position, 3f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(controlPoints[i].position + Vector3.up * 4f, $"P{i}");
#endif
            }
        }
    }
}
