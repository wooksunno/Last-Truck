using CraftingSystem;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 특산물(SpecialResourceNode) 채집 동기화 (NetworkGameState 프리팹에 같이 붙는다).
    ///
    ///  - 특산물은 채집 횟수가 차면 사라졌다가 일정 시간 뒤 다시 생긴다. 이 "남은 횟수 / 고갈 / 재생성"은 호스트만 계산한다.
    ///  - 클라이언트가 E로 채집을 끝내면 "이 노드 채집" 요청을 보내고, 호스트가 보너스(희귀 광물 등)를 굴려
    ///    결과 아이템을 그 사람에게만 준다. 두 사람이 동시에 마지막 한 번을 캐도 한 명만 받는다.
    ///  - 고갈/재생성은 모두에게 알려서 모든 화면에서 같이 사라지고 같이 생긴다.
    ///  - 노드는 모든 컴퓨터가 같은 시드로 같은 자리에 만들므로 "위치"로 같은 노드를 찾는다.
    ///
    /// 일반 자원(ResourceNode: 무한 채집)과 개인 가방/파우치/시설 가공은 각자 자기 컴퓨터에서 처리한다
    /// (다른 사람에게 영향이 없는 개인 상태라 동기화할 필요가 없다).
    /// </summary>
    public class NetworkGathering : NetworkBehaviour
    {
        #region 인스펙터

        [Tooltip("이 거리(m)보다 멀리서 온 채집 요청은 무시한다 (호스트가 확인).")]
        [SerializeField] private float maxGatherDistance = 6f;

        [Tooltip("위치로 노드를 찾을 때 허용 오차(m).")]
        [SerializeField] private float nodeMatchTolerance = 0.25f;

        #endregion

        #region 정적 조회

        public static NetworkGathering Instance { get; private set; }

        public static bool IsReady => Instance != null && Instance.Object != null && Instance.Object.IsValid;

        #endregion

        #region 생명주기

        public override void Spawned()
        {
            Instance = this;
            if (HasStateAuthority) SpecialResourceNode.DepletionChanged += OnHostDepletionChanged;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            SpecialResourceNode.DepletionChanged -= OnHostDepletionChanged;
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            SpecialResourceNode.DepletionChanged -= OnHostDepletionChanged;
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 채집 (SpecialResourceNode.Interact에서 부른다)

        /// <summary>
        /// 멀티플레이면 채집을 네트워크로 처리하고 true (노드는 더 할 일 없음). 싱글플레이면 false (기존 방식).
        /// </summary>
        public static bool TryHandleGather(SpecialResourceNode node, PlayerInventory inventory)
        {
            if (!GameLauncher.IsOnlineSession || node == null || inventory == null) return false;

            if (!IsReady)
            {
                Debug.LogWarning("[NetworkGathering] 채집 동기화 오브젝트가 없습니다. " +
                                 "메뉴 'LastTruck > Multiplayer > 1. 네트워크 프리팹 생성'을 다시 실행하세요. (지금은 내 화면에서만 처리)");
                return false;
            }

            // 게임 시작 전(다른 사람 로딩 중)에는 채집하지 않는다 (아직 맵을 만들지 못한 사람이 고갈 알림을 놓치지 않게).
            if (!NetworkGameState.AllowPlayerControl) return true;

            // 가방이 꽉 차 있으면 요청하지 않는다 (채집 횟수만 날아가지 않게). 보너스로 나올 수 있는 아이템도 확인한다.
            if (!HasRoomFor(inventory.Inventory, node.Item, node.Amount) ||
                (node.CommonBonusItem != null && !HasRoomFor(inventory.Inventory, node.CommonBonusItem, node.Amount)) ||
                (node.RareBonusItem != null && !HasRoomFor(inventory.Inventory, node.RareBonusItem, node.Amount)))
            {
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {node.Item.itemName}을(를) 획득하지 못했습니다.");
                return true;
            }

            if (Instance.HasStateAuthority)
            {
                Instance.HostGather(node, inventory);
            }
            else
            {
                TruckInventorySync.RememberLocalInventory(inventory);
                Instance.RPC_RequestGather(node.transform.position);
            }
            return true;
        }

        /// <summary>호스트 자신이 캤을 때.</summary>
        private void HostGather(SpecialResourceNode node, PlayerInventory inventory)
        {
            if (!node.TryTakeHit(out ItemData gathered, out int amount)) return;
            GiveToInventory(inventory, gathered, amount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestGather(Vector3 nodePosition, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            SpecialResourceNode node = FindNode(nodePosition);
            if (node == null) return;

            // 멀리서 온 요청은 무시 (캐릭터 위치 기준)
            NetworkPlayer character = NetworkPlayer.Find(source);
            if (character == null || !character.IsAlive) return;
            if (Vector3.Distance(character.transform.position, node.transform.position) > maxGatherDistance) return;

            if (!node.TryTakeHit(out ItemData gathered, out int amount)) return; // 이미 다른 사람이 마지막을 캤다

            int itemIndex = ItemIndexLookup.IndexOf(gathered);
            if (itemIndex < 0) return;
            RPC_GiveGathered(source, itemIndex, amount);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveGathered([RpcTarget] PlayerRef target, int itemIndex, int amount)
        {
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            PlayerInventory inventory = TruckInventorySync.LocalPlayerInventory;
            GiveToInventory(inventory, item, amount);
        }

        private static void GiveToInventory(PlayerInventory inventory, ItemData item, int amount)
        {
            if (inventory == null || item == null || amount <= 0) return;

            int added = TruckInventorySync.AddAndCount(inventory.Inventory, item, amount);
            if (added < amount)
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {item.itemName} x{amount - added}을(를) 받지 못했습니다.");
            else
                Debug.Log($"[SpecialResourceNode] {item.itemName} x{amount} 획득.");
        }

        #endregion

        #region 고갈 / 재생성 알림 (호스트 → 모두)

        private void OnHostDepletionChanged(SpecialResourceNode node, bool depleted)
        {
            if (node == null || Object == null || !Object.IsValid) return;
            RPC_SetDepleted(node.transform.position, depleted);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetDepleted(Vector3 nodePosition, NetworkBool depleted)
        {
            if (HasStateAuthority) return; // 호스트는 이미 반영됨
            SpecialResourceNode node = FindNode(nodePosition);
            if (node != null) node.ApplyNetworkDepleted(depleted);
        }

        #endregion

        #region 도우미

        private SpecialResourceNode FindNode(Vector3 position)
        {
            SpecialResourceNode best = null;
            float bestDistance = nodeMatchTolerance * nodeMatchTolerance;
            foreach (SpecialResourceNode node in SpecialResourceNode.AllNodes)
            {
                if (node == null) continue;
                float distance = (node.transform.position - position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }
            return best;
        }

        /// <summary>가방에 이 아이템을 amount개 넣을 자리가 있는가.</summary>
        public static bool HasRoomFor(ItemStackInventory inventory, ItemData item, int amount)
        {
            if (inventory == null || item == null) return false;
            int maxStack = Mathf.Max(1, item.maxStack);
            int room = 0;
            foreach (InventorySlot slot in inventory.Slots)
            {
                if (slot == null || slot.IsEmpty) room += maxStack;
                else if (ItemMatching.IsSameItem(slot.item, item)) room += Mathf.Max(0, maxStack - slot.count);
            }
            room += Mathf.Max(0, inventory.MaxSlots - inventory.Slots.Count) * maxStack;
            return room >= amount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        #endregion
    }
}
