using System;
using System.Collections.Generic;
using CraftingSystem;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 트럭 가공 탭 동기화 (NetworkGameState 프리팹에 같이 붙는다).
    ///
    /// 설비(모닥불, 숫돌 연마대 ...)마다 가공 "칸"이 있고, 칸마다 따로 가공한다 (TruckFacilities 규칙과 같음).
    ///  - 설비 번호 = TruckFacilities.All 순서, 칸 번호 = 0 ~ (칸 수 - 1).
    ///  - 재료 차감, 시간 계산, 완성은 호스트가 한다 → 넣은 사람이 나가도 가공은 계속되고 결과물은 칸에 남는다.
    ///  - 완성된 결과물은 "받기"를 누른 사람이 가방에 들어가면 가져간다 (가방이 가득 차 있으면 칸에 그대로 남는다).
    ///  - 가공 중인 칸을 누르면 재료를 돌려받고 칸이 비워진다.
    ///  - 칸 늘리기: 설비를 만든 재료 × 현재 칸 수를 트럭 인벤토리에서 내고 1칸 늘린다.
    ///  - 가공 시간은 시작한 사람의 능력(대장장이 등)으로 정한다.
    /// </summary>
    public class NetworkTruckProcessing : NetworkBehaviour
    {
        #region 네트워크 상태

        /// <summary>칸 하나의 상태.</summary>
        public struct SlotState : INetworkStruct
        {
            /// <summary>가공 중인 레시피 번호 + 1 (0 = 빈 칸).</summary>
            public int Recipe;
            /// <summary>아직 가공할 개수 (지금 가공 중인 것 포함).</summary>
            public int Queued;
            /// <summary>다 만들어져 받을 수 있는 개수.</summary>
            public int ReadyCount;
            public float Seconds;
            public TickTimer Timer;
        }

        /// <summary>네트워크 상태에 잡아 둘 설비 수 (현재 7개 + 여유).</summary>
        public const int FacilitySlots = 8;
        private const int MaxSlots = TruckFacilities.MaxSlots;

        [Networked, Capacity(FacilitySlots * MaxSlots)] private NetworkArray<SlotState> Slots => default;

        /// <summary>설비별 칸 수 (0이면 기본 1칸으로 본다).</summary>
        [Networked, Capacity(FacilitySlots)] private NetworkArray<int> SlotCounts => default;

        /// <summary>상태가 바뀔 때마다 1씩 오른다 (UI가 다시 그릴 때 사용).</summary>
        [Networked] private int Revision { get; set; }

        #endregion

        #region 조회

        public static NetworkTruckProcessing Instance { get; private set; }

        /// <summary>멀티플레이에서 트럭 가공을 이 스크립트가 맡고 있는가.</summary>
        public static bool IsActive => GameLauncher.IsOnlineSession && Instance != null && Instance.Object != null && Instance.Object.IsValid;

        public int StateRevision => Revision;

        private static bool ValidFacility(int facility) => facility >= 0 && facility < FacilitySlots;
        private static int Index(int facility, int slot) => facility * MaxSlots + slot;

        public int SlotCount(int facility)
        {
            if (!ValidFacility(facility)) return 1;
            return Mathf.Clamp(SlotCounts[facility], 1, MaxSlots);
        }

        /// <summary>화면에 보여줄 칸 상태.</summary>
        public ProcessSlotView SlotView(int facility, int slot)
        {
            if (!ValidFacility(facility) || slot < 0 || slot >= MaxSlots || Runner == null) return default;

            SlotState s = Slots[Index(facility, slot)];
            if (s.Recipe <= 0) return default;

            float remaining = 0f;
            if (s.Queued > 0)
            {
                float? left = s.Timer.RemainingTime(Runner);
                remaining = left.HasValue ? Mathf.Max(0f, left.Value) : 0f;
            }
            return new ProcessSlotView
            {
                recipe = ItemIndexLookup.RecipeAt(s.Recipe - 1),
                remaining = remaining,
                total = s.Seconds,
                queued = s.Queued,
                readyCount = s.ReadyCount,
            };
        }

        #endregion

        #region 생명주기

        public override void Spawned()
        {
            Instance = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 호스트: 시간 진행

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            for (int f = 0; f < FacilitySlots; f++)
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    SlotState s = Slots[Index(f, i)];
                    if (s.Recipe <= 0 || s.Queued <= 0) continue;
                    if (!s.Timer.Expired(Runner)) continue;

                    // 1개 완성 → 대기 중인 게 더 있으면 이어서 가공
                    s.ReadyCount++;
                    s.Queued--;
                    s.Timer = s.Queued > 0 ? TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, s.Seconds)) : TickTimer.None;
                    Slots.Set(Index(f, i), s);
                    Revision++;
                }
            }
        }

        #endregion

        #region 요청 (재료 넣기)

        private int _nextRequestId = 1;
        private readonly Dictionary<int, Action<bool, string>> _pendingRequests = new Dictionary<int, Action<bool, string>>();

        private int NewRequest(Action<bool, string> onResult)
        {
            int id = _nextRequestId++;
            if (onResult != null) _pendingRequests[id] = onResult;
            return id;
        }

        /// <summary>
        /// 칸에 재료 1회분을 넣는다. 빈 칸이면 가공을 시작하고, 같은 재료가 들어 있는 칸이면 대기 개수를 1 늘린다.
        /// 결과(성공 여부, 실패 메시지)는 onResult로.
        /// </summary>
        public void RequestPlace(RecipeData recipe, int facility, int slot, Action<bool, string> onResult)
        {
            int recipeIndex = ItemIndexLookup.RecipeIndexOf(recipe);
            if (recipeIndex < 0 || !ValidFacility(facility) || slot < 0 || slot >= MaxSlots)
            {
                onResult?.Invoke(false, "이 레시피는 멀티플레이에서 찾을 수 없습니다.");
                return;
            }

            RPC_RequestPlace(recipeIndex, facility, slot, NewRequest(onResult));
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPlace(int recipeIndex, int facility, int slot, int requestId, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            string error = null;
            TruckInventory truck = FindFirstObjectByType<TruckInventory>();
            using var bagScope = TruckInventory.SuppressBag(source != Runner.LocalPlayer);   // 남의 요청은 트럭 재료로만 (호스트 가방 보호)

            if (recipe == null || !ValidFacility(facility) || slot < 0 || slot >= MaxSlots)
                error = "레시피를 찾을 수 없습니다.";
            else if (!TruckFacilities.IsBuilt(truck, TruckFacilities.All[Mathf.Min(facility, TruckFacilities.All.Length - 1)]))
                error = "설비가 설치되어 있지 않습니다.";
            else if (slot >= SlotCount(facility))
                error = "아직 열리지 않은 칸입니다.";
            else if (Slots[Index(facility, slot)].Recipe != 0 && Slots[Index(facility, slot)].Recipe != recipeIndex + 1)
                error = "다른 재료를 가공 중인 칸입니다.";
            else if (Slots[Index(facility, slot)].Queued + Slots[Index(facility, slot)].ReadyCount >= TruckProcessingLocal.MaxPerSlot)
                error = "이 칸은 가득 찼습니다.";
            else if (!TruckInventorySync.ConsumeLocal(truck, recipe))
                error = "재료가 부족합니다.";

            if (error == null)
            {
                SlotState s = Slots[Index(facility, slot)];
                if (s.Recipe == 0)
                {
                    float seconds = Mathf.Max(0.05f, ProcessingSeconds(recipe, source));
                    s = new SlotState { Recipe = recipeIndex + 1, Queued = 1, ReadyCount = 0, Seconds = seconds, Timer = TickTimer.CreateFromSeconds(Runner, seconds) };
                }
                else
                {
                    // 같은 재료를 한 개 더: 완성품만 남아 있던 칸이면 새로 타이머를 시작한다
                    if (s.Queued == 0)
                    {
                        s.Seconds = Mathf.Max(0.05f, ProcessingSeconds(recipe, source));
                        s.Timer = TickTimer.CreateFromSeconds(Runner, s.Seconds);
                    }
                    s.Queued++;
                }
                Slots.Set(Index(facility, slot), s);
                Revision++;
            }

            RPC_Result(source, requestId, error == null, error ?? string.Empty);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Result([RpcTarget] PlayerRef target, int requestId, NetworkBool ok, string message)
        {
            if (!_pendingRequests.TryGetValue(requestId, out Action<bool, string> callback)) return;
            _pendingRequests.Remove(requestId);
            try { callback?.Invoke(ok, message); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>가공 시간 (요청한 사람의 대장장이 능력 반영).</summary>
        private float ProcessingSeconds(RecipeData recipe, PlayerRef source)
        {
            float seconds = recipe.processingSeconds;
            NetworkPlayer requester = NetworkPlayer.Find(source);
            CharacterAbilityController ability = requester != null ? requester.GetComponent<CharacterAbilityController>() : null;
            if (ability != null) seconds = ability.GetCalculatedProcessingTime(seconds, false);
            return Mathf.Max(0f, seconds);
        }

        #endregion

        #region 가공 중인 칸 비우기 (재료 환불)

        public void RequestCancel(int facility, int slot)
        {
            if (!ValidFacility(facility) || slot < 0 || slot >= MaxSlots) return;
            RPC_RequestCancel(facility, slot);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestCancel(int facility, int slot)
        {
            if (!ValidFacility(facility) || slot < 0 || slot >= MaxSlots) return;

            SlotState s = Slots[Index(facility, slot)];
            if (s.Recipe <= 0 || s.Queued <= 0) return;

            // 아직 안 만든 재료를 모두 돌려준다. 이미 완성된 것은 칸에 남는다.
            RecipeData recipe = ItemIndexLookup.RecipeAt(s.Recipe - 1);
            TruckInventory truck = FindFirstObjectByType<TruckInventory>();
            for (int i = 0; i < s.Queued; i++)
                TruckFacilities.Refund(truck, recipe);

            s.Queued = 0;
            s.Timer = TickTimer.None;
            Slots.Set(Index(facility, slot), s.ReadyCount > 0 ? s : default);
            Revision++;
        }

        #endregion

        #region 결과물 받기

        /// <summary>끝난 칸의 결과물을 받는다 (호출한 쪽이 가방 공간을 미리 확인한다).</summary>
        public void RequestClaim(int facility, int slot)
        {
            if (!ValidFacility(facility) || slot < 0 || slot >= MaxSlots) return;
            RPC_RequestClaim(facility, slot);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestClaim(int facility, int slot, RpcInfo info = default)
        {
            if (!ValidFacility(facility) || slot < 0 || slot >= MaxSlots) return;

            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            SlotState s = Slots[Index(facility, slot)];
            if (s.Recipe <= 0 || s.ReadyCount <= 0) return;

            // 완성품을 모두 꺼낸다. 아직 가공할 게 남아 있으면 칸은 계속 가공한다.
            int recipeIndex = s.Recipe - 1;
            int count = s.ReadyCount;
            s.ReadyCount = 0;
            Slots.Set(Index(facility, slot), s.Queued > 0 ? s : default);
            Revision++;

            RPC_GiveOutput(source, facility, slot, recipeIndex, count);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveOutput([RpcTarget] PlayerRef target, int facility, int slot, int recipeIndex, int count)
        {
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            PlayerInventory player = TruckInventorySync.LocalPlayerInventory;
            if (recipe == null || recipe.output == null || recipe.output.item == null || count <= 0) return;

            // 들어가는 만큼만 넣고, 그사이 가방이 찼으면 나머지는 칸으로 돌려놓는다.
            int given = 0;
            while (given < count && player != null && player.AddItem(recipe.output.item, recipe.output.count))
                given++;
            if (given < count)
                RPC_ReturnOutput(facility, slot, recipeIndex, count - given);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ReturnOutput(int facility, int slot, int recipeIndex, int count)
        {
            if (!ValidFacility(facility) || slot < 0 || slot >= MaxSlots || count <= 0) return;

            SlotState s = Slots[Index(facility, slot)];
            if (s.Recipe == 0)
            {
                Slots.Set(Index(facility, slot), new SlotState { Recipe = recipeIndex + 1, ReadyCount = count, Seconds = 0f, Timer = TickTimer.None });
                Revision++;
                return;
            }
            if (s.Recipe == recipeIndex + 1)
            {
                s.ReadyCount += count;
                Slots.Set(Index(facility, slot), s);
                Revision++;
                return;
            }

            // 칸이 이미 다른 재료로 쓰이고 있으면 결과물을 트럭 인벤토리에 넣어 둔다.
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            TruckInventory truck = FindFirstObjectByType<TruckInventory>();
            if (recipe != null && recipe.output != null && truck != null)
                TruckInventorySync.AddAndCount(truck.Inventory, recipe.output.item, recipe.output.count * count);
        }

        #endregion

        #region 칸 늘리기

        /// <summary>설비 칸을 1개 늘린다. 재료(설비 제작 재료 × 현재 칸 수)는 호스트가 트럭 인벤토리에서 뺀다.</summary>
        public void RequestUpgrade(int facility, Action<bool, string> onResult)
        {
            if (!ValidFacility(facility))
            {
                onResult?.Invoke(false, "알 수 없는 설비입니다.");
                return;
            }
            RPC_RequestUpgrade(facility, NewRequest(onResult));
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestUpgrade(int facility, int requestId, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            string error = null;
            TruckInventory truck = FindFirstObjectByType<TruckInventory>();
            using var bagScope = TruckInventory.SuppressBag(source != Runner.LocalPlayer);   // 남의 요청은 트럭 재료로만 (호스트 가방 보호)

            if (!ValidFacility(facility) || facility >= TruckFacilities.All.Length)
            {
                error = "알 수 없는 설비입니다.";
            }
            else
            {
                FacilityType type = TruckFacilities.All[facility];
                int current = SlotCount(facility);
                if (!TruckFacilities.IsBuilt(truck, type)) error = "설비가 설치되어 있지 않습니다.";
                else if (current >= MaxSlots) error = "더 늘릴 수 없습니다.";
                else if (!TruckFacilities.Pay(truck, TruckFacilities.UpgradeCost(type, current))) error = "재료가 부족합니다.";
                else
                {
                    SlotCounts.Set(facility, current + 1);
                    Revision++;
                }
            }

            RPC_Result(source, requestId, error == null, error ?? string.Empty);
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
