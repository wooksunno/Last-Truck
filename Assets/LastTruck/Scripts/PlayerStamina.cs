using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    /// <summary>
    /// 달리기 스태미나. 체력 막대 바로 밑에 스태미나 막대를 보여준다.
    ///
    /// - 씬에 따로 둘 필요가 없다: 게임이 시작되면 자동으로 만들어지고, 씬의 체력 막대(PlayerHPUI)를 복사해서 아래에 붙인다.
    /// - Shift를 누른 채 움직이면 줄어들고, 멈추거나 걸으면 잠깐 뒤에 차오른다.
    /// - 다 쓰면 "지침" 상태: 일정량(회복 기준)까지 찰 때까지 달릴 수 없다.
    /// - 싱글플레이(PlayerMove)와 멀티플레이(입력 Walk 버튼) 모두 PlayerStamina.Sprinting 하나만 보고 달린다.
    ///   (멀티플레이는 내 컴퓨터에서만 계산해서 입력에 실어 보낸다)
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerStamina : MonoBehaviour
    {
        [Header("스태미나")]
        [SerializeField] private float maxStamina = 100f;
        [Tooltip("달리는 동안 초당 줄어드는 양")]
        [SerializeField] private float drainPerSecond = 22f;
        [Tooltip("쉬는 동안 초당 차오르는 양")]
        [SerializeField] private float regenPerSecond = 20f;
        [Tooltip("달리기를 멈춘 뒤 이 시간(초)이 지나야 차오르기 시작한다")]
        [SerializeField] private float regenDelay = 0.8f;
        [Tooltip("다 써서 지친 뒤, 전체의 이 비율까지 차야 다시 달릴 수 있다")]
        [Range(0.05f, 1f)]
        [SerializeField] private float resumeRatio = 0.25f;

        public static PlayerStamina Instance { get; private set; }

        /// <summary>지금 달리는 중인가 (이동 코드가 속도를 올릴지 판단하는 값).</summary>
        public static bool Sprinting => Instance != null && Instance._sprinting;

        public float Current { get; private set; }
        public float Max => maxStamina;
        public bool Exhausted { get; private set; }

        private bool _sprinting;
        private float _regenTimer;

        private PlayerMove _move;
        private float _nextFindMove;

        private Scrollbar _bar;
        private Image _handle;
        private CanvasGroup _group;
        private float _nextFindBar;

        private static readonly Color NormalColor = new Color(0.95f, 0.78f, 0.25f, 1f);
        private static readonly Color TiredColor = new Color(0.55f, 0.50f, 0.45f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("PlayerStamina");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerStamina>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Current = maxStamina;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            ResolveMove();
            UpdateStamina();
            UpdateBar();
        }

        private void ResolveMove()
        {
            if (_move != null || Time.unscaledTime < _nextFindMove) return;
            _nextFindMove = Time.unscaledTime + 0.5f;

            // 멀티플레이: 내 캐릭터 / 싱글플레이: 씬의 플레이어
            var local = LastTruck.Networking.NetworkPlayer.Local;
            _move = local != null ? local.GetComponent<PlayerMove>() : null;
            if (_move == null && !LastTruck.Networking.GameLauncher.IsOnlineSession)
                _move = FindFirstObjectByType<PlayerMove>();
        }

        private void UpdateStamina()
        {
            float dt = Time.deltaTime;

            // 걸어 다닐 수 있는 상태인가 (UI가 열려 있거나 기절/트럭 탑승이면 달리지 않는다)
            bool canMove = _move != null && _move.isActiveAndEnabled && !_move.IsMovementBlocked;
            bool moving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
            Tick(canMove && moving && Input.GetKey(KeyCode.LeftShift), dt);
        }

        private void Tick(bool wantSprint, float dt)
        {
            if (wantSprint && !Exhausted && Current > 0f)
            {
                _sprinting = true;
                Current = Mathf.Max(0f, Current - drainPerSecond * dt);
                _regenTimer = regenDelay;
                if (Current <= 0f) Exhausted = true;
            }
            else
            {
                _sprinting = false;
                // Shift를 계속 누르고 있어도 지친 상태에서는 회복이 시작된다 (달리는 시도가 회복을 막지 않게)
                _regenTimer -= dt;
                if (_regenTimer <= 0f)
                    Current = Mathf.Min(maxStamina, Current + regenPerSecond * dt);
            }

            if (Exhausted && Current >= maxStamina * resumeRatio)
                Exhausted = false;
        }

        // ---------------------------------------------------------------- HUD

        private void UpdateBar()
        {
            if (_bar == null)
            {
                if (Time.unscaledTime < _nextFindBar) return;
                _nextFindBar = Time.unscaledTime + 1f;
                BuildBar();
                if (_bar == null) return;
            }

            float ratio = maxStamina > 0f ? Mathf.Clamp01(Current / maxStamina) : 0f;
            _bar.size = ratio;
            if (_handle != null) _handle.color = Exhausted ? TiredColor : NormalColor;

            // 가득 차 있고 안 달릴 때는 흐리게
            if (_group != null)
            {
                float target = (ratio >= 0.999f && !_sprinting) ? 0.7f : 1f;
                _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 3f);
            }
        }

        /// <summary>체력 막대(PlayerHP)를 복사해서 한 칸 아래에 스태미나 막대를 만든다.</summary>
        private void BuildBar()
        {
            PlayerHPUI hpUi = FindFirstObjectByType<PlayerHPUI>(FindObjectsInactive.Include);
            if (hpUi == null) return;

            Transform hpRoot = hpUi.transform.parent;   // UI/InformUI/PlayerHP
            if (hpRoot == null) return;

            // 같은 부모 밑에 이미 만들어 둔 게 있으면 그것을 쓴다
            Transform existing = hpRoot.parent != null ? hpRoot.parent.Find("PlayerStamina") : null;
            GameObject clone = existing != null ? existing.gameObject : Instantiate(hpRoot.gameObject, hpRoot.parent);
            clone.name = "PlayerStamina";

            foreach (PlayerHPUI ui in clone.GetComponentsInChildren<PlayerHPUI>(true))
                Destroy(ui);   // 체력 갱신 스크립트는 떼어낸다 (체력 막대를 같이 움직이면 안 되므로)

            var rt = (RectTransform)clone.transform;
            var src = (RectTransform)hpRoot;
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -(src.sizeDelta.y + 8f));

            _bar = clone.GetComponentInChildren<Scrollbar>(true);
            _handle = _bar != null && _bar.handleRect != null ? _bar.handleRect.GetComponent<Image>() : null;

            Text label = clone.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "SP";

            _group = clone.GetComponent<CanvasGroup>();
            if (_group == null) _group = clone.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
    }
}
