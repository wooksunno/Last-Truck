using LastTruck;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어가 버린 아이템. 바닥에 아이콘이 떠 있고 E키로 다시 주울 수 있다.
    /// (멀티플레이: 버린 사람 화면에만 보이는 연출이라 서로 복제되지 않는다. 주울 때도 그 사람만 주울 수 있다.)
    /// </summary>
    public class DroppedItem : MonoBehaviour, IInteractable, IInteractLabel, IInteractPriority
    {
        private const int InteractLayer = 4;   // PlayerInteract.interactLayer (Water)와 같은 레이어

        private ItemData _item;
        private int _count;
        private float _bobSeed;
        private Transform _visual;
        private float _spawnTime;
        private bool _picked;

        public string InteractLabel => _item != null ? _item.itemName + (_count > 1 ? " x" + _count : "") : "";
        public string InteractSubLabel => "E 줍기";
        public Color InteractLabelColor => new Color(1f, 0.88f, 0.55f);
        public int InteractPriority => 3;

        /// <summary>player 앞쪽 바닥에 아이템을 떨어뜨린다.</summary>
        public static DroppedItem Spawn(Transform player, ItemData item, int count)
        {
            if (player == null || item == null || count <= 0)
                return null;

            Vector3 pos = player.position + player.forward * 1.3f + Vector3.up * 0.9f;

            // 바닥에 맞춘다 (벽 속에 떨어지지 않게 위쪽에서 아래로 쏜다)
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point + Vector3.up * 0.35f;

            var go = new GameObject("DroppedItem_" + item.itemName);
            go.layer = InteractLayer;
            go.transform.position = pos;

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;

            var di = go.AddComponent<DroppedItem>();
            di.Init(item, count);
            return di;
        }

        private void Init(ItemData item, int count)
        {
            _item = item;
            _count = count;
            _bobSeed = Random.value * 10f;
            _spawnTime = Time.time;

            // 아이콘 (카메라를 바라보는 스프라이트) + 바닥 그림자 원
            var visual = new GameObject("Visual");
            visual.layer = InteractLayer;
            visual.transform.SetParent(transform, false);
            _visual = visual.transform;

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = item.icon;
            if (sr.sprite == null)
                sr.color = new Color(1f, 0.85f, 0.4f);
            visual.transform.localScale = Vector3.one * 0.9f;
            sr.sortingOrder = 5;
            if (sr.sprite == null)
            {
                // 아이콘이 없는 아이템은 작은 상자로 보인다
                Destroy(sr);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<Collider>());
                cube.transform.SetParent(visual.transform, false);
                cube.transform.localScale = Vector3.one * 0.4f;
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, -0.33f, 0f);
            ring.transform.localScale = new Vector3(0.7f, 0.01f, 0.7f);
            var rr = ring.GetComponent<Renderer>();
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0.85f, 0.35f, 1f);
            rr.sharedMaterial = mat;
        }

        private void LateUpdate()
        {
            if (_visual == null) return;

            // 위아래로 살짝 떠다니며 카메라를 바라본다
            _visual.localPosition = new Vector3(0f, Mathf.Sin((Time.time + _bobSeed) * 2.2f) * 0.08f, 0f);
            Camera cam = Camera.main;
            if (cam != null)
                _visual.rotation = Quaternion.LookRotation(_visual.position - cam.transform.position, Vector3.up);
        }

        public void Interact(GameObject player)
        {
            if (_picked || _item == null) return;
            if (Time.time - _spawnTime < 0.5f) return;   // 버리자마자 눌린 E로 바로 주워지지 않게

            PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inv == null) return;

            // 들어가는 만큼만 줍고 나머지는 남겨 둔다
            int fit = 0;
            int maxStack = Mathf.Max(1, _item.maxStack);
            foreach (InventorySlot s in inv.Slots)
            {
                if (s == null || s.IsEmpty) fit += maxStack;
                else if (ItemMatching.IsSameItem(s.item, _item)) fit += Mathf.Max(0, maxStack - s.count);
            }
            fit += Mathf.Max(0, inv.MaxSlots - inv.Slots.Count) * maxStack;
            int take = Mathf.Min(fit, _count);
            if (take <= 0)
            {
                HudToast.Show("인벤토리가 가득 찼습니다.");
                return;
            }

            if (!inv.AddItem(_item, take))
                return;

            _count -= take;
            if (_count <= 0)
            {
                _picked = true;
                Destroy(gameObject);
            }
        }
    }
}
