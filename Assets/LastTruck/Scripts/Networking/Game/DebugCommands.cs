using System.Text;
using CraftingSystem;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 테스트용 채팅 명령 (채팅창에 "/"로 시작하게 입력). 호스트가 실행하고 결과를 모두에게 알린다.
    ///  /help            명령 목록
    ///  /giveall [개수]  카탈로그의 모든 아이템/무기/도구를 트럭 인벤토리에 넣는다 (쌓이는 아이템은 [개수]만큼, 기본 한 묶음 최대치)
    ///  /cleartruck      트럭 인벤토리 비우기
    ///  /night [회차]    바로 밤 시작 (회차를 주면 그 밤의 웨이브 구성으로)
    ///  /killall         살아 있는 몬스터 모두 처치
    /// </summary>
    public static class DebugCommands
    {
        /// <summary>호스트: 명령을 실행하고 알릴 문구를 돌려준다 (null이면 알리지 않음).</summary>
        public static string HostExecute(string command, string sender)
        {
            string[] parts = command.Trim().TrimStart('/').Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;
            string name = parts[0].ToLowerInvariant();
            int number = parts.Length > 1 && int.TryParse(parts[1], out int parsed) ? parsed : 0;

            switch (name)
            {
                case "help":
                    return "명령: /giveall [개수], /cleartruck, /night [회차], /killall";
                case "giveall":
                    return GiveAll(number, sender);
                case "cleartruck":
                    return ClearTruck(sender);
                case "night":
                    return StartNight(number, sender);
                case "killall":
                    return KillAll(sender);
                default:
                    return $"알 수 없는 명령: /{name} (/help)";
            }
        }

        private static TruckInventory FindTruckInventory() => Object.FindFirstObjectByType<TruckInventory>();

        private static string GiveAll(int amount, string sender)
        {
            TruckInventory truck = FindTruckInventory();
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            if (truck == null || catalog == null) return "트럭 인벤토리를 찾지 못했습니다.";

            int kinds = 0;
            int total = 0;
            foreach (ItemData item in catalog.Items)
            {
                if (item == null) continue;
                int added;
                if (item.HasDurability)
                {
                    // 도구/무기는 한 칸에 하나 → 1개씩 (최대 내구도)
                    added = TruckInventorySync.AddAndCount(truck.Inventory, item, 1, item.maxDurability);
                }
                else
                {
                    int count = amount > 0 ? amount : truck.Inventory.StackLimitFor(item);
                    added = TruckInventorySync.AddAndCount(truck.Inventory, item, Mathf.Max(1, count));
                }
                if (added > 0)
                {
                    kinds++;
                    total += added;
                }
            }

            string full = truck.Inventory.UsedSlotCount >= truck.MaxSlots ? " (트럭 칸이 꽉 차서 일부는 못 넣음)" : string.Empty;
            return $"[테스트] {sender}: 트럭에 아이템 {kinds}종 {total}개를 넣었습니다{full}.";
        }

        private static string ClearTruck(string sender)
        {
            TruckInventory truck = FindTruckInventory();
            if (truck == null) return "트럭 인벤토리를 찾지 못했습니다.";
            truck.Inventory.ReplaceAll(new System.Collections.Generic.List<InventorySlot>());
            return $"[테스트] {sender}: 트럭 인벤토리를 비웠습니다.";
        }

        private static string StartNight(int night, string sender)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return "게임 관리자를 찾지 못했습니다.";
            gm.DebugStartNight(night);
            return $"[테스트] {sender}: {gm.CurrentCycle}번째 밤을 시작합니다.";
        }

        private static string KillAll(string sender)
        {
            int count = 0;
            foreach (NetworkMonster monster in Object.FindObjectsByType<NetworkMonster>(FindObjectsSortMode.None))
            {
                NetworkHealth health = monster != null ? monster.GetComponent<NetworkHealth>() : null;
                if (health == null || health.IsDead || !health.HasStateAuthority) continue;
                health.ApplyDamage(health.Current + 1f, monster.transform.position + Vector3.up, false);
                count++;
            }
            return $"[테스트] {sender}: 몬스터 {count}마리를 처치했습니다.";
        }
    }
}
