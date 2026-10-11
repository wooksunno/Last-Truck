using System;
using CraftingSystem;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 트럭 가공 탭(시간이 걸리는 가공) 동기화 (NetworkGameState 프리팹에 같이 붙는다).
    ///
    /// 싱글플레이와 같은 규칙: 트럭에는 가공 대기열이 "하나"만 있다 (한 번에 한 종류의 레시피).
    ///  - 누가 재료를 넣든 같은 대기열에 쌓이고, 남은 시간/완성 개수가 모두의 화면에 같이 보인다.
    ///  - 재료 차감, 시간 계산, 완성은 호스트가 한다 → 넣은 사람이 나가도 가공은 계속되고 결과물은 트럭에 남는다.
    ///  - 결과물은 "받기"를 누른 사람이 가방에 들어가는 만큼만 받는다 (못 받은 만큼은 대기열에 그대로).
    ///  - 가공 시간은 대기열을 시작한 사람의 능력(대장장이 등)으로 정한다.
    /// </summary>
    public class NetworkTruckProcessing : NetworkBehaviour
    {
        #region 네트워크 상태

        /// <summary>가공 중인 레시피 번호 + 1 (0 = 대기열 없음).</summary>
        [Networked] private int JobRecipe { get; set; }
        [Networked] private int Pending { get; set; }
        [Networked] private int Ready { get; set; }
        [Networked] private float SecondsPerItem { get; set; }
        [Networked] private TickTimer ItemTimer { get; set; }

        /// <summary>대기열이 바뀔 때마다 1씩 오른다 (UI가 다시 그릴 때 사용).</summary>
        [Networked] private int Revision { get; set; }

        #endregion

        #region 조회

        public static NetworkTruckProcessing Instance { get; private set; }

        /// <summary>멀티플레이에서 트럭 가공을 이 스크립트가 맡고 있는가.</summary>
        public static bool IsActive => GameLauncher.IsOnlineSession && Instance != null && Instance.Object != null && Instance.Object.IsValid;

        public RecipeData Recipe => JobRecipe > 0 ? ItemIndexLookup.RecipeAt(JobRecipe - 1) : null;
        public int PendingCount => Pending;
        public int ReadyCount => Ready;
        public int StateRevision => Revision;

        /// <summary>지금 가공 중인 1개의 남은 시간(초).</summary>
        public float Remaining
        {
            get
            {
                if (Pending <= 0 || Runner == null) return 0f;
                float? left = ItemTimer.RemainingTime(Runner);
                return left.HasValue ? Mathf.Max(0f, left.Value) : 0f;
            }
        }

        public float Total => Pending > 0 ? SecondsPerItem : 0f;

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
            if (!HasStateAuthority || Pending <= 0) return;
            if (!ItemTimer.Expired(Runner)) return;

            Pending--;
            Ready++;
            ItemTimer = Pending > 0 ? TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, SecondsPerItem)) : TickTimer.None;
            Revision++;
        }

        #endregion

        #region 가공 요청 (재료 1회분 넣기)

        private int _nextRequestId = 1;
        private readonly System.Collections.Generic.Dictionary<int, Action<bool, string>> _pendingRequests =
            new System.Collections.Generic.Dictionary<int, Action<bool, string>>();

        /// <summary>재료 1회분을 넣어 가공 대기열에 추가한다. 결과(성공 여부, 실패 메시지)는 onResult로.</summary>
        public void RequestProcess(RecipeData recipe, Action<bool, string> onResult)
        {
            int recipeIndex = ItemIndexLookup.RecipeIndexOf(recipe);
            if (recipeIndex < 0)
            {
                onResult?.Invoke(false, "이 레시피는 멀티플레이에서 찾을 수 없습니다.");
                return;
            }

            int id = _nextRequestId++;
            if (onResult != null) _pendingRequests[id] = onResult;
            RPC_RequestProcess(recipeIndex, id);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestProcess(int recipeIndex, int requestId, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            string error = null;

            if (recipe == null)
                error = "레시피를 찾을 수 없습니다.";
            else if (JobRecipe != 0 && JobRecipe != recipeIndex + 1)
                error = "다른 재료를 가공 중입니다. 결과물을 모두 받은 뒤에 바꿀 수 있습니다.";
            else if (!TruckInventorySync.ConsumeLocal(FindFirstObjectByType<TruckInventory>(), recipe))
                error = "재료가 부족합니다.";

            if (error == null)
            {
                if (JobRecipe == 0)
                {
                    JobRecipe = recipeIndex + 1;
                    SecondsPerItem = ProcessingSeconds(recipe, source);
                }

                Pending++;
                if (Pending == 1) ItemTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.05f, SecondsPerItem));
                Revision++;
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

        #region 결과물 받기

        /// <summary>내 가방에 들어갈 만큼(maxCount회분)만 결과물을 받는다.</summary>
        public void RequestClaim(int maxCount)
        {
            if (maxCount <= 0) return;
            RPC_RequestClaim(maxCount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestClaim(int maxCount, RpcInfo info = default)
        {
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            int count = Mathf.Min(Ready, Mathf.Max(0, maxCount));
            if (count <= 0 || JobRecipe == 0) return;

            int recipeIndex = JobRecipe - 1;
            Ready -= count;
            if (Ready <= 0 && Pending <= 0) JobRecipe = 0; // 다 받았으면 다른 레시피를 고를 수 있다
            Revision++;

            RPC_GiveOutput(source, recipeIndex, count);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GiveOutput([RpcTarget] PlayerRef target, int recipeIndex, int count)
        {
            RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
            PlayerInventory player = TruckInventorySync.LocalPlayerInventory;
            if (recipe == null || recipe.output == null || recipe.output.item == null) return;

            int claimed = 0;
            while (claimed < count && player != null && player.AddItem(recipe.output.item, recipe.output.count))
                claimed++;

            // 그사이 가방이 찼으면 못 받은 만큼 대기열로 돌려놓는다.
            if (claimed < count) RPC_ReturnOutput(recipeIndex, count - claimed);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ReturnOutput(int recipeIndex, int count)
        {
            if (count <= 0) return;
            if (JobRecipe != 0 && JobRecipe != recipeIndex + 1)
            {
                // 이미 다른 레시피로 바뀌었다 → 결과물을 트럭 인벤토리에 넣어 둔다.
                RecipeData recipe = ItemIndexLookup.RecipeAt(recipeIndex);
                TruckInventory truck = FindFirstObjectByType<TruckInventory>();
                if (recipe != null && recipe.output != null && truck != null)
                    TruckInventorySync.AddAndCount(truck.Inventory, recipe.output.item, recipe.output.count * count);
                return;
            }

            JobRecipe = recipeIndex + 1;
            Ready += count;
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
