using System;
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

        public const int MaxSlots = TruckInventory.MinSlots; // 트럭 칸 수와 같게 (모든 아이템 1개씩만 넣어도 130칸이 넘는다)

        /// <summary>슬롯별 아이템 번호 + 1 (0 = 빈 칸).</summary>
        [Networked, Capacity(MaxSlots)] private NetworkArray<int> SlotItems => default;
        [Networked, Capacity(MaxSlots)] private NetworkArray<int> SlotCounts => default;
        /// <summary>슬롯별 도구/무기의 남은 내구도 (내구도가 없는 아이템은 0).</summary>
        [Networked, Capacity(MaxSlots)] private NetworkArray<int> SlotDurabilities => default;
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
                SlotDurabilities.Set(written, slot.CurrentDurability);
                written++;
            }

            for (int i = written; i < MaxSlots; i++)
            {
                SlotItems.Set(i, 0);
                SlotCounts.Set(i, 0);
                SlotDurabilities.Set(i, 0);
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
                if (item != null && count > 0) slots.Add(new InventorySlot(item, count) { durability = SlotDurabilities[i] });
            }
            truck.Inventory.ReplaceAll(slots); // Changed 이벤트 → 열려 있는 트럭 창이 새로 그려진다
        }

        #endregion

        #region 요청 (클라이언트 → 호스트)

        /// <summary>durability: 도구/무기를 넣을 때 그 도구의 남은 내구도 (그 외에는 0).</summary>
        public void RequestDeposit(ItemData item, int count, int durability = 0)
        {
            int index = ItemIndexLookup.IndexOf(item);
            if (index >= 0 && count > 0) RPC_RequestDeposit(index, count, durability);
        }

        /// <summary>durability: 도구/무기를 꺼낼 때 꺼낼 "그" 도구의 남은 내구도 (그 외에는 0).</summary>
        public void RequestWithdraw(ItemData item, int count, int durability = 0)
        {
            int index = ItemIndexLookup.IndexOf(item);
            if (index >= 0 && count > 0) RPC_RequestWithdraw(index, count, durability);
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

        /// <summary>보상 아이템을 트럭 보관함에 넣어 달라고 호스트에게 요청한다 (도전과제 보상 등). 못 넣은 만큼은 요청한 사람 가방으로 돌아온다.</summary>
        public void RequestGrantItem(ItemData item, int count)
        {
            int index = ItemIndexLookup.IndexOf(item);
            if (index >= 0 && count > 0) RPC_RequestGrantItem(index, count);
        }

        /// <summary>과학자 광물 변환 요청. 비율/낮 여부는 호스트가 요청한 사람의 능력으로 다시 확인한다.</summary>
        public void RequestConvert(ItemData input)
        {
            int index = ItemIndexLookup.IndexOf(input);
            if (index >= 0) RPC_RequestConvert(index);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestConvert(int itemIndex, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            NetworkPlayer requester = NetworkPlayer.Find(source);
            CharacterAbilityController ability = requester != null ? requester.GetComponent<CharacterAbilityController>() : null;
            bool ok = TruckInventorySync.ConvertMineralLocal(Truck, ItemIndexLookup.ItemAt(itemIndex), ability, out string message);
            if (!string.IsNullOrEmpty(message)) RPC_ConvertResult(source, ok, message);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ConvertResult([RpcTarget] PlayerRef target, NetworkBool ok, string message)
        {
            HudToast.Show(message);
        }

        private readonly Dictionary<int, Action<bool>> _pendingConsumes = new Dictionary<int, Action<bool>>();
        private int _nextConsumeId = 1;

        public void RequestConsume(RecipeData recipe, Action<bool> onResult)
        {
            int index = ItemIndexLookup.RecipeIndexOf(recipe);
            if (index < 0)
            {
                onResult?.Invoke(false);
                return;
            }

            int id = _nextConsumeId++;
            _pendingConsumes[id] = onResult;
            RPC_RequestConsume(index, id);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestConsume(int recipeIndex, int requestId, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_RequestConsume), 60)) return;
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            bool ok;
            // 재료 할인(요리사 등)은 요청한 사람의 캐릭터 능력으로 계산한다.
            NetworkPlayer requester = NetworkPlayer.Find(source);
            CharacterAbilityController ability = requester != null ? requester.GetComponent<CharacterAbilityController>() : null;
            using (TruckInventory.SuppressBag(source != Runner.LocalPlayer))   // 남의 요청은 트럭 재료로만 (호스트 가방 보호)
                ok = TruckInventorySync.ConsumeLocal(Truck, ItemIndexLookup.RecipeAt(recipeIndex), ability);
            RPC_ConsumeResult(source, requestId, ok);
        }

        /// <summary>가방 칸 늘리기처럼 "아이템 N개"만 내는 비용을 호스트가 확인하고 뺀다 (가죽만 허용).</summary>
        public void RequestConsumeItem(ItemData item, int count, Action<bool> onResult)
        {
            int index = ItemIndexLookup.IndexOf(item);
            if (index < 0 || count <= 0)
            {
                onResult?.Invoke(false);
                return;
            }

            int id = _nextConsumeId++;
            _pendingConsumes[id] = onResult;
            RPC_RequestConsumeItem(index, count, id);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestConsumeItem(int itemIndex, int count, int requestId, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_RequestConsumeItem), 60)) return;
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            bool ok = false;
            if (item != null && item.itemID == ItemIds.TannedLeather && count > 0 && count <= 16)
            {
                using (TruckInventory.SuppressBag(source != Runner.LocalPlayer))   // 남의 요청은 트럭 재료로만
                    ok = TruckInventorySync.ConsumeItemLocal(Truck, item, count);
            }
            RPC_ConsumeResult(source, requestId, ok);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ConsumeResult([RpcTarget] PlayerRef target, int requestId, NetworkBool ok)
        {
            if (!_pendingConsumes.TryGetValue(requestId, out Action<bool> callback)) return;
            _pendingConsumes.Remove(requestId);
            try { callback?.Invoke(ok); }
            catch (Exception e) { Debug.LogException(e); }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDeposit(int itemIndex, int count, int durability, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_RequestDeposit), 60)) return;
            TruckInventory truck = Truck;
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            if (item == null || count <= 0) return;

            int stored = truck != null ? TruckInventorySync.AddAndCount(truck.Inventory, item, count, durability) : 0;
            int leftover = count - stored;
            if (leftover > 0) RPC_GiveItem(info.Source, itemIndex, leftover, durability); // 트럭이 꽉 차서 못 넣은 만큼 돌려준다
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestWithdraw(int itemIndex, int count, int durability, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_RequestWithdraw), 60)) return;
            TruckInventory truck = Truck;
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            if (truck == null || item == null || count <= 0) return;

            if (item.HasDurability)
            {
                // 도구/무기는 요청한 "그" 도구 한 개만 꺼낸다 (다른 사람이 먼저 가져갔으면 아무것도 안 한다)
                if (!truck.Inventory.RemoveDurable(item, durability)) return;
                RPC_GiveItem(info.Source, itemIndex, 1, durability);
                return;
            }

            int take = Mathf.Min(count, truck.GetItemCount(item)); // 다른 사람이 먼저 가져갔으면 남은 만큼만
            if (take <= 0) return;
            if (!truck.RemoveItem(item, take)) return;

            RPC_GiveItem(info.Source, itemIndex, take, 0);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestCraft(int recipeIndex, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_RequestCraft), 60)) return;
            TruckInventory truck = Truck;
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            if (truck == null || recipe == null) return;

            // 재료 할인(대장장이 등)은 제작을 요청한 사람의 캐릭터 능력으로 계산한다.
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            NetworkPlayer crafter = NetworkPlayer.Find(source);
            CharacterAbilityController ability = crafter != null ? crafter.GetComponent<CharacterAbilityController>() : null;

            TruckCraftingManager craft = truck.GetComponent<TruckCraftingManager>();
            if (craft != null)
            {
                using (TruckInventory.SuppressBag(source != Runner.LocalPlayer))   // 남의 요청은 트럭 재료로만 (호스트 가방 보호)
                    craft.TryCraft(recipe, ability);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestGrantRaw(int amount, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_RequestGrantRaw), 60)) return;
            TruckInventorySync.GrantRawLocal(Truck, Mathf.Clamp(amount, 1, 100));
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestGrantItem(int itemIndex, int count, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_RequestGrantItem), 30)) return;
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            TruckInventory truck = Truck;
            if (item == null || count <= 0) return;
            count = Mathf.Clamp(count, 1, 100);   // 비정상 요청 방어 (보상은 한 번에 많아야 수십 개)
            int stored = truck != null ? TruckInventorySync.AddAndCount(truck.Inventory, item, count) : 0;
            int leftover = count - stored;
            if (leftover > 0) RPC_GiveItem(info.Source, itemIndex, leftover, 0);
        }

        /// <summary>호스트 → 특정 플레이어: 이 아이템을 네 가방에 넣어라 (꺼내기 결과 / 넣기 실패 반환).</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveItem([RpcTarget] PlayerRef target, int itemIndex, int count, int durability)
        {
            ItemData item = ItemIndexLookup.ItemAt(itemIndex);
            if (item == null || count <= 0) return;

            PlayerInventory player = TruckInventorySync.LocalPlayerInventory;
            int added = player != null ? TruckInventorySync.AddAndCount(player.Inventory, item, count, durability) : 0;
            int leftover = count - added;
            if (leftover <= 0) return;

            // 내 가방이 꽉 찼다 → 남는 건 트럭으로 되돌린다 (호스트 자신이면 바로 넣는다)
            if (HasStateAuthority)
            {
                TruckInventory truck = Truck;
                if (truck != null) TruckInventorySync.AddAndCount(truck.Inventory, item, leftover, durability);
            }
            else
            {
                RPC_RequestDeposit(itemIndex, leftover, durability);
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
        /// <summary>내 가방 → 트럭. durability: 도구/무기면 넣을 "그" 도구의 남은 내구도 (칸의 CurrentDurability).</summary>
        public static void Deposit(TruckInventory truck, PlayerInventory player, ItemData item, int count, int durability = 0)
        {
            if (truck == null || player == null || item == null || count <= 0) return;
            _localPlayerInventory = player;

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                MoveItems(player.Inventory, truck.Inventory, item, count, durability);
                return;
            }

            if (item.HasDurability)
            {
                // 도구/무기는 한 개씩: 가방에서 그 도구를 먼저 빼고 닳은 정도와 함께 호스트에 요청한다
                if (durability <= 0 || !player.Inventory.RemoveDurable(item, durability)) return;
                net.RequestDeposit(item, 1, durability);
                return;
            }

            count = Mathf.Min(count, player.Inventory.GetItemCount(item));
            if (count <= 0 || !player.Inventory.RemoveItem(item, count)) return;
            net.RequestDeposit(item, count);
        }

        /// <summary>트럭 → 내 가방. durability: 도구/무기면 꺼낼 "그" 도구의 남은 내구도 (칸의 CurrentDurability).</summary>
        public static void Withdraw(TruckInventory truck, PlayerInventory player, ItemData item, int count, int durability = 0)
        {
            if (truck == null || player == null || item == null || count <= 0) return;
            _localPlayerInventory = player;

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                MoveItems(truck.Inventory, player.Inventory, item, count, durability);
                return;
            }

            net.RequestWithdraw(item, item.HasDurability ? 1 : count, durability);
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

        /// <summary>
        /// 과학자 고유 능력: 트럭 보관함의 광물을 다른 광물로 변환 (낮에만). 결과는 트럭 보관함에 들어간다.
        /// 싱글/호스트는 바로, 클라이언트는 호스트에게 요청한다 (호스트가 요청한 사람의 능력으로 다시 확인).
        /// </summary>
        public static void ConvertMineral(TruckInventory truck, ItemData input, CharacterAbilityController ability)
        {
            if (truck == null || input == null) return;

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                ConvertMineralLocal(truck, input, ability, out string message);
                if (!string.IsNullOrEmpty(message)) HudToast.Show(message);
                return;
            }

            net.RequestConvert(input);
        }

        /// <summary>테스트 버튼 "원재료 +N".</summary>
        /// <summary>
        /// 아이템을 트럭 보관함에 넣는다 (도전과제 보상 등). 싱글/호스트는 바로, 클라이언트는 호스트에게 요청한다.
        /// 트럭에 못 넣은 만큼은 내 가방(넘치면 발밑)으로.
        /// </summary>
        public static void GrantItem(TruckInventory truck, ItemData item, int count, PlayerInventory fallback)
        {
            if (item == null || count <= 0) return;
            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net != null) { net.RequestGrantItem(item, count); return; }

            int stored = truck != null ? AddAndCount(truck.Inventory, item, count) : 0;
            int leftover = count - stored;
            if (leftover > 0 && fallback != null) fallback.AddOrDrop(item, leftover);
        }

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
            CraftingCheats.AddRawMaterialsToTruck(truck, amount);
        }

        /// <summary>
        /// 트럭 가공을 시작할 때 재료를 뺀다. 결과는 onResult(성공 여부)로 알려준다.
        /// 싱글/호스트는 바로, 클라이언트는 호스트가 실제 수량을 확인한 뒤 응답이 오면 불린다.
        /// </summary>
        public static void ConsumeIngredients(TruckInventory truck, RecipeData recipe, Action<bool> onResult) =>
            ConsumeIngredients(truck, recipe, onResult, null);

        /// <summary>ability: 재료 할인(요리사 등)을 적용할 사람 (null이면 레시피 수량 그대로).</summary>
        public static void ConsumeIngredients(TruckInventory truck, RecipeData recipe, Action<bool> onResult, CharacterAbilityController ability)
        {
            if (truck == null || recipe == null)
            {
                onResult?.Invoke(false);
                return;
            }

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                onResult?.Invoke(ConsumeLocal(truck, recipe, ability));
                return;
            }

            net.RequestConsume(recipe, onResult);   // 호스트가 요청한 사람의 능력으로 다시 계산한다
        }

        /// <summary>과학자 광물 변환 실제 처리 (싱글/호스트). 성공하면 true, message는 화면에 띄울 안내.</summary>
        public static bool ConvertMineralLocal(TruckInventory truck, ItemData input, CharacterAbilityController ability, out string message)
        {
            message = null;
            if (truck == null || input == null || ability == null)
            {
                message = "광물 변환을 할 수 없습니다.";
                return false;
            }

            bool isDay = !global::GameManager.IsNight;
            if (!ability.TryGetMineralConversion(input, isDay, out string resultId, out int inputCount, out int outputCount))
            {
                message = isDay ? $"{input.itemName}은(는) 변환할 수 없습니다." : "광물 변환은 낮에만 할 수 있습니다.";
                return false;
            }

            ItemData result = string.IsNullOrEmpty(resultId) ? null : ItemCatalog.GetOrCreate().GetItem(resultId);
            if (result == null || inputCount <= 0 || outputCount <= 0)
            {
                message = "변환 결과 아이템을 찾을 수 없습니다.";
                return false;
            }

            if (truck.GetItemCount(input) < inputCount || !truck.RemoveItem(input, inputCount))
            {
                message = $"{input.itemName}이(가) {inputCount}개 필요합니다.";
                return false;
            }

            int added = AddAndCount(truck.Inventory, result, outputCount);
            if (added < outputCount)
            {
                // 보관함에 자리가 없다 → 되돌린다
                if (added > 0) truck.RemoveItem(result, added);
                AddAndCount(truck.Inventory, input, inputCount);
                message = "트럭 보관함에 자리가 없습니다.";
                return false;
            }

            message = $"광물 변환: {input.itemName} x{inputCount} → {result.itemName} x{outputCount}";
            AbilityDebug.Report(ability.CharacterName, "convert", "과학자 " + message);
            return true;
        }

        /// <summary>아이템 하나를 N개 낸다 (가방 칸 늘리기). 싱글/호스트는 바로, 클라이언트는 호스트가 확인한 뒤 onResult가 불린다.</summary>
        public static void ConsumeItem(TruckInventory truck, ItemData item, int count, Action<bool> onResult)
        {
            if (truck == null || item == null || count <= 0)
            {
                onResult?.Invoke(false);
                return;
            }

            NetworkTruckInventory net = IsRemoteClient ? Net : null;
            if (net == null)
            {
                onResult?.Invoke(ConsumeItemLocal(truck, item, count));
                return;
            }
            net.RequestConsumeItem(item, count, onResult);
        }

        public static bool ConsumeItemLocal(TruckInventory truck, ItemData item, int count)
        {
            if (truck == null || item == null || count <= 0) return false;
            if (truck.GetMaterialCount(item) < count) return false;
            return truck.RemoveMaterial(item, count);
        }

        /// <summary>재료가 모두 있으면 빼고 true (능력 할인 없이 레시피 수량 그대로).</summary>
        public static bool ConsumeLocal(TruckInventory truck, RecipeData recipe) => ConsumeLocal(truck, recipe, null);

        /// <summary>재료가 모두 있으면 빼고 true. ability가 있으면 요리사/대장장이 재료 할인을 반영한 개수만 뺀다.</summary>
        public static bool ConsumeLocal(TruckInventory truck, RecipeData recipe, CharacterAbilityController ability)
        {
            if (truck == null || recipe == null) return false;
            List<RecipeIngredient> cost = TruckFacilities.CostFor(recipe, ability);
            foreach (RecipeIngredient c in cost)
                if (truck.GetMaterialCount(c.item) < c.count) return false;
            foreach (RecipeIngredient c in cost)
                truck.RemoveMaterial(c.item, c.count);
            return true;
        }

        /// <summary>
        /// source → destination으로 옮긴다. 목적지에 실제로 들어간 만큼만 source에서 뺀다 (공간 부족 시 복사 방지).
        /// </summary>
        public static int MoveItems(ItemStackInventory source, ItemStackInventory destination, ItemData item, int count, int durability = 0)
        {
            if (source == null || destination == null || item == null || count <= 0) return 0;

            // 도구/무기: 닳은 정도를 그대로 가진 "그" 도구 한 개를 옮긴다
            if (item.HasDurability && durability > 0)
            {
                if (!source.RemoveDurable(item, durability))
                    return 0;   // 그 도구가 없다 (다른 사람이 먼저 가져감) → 복사되지 않게 아무것도 하지 않는다
                if (AddAndCount(destination, item, 1, durability) < 1)
                {
                    source.AddItem(item, 1, durability);   // 목적지가 꽉 찼다 → 되돌린다
                    Debug.LogWarning("공간이 부족하여 아이템을 옮기지 못했습니다.");
                    return 0;
                }
                return 1;
            }

            count = Mathf.Min(count, source.GetItemCount(item));
            if (count <= 0) return 0;

            int moved = AddAndCount(destination, item, count);
            if (moved > 0) source.RemoveItem(item, moved);
            if (moved < count) Debug.LogWarning("공간이 부족하여 아이템을 모두 옮기지 못했습니다.");
            return moved;
        }

        /// <summary>AddItem은 공간이 모자라면 일부만 넣고 false를 돌려주므로, 실제로 들어간 개수를 센다.</summary>
        public static int AddAndCount(ItemStackInventory inventory, ItemData item, int count, int durability = 0)
        {
            if (inventory == null || item == null || count <= 0) return 0;
            int before = inventory.GetItemCount(item);
            inventory.AddItem(item, count, durability);
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
