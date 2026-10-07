using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 맵에 놓인 일반 돌/나무 프리팹을 E키 꾹 누르기(LastTruck.PlayerInteract)로 채집한다.
    /// 채집에 성공하면 인벤토리에 아이템이 들어오고 이 오브젝트(프롭 전체)는 사라진다.
    /// PlayerInteract는 interactLayer(Water, 레이어 4)의 콜라이더만 찾으므로, 프롭의 자식 "GatherTrigger"
    /// (레이어 4, 트리거 콜라이더)에 이 컴포넌트를 붙여 사용한다. 설치는 에디터 메뉴 Tools/Gatherable Props 로 한다.
    /// </summary>
    public class GatherableProp : MonoBehaviour, LastTruck.IHoldInteractable
    {
        [SerializeField] private string displayName = "채집";
        [SerializeField] private string itemId = ItemIds.Stone;
        [SerializeField] private int amount = 1;
        [Tooltip("E키를 꾹 누르고 있어야 하는 시간(초).")]
        [SerializeField] private float gatherHoldSeconds = 1.5f;
        [Tooltip("채집이 끝나면 사라질 오브젝트. 비어 있으면 부모 오브젝트.")]
        [SerializeField] private GameObject target;

        private bool _done;

        public float RequiredHoldSeconds => gatherHoldSeconds;

        public void Configure(string name, string item, int amt, float holdSeconds, GameObject toRemove)
        {
            displayName = name;
            itemId = item;
            amount = amt;
            gatherHoldSeconds = holdSeconds;
            target = toRemove;
        }

        public void Interact(GameObject player)
        {
            if (_done || player == null)
                return;

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
                return;

            ItemData item = ItemCatalog.GetOrCreate().GetItem(itemId);
            if (item == null)
            {
                Debug.LogWarning($"[GatherableProp] {displayName}: 아이템 '{itemId}'를 찾을 수 없습니다.");
                return;
            }

            int finalAmount = amount;
            if (player.TryGetComponent<LastTruck.CharacterAbilityController>(out var abilityController))
                finalAmount = abilityController.GetCalculatedResourceAmount(amount);

            if (!inventory.AddItem(item, finalAmount))
            {
                Debug.LogWarning($"[GatherableProp] 인벤토리 공간이 부족하여 {item.itemName}을(를) 채집하지 못했습니다.");
                return;
            }

            _done = true;
            Debug.Log($"[GatherableProp] {displayName}에서 {item.itemName} x{finalAmount} 채집.");

            GameObject remove = target != null ? target : (transform.parent != null ? transform.parent.gameObject : gameObject);
            Destroy(remove);
        }
    }
}
