using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어를 중심으로 minDistance ~ maxDistance 사이의 원형 링(annulus) 안에서
/// 몬스터를 균등 분포로 스폰하는 컴포넌트.
///
/// - 스폰 위치는 스폰 시점에 1회만 계산되며(고정 방식), 이후 플레이어를 따라가지 않음.
/// - 장애물 체크는 하지 않고, 오직 "바닥(태그) 위인지"만 레이캐스트로 확인함.
/// - 몬스터는 한 번에 다 나오지 않고 minSpawnInterval~maxSpawnInterval 간격으로 하나씩 스폰됨.
/// - 위치 탐색은 코루틴으로 매 프레임 한 번씩 시도하며, maxSearchAttempts 안에 유효한 위치를
///   못 찾으면 경고를 띄우고 해당 몬스터의 스폰을 건너뜀(엔진 멈춤 방지).
///
/// [GameManager 연동]
/// - gameManager 필드가 연결되어 있으면, 밤이 시작될 때(OnNightStarted) 그 밤의 쿼터만큼 자동 스폰함.
/// - 스폰된 각 몬스터에는 MonsterKillNotifier가 자동으로 부착되어, 몬스터가 파괴될 때
///   GameManager에 킬을 자동으로 보고함 (몬스터 프리팹 자체는 수정할 필요 없음).
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject monsterPrefab;

    [Header("게임 매니저 연동")]
    [Tooltip("연결하면 밤이 시작될 때 그 밤의 쿼터만큼 자동으로 스폰합니다.")]
    [SerializeField] private GameManager gameManager;

    [Header("스폰 거리 (플레이어 기준 원형 링)")]
    [Tooltip("이 거리보다 가깝게는 스폰되지 않음 (화면 밖 스폰을 위해 넉넉하게 설정)")]
    [SerializeField] private float minDistance = 15f;
    [Tooltip("이 거리보다 멀게는 스폰되지 않음")]
    [SerializeField] private float maxDistance = 25f;

    [Header("분산 스폰 (최소 간격)")]
    [Tooltip("한 번에 여러 마리를 스폰할 때 몬스터끼리 최소한 이만큼은 떨어지도록 함")]
    [SerializeField] private float minSpacing = 3f;

    [Header("바닥 판정 (레이캐스트)")]
    [Tooltip("이 태그가 붙은 콜라이더에 맞아야 유효한 바닥으로 인정")]
    [SerializeField] private string groundTag = "Ground";
    [Tooltip("후보 좌표의 이 높이 위에서 아래로 레이를 쏨 (맵의 최고 고저차보다 높게 잡을 것)")]
    [SerializeField] private float raycastStartHeight = 50f;
    [Tooltip("레이캐스트 최대 감지 거리 (맵의 최대 낙차보다 크게 잡을 것)")]
    [SerializeField] private float raycastMaxDistance = 200f;
    [Tooltip("유효 스폰 위치 탐색 시도 횟수의 안전 상한. 이 횟수 안에 못 찾으면 경고를 띄우고 해당 몬스터 스폰을 건너뜀 (엔진 멈춤 방지)")]
    [SerializeField] private int maxSearchAttempts = 200;

    [Header("기즈모")]
    [SerializeField] private Color minDistanceColor = Color.yellow;
    [SerializeField] private Color maxDistanceColor = Color.red;
    [SerializeField] private int gizmoCircleSegments = 64;

    [Header("테스트용 자동 스폰 (프로토타입)")]
    [Tooltip("gameManager가 연결되어 있지 않을 때만 동작하는 테스트용 폴백입니다. " +
             "gameManager가 연결되면 이 옵션 대신 밤 시작 이벤트로 스폰됩니다.")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private int spawnCountOnStart = 5;

    [Header("스폰 시간차")]
    [Tooltip("몬스터 한 마리를 스폰한 뒤 다음 몬스터를 스폰하기까지의 최소 대기 시간(초)")]
    [SerializeField] private float minSpawnInterval = 0.5f;
    [Tooltip("몬스터 한 마리를 스폰한 뒤 다음 몬스터를 스폰하기까지의 최대 대기 시간(초)")]
    [SerializeField] private float maxSpawnInterval = 1.5f;

    private Coroutine spawnRoutine;

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnNightStarted += HandleNightStarted;
            gameManager.OnNightEnded += HandleNightEnded;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnNightStarted -= HandleNightStarted;
            gameManager.OnNightEnded -= HandleNightEnded;
        }
    }

    private void Start()
    {
        // gameManager가 연결되어 있으면 밤 시작 이벤트가 스폰을 트리거하므로,
        // 테스트용 자동 스폰(spawnOnStart)은 gameManager가 없을 때만 동작시킨다.
        if (gameManager == null && spawnOnStart)
        {
            SpawnMonsters(spawnCountOnStart);
        }
    }

    /// <summary>
    /// GameManager의 밤 시작 이벤트 핸들러. 그 밤의 총 쿼터만큼 스폰을 시작한다.
    /// </summary>
    private void HandleNightStarted(int quota)
    {
        SpawnMonsters(quota);
    }

    /// <summary>
    /// GameManager의 밤 종료 이벤트 핸들러. 혹시 아직 스폰이 진행 중이라면 중단한다
    /// (쿼터를 다 채워서 밤이 끝났는데도 스폰 코루틴이 남아있는 경우 방지).
    /// </summary>
    private void HandleNightEnded()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    /// <summary>
    /// count마리의 몬스터를 시간차를 두고 스폰한다. 서로 minSpacing 이상 떨어지도록 분산 배치됨.
    /// 이미 진행 중인 스폰 코루틴이 있다면 중단하고 새로 시작한다.
    /// </summary>
    public void SpawnMonsters(int count)
    {
        if (player == null)
        {
            Debug.LogError("[MonsterSpawner] player가 설정되지 않았습니다.");
            return;
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(SpawnMonstersRoutine(count));
    }

    /// <summary>
    /// 몬스터를 한 마리씩 스폰하면서 매번 minSpawnInterval~maxSpawnInterval 사이의 랜덤한 시간만큼 대기한다.
    /// </summary>
    private IEnumerator SpawnMonstersRoutine(int count)
    {
        List<Vector3> spawnedPositions = new List<Vector3>(count);

        for (int i = 0; i < count; i++)
        {
            Vector3? spawnPos = null;
            yield return FindValidSpawnPosition(spawnedPositions, result => spawnPos = result);

            if (spawnPos == null)
            {
                // 유효한 위치를 못 찾음 -> 이 몬스터는 건너뛰고 다음으로 진행 (엔진 멈춤 방지)
                continue;
            }

            spawnedPositions.Add(spawnPos.Value);

            if (monsterPrefab != null)
            {
                GameObject monster = Instantiate(monsterPrefab, spawnPos.Value, Quaternion.identity);
                AttachKillNotifier(monster);
            }

            // 마지막 몬스터를 스폰한 뒤에는 굳이 대기하지 않음
            if (i < count - 1)
            {
                float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
                yield return new WaitForSeconds(wait);
            }
        }

        spawnRoutine = null;
    }

    /// <summary>
    /// 스폰된 몬스터에 킬카운트 알림 컴포넌트를 부착한다.
    /// 몬스터 프리팹에 이미 붙어 있다면 중복 부착하지 않는다.
    /// </summary>
    private void AttachKillNotifier(GameObject monster)
    {
        if (monster.GetComponent<MonsterKillNotifier>() == null)
        {
            monster.AddComponent<MonsterKillNotifier>();
        }
    }

    /// <summary>
    /// 바닥 위 + 기존 스폰 위치들과 최소 간격을 만족하는 위치를 찾을 때까지 매 프레임 한 번씩 시도한다.
    /// 동기 while(true)와 달리 매 시도마다 한 프레임을 양보하므로 엔진이 멈추지 않는다.
    /// maxSearchAttempts 안에 못 찾으면 경고를 띄우고 실패(null)로 종료한다.
    /// </summary>
    private IEnumerator FindValidSpawnPosition(List<Vector3> alreadySpawned, System.Action<Vector3?> onResult)
    {
        int iterations = 0;

        while (maxSearchAttempts <= 0 || iterations < maxSearchAttempts)
        {
            iterations++;

            Vector3 candidateXZ = GetRandomPointInAnnulus(player.position, minDistance, maxDistance);

            if (TryRaycastGround(candidateXZ, out Vector3 groundPoint) &&
                IsFarEnoughFromOthers(groundPoint, alreadySpawned))
            {
                onResult(groundPoint);
                yield break;
            }

            // 매 시도마다 한 프레임 양보 -> 실패가 계속되어도 엔진이 멈추지 않음
            yield return null;
        }

        Debug.LogWarning($"[MonsterSpawner] 스폰 위치 탐색이 {iterations}회 시도했지만 실패했습니다. " +
                          $"조건(바닥 태그='{groundTag}', minSpacing={minSpacing})을 만족하는 위치가 " +
                          "부족한 맵일 수 있습니다. 해당 몬스터 스폰을 건너뜁니다.");
        onResult(null);
    }

    /// <summary>
    /// 플레이어 중심, minDistance ~ maxDistance 링 안에서 "면적 기준 균등 분포"로 점을 뽑는다.
    /// 단순히 각도와 반지름을 각각 균등하게 뽑으면 중심(minDistance)쪽 밀도가 높아지므로,
    /// r = sqrt(uniform(minDist^2, maxDist^2)) 공식으로 보정한다.
    /// 반환값은 y=0인 XZ 평면 오프셋이 적용된 월드 좌표(플레이어 y 기준)이며,
    /// 실제 y값은 이후 레이캐스트로 바닥에 맞춰 보정된다.
    /// </summary>
    private Vector3 GetRandomPointInAnnulus(Vector3 center, float rMin, float rMax)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);

        float rMinSq = rMin * rMin;
        float rMaxSq = rMax * rMax;
        float r = Mathf.Sqrt(Random.Range(rMinSq, rMaxSq));

        float x = center.x + r * Mathf.Cos(angle);
        float z = center.z + r * Mathf.Sin(angle);

        return new Vector3(x, center.y, z);
    }

    /// <summary>
    /// candidateXZ 위치의 상공에서 아래로 레이를 쏴서 groundTag가 붙은 콜라이더를 찾는다.
    /// </summary>
    private bool TryRaycastGround(Vector3 candidateXZ, out Vector3 groundPoint)
    {
        Vector3 rayOrigin = new Vector3(candidateXZ.x, candidateXZ.y + raycastStartHeight, candidateXZ.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastMaxDistance))
        {
            if (hit.collider.CompareTag(groundTag))
            {
                groundPoint = hit.point;
                return true;
            }
        }

        groundPoint = Vector3.zero;
        return false;
    }

    /// <summary>
    /// 기존에 스폰된 위치들과 minSpacing 이상 떨어져 있는지 확인한다.
    /// </summary>
    private bool IsFarEnoughFromOthers(Vector3 point, List<Vector3> others)
    {
        for (int i = 0; i < others.Count; i++)
        {
            if (Vector3.Distance(point, others[i]) < minSpacing)
            {
                return false;
            }
        }
        return true;
    }

    // ─────────────────────────────────────────────
    // Scene 뷰 기즈모: minDistance / maxDistance 원을 항상 표시
    // ─────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (player == null) return;

        DrawFlatWireCircle(player.position, minDistance, minDistanceColor);
        DrawFlatWireCircle(player.position, maxDistance, maxDistanceColor);
    }

    private void DrawFlatWireCircle(Vector3 center, float radius, Color color)
    {
        if (radius <= 0f) return;

        Gizmos.color = color;

        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= gizmoCircleSegments; i++)
        {
            float angle = (i / (float)gizmoCircleSegments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}