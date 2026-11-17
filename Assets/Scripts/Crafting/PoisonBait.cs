using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 독버섯 미끼. 땅에 놓으면 주변 몬스터가 플레이어/트럭 대신 이 미끼로 몰려가서 먹고, 독에 중독된다.
    ///
    ///  - 유인: lureRadius 안의 몬스터는 (플레이어가 가까이에 있지 않은 한) 미끼를 향해 걸어간다.
    ///  - 먹기: 미끼에 닿으면 feedSeconds 동안 멈춰 서서 먹고, 그동안 독 피해(poisonTick마다 poisonDamage, poisonTicks번)를 입는다.
    ///    한 몬스터는 한 미끼를 한 번만 먹는다. 미끼는 biteCount마리가 먹으면 사라지고, 먹는 몬스터가 없어도 lifetime이 지나면 사라진다.
    ///  - 판정(유인/독 피해)은 싱글 또는 호스트에서만 한다 (몬스터 AI가 거기서 돌기 때문). 멀티플레이 클라이언트에서는
    ///    VisualOnly로 모양만 보이고, 호스트가 사라졌다고 알려주면 같이 사라진다 (LastTruck.Networking.NetworkGathering).
    /// 몬스터 AI(MonsterAI / NetworkMonster)가 FindFor / Reached / Feed를 부른다.
    /// </summary>
    public class PoisonBait : MonoBehaviour
    {
        public const float LureRadius = 22f;
        public const float EatRadius = 1.6f;
        public const float LifetimeSeconds = 28f;
        public const int BiteCount = 4;
        private const float FeedSeconds = 3.5f;
        private const float PoisonTick = 0.75f;
        private const int PoisonTicks = 7;
        private const int PoisonDamage = 8;
        private const float PlayerPriorityDistance = 3.5f;   // 플레이어가 이 안에 있으면 미끼보다 플레이어를 노린다

        private static readonly List<PoisonBait> Active = new List<PoisonBait>();

        /// <summary>호스트/싱글: 미끼가 사라지는 순간 (클라이언트 화면에서도 지우도록 네트워크가 듣는다).</summary>
        public static event System.Action<PoisonBait> Gone;

        public int NetworkId { get; set; }
        public bool VisualOnly { get; set; }

        private float _bornAt;
        private int _bitesLeft = BiteCount;
        private bool _removing;
        private Transform _cap, _ring;
        private readonly HashSet<Combat.Damageable> _eaten = new HashSet<Combat.Damageable>();
        private readonly Dictionary<Combat.Damageable, float> _feeding = new Dictionary<Combat.Damageable, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active.Clear(); Gone = null; }

        // ---------------------------------------------------------------- 만들기

        public static PoisonBait Spawn(Vector3 position, bool visualOnly)
        {
            var root = BuildVisual();
            root.transform.position = position;
            var bait = root.AddComponent<PoisonBait>();
            bait.VisualOnly = visualOnly;
            return bait;
        }

        private static GameObject BuildVisual()
        {
            var root = new GameObject("PoisonMushroomBait");
            var stem = Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.14f, 0.16f, 0.14f), Lit(new Color(0.93f, 0.9f, 0.78f)));
            stem.name = "Stem";
            var cap = Prim(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.52f, 0.28f, 0.52f), Lit(new Color(0.55f, 0.18f, 0.72f)));
            cap.name = "Cap";
            // 갓 위의 하얀 반점
            Vector3[] spots = { new Vector3(0.12f, 0.46f, 0.08f), new Vector3(-0.1f, 0.47f, 0.1f), new Vector3(0.02f, 0.49f, -0.12f), new Vector3(0.18f, 0.41f, -0.08f), new Vector3(-0.17f, 0.42f, -0.06f) };
            foreach (Vector3 s in spots) Prim(PrimitiveType.Sphere, root.transform, s, new Vector3(0.07f, 0.04f, 0.07f), Lit(new Color(0.97f, 0.95f, 0.9f)));
            // 바닥의 독 기운 (연두색으로 은은히 숨 쉰다)
            var ring = Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.1f, 0.005f, 1.1f), Glow(new Color(0.45f, 0.95f, 0.3f, 0.26f)));
            ring.name = "Aura";
            return root;
        }

        private void Awake()
        {
            _bornAt = Time.time;
            _cap = transform.Find("Cap");
            _ring = transform.Find("Aura");
            Active.Add(this);
        }

        private void OnDestroy() { Active.Remove(this); }

        // ---------------------------------------------------------------- 몬스터 AI가 쓰는 창구

        /// <summary>이 몬스터가 지금 향해 가야 할(또는 먹고 있는) 미끼. 없으면 null.</summary>
        public static PoisonBait FindFor(Combat.Damageable monster, Vector3 position)
        {
            PoisonBait best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                PoisonBait b = Active[i];
                if (b == null || b._removing) continue;
                if (monster != null && b._eaten.Contains(monster) && !b.IsFeeding(monster)) continue;   // 이미 먹은 미끼
                if (monster != null && !b.IsFeeding(monster) && b._bitesLeft <= 0) continue;
                float d = Horizontal(position, b.transform.position);
                if (d > LureRadius || d >= bestDist) continue;
                bestDist = d; best = b;
            }
            return best;
        }

        /// <summary>플레이어가 가까이 있으면 미끼보다 플레이어를 노린다 (몬스터 AI가 판단에 쓴다).</summary>
        public static bool PlayerIsCloser(Vector3 monsterPosition, Vector3 playerPosition) =>
            Horizontal(monsterPosition, playerPosition) <= PlayerPriorityDistance;

        public bool Reached(Vector3 position) => Horizontal(position, transform.position) <= EatRadius + 0.3f;

        public bool IsFeeding(Combat.Damageable monster) =>
            monster != null && _feeding.TryGetValue(monster, out float until) && Time.time < until;

        /// <summary>
        /// 미끼에 닿은 몬스터가 먹기 시작한다 (호스트/싱글에서만 판정). 먹는 동안은 AI가 멈춰 서 있으면 된다.
        /// 이미 먹는 중이면 true만 돌려준다. 미끼가 이미 다 떨어졌으면 false.
        /// </summary>
        public bool Feed(Combat.Damageable monster)
        {
            if (VisualOnly || _removing || monster == null) return false;
            if (IsFeeding(monster)) return true;
            if (_eaten.Contains(monster) || _bitesLeft <= 0) return false;

            _eaten.Add(monster);
            _feeding[monster] = Time.time + FeedSeconds;
            _bitesLeft--;
            StartCoroutine(Poison(monster));
            if (_bitesLeft <= 0) StartCoroutine(RemoveAfter(FeedSeconds));
            return true;
        }

        private IEnumerator Poison(Combat.Damageable monster)
        {
            for (int i = 0; i < PoisonTicks; i++)
            {
                yield return new WaitForSeconds(PoisonTick);
                if (monster == null || monster.IsDeadOffline || monster.CurrentHealth <= 0) yield break;
                monster.TakeDamage(PoisonDamage, monster.transform.position + Vector3.up * 0.8f, null);
            }
        }

        private IEnumerator RemoveAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Remove();
        }

        // ---------------------------------------------------------------- 수명 / 연출

        private void Update()
        {
            float age = Time.time - _bornAt;
            if (!VisualOnly && age >= LifetimeSeconds) { Remove(); return; }
            if (VisualOnly && age >= LifetimeSeconds + 1f) { Destroy(gameObject); return; }

            if (_removing) return;
            // 갓이 살짝 부풀었다 가라앉고, 독 기운이 숨 쉰다. 수명이 끝나갈수록 작아진다.
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            if (_cap != null) _cap.localScale = new Vector3(0.52f, 0.28f, 0.52f) * pulse;
            if (_ring != null)
            {
                float breathe = 1f + 0.12f * Mathf.Sin(Time.time * 2.2f);
                _ring.localScale = new Vector3(1.1f * breathe, 0.005f, 1.1f * breathe);
            }
            float left = LifetimeSeconds - age;
            if (left < 2f) transform.localScale = Vector3.one * Mathf.Clamp01(left / 2f);
        }

        /// <summary>미끼를 없앤다 (호스트/싱글은 Gone으로 알린다). 클라이언트는 호스트 신호를 받아 부른다.</summary>
        public void Remove()
        {
            if (_removing) return;
            _removing = true;
            if (!VisualOnly) Gone?.Invoke(this);
            StartCoroutine(Shrink());
        }

        private IEnumerator Shrink()
        {
            float t = 0f;
            Vector3 start = transform.localScale;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                transform.localScale = start * (1f - t / 0.25f);
                yield return null;
            }
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- 도형 / 재질

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Transform Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material Lit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        private static Material Glow(Color c)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { color = c };
            return m;
        }
    }
}
