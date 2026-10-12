using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 플레이어 스폰 범위 (프리팹: Multiplayer/Prefabs/SpawnPoints/PlayerSpawnPoint).
    ///  - 맵 씬에 끌어다 놓고 위치/회전과 Size(박스 크기)만 조절한다. 씬 뷰에 초록 박스 + 바라보는 방향 화살표로 보인다.
    ///  - 멀티플레이: 호스트가 이 박스 안에 참가자들을 서로 떨어뜨려 스폰한다 (바닥 높이는 레이로 맞춤).
    ///  - 싱글플레이: 씬에 놓인 캐릭터를 박스 가운데로 옮긴다.
    ///  - 스폰이 끝나면(게임 시작) 스스로 지워진다 → 게임 중에는 아무 영향 없음.
    ///  - 씬에 없으면 예전 방식(씬에 놓인 싱글플레이 캐릭터 자리)으로 스폰한다.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class PlayerSpawnPoint : MonoBehaviour
    {
        #region 인스펙터

        [Tooltip("스폰 범위 박스 크기 (m). 이 안에서 플레이어들이 나타난다.")]
        [SerializeField] private Vector3 size = new Vector3(6f, 2f, 6f);

        [Tooltip("플레이어끼리 최소 이만큼 떨어져서 스폰한다 (m).")]
        [SerializeField] private float minSpacing = 1.5f;

        #endregion

        #region 조회

        /// <summary>씬에 놓인 스폰 범위 (없으면 null).</summary>
        public static PlayerSpawnPoint Active { get; private set; }

        private readonly System.Collections.Generic.List<Vector3> _used = new System.Collections.Generic.List<Vector3>();

        #endregion

        #region 생명주기

        private void Awake()
        {
            if (Active == null) Active = this;
        }

        private void Start()
        {
            // 싱글플레이: 씬에 놓인 캐릭터를 범위 안으로 옮기고 바로 지운다.
            if (!GameLauncher.IsOnlineSession)
            {
                MoveOfflinePlayers();
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // 멀티플레이: 모두 스폰되고 게임이 시작되면 지운다.
            if (GameLauncher.IsOnlineSession && NetworkGameState.AllowPlayerControl) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        #endregion

        #region 스폰 위치

        /// <summary>slot번째 플레이어 위치: 박스 안 무작위 + 다른 플레이어와 minSpacing 이상, 바닥 높이에 맞춤.</summary>
        public void GetSpawnPose(int slot, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 half = size * 0.5f;
            var random = new System.Random(unchecked(slot * 7919 + 17));

            Vector3 best = transform.position;
            float bestSpacing = -1f;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector3 local = new Vector3(
                    (float)(random.NextDouble() * 2 - 1) * half.x,
                    0f,
                    (float)(random.NextDouble() * 2 - 1) * half.z);
                Vector3 candidate = transform.TransformPoint(local);

                float spacing = float.MaxValue;
                foreach (Vector3 used in _used)
                {
                    Vector3 d = used - candidate;
                    d.y = 0f;
                    spacing = Mathf.Min(spacing, d.magnitude);
                }

                if (spacing > bestSpacing)
                {
                    bestSpacing = spacing;
                    best = candidate;
                }
                if (spacing >= minSpacing) break;
            }

            _used.Add(best);
            position = SnapToGround(best, transform.position.y, half.y + 2f);
        }

        private void MoveOfflinePlayers()
        {
            int slot = 0;
            foreach (PlayerMove move in FindObjectsByType<PlayerMove>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (move.GetComponent<NetworkPlayer>() != null) continue;
                GetSpawnPose(slot++, out Vector3 position, out Quaternion rotation);

                Rigidbody body = move.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.position = position;
                    body.rotation = rotation;
                }
                move.transform.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>위에서 아래로 레이를 쏴서 원래 높이에 가장 가까운 바닥에 맞춘다 (나무 위 등 피함).</summary>
        public static Vector3 SnapToGround(Vector3 point, float referenceY, float searchHeight)
        {
            Vector3 origin = new Vector3(point.x, referenceY + searchHeight, point.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, searchHeight * 2f + 20f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.NaN;
            foreach (RaycastHit hit in hits)
            {
                if (float.IsNaN(bestY) || Mathf.Abs(hit.point.y - referenceY) < Mathf.Abs(bestY - referenceY)) bestY = hit.point.y;
            }
            point.y = float.IsNaN(bestY) ? referenceY + 0.5f : bestY + 0.1f;
            return point;
        }

        #endregion

        #region 씬 뷰 표시

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.15f);
            Gizmos.DrawCube(new Vector3(0f, size.y * 0.5f, 0f), size);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
            // 바라보는 방향 화살표
            Vector3 tip = new Vector3(0f, 0.1f, size.z * 0.5f + 1f);
            Gizmos.DrawLine(new Vector3(0f, 0.1f, 0f), tip);
            Gizmos.DrawLine(tip, tip + new Vector3(-0.4f, 0f, -0.5f));
            Gizmos.DrawLine(tip, tip + new Vector3(0.4f, 0f, -0.5f));
            Gizmos.matrix = Matrix4x4.identity;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * (size.y + 0.5f), "Player Spawn");
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        #endregion
    }
}
