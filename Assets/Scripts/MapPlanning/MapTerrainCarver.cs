using System.Collections.Generic;
using UnityEngine;

namespace MapPlanning
{
    /// <summary>
    /// MapLayoutCurve와 MapZoneMarker들을 참고해서 Terrain 하나를 실제로 깎아,
    /// "동굴처럼 파여진 통로/방" 모양을 3D로 미리 볼 수 있게 해주는 기획용 도구.
    /// 인스펙터 톱니바퀴(⋮) 메뉴에서 "Carve Terrain From Curve"를 실행하면
    /// targetTerrain의 높이맵을 곡선/구역 모양대로 깎는다. 컨트롤 포인트를 옮긴 뒤
    /// 몇 번이고 다시 실행해서 갱신할 수 있다. 실제 게임 로직과는 무관한 순수 기획용 도구.
    /// </summary>
    [ExecuteAlways]
    public class MapTerrainCarver : MonoBehaviour
    {
        [Tooltip("본선 + 갈래길들. 여러 개를 등록하면 갈라지는 길 네트워크로 깎인다. " +
                 "고산지역처럼 트럭으로 못 가게 하고 싶은 구역은 어떤 곡선과도 연결하지 않으면 된다.")]
        [SerializeField] private List<MapLayoutCurve> curves = new List<MapLayoutCurve>();
        [SerializeField] private List<MapZoneMarker> zoneMarkers = new List<MapZoneMarker>();
        [SerializeField] private Terrain targetTerrain;

        [Header("기본 지형 높이 (0~1, Terrain 최대 높이에 대한 비율)")]
        [Tooltip("아무 구역에도 속하지 않는 일반 야생 지역의 기준 높이")]
        [SerializeField] private float baseHeight = 0.55f;
        [Tooltip("기준 높이에 더해지는 은은한 굴곡(자연스러운 기복). 0이면 완전히 평평함")]
        [SerializeField] private float baseRoughness = 0.03f;
        [SerializeField] private float baseNoiseScale = 0.02f;

        [Header("구역별 지형 높이 (0~1)")]
        [Tooltip("Flat 구역(트럭 시작/공용 채집지 등): 노이즈 없이 이 높이로 고정")]
        [SerializeField] private float flatHeight = 0.55f;
        [Tooltip("Mountain 구역: 중심부가 이 높이까지 솟아오름")]
        [SerializeField] private float mountainPeakHeight = 0.95f;
        [Tooltip("Pit 구역(채석장/광맥): 중심부가 이 높이까지 파임")]
        [SerializeField] private float pitFloorHeight = 0.05f;
        [Tooltip("Desert 구역: 기준 높이(듄 굴곡은 별도로 얹힘)")]
        [SerializeField] private float desertHeight = 0.48f;
        [SerializeField] private float desertDuneAmplitude = 0.02f;
        [SerializeField] private float desertDuneScale = 0.08f;

        [Header("통로(곡선) 설정 — 구역들을 잇는 낮은 길")]
        [Tooltip("통로 바닥 높이. 구역 안에 들어가면 구역 쪽 모양이 우선한다.")]
        [SerializeField] private float pathFloorHeight = 0.3f;
        [SerializeField] private float pathHalfWidth = 7f;
        [SerializeField] private float falloff = 8f;
        [SerializeField] private int curveSamples = 400;

        [ContextMenu("Carve Terrain From Curve")]
        public void CarveFromCurve()
        {
            if (curves == null || curves.Count == 0 || targetTerrain == null || targetTerrain.terrainData == null)
            {
                Debug.LogWarning("[MapTerrainCarver] curves 또는 targetTerrain이 비어 있습니다.");
                return;
            }

            TerrainData data = targetTerrain.terrainData;
            int res = data.heightmapResolution;
            Vector3 terrainPos = targetTerrain.transform.position;
            Vector3 terrainSize = data.size;

            // 등록된 모든 곡선(본선+갈래길)의 샘플 포인트를 한 목록에 합친다.
            // 곡선과 연결되지 않은 구역(예: 고산지역)은 이 목록에 전혀 가까워지지 않으므로 자연히 "도보 전용"이 된다.
            var pathPoints = new List<Vector2>();
            for (int c = 0; c < curves.Count; c++)
            {
                MapLayoutCurve cv = curves[c];
                if (cv == null) continue;
                for (int i = 0; i <= curveSamples; i++)
                {
                    Vector3 p = cv.SamplePoint((float)i / curveSamples);
                    pathPoints.Add(new Vector2(p.x, p.z));
                }
            }

            float[,] heights = new float[res, res];

            for (int zy = 0; zy < res; zy++)
            {
                float worldZ = terrainPos.z + ((float)zy / (res - 1)) * terrainSize.z;
                for (int xy = 0; xy < res; xy++)
                {
                    float worldX = terrainPos.x + ((float)xy / (res - 1)) * terrainSize.x;
                    Vector2 p = new Vector2(worldX, worldZ);

                    // 기본 지형: 기준 높이 + 은은한 Perlin 기복(자연스러운 야생 지형)
                    float noise = (Mathf.PerlinNoise(worldX * baseNoiseScale, worldZ * baseNoiseScale) - 0.5f) * 2f;
                    float baseH = baseHeight + noise * baseRoughness;

                    // 가장 영향력(weight)이 강한 요소 하나가 이 지점의 모양을 결정한다.
                    // (산+구덩이가 동시에 최대치로 겹쳐서 서로 상쇄되는 것을 막기 위함)
                    // 구역 마커를 먼저 평가하고 통로는 그 뒤에 "엄격히 더 클 때만" 덮어써서,
                    // 구역 중심이 곡선 컨트롤 포인트와 겹칠 때(가중치 동률) 구역 모양이 우선하도록 한다.
                    // 마커끼리 가중치가 동률이면(예: 넓은 산 속에 파묻힌 좁은 희귀 광맥) 반경이 더 작은
                    // (더 구체적인/중첩된) 쪽이 이긴다 — 그렇지 않으면 큰 구역이 항상 먼저 평가돼 작은 구역을 덮어버린다.
                    float bestWeight = 0f;
                    float bestTarget = baseH;
                    float bestRadius = float.MaxValue;

                    if (zoneMarkers != null)
                    {
                        for (int m = 0; m < zoneMarkers.Count; m++)
                        {
                            MapZoneMarker marker = zoneMarkers[m];
                            if (marker == null) continue;

                            float d = Vector2.Distance(p, new Vector2(marker.transform.position.x, marker.transform.position.z));
                            float w = FalloffAmount(d, marker.radius) * Mathf.Clamp01(marker.shapeIntensity);
                            bool strictlyBetter = w > bestWeight;
                            bool tiedButMoreSpecific = w >= bestWeight && marker.radius < bestRadius;
                            if (!strictlyBetter && !tiedButMoreSpecific) continue;

                            bestWeight = w;
                            bestRadius = marker.radius;
                            bestTarget = TargetHeightForShape(marker, worldX, worldZ);
                        }
                    }

                    float pathDist = NearestDistance(p, pathPoints);
                    float pathWeight = FalloffAmount(pathDist, pathHalfWidth);
                    if (pathWeight > bestWeight)
                    {
                        bestWeight = pathWeight;
                        bestTarget = pathFloorHeight;
                    }

                    // heights 배열은 [z, x] 순서 (Unity TerrainData 관례)
                    heights[zy, xy] = Mathf.Lerp(baseH, bestTarget, bestWeight);
                }
            }

            data.SetHeights(0, 0, heights);
            Debug.Log("[MapTerrainCarver] Terrain을 구역별 지형(평지/고산/채석장/사막 등)으로 다시 깎았습니다.");
        }

        private float TargetHeightForShape(MapZoneMarker marker, float worldX, float worldZ)
        {
            switch (marker.shape)
            {
                case MapZoneMarker.TerrainShape.Mountain:
                    return mountainPeakHeight;
                case MapZoneMarker.TerrainShape.Pit:
                    return pitFloorHeight;
                case MapZoneMarker.TerrainShape.Desert:
                    float dune = (Mathf.PerlinNoise(worldX * desertDuneScale, worldZ * desertDuneScale) - 0.5f) * 2f;
                    return desertHeight + dune * desertDuneAmplitude;
                case MapZoneMarker.TerrainShape.Flat:
                default:
                    return flatHeight;
            }
        }

        private float FalloffAmount(float dist, float halfWidth)
        {
            if (dist <= halfWidth) return 1f;
            if (dist >= halfWidth + falloff) return 0f;
            float t = (dist - halfWidth) / falloff;
            return 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        private static float NearestDistance(Vector2 p, List<Vector2> points)
        {
            float best = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = Vector2.Distance(p, points[i]);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
