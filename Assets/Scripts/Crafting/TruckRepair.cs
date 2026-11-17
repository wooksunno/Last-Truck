using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 수리 아이템을 손(1/2/3 슬롯)에 들고 손상된 트럭에서 E키를 꾹 누르면 수리가 완료되어 내구도가 가득 찬다.
    /// 수리 아이템은 RepairItemCost개 소모된다. 값은 아래 표에서 조정한다.
    ///
    /// 수리공 고유 능력 (PlayerAbility_Mechanic): 누르는 시간 감소 / 수리 아이템 소모량 감소 (최소 1개).
    /// (야간 정비는 팀 논의 후 연결 예정 - CharacterAbilityController.TryUseNightRepair)
    /// </summary>
    public class TruckRepair : MonoBehaviour
    {
        private const float RepairHoldSeconds = 10f;
        private const int RepairItemCost = 1;   // 수리 1회에 쓰는 수리 아이템 개수 (수리공은 능력으로 줄어든다, 최소 1개)

        private static readonly HashSet<string> RepairItems = new HashSet<string>
        {
            ItemIds.EmergencyPatchBoard,
            ItemIds.WeldingKit,
            ItemIds.HighTensionRepairPack,
        };

        private LastTruck.TruckInformation _info;
        private PlayerInventory _player;

        private void Awake()
        {
            _info = GetComponent<LastTruck.TruckInformation>();
        }

        public float CurrentRepairHoldSeconds =>
            TryGetHeldRepairItem(out _, out float seconds) ? seconds : 0f;

        private LastTruck.CharacterAbilityController _ability;

        /// <summary>내 캐릭터의 고유 능력 (멀티플레이: 내 캐릭터).</summary>
        private LastTruck.CharacterAbilityController Ability
        {
            get
            {
                if (_ability == null)
                    _ability = LastTruck.Networking.LocalPlayerLookup.AbilityController;
                return _ability;
            }
        }

        /// <summary>수리 1회에 필요한 수리 아이템 개수 (수리공 할인 반영).</summary>
        private int RepairCost => Ability != null ? Mathf.Max(1, Ability.GetCalculatedRepairCost(RepairItemCost)) : RepairItemCost;

private bool TryGetHeldRepairItem(out ItemData item, out float seconds)
        {
            item = null;
            seconds = 0f;

            if (_info == null || _info.CurrentDurability >= _info.MaxDurability)
                return false;

            if (_player == null)
                _player = LastTruck.Networking.LocalPlayerLookup.Inventory; // 멀티플레이: 내 캐릭터의 가방
            if (_player == null)
                return false;

            InventorySlot hand = _player.SelectedSlot;
            if (hand == null || hand.IsEmpty || hand.item == null)
                return false;

            if (!RepairItems.Contains(hand.item.itemID))
                return false;

            if (_player.GetItemCount(hand.item) < RepairCost)
                return false;

            item = hand.item;
            seconds = Ability != null ? Ability.GetCalculatedRepairTime(RepairHoldSeconds) : RepairHoldSeconds;   // 수리공: 수리 시간 감소
            return true;
        }

        public bool TryRepair(GameObject player)
        {
            if (!TryGetHeldRepairItem(out ItemData item, out _))
                return false;

            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inventory == null)
                inventory = _player;
            int cost = RepairCost;   // 수리공: 수리 아이템 소모량 감소
            if (inventory == null || inventory.GetItemCount(item) < cost || !inventory.RemoveItem(item, cost))
                return false;

            // 멀티플레이: 내구도는 호스트가 관리하므로 수리도 호스트에게 요청한다 (싱글/호스트는 바로 수리).
            if (!LastTruck.Networking.NetworkTruck.RequestRepair())
                _info.Repair(_info.MaxDurability);
            Debug.Log($"[TruckRepair] 트럭이 수리되었습니다. ({item.itemName} x{cost} 사용) 내구도: {_info.CurrentDurability}/{_info.MaxDurability}");
            if (cost != RepairItemCost)
                LastTruck.AbilityDebug.Report(Ability != null ? Ability.CharacterName : null, "repair", $"수리공 할인으로 수리 아이템 {RepairItemCost}개 → {cost}개 사용");
            return true;
        }
    }
}
