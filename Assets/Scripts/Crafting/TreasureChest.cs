using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 동굴/야영지에 놓인 보물상자. E를 꾹 누르면 뚜껑이 열리며 등급에 맞는 물건이 몇 가지 나온다.
    /// 한 번 열면 비고, 시간이 지나면(refillMinutes) 다시 닫히고 채워진다.
    /// 멀티플레이: 상자는 사람마다 따로 열린다(내 화면의 상자만 비고, 받은 물건은 내 가방으로). 호스트를 거치지 않는다.
    /// 구조: 루트(Water 레이어 + 트리거 구 + 이 컴포넌트) 아래에 상자 모델. 모델 자식 중 이름에 "Lid"가 들어간 것을 뚜껑으로 연다.
    /// </summary>
    public class TreasureChest : MonoBehaviour, LastTruck.IHoldInteractable, LastTruck.IInteractLabel, LastTruck.IInteractPriority, IGatherOutline
    {
        public enum Tier { Wood = 0, Iron = 1, Gold = 2 }

        [SerializeField] private Tier tier = Tier.Wood;
        [SerializeField] private float refillMinutes = 20f;
        [SerializeField] private float openHoldSeconds = 1.2f;
        [SerializeField] private Transform lid;

        private bool _open;
        private float _refillAt;
        private float _lidT;          // 0 닫힘 ~ 1 열림
        private Quaternion _lidClosed;
        private System.Random _rng;

        public bool CanGatherNow => !_open;
        public GameObject OutlineRoot => gameObject;
        public float RequiredHoldSeconds => _open ? 0f : openHoldSeconds;
        public int InteractPriority => _open ? -10 : 3;

        public string InteractLabel => tier == Tier.Gold ? "황금 보물상자" : tier == Tier.Iron ? "철 보물상자" : "나무 보물상자";
        public string InteractSubLabel
        {
            get
            {
                if (!_open) return "E 꾹: 열기";
                int s = Mathf.Max(0, Mathf.CeilToInt(_refillAt - Time.time));
                return $"비어 있어요 · {s / 60}:{s % 60:00} 뒤 다시 채워져요";
            }
        }
        public Color InteractLabelColor => tier == Tier.Gold ? new Color(1f, 0.82f, 0.3f) : tier == Tier.Iron ? new Color(0.78f, 0.84f, 0.95f) : new Color(0.9f, 0.68f, 0.42f);

        public void Configure(Tier t, Transform lidTransform, float refill = 20f)
        {
            tier = t; lid = lidTransform; refillMinutes = refill;
        }

        private void Awake()
        {
            if (lid == null)
                foreach (var tr in GetComponentsInChildren<Transform>(true))
                    if (tr != transform && tr.name.Contains("Lid")) { lid = tr; break; }
            if (lid != null) _lidClosed = lid.localRotation;
            Vector3 p = transform.position;
            _rng = new System.Random(Mathf.RoundToInt(p.x * 13f + p.z * 7f + p.y) ^ System.Environment.TickCount);
        }

        private void Update()
        {
            if (_open && Time.time >= _refillAt)
                _open = false;   // 다시 채워졌다: 뚜껑이 스르르 닫힌다

            float target = _open ? 1f : 0f;
            if (!Mathf.Approximately(_lidT, target))
            {
                _lidT = Mathf.MoveTowards(_lidT, target, Time.deltaTime * (_open ? 2.4f : 0.8f));
                if (lid != null)
                {
                    // 열릴 때 살짝 튕기며 뒤로 넘어간다 (뒤쪽 경첩 = 뚜껑의 원점)
                    float k = _lidT;
                    float ease = 1f - Mathf.Pow(1f - k, 3f);
                    float bounce = _open ? Mathf.Sin(k * Mathf.PI) * 8f : 0f;
                    lid.localRotation = _lidClosed * Quaternion.Euler(-(ease * 105f + bounce), 0f, 0f);
                }
            }
        }

        public void Interact(GameObject player)
        {
            if (_open)
            {
                LastTruck.HudToast.Show("이미 비운 상자예요");
                return;
            }
            PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inv == null) return;

            _open = true;
            _refillAt = Time.time + refillMinutes * 60f;

            var catalog = ItemCatalog.GetOrCreate();
            var got = new List<string>();
            foreach (var (id, count) in RollLoot())
            {
                ItemData item = catalog.GetItem(id);
                if (item == null || count <= 0) continue;
                inv.AddOrDrop(item, count);
                got.Add($"{item.itemName} {count}");
            }

            Vector3 top = transform.position + Vector3.up * 0.9f;
            Color sparkle = tier == Tier.Gold ? new Color(1f, 0.85f, 0.3f) : tier == Tier.Iron ? new Color(0.8f, 0.9f, 1f) : new Color(1f, 0.8f, 0.5f);
            ActionFx.Burst(top, sparkle, tier == Tier.Gold ? 22 : 14, 3.2f, 0.08f, 1.2f, 0.6f, 1.1f, true, 0.3f);
            ActionFx.Pickup(top + Vector3.up * 0.3f);
            LastTruck.HudToast.Show(got.Count > 0 ? "보물상자: " + string.Join(", ", got) : "상자가 비어 있었어요", 3.5f);
        }

        // ───── 전리품 ─────
        // (아이템 id, 최소, 최대, 가중치). 등급마다 서로 다른 것을 2~4가지 고른다.
        private static readonly (string id, int min, int max, int w)[] WoodLoot =
        {
            (ItemIds.Wood, 6, 12, 10), (ItemIds.Stone, 6, 12, 10), (ItemIds.CopperOre, 3, 6, 8), (ItemIds.Herb, 3, 5, 7),
            (ItemIds.Charcoal, 2, 4, 6), (ItemIds.Meat, 1, 3, 6), (ItemIds.ThrowingStone, 3, 6, 5), (ItemIds.WetHide, 1, 2, 4),
            (ItemIds.HerbBandage, 1, 1, 3), (ItemIds.Jerky, 1, 2, 3),
        };
        private static readonly (string id, int min, int max, int w)[] IronLoot =
        {
            (ItemIds.IronOre, 4, 7, 10), (ItemIds.Copper, 2, 4, 8), (ItemIds.Iron, 1, 3, 6), (ItemIds.Charcoal, 3, 6, 6),
            (ItemIds.Oil, 2, 4, 6), (ItemIds.HerbBandage, 1, 2, 6), (ItemIds.Jerky, 2, 3, 5), (ItemIds.TannedLeather, 1, 2, 4),
            (ItemIds.ToolRepairKit, 1, 1, 4), (ItemIds.FragGrenade, 1, 2, 3), (ItemIds.CopperRound, 5, 10, 3),
        };
        private static readonly (string id, int min, int max, int w)[] GoldLoot =
        {
            (ItemIds.PlatinumOre, 2, 4, 10), (ItemIds.Iron, 3, 5, 8), (ItemIds.PurePlatinum, 1, 2, 5), (ItemIds.WeaponUpgradeKit, 1, 1, 5),
            (ItemIds.CombatStimulant, 1, 2, 5), (ItemIds.HeartyStew, 1, 2, 5), (ItemIds.ToolRepairKit, 1, 2, 5),
            (ItemIds.EmergencyRevivalKit, 1, 1, 2), (ItemIds.Diamond, 1, 1, 2), (ItemIds.IronRound, 6, 12, 4),
        };

        private List<(string id, int count)> RollLoot()
        {
            var table = new List<(string id, int min, int max, int w)>(tier == Tier.Gold ? GoldLoot : tier == Tier.Iron ? IronLoot : WoodLoot);
            int picks = tier == Tier.Gold ? 4 : tier == Tier.Iron ? 3 : _rng.Next(2, 4);
            var result = new List<(string, int)>();
            for (int i = 0; i < picks && table.Count > 0; i++)
            {
                int total = 0;
                foreach (var e in table) total += e.w;
                int roll = _rng.Next(total);
                for (int k = 0; k < table.Count; k++)
                {
                    roll -= table[k].w;
                    if (roll >= 0) continue;
                    result.Add((table[k].id, _rng.Next(table[k].min, table[k].max + 1)));
                    table.RemoveAt(k);
                    break;
                }
            }
            return result;
        }
    }
}
