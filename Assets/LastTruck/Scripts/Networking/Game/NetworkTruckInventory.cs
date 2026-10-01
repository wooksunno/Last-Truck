using System.Collections.Generic;
using CraftingSystem;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 트럭 인벤토리 실시간 동기화 (NetworkGameState 프리팹에 같이 붙는다).
    ///
    /// 규칙
    ///  - 트럭 인벤토리의 "진짜 값"은 호스트에만 있다. 호스트는 바뀔 때마다 슬롯 목록(아이템 번호 + 개수)을 네트워크 배열에 쓴다.
    ///  - 클라이언트는 그 배열을 받아 자기 화면의 트럭 인벤토리를 통째로 덮어쓴다 → 열려 있는 트럭 창이 즉시 새로 그려진다.
    ///  - 클라이언트는 트럭 인벤토리를 직접 바꾸지 않고 "넣기/꺼내기/제작" 요청만 보낸다 (TruckInventorySync).
    ///    호스트가 실제 수량을 확인한 뒤 처리하므로, 두 사람이 동시에 같은 아이템을 꺼내도 복사되지 않는다.
    ///  - 꺼내기: 호스트가 트럭에서 뺀 만큼만 요청한 사람에게 돌려준다 (그 사람 가방이 꽉 차면 남는 건 다시 트럭으로).
    ///  - 넣기: 클라이언트 가방에서 먼저 빼고 요청 → 트럭이 꽉 차서 못 넣은 만큼은 돌려받는다.
    ///
    /// 아이템/레시피는 ItemCatalog 안의 순서(번호)로 주고받는다 (모든 컴퓨터가 같은 카탈로그를 쓴다).
    /// </summary>
    public class NetworkTruckInventory : NetworkBehaviour
    {
        #region 네트워크 상태

        public const int MaxSlots = 64;

        /// <summary>슬롯별 아이템 번호 + 1 (0 = 빈 칸).</summary>
        [Networked, Capacity(MaxSlots)] private NetworkArray<int> SlotItems => default;
        [Networked, Capacity(MaxSlots)] private NetworkArray<int> SlotCounts => default;
        [Networked] private int SlotCount { get; set; }

        /// <summary>호스트가 값을 쓸 때마다 1씩 오른다 (클라이언트는 바뀌었을 때만 적용).</summary>
        [Networked] private int Revision { get; set; }

        #endregion

        #region 상태 / 조회

        public static NetworkTruckInventory Instance { get; private set; }

        public static bool IsReady => Instance != null && Instance.Object != null && Instance.Object.IsValid;

        private TruckInventory _truck;
        private TruckInventory _subscribedTruck;
        private bool _dirty = true;
        private int _appliedRevision;

        private TruckInventory Truck
        {
            get
            {
                if (_truck == null) _truck = FindFirstObjectByType<TruckInventory>();
                return _truck;
            }
        }

        #endregion

        #region 생명주기

        public override void Spawned()
        {
            Instance = this;
            _dirty = true;
            _appliedRevision = 0;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Subscribe()
        {
            TruckInventory truck = Truck;
            if (truck == _subscribedTruck) return;
            Unsubscribe();
            _subscribedTruck = truck;
            if (_subscribedTruck != null)
            {
                _subscribedTruck.Changed += OnHostTruckChanged;
                _dirty = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedTruck != null) _subscribedTruck.Changed -= OnHostTruckChanged;
            _subscribedTruck = null;
        }

        private void OnHostTruckChanged() => _dirty = true;

        #endregion

        #region 호스트: 트럭 → 네트워크 배열

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            Subscribe();
            if (!_dirty || Truck == null) return;
            _dirty = false;
            WriteSnapshot(Truck);
        }

        private void WriteSnapshot(TruckInventory truck)
        {
            IReadOnlyList<InventorySlot> slots = truck.Slots;
            int written = 0;
            for (int i = 0; i < slots.Count && written < MaxSlots; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null || slot.IsEmpty) continue;

                int index = ItemIndexLookup.IndexOf(slot.item);
                if (index < 0)
                {
                    Debug.LogWarning($"[NetworkTruckInventory] 카탈로그에 없는 아이템이라 동기화하지 못했습니다: {slot.item.itemName}");
                    continue;
                }

                SlotItems.Set(written, index + 1);
                SlotCounts.Set(written, slot.count);
                written++;
            }

            for (int i = written; i < MaxSlots; i++)
            {
                SlotItems.Set(i, 0);
                SlotCounts.Set(i, 0);
            }

            SlotCount = written;
            Revision++;
        }

        #endregion

        #region 클라이언트: 네트워크 배열 → 트럭

        public override void Render()
        {
            if (HasStateAuthority) return;
            if (Revision == 0 || Revision == _appliedRevision) return;

            TruckInventory truck = Truck;
            if (truck == null) return;

            _appliedRevision = Revision;
            var slots = new List<InventorySlot>(SlotCount);
            for (int i = 0; i < SlotCount && i < MaxSlots; i++)
            {
                ItemData item = ItemIndexLookup.ItemAt(SlotItems[i] - 1);
                int count = SlotCounts[i];
                if (item != null && count > 0) slots.Add(new InventorySlot(item, count));
            }
            truck.Inventory.ReplaceAll(slots); // Changed 이벤트 → 열려 있는 트럭 창이 새로 그려진다
        }

        #endregion

        #region 요청 (클라이언트 → 호스트)

        public void RequestDeposit(ItemData item, int count)
        {
            int index = ItemIndexLookup.IndexOf(item);
            if (index >= 0 && count > 0) RPC_RequestDeposit(index, count);
        }

        public void RequestWithdraw(ItemData item, int count)
        {
            int index = ItemIndexLookup.IndexOf(item);
            if (index >= 0 && count > 0) RPC_RequestWithdraw(index, count);
        }

        public void RequestCraft(RecipeData recipe)
        {
            int index = ItemIndexLookup.RecipeIndexOf(recipe);
            if (index >= 0) RPC_RequestCraft(index);
        }

        public void RequestGrantRaw(int amount)
        {
            if (amount > 0) RPC_RequestGrantRaw(amount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDeposit(int itemIndex, int count, RpcInfo info = default)
        {
            TruckInventory truck = Truck;
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            if (item == null || count <= 0) return;

            int stored = truck != null ? TruckInventorySync.AddAndCount(truck.Inventory, item, count) : 0;
            int leftover = count - stored;
            if (leftover > 0) RPC_GiveItem(info.Source, itemIndex, leftover); // 트럭이 꽉 차서 못 넣은 만큼 돌려준다
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestWithdraw(int itemIndex, int count, RpcInfo info = default)
        {
            TruckInventory truck = Truck;
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            if (truck == null || item == null || count <= 0) return;

            int take = Mathf.Min(count, truck.GetItemCount(item)); // 다른 사람이 먼저 가져갔으면 남은 만큼만
            if (take <= 0) return;
            if (!truck.RemoveItem(item, take)) return;

            RPC_GiveItem(info.Source, itemIndex, take);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestCraft(int recipeIndex)
        {
            TruckInventory truck = Truck;
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            if (truck == null || recipe == null) return;

            TruckCraftingManager craft = truck.GetComponent<TruckCraftingManager>();
            if (craft != null) craft.TryCraft(recipe);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestGrantRaw(int amount)
        {
            TruckInventorySync.GrantRawLocal(Truck, Mathf.Clamp(amount, 1, 100));
        }

        /// <summary>호스트 → 특정 플레이어: 이 아이템을 네 가방에 넣어라 (꺼내기 결과 / 넣기 실패 반환).</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveItem([RpcTarget] PlayerRef target, int itemIndex, int count)
        {
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            if (item == null || count <= 0) return;

            PlayerInventory player = TruckInventorySync.LocalPlayerInventory;
            int added = player != null ? TruckInventorySync.AddAndCount(player.Inventory, item, count) : 0;
            int leftover = count - added;
            if (leftover <= 0) return;

            // 내 가방이 꽉 찼다 → 남는 건 트럭으로 되돌린다 (호스트 자신이면 바로 넣는다)
            if (HasStateAuthority)
            {
                TruckInventory truck = Truck;
                if (truck != null) TruckInventorySync.AddAndCount(truck.Inventory, item, leftover);
            }
            else
            {
                RPC_RequestDeposit(itemIndex, leftover);
            }
            Debug.LogWarning($"[트럭 인벤토리] 가방 공간이 부족해서 {item.itemName} x{leftover}을(를) 트럭에 되돌렸습니다.");
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }

    /// <summary>
    /// UI(GameUIController)가 트럭 인벤토리를 바꿀 때 쓰는 창구.
    ///  - 싱글플레이 / 호스트: 바로 트럭 인벤토리를 바꾼다 (호스트는 NetworkTruckInventory가 알아서 모두에게 보낸다).
    ///  - 클라이언트: 호스트에게 요청만 보낸다.
    /// </summary>
    public static class TruckInventorySync
    {
        #region 조회

        /// <summary>멀티플레이 클라이언트인가 (트럭 인벤토리를 직접 바꾸면 안 되는 컴퓨터).</summary>
        public static bool IsRemoteClient
        {
            get
            {
                if (!GameLauncher.IsOnlineSession) return false;
                GameLauncher launcher = GameLauncher.Instance;
                return launcher == null || !launcher.IsHost;
            }
        }

        private static PlayerInventory _localPlayerInventory;
        private static bool _warnedMissing;

        /// <summary>이 컴퓨터 플레이어의 가방 (꺼낸 아이템을 받을 곳).</summary>
        public static PlayerInventory LocalPlayerInventory
        {
            get
            {
                if (_localPlayerInventory != null) return _localPlayerInventory;
                NetworkPlayer local = NetworkPlayer.Local;
                if (local != null) _localPlayerInventory = local.GetComponentInChildren<PlayerInventory>(true);
                return _localPlayerInventory;
            }
        }

        /// <summary>이 컴퓨터 플레이어의 가방을 기억한다 (호스트가 돌려주는 아이템을 받을 곳).</summary>
        public static void RememberLocalInventory(PlayerInventory inventory)
        {
            if (inventory != null) _localPlayerInventory = inventory;
        }

        private static NetworkTruckInventory Net
        {
            get
            {
                if (NetworkTruckInventory.IsReady) return NetworkTruckInventory.Instance;
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogWarning("[TruckInventorySync] 트럭 인벤토리 동기화 오브젝트가 없습니다. " +
                                     "메뉴 'LastTruck > Multiplayer > 1. 네트워크 프리팹 생성'을 다시 실행하세요. (지금은 내 화면에서만 바뀝니다)");
                }
                return null;
            }
        }

        #endregion

        #region UI에서 부르는 함수

        /// <summary>내 가방 → 트럭.</summary>
        public static void Deposit(TruckInventory truck, PlayerInventory player, ItemData item, int count)
        {
            if (truck == null || player == null || item == null || count <= 0) return;
            _localPlayerInventory = player;

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                MoveItems(player.Inventory, truck.Inventory, item, count);
                return;
            }

            count = Mathf.Min(count, player.Inventory.GetItemCount(item));
            if (count <= 0 || !player.Inventory.RemoveItem(item, count)) return;
            net.RequestDeposit(item, count);
        }

        /// <summary>트럭 → 내 가방.</summary>
        public static void Withdraw(TruckInventory truck, PlayerInventory player, ItemData item, int count)
        {
            if (truck == null || player == null || item == null || count <= 0) return;
            _localPlayerInventory = player;

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                MoveItems(truck.Inventory, player.Inventory, item, count);
                return;
            }

            net.RequestWithdraw(item, count);
        }

        /// <summary>트럭 재료로 제작/가공. 클라이언트는 요청만 보내므로 결과는 트럭 창이 갱신되면서 보인다.</summary>
        public static void Craft(TruckCraftingManager craft, RecipeData recipe)
        {
            if (craft == null || recipe == null) return;

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                if (!craft.TryCraft(recipe)) Debug.LogWarning("재료가 부족합니다.");
                return;
            }

            net.RequestCraft(recipe);
        }

        /// <summary>테스트 버튼 "원재료 +N".</summary>
        public static void GrantRaw(TruckInventory truck, int amount)
        {
            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null) GrantRawLocal(truck, amount);
            else net.RequestGrantRaw(amount);
        }

        #endregion

        #region 실제 처리 (호스트 / 싱글)

        public static void GrantRawLocal(TruckInventory truck, int amount)
        {
            if (truck == null || amount <= 0) return;
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            truck.AddItem(catalog.GetItem(ItemIds.Wood), amount);
            truck.AddItem(catalog.GetItem(ItemIds.Stone), amount);
            truck.AddItem(catalog.GetItem(ItemIds.IronOre), amount);
            truck.AddItem(catalog.GetItem(ItemIds.CopperOre), amount);
        }

        /// <summary>
        /// source → destination으로 옮긴다. 목적지에 실제로 들어간 만큼만 source에서 뺀다 (공간 부족 시 복사 방지).
        /// </summary>
        public static int MoveItems(ItemStackInventory source, ItemStackInventory destination, ItemData item, int count)
        {
            if (source == null || destination == null || item == null || count <= 0) return 0;
            count = Mathf.Min(count, source.GetItemCount(item));
            if (count <= 0) return 0;

            int moved = AddAndCount(destination, item, count);
            if (moved > 0) source.RemoveItem(item, moved);
            if (moved < count) Debug.LogWarning("공간이 부족하여 아이템을 모두 옮기지 못했습니다.");
            return moved;
        }

        /// <summary>AddItem은 공간이 모자라면 일부만 넣고 false를 돌려주므로, 실제로 들어간 개수를 센다.</summary>
        public static int AddAndCount(ItemStackInventory inventory, ItemData item, int count)
        {
            if (inventory == null || item == null || count <= 0) return 0;
            int before = inventory.GetItemCount(item);
            inventory.AddItem(item, count);
            return Mathf.Clamp(inventory.GetItemCount(item) - before, 0, count);
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _localPlayerInventory = null;
            _warnedMissing = false;
        }
    }

    /// <summary>아이템/레시피 ↔ 카탈로그 번호 변환 (네트워크로는 번호만 보낸다).</summary>
    public static class ItemIndexLookup
    {
        private static ItemCatalog _catalog;
        private static Dictionary<string, int> _itemIndex;
        private static Dictionary<string, int> _recipeIndex;

        private static ItemCatalog Catalog
        {
            get
            {
                ItemCatalog current = ItemCatalog.GetOrCreate();
                if (current != _catalog)
                {
                    _catalog = current;
                    _itemIndex = null;
                    _recipeIndex = null;
                }
                return _catalog;
            }
        }

        public static int IndexOf(ItemData item)
        {
            if (item == null || Catalog == null) return -1;
            if (_itemIndex == null)
            {
                _itemIndex = new Dictionary<string, int>();
                for (int i = 0; i < _catalog.Items.Count; i++)
                {
                    ItemData entry = _catalog.Items[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.itemID) && !_itemIndex.ContainsKey(entry.itemID))
                        _itemIndex[entry.itemID] = i;
                }
            }
            return !string.IsNullOrEmpty(item.itemID) && _itemIndex.TryGetValue(item.itemID, out int index) ? index : -1;
        }

        public static ItemData ItemAt(int index)
        {
            ItemCatalog catalog = Catalog;
            return catalog != null && index >= 0 && index < catalog.Items.Count ? catalog.Items[index] : null;
        }

        public static int RecipeIndexOf(RecipeData recipe)
        {
            if (recipe == null || Catalog == null) return -1;
            if (_recipeIndex == null)
            {
                _recipeIndex = new Dictionary<string, int>();
                for (int i = 0; i < _catalog.Recipes.Count; i++)
                {
                    RecipeData entry = _catalog.Recipes[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.recipeID) && !_recipeIndex.ContainsKey(entry.recipeID))
                        _recipeIndex[entry.recipeID] = i;
                }
            }
            return !string.IsNullOrEmpty(recipe.recipeID) && _recipeIndex.TryGetValue(recipe.recipeID, out int index) ? index : -1;
        }

        public static RecipeData RecipeAt(int index)
        {
            ItemCatalog catalog = Catalog;
            return catalog != null && index >= 0 && index < catalog.Recipes.Count ? catalog.Recipes[index] : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _catalog = null;
            _itemIndex = null;
            _recipeIndex = null;
        }
    }
}
