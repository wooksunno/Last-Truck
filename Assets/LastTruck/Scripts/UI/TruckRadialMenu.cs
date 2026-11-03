using System;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 트럭 원형 메뉴: 트럭 근처에서 Tab을 누르면 화면 가운데에 5개 항목(보관함/제작/가공/도감/강화)이 원 둘레에 펼쳐진다.
    ///
    /// 조작 (마우스 플릭)
    ///  - 열리면 커서는 숨겨지고, 마우스를 움직인 만큼 "가상 커서"(중심에서의 벡터 v)가 움직인다.
    ///  - |v|가 데드존보다 크면 v의 방향(Atan2)으로 항목을 고르고, 그 항목이 커지며 이름이 가운데에 뜬다.
    ///  - Tab을 누른 채 방향을 잡고 놓으면(0.3초 이상 눌렀을 때) 확정. Tab을 톡 눌러 열었다면 마우스 왼쪽 클릭으로 확정.
    ///  - Esc / 오른쪽 클릭 / (톡 눌러 연 뒤) Tab 한 번 더 = 취소.
    ///
    /// 배치: 항목 i의 각도 = 90° - 360°/개수 × i (위쪽에서 시계 방향). 위치 = 중심 + 반지름 × (cos, -sin) (UI는 y가 아래).
    /// 선택: SelectIndex(v)는 v를 위쪽 기준 시계 방향 각도로 바꾼 뒤 부채꼴 한 칸(360/개수) 폭으로 나눈다.
    /// 확정 이벤트: TruckRadialMenu.Selected(int) — 기본 동작은 GameUIController.OpenTruckAt(트럭, 플레이어, 번호)로 해당 창을 연다.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public class TruckRadialMenu : MonoBehaviour
    {
        public const int Count = 5;
        private const string SettingsPath = "UI/LastTruckRadialPanelSettings";
        private const string UssPath = "UI/LastTruckHud";

        // 조절 값 (UI 기준 픽셀)
        private const float WheelSize = 700f;
        private const float RingRadius = 215f;
        private const float NodeSize = 132f;
        private const float Deadzone = 52f;
        private const float MaxCursor = 150f;
        private const float MouseSensitivity = 14f;
        private const float HoldSeconds = 0.3f;

        private static readonly string[] Names = { "보관함", "제작", "가공", "도감", "강화" };

        /// <summary>항목이 확정됐을 때 (0 보관함, 1 제작, 2 가공, 3 도감, 4 강화).</summary>
        public static event Action<int> Selected;

        private static TruckRadialMenu _instance;
        public static bool IsOpen => _instance != null && _instance._open;

        // ---------------------------------------------------------------- 계산 (정적)

        /// <summary>항목 index의 각도(도). 위쪽이 90°, 시계 방향으로 갈수록 줄어든다.</summary>
        public static float NodeAngleDeg(int index, int count) => 90f - 360f / count * index;

        /// <summary>항목 index의 위치 (중심 기준, UI 좌표: x 오른쪽, y 아래).</summary>
        public static Vector2 NodeOffset(int index, int count, float radius)
        {
            float a = NodeAngleDeg(index, count) * Mathf.Deg2Rad;
            return new Vector2(radius * Mathf.Cos(a), -radius * Mathf.Sin(a));
        }

        /// <summary>마우스 벡터 v(오른쪽 +x, 위 +y)가 가리키는 항목 번호. 데드존 안이면 -1.</summary>
        public static int SelectIndex(Vector2 v, float deadzone, int count)
        {
            if (v.magnitude < deadzone) return -1;
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;          // 오른쪽 0°, 위 90°
            float step = 360f / count;
            // 위쪽(90°)에서 시계 방향으로 잰 각도 + 반 칸 → 칸 번호
            float clockwiseFromTop = Mathf.Repeat(90f - angle + step * 0.5f, 360f);
            return Mathf.FloorToInt(clockwiseFromTop / step) % count;
        }

        // ---------------------------------------------------------------- 상태

        private bool _built;
        private bool _open;
        private TruckStation _truck;
        private PlayerInventory _player;
        private PlayerMove _move;
        private float _openedAt;
        private bool _sticky;
        private Vector2 _v;
        private int _index = -1;
        private bool _processLocked;

        private VisualElement _overlay;
        private readonly VisualElement[] _nodes = new VisualElement[Count];
        private readonly VisualElement[] _locks = new VisualElement[Count];
        private readonly float[] _scale = new float[Count];
        private VisualElement _cursor;
        private VisualElement _center;
        private VisualElement _centerIcon;
        private Label _centerLabel;
        private StyleFontDefinition _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("TruckRadialMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<TruckRadialMenu>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            Selected = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---------------------------------------------------------------- 열기 / 닫기

        /// <summary>
        /// 원형 메뉴를 연다. UI 에셋이 없어서 메뉴를 못 만들면 예전처럼 트럭 창을 바로 연다.
        /// </summary>
        public static void Open(TruckStation truck, PlayerInventory player)
        {
            if (truck == null || player == null) return;

            if (_instance == null || !_instance.EnsureBuilt())
            {
                truck.OnInteract(player);
                return;
            }
            _instance.Show(truck, player);
        }

        private bool EnsureBuilt()
        {
            if (_built) return true;

            var skin = LastTruckUiSkin.Get();
            PanelSettings settings = Resources.Load<PanelSettings>(SettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (skin == null || settings == null || uss == null || skin.roundBrown == null || skin.roundGreyDark == null)
                return false;
            if (UiIcons.Get(GameUIController.TabIconNames[0]) == null)
                return false;

            _font = LastTruckHud.SharedFont ?? new StyleFontDefinition(StyleKeyword.Null);

            var go = new GameObject("UIDocument_Radial");
            go.transform.SetParent(transform, false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            VisualElement root = doc.rootVisualElement;
            root.AddToClassList("lt-root");
            root.pickingMode = PickingMode.Ignore;
            root.styleSheets.Add(uss);

            // 어둡게 깔기
            _overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.top = 0; _overlay.style.right = 0; _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.38f);
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            // 바퀴 (화면 가운데)
            var wheel = new VisualElement { pickingMode = PickingMode.Ignore };
            wheel.style.position = Position.Absolute;
            wheel.style.left = new Length(50f, LengthUnit.Percent);
            wheel.style.top = new Length(50f, LengthUnit.Percent);
            wheel.style.width = WheelSize;
            wheel.style.height = WheelSize;
            wheel.style.marginLeft = -WheelSize * 0.5f;
            wheel.style.marginTop = -WheelSize * 0.5f;
            _overlay.Add(wheel);

            float c = WheelSize * 0.5f;

            // 바깥 원판
            var disc = new VisualElement { pickingMode = PickingMode.Ignore };
            disc.style.position = Position.Absolute;
            float discSize = (RingRadius + NodeSize * 0.62f) * 2f;
            disc.style.left = c - discSize * 0.5f; disc.style.top = c - discSize * 0.5f;
            disc.style.width = discSize; disc.style.height = discSize;
            disc.style.backgroundImage = new StyleBackground(skin.roundBrownDark);
            disc.style.unityBackgroundImageTintColor = new Color(0.55f, 0.46f, 0.4f, 0.78f);
            wheel.Add(disc);

            // 가운데 원 + 이름
            _center = new VisualElement { pickingMode = PickingMode.Ignore };
            _center.style.position = Position.Absolute;
            const float centerSize = 170f;
            _center.style.left = c - centerSize * 0.5f; _center.style.top = c - centerSize * 0.5f;
            _center.style.width = centerSize; _center.style.height = centerSize;
            _center.style.backgroundImage = new StyleBackground(skin.roundGreyDark);
            _center.style.justifyContent = Justify.Center;
            _center.style.alignItems = Align.Center;
            wheel.Add(_center);

            _centerIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            _centerIcon.style.width = 70; _centerIcon.style.height = 70;
            var truckIcon = UiIcons.Get("truck");
            if (truckIcon != null) _centerIcon.style.backgroundImage = new StyleBackground(truckIcon);
            _center.Add(_centerIcon);

            _centerLabel = new Label { pickingMode = PickingMode.Ignore };
            _centerLabel.AddToClassList("lt-text");
            _centerLabel.style.fontSize = 30;
            _centerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _centerLabel.style.unityFontDefinition = _font;
            _centerLabel.style.display = DisplayStyle.None;
            _center.Add(_centerLabel);

            // 항목 5개
            for (int i = 0; i < Count; i++)
            {
                Vector2 off = NodeOffset(i, Count, RingRadius);
                var node = new VisualElement { pickingMode = PickingMode.Ignore };
                node.style.position = Position.Absolute;
                node.style.left = c + off.x - NodeSize * 0.5f;
                node.style.top = c + off.y - NodeSize * 0.5f;
                node.style.width = NodeSize; node.style.height = NodeSize;
                node.style.backgroundImage = new StyleBackground(skin.roundBrown);
                node.style.justifyContent = Justify.Center;
                node.style.alignItems = Align.Center;

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.style.width = 76; icon.style.height = 76;
                var sp = UiIcons.Get(GameUIController.TabIconNames[i]);
                if (sp != null) icon.style.backgroundImage = new StyleBackground(sp);
                node.Add(icon);

                // 잠김 표시 (가공: 설비가 없을 때)
                var lockIcon = new VisualElement { pickingMode = PickingMode.Ignore };
                lockIcon.style.position = Position.Absolute;
                lockIcon.style.right = 6; lockIcon.style.bottom = 6;
                lockIcon.style.width = 36; lockIcon.style.height = 36;
                var ls = UiIcons.Get("lock");
                if (ls != null) lockIcon.style.backgroundImage = new StyleBackground(ls);
                lockIcon.style.display = DisplayStyle.None;
                node.Add(lockIcon);

                wheel.Add(node);
                _nodes[i] = node;
                _locks[i] = lockIcon;
                _scale[i] = 1f;
            }

            // 가상 커서 점
            _cursor = new VisualElement { pickingMode = PickingMode.Ignore };
            _cursor.style.position = Position.Absolute;
            _cursor.style.width = 22; _cursor.style.height = 22;
            _cursor.style.backgroundImage = new StyleBackground(skin.roundBrownDark);
            _cursor.style.unityBackgroundImageTintColor = new Color(1f, 0.95f, 0.8f, 1f);
            wheel.Add(_cursor);

            _built = true;
            return true;
        }

        private void Show(TruckStation truck, PlayerInventory player)
        {
            _truck = truck;
            _player = player;
            _open = true;
            _openedAt = Time.unscaledTime;
            _sticky = false;
            _v = Vector2.zero;
            _index = -1;

            TruckInventory inv = truck.TruckInventory;
            _processLocked = inv != null && !TruckFacilities.AnyBuilt(inv);
            for (int i = 0; i < Count; i++)
                _locks[i].style.display = (i == 2 && _processLocked) ? DisplayStyle.Flex : DisplayStyle.None;

            _overlay.style.display = DisplayStyle.Flex;

            // 이동/카메라 입력을 막는다 (플레이어 이동 스크립트의 UI 열림 상태)
            _move = player.GetComponent<PlayerMove>();
            if (_move != null) _move.IsUIOpen = true;
            ApplyCursor();
            Refresh(true);
        }

        private void Close(bool restoreInput)
        {
            _open = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            if (restoreInput && _move != null) _move.IsUIOpen = false;
            _move = null;
        }

        // ---------------------------------------------------------------- 입력

        private void Update()
        {
            if (!_open) return;

            // 다른 창이 열렸거나 플레이어가 사라졌으면 닫는다
            GameUIController ui = GameUIController.Instance;
            if (_truck == null || _player == null || (ui != null && ui.IsPopupOpen))
            {
                Close(false);
                return;
            }

            // 가상 커서: 마우스 이동량을 쌓는다 (커서는 잠겨 있다)
            Vector2 delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * MouseSensitivity;
            _v += delta;
            if (_v.magnitude > MaxCursor) _v = _v.normalized * MaxCursor;
            _index = SelectIndex(_v, Deadzone, Count);

            bool heldLong = Time.unscaledTime - _openedAt >= HoldSeconds;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1) || (_sticky && Input.GetKeyDown(KeyCode.Tab)))
            {
                Close(true);
                return;
            }

            if (Input.GetKeyUp(KeyCode.Tab))
            {
                if (heldLong)
                {
                    // 누른 채 방향을 잡고 놓음 → 확정 (방향을 안 잡았으면 취소)
                    if (_index >= 0) Confirm(); else Close(true);
                    return;
                }
                _sticky = true;   // 톡 눌러서 열었다: 이제 클릭으로 확정
            }

            if (Input.GetMouseButtonDown(0) && _index >= 0)
            {
                Confirm();
                return;
            }

            Refresh(false);
        }

        private void LateUpdate()
        {
            if (_open) ApplyCursor();
        }

        private static void ApplyCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        private void Confirm()
        {
            int idx = _index;
            TruckStation truck = _truck;
            PlayerInventory player = _player;
            PlayerMove move = _move;
            Close(false);   // 창이 이어서 열리므로 입력 막힘 상태는 그대로 둔다

            Selected?.Invoke(idx);

            GameUIController ui = GameUIController.Instance;
            if (ui != null && truck != null && player != null)
                ui.OpenTruckAt(truck, player, idx);
            else if (move != null)
                move.IsUIOpen = false;
        }

        // ---------------------------------------------------------------- 그리기

        private void Refresh(bool instant)
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < Count; i++)
            {
                bool sel = i == _index;
                float target = sel ? 1.32f : 1f;
                _scale[i] = instant ? target : Mathf.MoveTowards(_scale[i], target, dt * 5f);
                _nodes[i].style.scale = new Scale(new Vector2(_scale[i], _scale[i]));

                bool locked = i == 2 && _processLocked;
                Color tint = sel ? new Color(1f, 0.86f, 0.45f, 1f)
                    : (locked ? new Color(0.5f, 0.5f, 0.55f, 1f) : new Color(0.88f, 0.8f, 0.72f, 1f));
                _nodes[i].style.unityBackgroundImageTintColor = tint;
            }

            // 가운데: 고른 항목 이름 (없으면 트럭 그림)
            if (_index >= 0)
            {
                _centerLabel.text = Names[_index];
                _centerLabel.style.display = DisplayStyle.Flex;
                _centerIcon.style.display = DisplayStyle.None;
            }
            else
            {
                _centerLabel.style.display = DisplayStyle.None;
                _centerIcon.style.display = DisplayStyle.Flex;
            }

            // 가상 커서 점 (위가 +y 이므로 화면 y는 부호를 뒤집는다)
            float c = WheelSize * 0.5f;
            _cursor.style.left = c + _v.x * 0.7f - 11f;
            _cursor.style.top = c - _v.y * 0.7f - 11f;
        }
    }
}
