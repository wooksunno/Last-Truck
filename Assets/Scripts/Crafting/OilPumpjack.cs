using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 유정(OilWellSite)에 설치된 석유 펌프잭. 머리를 끄덕이며 일정 시간마다 기름을 탱크에 모은다.
    ///  - E: 모인 기름 받기 (가방에 안 들어가는 만큼은 발밑에 떨어진다)
    ///  - 탱크가 비었을 때 E 꾹(2초): 펌프잭을 아이템으로 회수
    /// 멀티플레이: 탱크 양은 호스트 값이 기준이다. 각 화면은 표시용으로 같은 속도로 채우고, 받을 때 호스트가 맞춰 준다.
    /// </summary>
    public class OilPumpjack : MonoBehaviour, LastTruck.IHoldInteractable, LastTruck.IInteractLabel, LastTruck.IInteractPriority
    {
        public const float SecondsPerOil = 20f;
        public const int Capacity = 15;
        private const float RetrieveHoldSeconds = 2f;
        private const int InteractLayer = 4;   // PlayerInteract.interactLayer (Water)

        private OilWellSite _site;
        private float _tank;
        private Transform _beam;
        private Transform _rod;
        private float _phase;

        public OilWellSite Site => _site;
        public float Tank => _tank;
        public int Ready => Mathf.FloorToInt(_tank);

        public float RequiredHoldSeconds => Ready >= 1 ? 0f : RetrieveHoldSeconds;
        public int InteractPriority => 6;
        public string InteractLabel => "석유 펌프잭";
        public string InteractSubLabel => Ready >= 1
            ? $"기름 {Ready}/{Capacity} · E: 받기"
            : $"기름 모으는 중 {Mathf.RoundToInt((_tank % 1f) * 100f)}% · E 꾹: 회수";
        public Color InteractLabelColor => new Color(0.95f, 0.75f, 0.35f);

        public static OilPumpjack Create(OilWellSite site)
        {
            var root = new GameObject("OilPumpjack");
            root.layer = InteractLayer;
            root.transform.position = site.transform.position;
            root.transform.rotation = Quaternion.Euler(0f, Mathf.Repeat(site.transform.position.x * 37f + site.transform.position.z * 11f, 360f), 0f);

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 3f;
            trigger.center = new Vector3(0f, 1f, 0f);

            var pump = root.AddComponent<OilPumpjack>();
            pump._site = site;
            pump.BuildVisual();
            return pump;
        }

        public void SetTank(float value) { _tank = Mathf.Clamp(value, 0f, Capacity); }

        /// <summary>모인 기름(정수)을 꺼낸다.</summary>
        public int TakeReady()
        {
            int n = Ready;
            _tank -= n;
            return n;
        }

        private void Update()
        {
            bool working = _tank < Capacity;
            if (working)
                _tank = Mathf.Min(Capacity, _tank + Time.deltaTime / SecondsPerOil);

            // 일하는 동안 머리를 끄덕인다 (가득 차면 멈춤)
            if (working) _phase += Time.deltaTime * 1.6f;
            float tilt = Mathf.Sin(_phase) * 14f;
            if (_beam != null) _beam.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            if (_rod != null) _rod.localPosition = new Vector3(0f, 1.25f + Mathf.Sin(_phase) * 0.45f, 2.2f);
        }

        public void Interact(GameObject player)
        {
            PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inv == null || _site == null) return;
            ItemCatalog catalog = ItemCatalog.GetOrCreate();

            if (Ready >= 1)
            {
                // 멀티플레이: 호스트가 탱크에서 꺼내 준다
                if (LastTruck.Networking.NetworkGathering.TryCollectPump(_site.transform.position)) return;
                int n = TakeReady();
                inv.AddOrDrop(catalog.GetItem(ItemIds.Oil), n);
                LastTruck.HudToast.Show($"기름 {n}개를 받았어요");
                return;
            }

            // 비어 있으면 회수
            if (LastTruck.Networking.NetworkGathering.TryRemovePump(_site.transform.position)) return;
            inv.AddOrDrop(catalog.GetItem(ItemIds.OilPumpjack), 1);
            _site.RemovePump();
            LastTruck.HudToast.Show("석유 펌프잭을 회수했어요");
        }

        // ───── 모양: 받침 + A자 기둥 + 끄덕이는 빔(말머리) + 오르내리는 막대 + 기름통 ─────
        private void BuildVisual()
        {
            Color steel = new Color(0.32f, 0.34f, 0.38f);
            Color yellow = new Color(0.95f, 0.72f, 0.18f);
            Color dark = new Color(0.12f, 0.11f, 0.1f);
            Color red = new Color(0.65f, 0.18f, 0.12f);

            Part(PrimitiveType.Cube, transform, new Vector3(0f, 0.15f, 0.4f), new Vector3(1.6f, 0.3f, 4.4f), steel);          // 받침
            Part(PrimitiveType.Cube, transform, new Vector3(-0.45f, 1.3f, 0f), new Vector3(0.18f, 2.4f, 0.18f), steel, new Vector3(0f, 0f, -10f));   // A자 기둥
            Part(PrimitiveType.Cube, transform, new Vector3(0.45f, 1.3f, 0f), new Vector3(0.18f, 2.4f, 0.18f), steel, new Vector3(0f, 0f, 10f));
            Part(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.1f, -1.5f), new Vector3(0.9f, 0.6f, 0.9f), dark);       // 모터 쪽 기름통
            var drum = Part(PrimitiveType.Cylinder, transform, new Vector3(0.9f, 0.55f, -1.4f), new Vector3(0.6f, 0.5f, 0.6f), red);   // 탱크 드럼
            drum.name = "Tank";

            // 빔(피벗이 기둥 꼭대기)
            var beam = new GameObject("Beam").transform;
            beam.SetParent(transform, false);
            beam.localPosition = new Vector3(0f, 2.5f, 0f);
            _beam = beam;
            Part(PrimitiveType.Cube, beam, new Vector3(0f, 0f, 0.3f), new Vector3(0.28f, 0.28f, 4.2f), yellow);              // 빔
            Part(PrimitiveType.Cube, beam, new Vector3(0f, -0.35f, 2.35f), new Vector3(0.4f, 1.0f, 0.5f), yellow, new Vector3(15f, 0f, 0f)); // 말머리
            Part(PrimitiveType.Cube, beam, new Vector3(0f, -0.25f, -1.75f), new Vector3(0.6f, 0.7f, 0.5f), dark);            // 평형추

            // 유정 구멍으로 오르내리는 막대
            var rod = Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 1.25f, 2.2f), new Vector3(0.08f, 1.0f, 0.08f), dark);
            _rod = rod.transform;
            Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.1f, 2.2f), new Vector3(0.5f, 0.15f, 0.5f), dark);       // 유정 머리
        }

        private static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            Object.Destroy(go.GetComponent<Collider>());   // 모양만 (부딪힘은 루트의 트리거가 아닌 아래 상자 하나로)
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); else mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private void Start()
        {
            // 캐릭터가 펌프잭을 뚫고 지나가지 않도록 몸체에 단단한 상자 하나
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.layer = 0;
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.2f, 0.3f);
            box.size = new Vector3(1.4f, 2.4f, 4.2f);
        }
    }
}
