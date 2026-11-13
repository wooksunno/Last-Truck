using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 나무/큰 바위/수정 기둥 같은 큰 맵 오브젝트에 "트럭만 막는" 충돌체를 붙인다 (게임 시작 시 한 번).
    ///  - 맵 오브젝트는 TruckPass 레이어(9)에 있어서 트럭이 그냥 통과했다. 레이어를 바꾸면 바퀴가 바위에 올라타
    ///    튀어 오르므로, 대신 TruckBlock 레이어(11)의 단순한 충돌체(나무 = 줄기 캡슐, 바위 = 상자)를 자식으로 하나 더 단다.
    ///  - TruckBlock은 트럭 몸체와만 부딪히고 바퀴와는 부딪히지 않는다 → 바퀴가 올라타지 않고 몸체가 막힌다.
    ///  - 자갈/길/철도/버섯/작은 돌처럼 낮은 것은 그대로 지나간다.
    ///  - 채집해서 오브젝트가 없어지면 자식인 충돌체도 같이 없어진다.
    /// </summary>
    public static class TruckObstacleBlockers
    {
        public const int BlockLayer = 11;

        private static readonly string[] BlockNames =
        {
            "Tree", "Rock", "Crystal", "Cactus", "Stalag", "Column", "Boulder", "Crate", "Wheelbarrow", "Barrel", "Pillar", "Rune_Stone",
        };

        private static readonly string[] PassNames =
        {
            "Pebble", "Path", "Rail", "Track", "Slat", "Mushroom", "Climbing", "Coin", "Root", "Grass", "Flower", "Stones_Crystals",
        };

        // 이보다 낮은 오브젝트는 트럭이 그냥 지나간다 (m)
        private const float MinHeight = 0.9f;
        private const float MinFootprint = 0.7f;

        private static bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _done = false;

        /// <summary>트럭이 처음 움직일 준비가 될 때 부른다 (여러 번 불러도 한 번만 만든다).</summary>
        public static void EnsureBuilt(Transform truck)
        {
            if (_done) return;
            _done = true;

            // 트럭 바퀴는 막지 않는다 (바퀴가 상자 위로 올라타면 트럭이 튀어 오른다)
            Physics.IgnoreLayerCollision(TruckMove.WheelLayer, BlockLayer, true);
            Physics.IgnoreLayerCollision(BlockLayer, BlockLayer, true);
            Physics.IgnoreLayerCollision(TruckMove.PassLayer, BlockLayer, true);

            Bounds? truckBounds = null;
            if (truck != null)
            {
                foreach (Collider tc in truck.GetComponentsInChildren<Collider>())
                {
                    if (tc is WheelCollider || tc.isTrigger) continue;
                    if (truckBounds == null) truckBounds = tc.bounds;
                    else { Bounds tb = truckBounds.Value; tb.Encapsulate(tc.bounds); truckBounds = tb; }
                }
                if (truckBounds.HasValue) { Bounds tb = truckBounds.Value; tb.Expand(1f); truckBounds = tb; }
            }

            int made = 0;
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c == null || c.isTrigger || !c.enabled || c.gameObject.layer != TruckMove.PassLayer) continue;
                if (truck != null && c.transform.IsChildOf(truck)) continue;
                if (c.attachedRigidbody != null) continue;
                if (!ShouldBlock(c.name)) continue;

                Bounds b = c.bounds;
                float footprint = Mathf.Max(b.size.x, b.size.z);
                if (b.size.y < MinHeight || footprint < MinFootprint || footprint > 40f) continue;

                // 트럭이 서 있는 자리와 겹치면 만들지 않는다 (겹친 채 생기면 트럭이 튕겨 나간다)
                if (truckBounds.HasValue && truckBounds.Value.Intersects(b)) continue;

                AddBlocker(c, b);
                made++;
            }
            Debug.Log($"[TruckObstacleBlockers] 트럭을 막는 충돌체 {made}개");
        }

        private static bool ShouldBlock(string name)
        {
            foreach (string p in PassNames) if (name.Contains(p)) return false;
            foreach (string n in BlockNames) if (name.Contains(n)) return true;
            return false;
        }

        private static void AddBlocker(Collider source, Bounds b)
        {
            var go = new GameObject("TruckBlocker");
            go.layer = BlockLayer;
            go.transform.SetParent(source.transform, false);
            go.transform.rotation = Quaternion.identity;   // 월드 기준 상자 (부모 회전과 상관없이 bounds에 맞춘다)

            Vector3 ls = go.transform.lossyScale;
            float s = Mathf.Max(0.0001f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);

            bool tree = source.name.Contains("Tree") && b.size.y > 3f;
            if (tree)
            {
                // 나무: 밑동 줄기만 막는다 (가지/잎은 트럭 위로 지나가도 된다)
                float radius = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.12f, 0.35f, 0.9f);
                float height = Mathf.Min(3.5f, b.size.y);
                go.transform.position = new Vector3(b.center.x, b.min.y + height * 0.5f, b.center.z);
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.radius = radius / s;
                cap.height = height / s;
            }
            else
            {
                // 바위/기둥: 겉모양보다 조금 작은 상자 (모서리에 걸려 튀지 않게)
                go.transform.position = b.center;
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(b.size.x * 0.8f, b.size.y * 0.9f, b.size.z * 0.8f) / s;
            }
        }
    }
}
