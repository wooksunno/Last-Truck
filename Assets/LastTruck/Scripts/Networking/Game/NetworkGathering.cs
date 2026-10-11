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
    ///  - 일반 채집 프롭(GatherableProp: 캐면 사라지는 돌/나무/풀)은 먼저 캔 한 명만 받고 모든 화면에서 사라진다.
    ///  - 덫(TrapController)은 호스트가 모두에게 같은 자리에 만들고, 밟았는지는 호스트가 판정한다.
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
            if (HasStateAuthority)
            {
                SpecialResourceNode.DepletionChanged += OnHostDepletionChanged;
                TrapPlaced.Triggered += OnHostTrapTriggered;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            SpecialResourceNode.DepletionChanged -= OnHostDepletionChanged;
            TrapPlaced.Triggered -= OnHostTrapTriggered;
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            SpecialResourceNode.DepletionChanged -= OnHostDepletionChanged;
            TrapPlaced.Triggered -= OnHostTrapTriggered;
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
            GiveToInventory(inventory, gathered, amount, true);
        }

        #endregion

        #region 일반 채집 프롭 (GatherableProp: 캐면 사라지는 돌/나무/풀)

        /// <summary>
        /// 멀티플레이면 프롭 채집을 네트워크로 처리하고 true. 싱글플레이면 false (기존 방식).
        /// 먼저 요청이 도착한 사람만 아이템을 받고, 프롭은 모든 화면에서 사라진다.
        /// </summary>
        public static bool TryHandleProp(GatherableProp prop, PlayerInventory inventory, ItemData item)
        {
            if (!GameLauncher.IsOnlineSession || prop == null || inventory == null || item == null) return false;

            if (!IsReady)
            {
                Debug.LogWarning("[NetworkGathering] 채집 동기화 오브젝트가 없습니다. 메뉴 1번을 다시 실행하세요. (지금은 내 화면에서만 처리)");
                return false;
            }

            if (!NetworkGameState.AllowPlayerControl) return true;

            if (!HasRoomFor(inventory.Inventory, item, prop.Amount))
            {
                Debug.LogWarning($"[GatherableProp] 인벤토리 공간이 부족하여 {item.itemName}을(를) 채집하지 못했습니다.");
                return true;
            }

            if (Instance.HasStateAuthority)
            {
                if (prop.IsDone) return true;
                Instance.RPC_PropRemoved(prop.transform.position);
                prop.RemoveGathered();
                GiveToInventory(inventory, item, prop.Amount, false);
            }
            else
            {
                TruckInventorySync.RememberLocalInventory(inventory);
                Instance.RPC_RequestProp(prop.transform.position);
            }
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestProp(Vector3 propPosition, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            GatherableProp prop = FindProp(propPosition);
            if (prop == null || prop.IsDone) return; // 다른 사람이 먼저 캤다

            NetworkPlayer character = NetworkPlayer.Find(source);
            if (character == null || !character.IsAlive) return;
            if (Vector3.Distance(character.transform.position, prop.transform.position) > maxGatherDistance) return;

            ItemData item = ItemCatalog.GetOrCreate().GetItem(prop.ItemId);
            int itemIndex = ItemIndexLookup.IndexOf(item);
            int amount = prop.Amount;

            RPC_PropRemoved(prop.transform.position);
            prop.RemoveGathered();

            if (itemIndex >= 0) RPC_GiveGathered(source, itemIndex, amount, false);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PropRemoved(Vector3 propPosition)
        {
            if (HasStateAuthority) return; // 호스트는 직접 지운다
            GatherableProp prop = FindProp(propPosition);
            if (prop != null) prop.RemoveGathered();
        }

        private GatherableProp FindProp(Vector3 position)
        {
            GatherableProp best = null;
            float bestDistance = nodeMatchTolerance * nodeMatchTolerance;
            foreach (GatherableProp prop in GatherableProp.AllProps)
            {
                if (prop == null) continue;
                float distance = (prop.transform.position - position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = prop;
                }
            }
            return best;
        }

        #endregion

        #region 받은 아이템 넣기

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
            RPC_GiveGathered(source, itemIndex, amount, true);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveGathered([RpcTarget] PlayerRef target, int itemIndex, int amount, NetworkBool applyMiningBonus)
        {
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            PlayerInventory inventory = TruckInventorySync.LocalPlayerInventory;
            GiveToInventory(inventory, item, amount, applyMiningBonus);
        }

        /// <summary>
        /// 받은 아이템을 가방에 넣는다. 캐릭터 고유 능력(탐험가 채집량, 광부 광물 보너스)은 캔 사람 기준으로 여기서 적용한다
        /// (호스트는 다른 사람의 능력을 계산하지 않는다). applyMiningBonus: 특산물(광맥)만 광부 보너스 적용 - 기존과 같음.
        /// </summary>
        private static void GiveToInventory(PlayerInventory inventory, ItemData item, int amount, bool applyMiningBonus)
        {
            if (inventory == null || item == null || amount <= 0) return;

            LastTruck.CharacterAbilityController ability = inventory.GetComponent<LastTruck.CharacterAbilityController>();
            if (ability != null)
            {
                amount = ability.GetCalculatedResourceAmount(amount);
                if (applyMiningBonus) amount = ability.GetCalculatedMiningAmount(item, amount);
            }

            int added = TruckInventorySync.AddAndCount(inventory.Inventory, item, amount);
            if (added < amount)
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {item.itemName} x{amount - added}을(를) 받지 못했습니다.");
            else
                Debug.Log($"[SpecialResourceNode] {item.itemName} x{amount} 획득.");
        }

        #endregion

        #region 덫 (TrapController / TrapPlaced)

        // 덫은 호스트가 만들고(모두에게 같은 자리) 밟았는지도 호스트만 판정한다. 몬스터 이동이 호스트에서 돌기 때문.
        private readonly System.Collections.Generic.Dictionary<int, TrapPlaced> _traps = new System.Collections.Generic.Dictionary<int, TrapPlaced>();
        private int _nextTrapId = 1;

        /// <summary>멀티플레이면 덫 설치를 네트워크로 처리하고 true. 싱글플레이면 false (기존 방식).</summary>
        public static bool TryPlaceTrap(Vector3 position, int damage, float rootSeconds, float triggerRadius)
        {
            if (!GameLauncher.IsOnlineSession || !IsReady) return false;
            Instance.RPC_RequestTrap(position, damage, rootSeconds, triggerRadius);
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestTrap(Vector3 position, int damage, float rootSeconds, float triggerRadius, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            NetworkPlayer owner = NetworkPlayer.Find(source);
            // 너무 먼 위치면 요청을 버리지 않고 설치자 발밑에 놓는다 (덫 아이템은 이미 소모됐으므로).
            if (owner != null && Vector3.Distance(owner.transform.position, position) > maxGatherDistance)
                position = owner.transform.position + Vector3.up * 0.02f;

            int id = _nextTrapId++;
            RPC_SpawnTrap(id, position, damage, Mathf.Clamp(rootSeconds, 0f, 10f), Mathf.Clamp(triggerRadius, 0.1f, 3f),
                owner != null ? owner.Object.InputAuthority : PlayerRef.None);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnTrap(int id, Vector3 position, int damage, float rootSeconds, float triggerRadius, PlayerRef owner)
        {
            if (_traps.ContainsKey(id)) return;

            GameObject trapObject = TrapController.BuildTrapVisual();
            trapObject.transform.position = position;
            TrapPlaced trap = trapObject.AddComponent<TrapPlaced>();
            NetworkPlayer ownerCharacter = NetworkPlayer.Find(owner);
            trap.Setup(damage, rootSeconds, triggerRadius, ownerCharacter != null ? ownerCharacter.transform : null);
            trap.NetworkId = id;
            trap.VisualOnly = !HasStateAuthority; // 판정은 호스트만
            _traps[id] = trap;
        }

        private void OnHostTrapTriggered(TrapPlaced trap)
        {
            if (trap == null || trap.NetworkId == 0 || Object == null || !Object.IsValid) return;
            _traps.Remove(trap.NetworkId);
            RPC_TrapTriggered(trap.NetworkId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_TrapTriggered(int id)
        {
            if (HasStateAuthority) return; // 호스트는 이미 터짐
            if (_traps.TryGetValue(id, out TrapPlaced trap))
            {
                _traps.Remove(id);
                if (trap != null) trap.PlayTriggered();
            }
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
