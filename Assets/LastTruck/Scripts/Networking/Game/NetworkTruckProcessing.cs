using System;
using CraftingSystem;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 트럭 가공 탭(시간이 걸리는 가공) 동기화 (NetworkGameState 프리팹에 같이 붙는다).
    ///
    /// 가공 시설(모닥불, 숫돌 연마대 ...)마다 대기열이 하나씩 따로 있다 (시설 번호 = GameUIController의 시설 탭 순서).
    ///  - 한 시설의 대기열에는 한 번에 한 종류의 레시피만 쌓인다.
    ///  - 누가 재료를 넣든 같은 대기열에 쌓이고, 남은 시간/완성 개수가 모두의 화면에 같이 보인다.
    ///  - 재료 차감, 시간 계산, 완성은 호스트가 한다 → 넣은 사람이 나가도 가공은 계속되고 결과물은 트럭에 남는다.
    ///  - 결과물은 "받기"를 누른 사람이 가방에 들어가는 만큼만 받는다 (못 받은 만큼은 대기열에 그대로).
    ///  - 기다리는 재료는 입력칸을 눌러 1개씩 뺄 수 있다 (재료는 트럭 인벤토리로 돌아온다).
    ///  - 가공 시간은 대기열을 시작한 사람의 능력(대장장이 등)으로 정한다.
    /// </summary>
    public class NetworkTruckProcessing : NetworkBehaviour
    {
        #region 네트워크 상태

        /// <summary>시설 하나의 대기열 상태.</summary>
        public struct FacilityState : INetworkStruct
        {
            /// <summary>가공 중인 레시피 번호 + 1 (0 = 대기열 없음).</summary>
            public int Recipe;
            public int Pending;
            public int Ready;
            public float SecondsPerItem;
            public TickTimer ItemTimer;
        }

        /// <summary>동시에 따로 돌릴 수 있는 시설 수 (현재 7개 + 여유).</summary>
        public const int FacilitySlots = 8;

        [Networked, Capacity(FacilitySlots)] private NetworkArray<FacilityState> States => default;

        /// <summary>대기열이 바뀔 때마다 1씩 오른다 (UI가 다시 그릴 때 사용).</summary>
        [Networked] private int Revision { get; set; }

        #endregion

        #region 조회

        public static NetworkTruckProcessing Instance { get; private set; }

        /// <summary>멀티플레이에서 트럭 가공을 이 스크립트가 맡고 있는가.</summary>
        public static bool IsActive => GameLauncher.IsOnlineSession && Instance != null && Instance.Object != null && Instance.Object.IsValid;

        public int StateRevision => Revision;

        private static bool ValidSlot(int slot) => slot >= 0 && slot < FacilitySlots;

        public RecipeData RecipeOf(int slot)
        {
            if (!ValidSlot(slot)) return null;
            int r = States[slot].Recipe;
            return r > 0 ? ItemIndexLookup.RecipeAt(r - 1) : null;
        }

        public int PendingCount(int slot) => ValidSlot(slot) ? States[slot].Pending : 0;
        public int ReadyCount(int slot) => ValidSlot(slot) ? States[slot].Ready : 0;

        /// <summary>지금 가공 중인 1개의 남은 시간(초).</summary>
        public float Remaining(int slot)
        {
            if (!ValidSlot(slot) || Runner == null) return 0f;
            FacilityState s = States[slot];
            if (s.Pending <= 0) return 0f;
            float? left = s.ItemTimer.RemainingTime(Runner);
            return left.HasValue ? Mathf.Max(0f, left.Value) : 0f;
        }

        public float Total(int slot)
        {
            if (!ValidSlot(slot)) return 0f;
            FacilityState s = States[slot];
            return s.Pending > 0 ? s.SecondsPerItem : 0f;
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

            for (int i = 0; i < FacilitySlots; i++)
            {
                FacilityState s = States[i];
                if (s.Pending <= 0) continue;
                if (!s.ItemTimer.Expired(Runner)) continue;

                s.Pending--;
                s.Ready++;
                s.ItemTimer = s.Pending > 0 ? TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, s.SecondsPerItem)) : TickTimer.None;
                States.Set(i, s);
                Revision++;
            }
        }

        #endregion

        #region 가공 요청 (재료 1회분 넣기)

        private int _nextRequestId = 1;
        private readonly System.Collections.Generic.Dictionary<int, Action<bool, string>> _pendingRequests =
            new System.Collections.Generic.Dictionary<int, Action<bool, string>>();

        /// <summary>재료 1회분을 넣어 해당 시설의 가공 대기열에 추가한다. 결과(성공 여부, 실패 메시지)는 onResult로.</summary>
        public void RequestProcess(RecipeData recipe, int slot, Action<bool, string> onResult)
        {
            int recipeIndex = ItemIndexLookup.RecipeIndexOf(recipe);
            if (recipeIndex < 0 || !ValidSlot(slot))
            {
                onResult?.Invoke(false, "이 레시피는 멀티플레이에서 찾을 수 없습니다.");
                return;
            }

            int id = _nextRequestId++;
            if (onResult != null) _pendingRequests[id] = onResult;
            RPC_RequestProcess(recipeIndex, slot, id);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestProcess(int recipeIndex, int slot, int requestId, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            string error = null;

            if (!ValidSlot(slot))
            {
                error = "알 수 없는 가공 시설입니다.";
            }
            else
            {
                FacilityState s = States[slot];
                if (recipe == null)
                    error = "레시피를 찾을 수 없습니다.";
                else if (s.Recipe != 0 && s.Recipe != recipeIndex + 1)
                    error = "다른 재료를 가공 중입니다. 결과물을 모두 받은 뒤에 바꿀 수 있습니다.";
                else if (!TruckInventorySync.ConsumeLocal(FindFirstObjectByType<TruckInventory>(), recipe))
                    error = "재료가 부족합니다.";

                if (error == null)
                {
                    s.Recipe = recipeIndex + 1;
                    if (s.Pending == 0) s.SecondsPerItem = ProcessingSeconds(recipe, source);   // 새 대기열을 시작한 사람의 능력 기준

                    s.Pending++;
                    if (s.Pending == 1) s.ItemTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, s.SecondsPerItem));
                    States.Set(slot, s);
                    Revision++;
                }
            }

            RPC_ProcessResult(source, requestId, error == null, error ?? string.Empty);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ProcessResult([RpcTarget] PlayerRef target, int requestId, NetworkBool ok, string message)
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

        #region 대기 중인 재료 빼기

        /// <summary>해당 시설에서 아직 가공이 끝나지 않은 재료 1회분(맨 뒤)을 빼서 트럭 인벤토리로 돌려준다.</summary>
        public void RequestCancel(int slot)
        {
            if (!ValidSlot(slot)) return;
            RPC_RequestCancel(slot);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestCancel(int slot)
        {
            if (!ValidSlot(slot)) return;

            FacilityState s = States[slot];
            if (s.Pending <= 0 || s.Recipe == 0) return;

            RecipeData recipe = ItemIndexLookup.RecipeAt(s.Recipe - 1);
            TruckInventory truck = FindFirstObjectByType<TruckInventory>();
            if (recipe != null && truck != null && recipe.inputs != null)
            {
                foreach (RecipeIngredient ingredient in recipe.inputs)
                {
                    if (ingredient != null && ingredient.item != null && ingredient.count > 0)
                        TruckInventorySync.AddAndCount(truck.Inventory, ingredient.item, ingredient.count);
                }
            }

            s.Pending--;
            if (s.Pending <= 0)
            {
                // 가공 중이던 것까지 뺐다 → 타이머를 멈추고, 받을 결과물도 없으면 다른 레시피를 고를 수 있다.
                s.ItemTimer = TickTimer.None;
                if (s.Ready <= 0) { s.Recipe = 0; }
            }
            States.Set(slot, s);
            Revision++;
        }

        #endregion

        #region 결과물 받기

        /// <summary>내 가방에 들어갈 만큼(maxCount회분)만 해당 시설의 결과물을 받는다.</summary>
        public void RequestClaim(int slot, int maxCount)
        {
            if (maxCount <= 0 || !ValidSlot(slot)) return;
            RPC_RequestClaim(slot, maxCount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestClaim(int slot, int maxCount, RpcInfo info = default)
        {
            if (!ValidSlot(slot)) return;

            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            FacilityState s = States[slot];
            int count = Mathf.Min(s.Ready, Mathf.Max(0, maxCount));
            if (count <= 0 || s.Recipe == 0) return;

            int recipeIndex = s.Recipe - 1;
            s.Ready -= count;
            if (s.Ready <= 0 && s.Pending <= 0) { s.Recipe = 0; } // 다 받았으면 다른 레시피를 고를 수 있다
            States.Set(slot, s);
            Revision++;

            RPC_GiveOutput(source, slot, recipeIndex, count);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveOutput([RpcTarget] PlayerRef target, int slot, int recipeIndex, int count)
        {
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            PlayerInventory player = TruckInventorySync.LocalPlayerInventory;
            if (recipe == null || recipe.output == null || recipe.output.item == null) return;

            int claimed = 0;
            while (claimed < count && player != null && player.AddItem(recipe.output.item, recipe.output.count))
                claimed++;

            // 그사이 가방이 찼으면 못 받은 만큼 대기열로 돌려놓는다.
            if (claimed < count) RPC_ReturnOutput(slot, recipeIndex, count - claimed);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ReturnOutput(int slot, int recipeIndex, int count)
        {
            if (count <= 0 || !ValidSlot(slot)) return;

            FacilityState s = States[slot];
            if (s.Recipe != 0 && s.Recipe != recipeIndex + 1)
            {
                // 이미 다른 레시피로 바뀌었다 → 결과물을 트럭 인벤토리에 넣어 둔다.
                RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
                TruckInventory truck = FindFirstObjectByType<TruckInventory>();
                if (recipe != null && recipe.output != null && truck != null)
                    TruckInventorySync.AddAndCount(truck.Inventory, recipe.output.item, recipe.output.count * count);
                return;
            }

            s.Recipe = recipeIndex + 1;
            s.Ready += count;
            States.Set(slot, s);
            Revision++;
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
