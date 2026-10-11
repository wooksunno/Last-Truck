using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 트럭 시작 위치 (프리팹: Multiplayer/Prefabs/SpawnPoints/TruckSpawnPoint).
    ///  - 맵 씬에 놓고 위치/방향(파란 화살표 = 트럭 앞)만 맞춘다. 씬 뷰에 트럭 크기 박스로 보인다.
    ///  - 씬이 시작되면 모든 컴퓨터에서 씬의 트럭을 이 위치/방향으로 옮긴 뒤 스스로 지워진다
    ///    (트럭은 씬 오브젝트라 모두 같은 자리로 옮겨지고, 그 뒤 위치는 NetworkTruck이 동기화).
    ///  - 높이는 바닥에 맞춘다 (원래 트럭이 바닥에서 떠 있던 높이를 유지).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class TruckSpawnPoint : MonoBehaviour
    {
        [Tooltip("씬 뷰에 그릴 트럭 크기 (m, 표시용).")]
        [SerializeField] private Vector3 previewSize = new Vector3(3f, 2.5f, 6f);

        private void Awake()
        {
            MoveTruck();
            Destroy(gameObject);
        }

        private void MoveTruck()
        {
            LastTruck.TruckInformation info = FindFirstObjectByType<LastTruck.TruckInformation>();
            if (info == null)
            {
                Debug.LogWarning("[TruckSpawnPoint] 씬에서 트럭(TruckInformation)을 찾지 못했습니다.");
                return;
            }

            Rigidbody body = info.GetComponentInParent<Rigidbody>();
            Transform truck = body != null ? body.transform : info.transform;
            Collider[] own = truck.GetComponentsInChildren<Collider>(true);

            // 원래 트럭이 바닥에서 얼마나 떠 있었는지
            float clearance = 0.5f;
            if (TryGroundY(truck.position, own, out float originalGround)) clearance = truck.position.y - originalGround;

            Vector3 target = transform.position;
            if (TryGroundY(target, own, out float groundY)) target.y = groundY + clearance;
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            truck.SetPositionAndRotation(target, rotation);
            if (body != null)
            {
                body.position = target;
                body.rotation = rotation;
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = Vector3.zero;
#else
                body.velocity = Vector3.zero;
#endif
                body.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }

        private static bool TryGroundY(Vector3 point, Collider[] ignore, out float groundY)
        {
            groundY = 0f;
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 20f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            foreach (RaycastHit hit in hits)
            {
                if (System.Array.IndexOf(ignore, hit.collider) >= 0) continue;
                if (float.IsNaN(best) || Mathf.Abs(hit.point.y - point.y) < Mathf.Abs(best - point.y)) best = hit.point.y;
            }
            if (float.IsNaN(best)) return false;
            groundY = best;
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
            Gizmos.DrawCube(new Vector3(0f, previewSize.y * 0.5f, 0f), previewSize);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, previewSize.y * 0.5f, 0f), previewSize);
            Vector3 tip = new Vector3(0f, 0.2f, previewSize.z * 0.5f + 1.5f);
            Gizmos.DrawLine(new Vector3(0f, 0.2f, 0f), tip);
            Gizmos.DrawLine(tip, tip + new Vector3(-0.6f, 0f, -0.8f));
            Gizmos.DrawLine(tip, tip + new Vector3(0.6f, 0f, -0.8f));
            Gizmos.matrix = Matrix4x4.identity;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * (previewSize.y + 0.5f), "Truck Spawn (앞 →)");
#endif
        }
    }
}
