using UnityEngine;
using UnityEngine.AI;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어가 세우는 나무 바리케이드 / 돌 방벽. 몬스터의 길(NavMesh)을 막는다.
    /// 영구히 남으면 안전지대를 만들 수 있으므로 일정 시간이 지나면 무너진다 (나무 5분, 돌 10분).
    /// → 나무/돌을 계속 쓰게 만드는 소비처.
    /// 멀티플레이: 호스트가 모두에게 같은 자리에 세운다(NetworkGathering). 무너지는 시간은 각자 같은 시간으로 센다.
    /// </summary>
    public class Barricade : MonoBehaviour
    {
        public enum Kind { Wood = 0, Stone = 1 }

        private float _dieAt;
        private float _bornAt;
        private Vector3 _baseScale;

        public static float LifetimeOf(Kind kind) => kind == Kind.Stone ? 600f : 300f;

        /// <summary>캐릭터 앞 1.8m 바닥 위치.</summary>
        public static Vector3 PlacementPoint(Transform player)
        {
            Vector3 p = player.position + player.forward * 1.8f;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                p = hit.point;
            return p;
        }

        public static Barricade Spawn(Kind kind, Vector3 position, float yaw)
        {
            var root = new GameObject(kind == Kind.Stone ? "StoneWall" : "WoodBarricade");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            Vector3 size = kind == Kind.Stone ? new Vector3(4f, 1.8f, 0.9f) : new Vector3(3f, 1.4f, 0.6f);

            // 사람/몬스터가 못 지나가는 몸체 + 몬스터 길찾기에서 구멍을 뚫는 장애물
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = size + new Vector3(0.4f, 0f, 0.4f);
            obstacle.carving = true;

            if (kind == Kind.Stone) BuildStone(root.transform, size); else BuildWood(root.transform, size);

            var b = root.AddComponent<Barricade>();
            b._bornAt = Time.time;
            b._dieAt = Time.time + LifetimeOf(kind);
            b._baseScale = Vector3.one;
            return b;
        }

        private void Update()
        {
            // 처음엔 살짝 솟아오르고, 마지막 3초 동안 가라앉으며 사라진다
            float grow = Mathf.Clamp01((Time.time - _bornAt) / 0.3f);
            float left = _dieAt - Time.time;
            float sink = Mathf.Clamp01(left / 3f);
            transform.localScale = new Vector3(1f, Mathf.Min(grow, sink), 1f);
            if (left <= 0f) Destroy(gameObject);
        }

        // ───── 모양 ─────
        private static void BuildWood(Transform root, Vector3 size)
        {
            Color plank = new Color(0.55f, 0.38f, 0.22f), post = new Color(0.4f, 0.27f, 0.15f);
            for (int i = 0; i < 3; i++)   // 가로 판자 3장
                Part(PrimitiveType.Cube, root, new Vector3(0f, 0.3f + i * 0.45f, 0f), new Vector3(size.x, 0.32f, 0.12f), plank, new Vector3(0f, 0f, (i - 1) * 3f));
            foreach (float x in new[] { -size.x * 0.4f, size.x * 0.4f })   // 기둥
                Part(PrimitiveType.Cube, root, new Vector3(x, size.y * 0.5f, 0.08f), new Vector3(0.16f, size.y + 0.2f, 0.16f), post);
            foreach (float x in new[] { -0.9f, 0f, 0.9f })   // 앞쪽 뾰족 말뚝
                Part(PrimitiveType.Cube, root, new Vector3(x, 0.55f, -0.35f), new Vector3(0.1f, 1.1f, 0.1f), post, new Vector3(-35f, 0f, 0f));
        }

        private static void BuildStone(Transform root, Vector3 size)
        {
            Color a = new Color(0.5f, 0.49f, 0.47f), b = new Color(0.42f, 0.41f, 0.4f);
            int cols = 4;
            for (int row = 0; row < 3; row++)
                for (int c = 0; c < cols; c++)
                {
                    float w = size.x / cols;
                    float x = -size.x * 0.5f + w * (c + 0.5f) + (row % 2 == 0 ? 0f : w * 0.25f);
                    if (x > size.x * 0.5f - w * 0.3f) continue;
                    Part(PrimitiveType.Cube, root, new Vector3(x, 0.3f + row * 0.6f, 0f), new Vector3(w * 0.95f, 0.56f, size.z), (row + c) % 2 == 0 ? a : b,
                        new Vector3(0f, (c - 1.5f) * 2f, 0f));
                }
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            Destroy(go.GetComponent<Collider>());   // 충돌은 루트 상자 하나로
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); else mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
