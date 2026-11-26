using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 유정(OilWellSite)에 설치된 석유 펌프잭. 머리를 끄덕이며 일정 시간마다 기름을 탱크에 모은다.
    ///  - E: 펌프 화면을 연다 (땅속 기름이 관을 타고 빨려 올라와 탱크에 차는 단면 장면 + 기름 받기 / 회수 버튼)
    ///    화면을 열 수 없으면 예전처럼: 기름이 있으면 받기, 비었으면 회수
    ///  - 받기: 가방에 안 들어가는 만큼은 발밑에 떨어진다. 회수: 탱크가 비었을 때만
    /// 멀티플레이: 탱크 양은 호스트 값이 기준이다. 각 화면은 표시용으로 같은 속도로 채우고, 받을 때 호스트가 맞춰 준다.
    /// </summary>
    public class OilPumpjack : MonoBehaviour, LastTruck.IHoldInteractable, LastTruck.IInteractLabel, LastTruck.IInteractPriority
    {
        public const float SecondsPerOil = 20f;
        public const int Capacity = 15;
        private const int InteractLayer = 4;   // PlayerInteract.interactLayer (Water)

        private OilWellSite _site;
        private float _tank;
        private Transform _beam;
        private Transform _rod;
        private Transform _crank;
        private Transform _pulley;
        private Transform _gaugeFill;
        private Renderer _fullLamp;
        private Transform[] _pitman = new Transform[2];
        private float _phase;
        private float _lastSin;
        private float _nextVent;
        private bool _display;   // 펌프 화면(단면 장면)에 띄운 모형: 탱크/충돌/연기 없이 진짜 펌프잭의 움직임만 따라 한다
        private OilPumpjack _mirror;

        // 배치 (로컬 좌표, z+ = 말머리 쪽). 유정 구멍(사이트 위치)이 원점이라 막대가 유정 머리로 바로 들어간다.
        private const float PivotZ = -2.3f, PivotY = 3.1f;      // 삼손 기둥 꼭대기 (빔이 도는 축)
        private const float HeadZ = 0.0f;                        // 말머리 앞면 = 유정 바로 위
        private const float TailZ = -4.5f;                       // 빔 꼬리 (피트먼 팔이 걸리는 곳)
        private const float CrankZ = -4.5f, CrankY = 1.25f, CrankR = 0.55f;
        private static readonly Vector3 TankPos = new Vector3(2.6f, 0f, -1.6f);

        public OilWellSite Site => _site;
        public float Tank => _tank;
        public int Ready => Mathf.FloorToInt(_tank);

        public float RequiredHoldSeconds => 0f;
        public int InteractPriority => 6;
        public string InteractLabel => "석유 펌프잭";
        public string InteractSubLabel => $"기름 {Ready}/{Capacity} · E: 펌프 화면";
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

        /// <summary>펌프가 일하는 중인가 (탱크가 가득 차면 멈춘다).</summary>
        public bool Working => _tank < Capacity;
        /// <summary>크랭크 위상 (단면 장면이 막대/기름 움직임을 맞춘다).</summary>
        public float Phase => _phase;
        /// <summary>다음 기름 1개까지 남은 초.</summary>
        public float SecondsToNext => Working ? (1f - (_tank % 1f)) * SecondsPerOil : 0f;

        /// <summary>펌프 화면용 모형: 진짜 펌프잭과 같은 모양이지만 탱크 없이 움직임만 따라 한다.</summary>
        public static OilPumpjack CreateDisplay(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("PumpjackModel");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            var pump = root.AddComponent<OilPumpjack>();
            pump._display = true;
            pump.BuildVisual();
            return pump;
        }

        /// <summary>모형이 따라 할 진짜 펌프잭.</summary>
        public void Mirror(OilPumpjack real) { _mirror = real; }

        /// <summary>모인 기름(정수)을 꺼낸다.</summary>
        public int TakeReady()
        {
            int n = Ready;
            _tank -= n;
            return n;
        }

        private void Update()
        {
            if (_display)
            {
                // 모형: 진짜 펌프잭의 위상/탱크를 그대로 따라 한다 (진짜가 없으면 계속 일하는 것처럼)
                if (_mirror != null) { _tank = _mirror._tank; _phase = _mirror._phase; }
                else { _tank = 0f; _phase += Time.deltaTime * 1.7f; }
            }
            bool working = _tank < Capacity;
            if (working && !_display)
                _tank = Mathf.Min(Capacity, _tank + Time.deltaTime / SecondsPerOil);

            // 크랭크가 돌면 피트먼 팔이 빔 꼬리를 당겨 말머리가 끄덕이고, 광택 막대가 유정을 오르내린다 (가득 차면 멈춤)
            if (working && !_display) _phase += Time.deltaTime * 1.7f;
            float sn = Mathf.Sin(_phase);
            float tilt = sn * 13f;   // +면 말머리가 내려간다
            if (_beam != null) _beam.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            // 크랭크 핀 높이가 sin(위상)을 따라가게 돌려서 빔 꼬리와 같이 오르내린다 (피트먼 팔 길이가 거의 일정)
            if (_crank != null) _crank.localRotation = Quaternion.Euler(-(_phase - Mathf.PI * 0.5f) * Mathf.Rad2Deg, 0f, 0f);
            if (_pulley != null && working) _pulley.Rotate(Vector3.right, Time.deltaTime * 900f, Space.Self);

            // 광택 막대: 말머리 앞면에서 유정 머리까지 (줄에 매달려 위아래로)
            if (_rod != null && _beam != null)
            {
                Vector3 hang = transform.InverseTransformPoint(_beam.TransformPoint(new Vector3(0f, -0.2f, HeadZ - PivotZ)));
                float bottom = 0.62f;
                float len = Mathf.Max(0.2f, hang.y - bottom);
                _rod.localPosition = new Vector3(0f, bottom + len * 0.5f, 0f);
                _rod.localScale = new Vector3(0.07f, len * 0.5f, 0.07f);
            }

            // 피트먼 팔: 크랭크 핀 ~ 빔 꼬리
            if (_crank != null && _beam != null)
            {
                Vector3 tail = transform.InverseTransformPoint(_beam.TransformPoint(new Vector3(0f, -0.15f, TailZ - PivotZ)));
                for (int i = 0; i < 2; i++)
                {
                    if (_pitman[i] == null) continue;
                    float x = i == 0 ? -0.62f : 0.62f;
                    Vector3 pin = transform.InverseTransformPoint(_crank.TransformPoint(new Vector3(0f, CrankR, 0f)));
                    Vector3 a = new Vector3(x, pin.y, pin.z), b = new Vector3(x, tail.y, tail.z);
                    _pitman[i].localPosition = (a + b) * 0.5f;
                    _pitman[i].localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                    _pitman[i].localScale = new Vector3(0.12f, Vector3.Distance(a, b) * 0.5f, 0.12f);
                }
            }

            // 막대가 내려가는 순간 유정 머리에서 기름이 찔끔 튄다
            if (working && !_display && _lastSin <= 0f && sn > 0f)
                ActionFx.Burst(transform.TransformPoint(new Vector3(0f, 0.75f, 0f)), OilWellRig.OilColor, 5, 1.4f, 0.07f, 0.6f, 2.2f, 0.5f, false, 0.08f);
            _lastSin = sn;

            // 탱크 계기: 모인 만큼 검은 기름이 차오르고, 가득 차면 빨간 등이 켜진다
            float f = _tank / Capacity;
            if (_gaugeFill != null)
            {
                _gaugeFill.localScale = new Vector3(0.12f, Mathf.Max(0.001f, f * 0.75f), 0.06f);
                _gaugeFill.localPosition = new Vector3(TankPos.x - 0.86f, 0.2f + f * 0.75f, TankPos.z);
            }
            if (_fullLamp != null)
            {
                bool full = !working;
                Color c = full ? Color.Lerp(new Color(0.5f, 0.05f, 0.03f), new Color(1f, 0.15f, 0.08f), 0.5f + 0.5f * Mathf.Sin(Time.time * 6f)) : new Color(0.18f, 0.08f, 0.07f);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_EmissionColor", full ? c * 1.5f : Color.black);
                _fullLamp.SetPropertyBlock(mpb);
            }

            // 탱크 숨구멍에서 가끔 김이 오른다
            if (working && !_display && Time.time >= _nextVent)
            {
                _nextVent = Time.time + Random.Range(2.5f, 5f);
                ActionFx.Burst(transform.TransformPoint(TankPos + new Vector3(0.3f, 2.05f, 0f)), new Color(0.55f, 0.52f, 0.5f), 3, 0.5f, 0.1f, 1.2f, -0.2f, 1f, false, 0.05f);
            }
        }

        public void Interact(GameObject player)
        {
            if (player == null || _site == null || _display) return;
            // 펌프 화면을 연다 (못 열면 예전처럼 바로 받기/회수)
            if (GameUIController.Instance != null && GameUIController.Instance.OpenPumpScreen(this)) return;
            if (Ready >= 1) Collect(player); else Retrieve(player);
        }

        /// <summary>모인 기름을 받는다 (가방에 안 들어가는 만큼은 발밑에).</summary>
        public void Collect(GameObject player)
        {
            PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inv == null || _site == null || Ready < 1) return;
            GushFromTank();
            // 멀티플레이: 호스트가 탱크에서 꺼내 준다
            if (LastTruck.Networking.NetworkGathering.TryCollectPump(_site.transform.position)) return;
            int n = TakeReady();
            inv.AddOrDrop(ItemCatalog.GetOrCreate().GetItem(ItemIds.Oil), n);
            LastTruck.HudToast.Show($"기름 {n}개를 받았어요");
        }

        /// <summary>탱크가 비었으면 펌프잭을 아이템으로 회수한다.</summary>
        public bool Retrieve(GameObject player)
        {
            PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inv == null || _site == null) return false;
            if (Ready >= 1) { LastTruck.HudToast.Show("기름을 먼저 받아야 회수할 수 있어요"); return false; }
            if (LastTruck.Networking.NetworkGathering.TryRemovePump(_site.transform.position)) return true;
            inv.AddOrDrop(ItemCatalog.GetOrCreate().GetItem(ItemIds.OilPumpjack), 1);
            _site.RemovePump();
            LastTruck.HudToast.Show("석유 펌프잭을 회수했어요");
            return true;
        }

        // ───── 모양: 콘크리트 받침 + 삼손 기둥(A자) + 끄덕이는 노란 빔과 말머리 + 크랭크·평형추 + 피트먼 팔 + 모터 + 광택 막대 + 저장 탱크(계기) ─────
        private void BuildVisual()
        {
            Color concrete = new Color(0.55f, 0.54f, 0.5f);
            Color steel = new Color(0.32f, 0.34f, 0.38f);
            Color dark = new Color(0.13f, 0.12f, 0.12f);
            Color yellow = new Color(0.95f, 0.7f, 0.16f);
            Color red = new Color(0.62f, 0.16f, 0.1f);
            Color tankRed = new Color(0.5f, 0.17f, 0.12f);
            Color glass = new Color(0.75f, 0.85f, 0.85f);

            Transform T = transform;
            // 받침 (유정 쪽은 비워 두고 뒤로 길게)
            OilWellRig.Part(PrimitiveType.Cube, T, "Pad", new Vector3(0f, 0.08f, -2.6f), new Vector3(2.0f, 0.16f, 5.4f), concrete);
            OilWellRig.Part(PrimitiveType.Cube, T, "Skid", new Vector3(0f, 0.24f, -2.9f), new Vector3(1.2f, 0.16f, 4.4f), steel);

            // 삼손 기둥: 앞뒤 두 쌍의 다리가 꼭대기 축에서 만난다
            Vector3 top = new Vector3(0f, PivotY, PivotZ);
            foreach (float x in new[] { -0.5f, 0.5f })
                foreach (float dz in new[] { -0.9f, 0.7f })
                {
                    Vector3 foot = new Vector3(x * 1.6f, 0.3f, PivotZ + dz);
                    Vector3 headPt = new Vector3(x * 0.5f, PivotY - 0.1f, PivotZ);
                    var leg = OilWellRig.Part(PrimitiveType.Cube, T, "SamsonLeg", (foot + headPt) * 0.5f, new Vector3(0.16f, Vector3.Distance(foot, headPt), 0.16f), steel);
                    leg.localRotation = Quaternion.FromToRotation(Vector3.up, (headPt - foot).normalized);
                }
            OilWellRig.Part(PrimitiveType.Cylinder, T, "Saddle", top, new Vector3(0.22f, 0.45f, 0.22f), dark, new Vector3(0f, 0f, 90f));

            // 빔 (피벗 = 삼손 기둥 꼭대기)
            _beam = OilWellRig.Empty(T, "Beam", top);
            float front = HeadZ - PivotZ, back = TailZ - PivotZ;
            OilWellRig.Part(PrimitiveType.Cube, _beam, "WalkingBeam", new Vector3(0f, 0.2f, (front + back) * 0.5f - 0.2f), new Vector3(0.3f, 0.36f, front - back + 0.2f), yellow);
            // 말머리: 앞쪽이 둥글게 휜 판 (세 조각으로 호를 만든다)
            for (int i = 0; i < 3; i++)
            {
                float a = (-35f + i * 35f);
                float rad = a * Mathf.Deg2Rad;
                Vector3 p = new Vector3(0f, 0.15f + Mathf.Sin(rad) * 0.75f, front - 0.25f + Mathf.Cos(rad) * 0.3f);
                OilWellRig.Part(PrimitiveType.Cube, _beam, "HorseHead", p, new Vector3(0.42f, 0.62f, 0.28f), yellow, new Vector3(-a, 0f, 0f));
            }
            OilWellRig.Part(PrimitiveType.Cube, _beam, "HeadBack", new Vector3(0f, 0.15f, front - 0.55f), new Vector3(0.36f, 1.2f, 0.5f), yellow);
            // 말머리에서 늘어진 굴레 줄 두 가닥
            foreach (float x in new[] { -0.08f, 0.08f })
                OilWellRig.Part(PrimitiveType.Cylinder, _beam, "Bridle", new Vector3(x, -0.25f, front + 0.02f), new Vector3(0.025f, 0.45f, 0.025f), dark);
            // 꼬리 쪽 가로대(피트먼 팔이 걸린다)
            OilWellRig.Part(PrimitiveType.Cylinder, _beam, "Equalizer", new Vector3(0f, -0.15f, back), new Vector3(0.12f, 0.7f, 0.12f), dark, new Vector3(0f, 0f, 90f));

            // 감속기 + 크랭크 + 평형추 (크랭크는 x축으로 돈다)
            OilWellRig.Part(PrimitiveType.Cube, T, "Gearbox", new Vector3(0f, 0.95f, CrankZ), new Vector3(0.7f, 1.1f, 0.9f), steel);
            _crank = OilWellRig.Empty(T, "Crank", new Vector3(0f, CrankY, CrankZ));
            foreach (float x in new[] { -0.55f, 0.55f })
            {
                OilWellRig.Part(PrimitiveType.Cube, _crank, "CrankArm", new Vector3(x, 0.05f, 0f), new Vector3(0.12f, 1.5f, 0.3f), red);
                OilWellRig.Part(PrimitiveType.Cube, _crank, "Counterweight", new Vector3(x * 1.05f, -0.62f, 0f), new Vector3(0.2f, 0.55f, 0.85f), dark);
            }
            OilWellRig.Part(PrimitiveType.Cylinder, _crank, "CrankShaft", Vector3.zero, new Vector3(0.18f, 0.62f, 0.18f), dark, new Vector3(0f, 0f, 90f));
            for (int i = 0; i < 2; i++)
                _pitman[i] = OilWellRig.Part(PrimitiveType.Cylinder, T, "PitmanArm", Vector3.zero, new Vector3(0.12f, 1f, 0.12f), steel);

            // 모터 + 벨트 풀리
            OilWellRig.Part(PrimitiveType.Cube, T, "Motor", new Vector3(0f, 0.7f, CrankZ - 1.1f), new Vector3(0.7f, 0.7f, 0.9f), new Color(0.22f, 0.35f, 0.45f));
            _pulley = OilWellRig.Part(PrimitiveType.Cylinder, T, "Pulley", new Vector3(0.45f, 0.75f, CrankZ - 1.1f), new Vector3(0.45f, 0.05f, 0.45f), dark, new Vector3(0f, 0f, 90f));
            OilWellRig.Part(PrimitiveType.Cube, _pulley, "Mark", new Vector3(0f, 0f, 0.3f), new Vector3(0.15f, 1.2f, 0.35f), yellow);

            // 광택 막대 + 유정 위 스터핑 박스
            _rod = OilWellRig.Part(PrimitiveType.Cylinder, T, "PolishedRod", new Vector3(0f, 1.5f, 0f), new Vector3(0.07f, 0.8f, 0.07f), new Color(0.7f, 0.72f, 0.75f));
            OilWellRig.Part(PrimitiveType.Cylinder, T, "StuffingBox", new Vector3(0f, 0.7f, 0f), new Vector3(0.22f, 0.1f, 0.22f), dark);

            if (_display) return;   // 펌프 화면 모형은 탱크를 따로 단면으로 그린다

            // 유정에서 탱크로 가는 배관
            Vector3 a0 = new Vector3(0.25f, 0.4f, 0f), a1 = new Vector3(TankPos.x - 0.8f, 0.4f, 0f), a2 = new Vector3(TankPos.x - 0.8f, 0.4f, TankPos.z);
            Pipe(T, a0, a1, steel); Pipe(T, a1, a2, steel);

            // 저장 탱크 + 사다리 + 숨구멍 + 유리 계기
            OilWellRig.Part(PrimitiveType.Cylinder, T, "StorageTank", TankPos + new Vector3(0f, 1.0f, 0f), new Vector3(1.5f, 1.0f, 1.5f), tankRed);
            OilWellRig.Part(PrimitiveType.Cylinder, T, "TankTop", TankPos + new Vector3(0f, 2.02f, 0f), new Vector3(1.56f, 0.04f, 1.56f), dark);
            OilWellRig.Part(PrimitiveType.Cylinder, T, "Vent", TankPos + new Vector3(0.3f, 2.15f, 0f), new Vector3(0.1f, 0.12f, 0.1f), dark);
            OilWellRig.Part(PrimitiveType.Cube, T, "GaugeGlass", new Vector3(TankPos.x - 0.8f, 0.95f, TankPos.z), new Vector3(0.1f, 1.6f, 0.14f), glass);
            _gaugeFill = OilWellRig.Part(PrimitiveType.Cube, T, "GaugeOil", new Vector3(TankPos.x - 0.86f, 0.2f, TankPos.z), new Vector3(0.12f, 0.01f, 0.06f), OilWellRig.OilColor);
            _fullLamp = OilWellRig.Part(PrimitiveType.Sphere, T, "FullLamp", TankPos + new Vector3(-0.55f, 2.15f, 0f), Vector3.one * 0.16f, red).GetComponent<Renderer>();
            OilWellRig.Part(PrimitiveType.Cube, T, "Valve", new Vector3(TankPos.x, 0.3f, TankPos.z - 0.8f), new Vector3(0.18f, 0.18f, 0.2f), red);
            for (int i = 0; i < 6; i++)
                OilWellRig.Part(PrimitiveType.Cube, T, "Ladder", TankPos + new Vector3(0.78f, 0.25f + i * 0.32f, -0.35f), new Vector3(0.06f, 0.04f, 0.4f), steel);
        }

        private static void Pipe(Transform parent, Vector3 a, Vector3 b, Color c)
        {
            var p = OilWellRig.Part(PrimitiveType.Cylinder, parent, "Pipe", (a + b) * 0.5f, new Vector3(0.12f, Vector3.Distance(a, b) * 0.5f + 0.06f, 0.12f), c);
            p.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        /// <summary>기름을 받을 때 탱크 밸브에서 기름이 콸콸 쏟아진다.</summary>
        private void GushFromTank()
        {
            Vector3 valve = transform.TransformPoint(new Vector3(TankPos.x, 0.35f, TankPos.z - 0.95f));
            ActionFx.Burst(valve, OilWellRig.OilColor, 20, 3.0f, 0.1f, 1.0f, 2.4f, 0.5f, false, 0.12f);
            ActionFx.Burst(transform.TransformPoint(new Vector3(0f, 0.75f, 0f)), OilWellRig.OilColor, 12, 3.2f, 0.09f, 1.0f, 2.0f, 1.0f, false, 0.1f);
        }

        private void Start()
        {
            if (_display) return;
            // 캐릭터가 펌프잭을 뚫고 지나가지 않도록: 빔/크랭크 쪽 상자 + 탱크 원통 (유정 바로 앞은 비워 둬서 E를 누를 수 있다)
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.layer = 0;
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.4f, -3.0f);
            box.size = new Vector3(1.8f, 2.8f, 4.6f);
            var tank = body.AddComponent<CapsuleCollider>();
            tank.center = TankPos + new Vector3(0f, 1.0f, 0f);
            tank.radius = 0.78f; tank.height = 2.1f; tank.direction = 1;
        }
    }
}
