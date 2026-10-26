using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 기름이 나오는 땅(유정). 사막 기름 웅덩이마다 하나씩 있다.
    /// 석유 펌프잭을 1/2/3 키로 손에 든 채 E키를 꾹 누르면 펌프잭이 설치되고, 시간이 지나면 기름이 모인다.
    /// 펌프잭을 들고 있지 않을 때는 우선순위를 낮춰서, 같은 웅덩이의 구리 수동 펌프 채집(SpecialResourceNode)이 먼저 잡히게 한다.
    /// 펌프잭이 설치된 동안에는 그 웅덩이의 손 펌프 채집을 막는다.
    /// 멀티플레이: 설치/수거/회수는 호스트가 처리하고(NetworkGathering) 모두에게 같은 자리에 보인다. 유정은 "위치"로 찾는다.
    /// </summary>
    public class OilWellSite : MonoBehaviour, LastTruck.IHoldInteractable, LastTruck.IInteractLabel, LastTruck.IInteractPriority
    {
        public static readonly List<OilWellSite> All = new List<OilWellSite>();

        [Tooltip("같은 웅덩이의 손 펌프 채집 노드. 펌프잭이 있는 동안 막는다.")]
        [SerializeField] private SpecialResourceNode handPumpNode;
        [SerializeField] private float installHoldSeconds = 1.5f;

        private Collider _collider;

        public OilPumpjack Pump { get; private set; }

        public float RequiredHoldSeconds => installHoldSeconds;

        // 펌프잭을 들고 있으면 이 유정이 먼저 잡히고, 아니면 손 펌프 채집이 먼저 잡힌다
        public int InteractPriority => LocalPlayerHoldsPumpjack() ? 5 : -5;

        public string InteractLabel => "유정 (기름이 나오는 땅)";
        public string InteractSubLabel => LocalPlayerHoldsPumpjack() ? "E 꾹: 석유 펌프잭 설치" : "석유 펌프잭을 들고 오면 설치할 수 있어요";
        public Color InteractLabelColor => new Color(0.85f, 0.7f, 0.45f);

        public void Configure(SpecialResourceNode node) { handPumpNode = node; }

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        private static bool LocalPlayerHoldsPumpjack()
        {
            GameObject player = GatherRules.Player;
            PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            return IsHoldingPumpjack(inv);
        }

        private static bool IsHoldingPumpjack(PlayerInventory inv)
        {
            InventorySlot hand = inv != null ? inv.SelectedSlot : null;
            return hand != null && !hand.IsEmpty && hand.item != null && hand.item.itemID == ItemIds.OilPumpjack;
        }

        public void Interact(GameObject player)
        {
            if (Pump != null || player == null) return;
            PlayerInventory inv = player.GetComponent<PlayerInventory>();
            if (inv == null) return;

            if (!IsHoldingPumpjack(inv))
            {
                LastTruck.HudToast.Show("석유 펌프잭을 1/2/3 키로 손에 들고 설치하세요");
                return;
            }

            ItemData pumpItem = inv.SelectedSlot.item;
            if (!inv.RemoveItem(pumpItem, 1)) return;

            // 멀티플레이: 호스트가 설치하고 모두에게 보여준다 (이미 다른 사람이 설치했으면 호스트가 아이템을 돌려준다)
            if (LastTruck.Networking.NetworkGathering.TryPlacePump(transform.position))
                return;

            SpawnPump();
            LastTruck.HudToast.Show("석유 펌프잭을 설치했어요. 시간이 지나면 기름이 모여요");
        }

        /// <summary>이 유정에 펌프잭을 세운다 (네트워크에서는 모든 컴퓨터가 부른다).</summary>
        public OilPumpjack SpawnPump()
        {
            if (Pump != null) return Pump;
            Pump = OilPumpjack.Create(this);
            if (handPumpNode != null) handPumpNode.SetSuppressed(true);
            if (_collider != null) _collider.enabled = false;
            return Pump;
        }

        /// <summary>펌프잭을 걷어낸다 (네트워크에서는 모든 컴퓨터가 부른다).</summary>
        public void RemovePump()
        {
            if (Pump != null) Destroy(Pump.gameObject);
            Pump = null;
            if (handPumpNode != null) handPumpNode.SetSuppressed(false);
            if (_collider != null) _collider.enabled = true;
        }

        /// <summary>위치로 유정을 찾는다 (모든 컴퓨터에서 같은 자리에 있다).</summary>
        public static OilWellSite FindAt(Vector3 position, float tolerance = 0.5f)
        {
            OilWellSite best = null;
            float bestSq = tolerance * tolerance;
            foreach (var s in All)
            {
                if (s == null) continue;
                float d = (s.transform.position - position).sqrMagnitude;
                if (d <= bestSq) { bestSq = d; best = s; }
            }
            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { All.Clear(); }
    }
}
