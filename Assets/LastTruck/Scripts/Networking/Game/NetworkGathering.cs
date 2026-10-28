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

            // 가방이 꽉 차 있어도 캔다. 받을 때 안 들어가는 만큼은 발밑에 떨어진다 (GiveToInventory).
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

            // 가방이 꽉 차 있어도 캔다. 받을 때 안 들어가는 만큼은 발밑에 떨어진다 (GiveToInventory).
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

            // 안 들어가는 만큼은 받은 사람 발밑에 떨어진다 (떨어진 아이템은 그 사람 화면에만 보인다)
            int dropped = inventory.AddOrDrop(item, amount);
            Debug.Log($"[SpecialResourceNode] {item.itemName} x{amount} 획득" + (dropped > 0 ? $" (가방 초과 {dropped}개는 바닥에)" : "."));
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

        #region 바리케이드 / 돌 방벽 (TrapController → Barricade)

        // 몬스터 길찾기(NavMesh)는 호스트에서 돌므로 호스트 화면에도 반드시 있어야 한다. 모두에게 같은 자리에 세운다.

        /// <summary>멀티플레이면 바리케이드 설치를 호스트에 요청하고 true (아이템은 이미 소모됨). 싱글이면 false.</summary>
        public static bool TryPlaceBarricade(int kind, Vector3 position, float yaw)
        {
            if (!GameLauncher.IsOnlineSession || !IsReady) return false;
            Instance.RPC_RequestBarricade(kind, position, yaw);
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestBarricade(int kind, Vector3 position, float yaw, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            NetworkPlayer owner = NetworkPlayer.Find(source);
            // 너무 먼 위치면 설치자 바로 앞에 세운다 (아이템은 이미 소모됐으므로 버리지 않는다)
            if (owner != null && Vector3.Distance(owner.transform.position, position) > maxGatherDistance)
                position = owner.transform.position + owner.transform.forward * 1.8f;
            RPC_SpawnBarricade(Mathf.Clamp(kind, 0, 1), position, yaw);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnBarricade(int kind, Vector3 position, float yaw)
        {
            Barricade.Spawn((Barricade.Kind)kind, position, yaw);
        }

        #endregion

        #region 석유 펌프잭 (OilWellSite / OilPumpjack)

        // 펌프잭은 호스트가 설치/수거/회수를 정하고 모두에게 알린다. 유정은 "위치"로 찾는다.
        // 탱크 양은 각 화면이 같은 속도로 채우되(표시용), 기름을 꺼낼 때는 호스트 값이 기준이다.

        /// <summary>멀티플레이면 펌프잭 설치를 호스트에 요청하고 true (펌프잭 아이템은 이미 소모됨). 싱글이면 false.</summary>
        public static bool TryPlacePump(Vector3 sitePosition)
        {
            if (!GameLauncher.IsOnlineSession || !IsReady) return false;
            Instance.RPC_RequestPump(sitePosition);
            return true;
        }

        public static bool TryCollectPump(Vector3 sitePosition)
        {
            if (!GameLauncher.IsOnlineSession || !IsReady) return false;
            Instance.RPC_RequestPumpCollect(sitePosition);
            return true;
        }

        public static bool TryRemovePump(Vector3 sitePosition)
        {
            if (!GameLauncher.IsOnlineSession || !IsReady) return false;
            Instance.RPC_RequestPumpRemove(sitePosition);
            return true;
        }

        private bool IsNear(PlayerRef source, Vector3 position)
        {
            NetworkPlayer character = NetworkPlayer.Find(source);
            return character != null && character.IsAlive &&
                   Vector3.Distance(character.transform.position, position) <= maxGatherDistance + 3f;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPump(Vector3 sitePosition, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            OilWellSite site = OilWellSite.FindAt(sitePosition);
            if (site == null || site.Pump != null || !IsNear(source, sitePosition))
            {
                // 이미 다른 사람이 설치했거나 멀면: 소모된 펌프잭을 돌려준다
                int pumpIndex = ItemIndexLookup.IndexOf(ItemCatalog.GetOrCreate().GetItem(ItemIds.OilPumpjack));
                if (pumpIndex >= 0) RPC_GiveItemExact(source, pumpIndex, 1);
                return;
            }
            RPC_SpawnPump(sitePosition);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnPump(Vector3 sitePosition)
        {
            OilWellSite site = OilWellSite.FindAt(sitePosition);
            if (site != null) site.SpawnPump();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPumpCollect(Vector3 sitePosition, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            OilWellSite site = OilWellSite.FindAt(sitePosition);
            if (site == null || site.Pump == null || !IsNear(source, sitePosition)) return;

            int amount = site.Pump.TakeReady();
            RPC_SetPumpTank(sitePosition, site.Pump.Tank);
            int oilIndex = ItemIndexLookup.IndexOf(ItemCatalog.GetOrCreate().GetItem(ItemIds.Oil));
            if (amount > 0 && oilIndex >= 0) RPC_GiveItemExact(source, oilIndex, amount);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetPumpTank(Vector3 sitePosition, float tank)
        {
            if (HasStateAuthority) return; // 호스트는 이미 반영됨
            OilWellSite site = OilWellSite.FindAt(sitePosition);
            if (site != null && site.Pump != null) site.Pump.SetTank(tank);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPumpRemove(Vector3 sitePosition, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            OilWellSite site = OilWellSite.FindAt(sitePosition);
            if (site == null || site.Pump == null || !IsNear(source, sitePosition)) return;
            if (site.Pump.Ready >= 1) return; // 기름이 남아 있으면 먼저 받아야 한다

            RPC_RemovePump(sitePosition);
            int pumpIndex = ItemIndexLookup.IndexOf(ItemCatalog.GetOrCreate().GetItem(ItemIds.OilPumpjack));
            if (pumpIndex >= 0) RPC_GiveItemExact(source, pumpIndex, 1);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RemovePump(Vector3 sitePosition)
        {
            OilWellSite site = OilWellSite.FindAt(sitePosition);
            if (site != null) site.RemovePump();
        }

        /// <summary>능력 보너스 없이 정확히 그 개수만 준다 (펌프잭 반환/펌프 기름). 넘치면 발밑에 떨어진다.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveItemExact([RpcTarget] PlayerRef target, int itemIndex, int amount)
        {
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            PlayerInventory inventory = TruckInventorySync.LocalPlayerInventory;
            if (item == null || inventory == null || amount <= 0) return;
            inventory.AddOrDrop(item, amount);
            if (item.itemID == ItemIds.Oil) LastTruck.HudToast.Show($"기름 {amount}개를 받았어요");
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
            int maxStack = inventory.StackLimitFor(item);
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
