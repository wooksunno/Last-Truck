using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class RadarChartUI : Graphic
    {
        // 5개 스탯 항목 (0.0f ~ 1.0f 비율값)
        [System.Serializable]
        public struct StatValues
        {
            public float hp;          // 체력
            public float attack;      // 공격력
            public float moveSpeed;   // 이동속도
            public float attackSpeed; // 공격속도
            public float gatherSpeed; // 채집속도

            public StatValues(float hp, float attack, float moveSpeed, float attackSpeed, float gatherSpeed)
            {
                this.hp = Mathf.Clamp01(hp);
                this.attack = Mathf.Clamp01(attack);
                this.moveSpeed = Mathf.Clamp01(moveSpeed);
                this.attackSpeed = Mathf.Clamp01(attackSpeed);
                this.gatherSpeed = Mathf.Clamp01(gatherSpeed);
            }
        }

        [Header("차트 설정")]
        [SerializeField] private float chartRadius = 100f; // 그래프 최대 반지름
        public StatValues currentStats; // 현재 그리고 있는 스탯 값

        // 5각형 정점 생성을 위한 렌더링 함수
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float[] statArray = new float[5]
            {
                currentStats.hp,
                currentStats.attack,
                currentStats.moveSpeed,
                currentStats.attackSpeed,
                currentStats.gatherSpeed
            };

            // 중심점 정점 추가 (Index 0)
            UIVertex centerVertex = UIVertex.simpleVert;
            centerVertex.color = color;
            centerVertex.position = Vector3.zero;
            vh.AddVert(centerVertex);

            int categoriesCount = 5;
            float angleStep = 360f / categoriesCount;

            // 5개 외곽 정점 추가 (12시 방향부터 시계방향 배치)
            for (int i = 0; i < categoriesCount; i++)
            {
                // 12시 방향 시작을 위해 90도(Mathf.PI / 2) 보정
                float angleRad = (90f - angleStep * i) * Mathf.Deg2Rad;
                float currentRadius = chartRadius * Mathf.Clamp01(statArray[i]);

                Vector3 vertexPos = new Vector3(
                    Mathf.Cos(angleRad) * currentRadius,
                    Mathf.Sin(angleRad) * currentRadius,
                    0f
                );

                UIVertex outerVertex = UIVertex.simpleVert;
                outerVertex.color = color;
                outerVertex.position = vertexPos;
                vh.AddVert(outerVertex);
            }

            // 삼각형 인덱스 연결 (중심점과 각 정점을 연결)
            for (int i = 0; i < categoriesCount; i++)
            {
                int nextIndex = (i == categoriesCount - 1) ? 1 : i + 2;
                vh.AddTriangle(0, i + 1, nextIndex);
            }
        }

        // 외부에서 스탯 데이터를 갱신할 때 호출
        public void SetStatValues(StatValues newStats)
        {
            currentStats = newStats;
            SetVerticesDirty(); // 메쉬 재그리기 요청
        }
    }
}
