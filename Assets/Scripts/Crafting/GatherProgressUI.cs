using UnityEngine;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// E키를 꾹 눌러 채집할 때 화면 중앙 아래쪽에 원형(Radial) 진행률을 표시한다.
    /// LastTruck.PlayerInteract의 IsHolding/HoldProgress01 값을 매 프레임 읽어 반영한다.
    /// </summary>
    public class GatherProgressUI : MonoBehaviour
    {
        private LastTruck.PlayerInteract _playerInteract;
        private EatController _eat;
        private TrapController _trap;
        private Image _fillImage;
        private GameObject _root;
        private GameObject _labelRoot;
        private Text _nameText;
        private Text _subText;

        private static Sprite _cachedCircleSprite;

        [Header("에디터에서 미리 그려둔 UI (비워두면 코드로 생성)")]
        [SerializeField] private GameObject authoredGaugeRoot;
        [SerializeField] private Image authoredFill;
        [SerializeField] private GameObject authoredLabelRoot;
        [SerializeField] private Text authoredNameText;
        [SerializeField] private Text authoredSubText;

public void Initialize(LastTruck.PlayerInteract playerInteract, EatController eat = null, TrapController trap = null)
        {
            _playerInteract = playerInteract;
            _eat = eat;
            _trap = trap;
            if (_root == null)
            {
                if (authoredGaugeRoot != null && authoredFill != null && authoredLabelRoot != null && authoredNameText != null && authoredSubText != null)
                {
                    _root = authoredGaugeRoot;
                    _fillImage = authoredFill;
                    _labelRoot = authoredLabelRoot;
                    _nameText = authoredNameText;
                    _subText = authoredSubText;
                    _fillImage.fillAmount = 0f;
                    _root.SetActive(false);
                    _labelRoot.SetActive(false);
                }
                else
                {
                    BuildUI();
                }
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("GatherProgressCanvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGO.AddComponent<CanvasScaler>();

            _root = new GameObject("GatherProgressRoot");
            _root.transform.SetParent(canvasGO.transform, false);
            RectTransform rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, -90f);
            rootRect.sizeDelta = new Vector2(56f, 56f);

            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(_root.transform, false);
            RectTransform bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgGO.AddComponent<Image>();
            bgImage.sprite = GetCircleSprite();
            bgImage.color = new Color(0f, 0f, 0f, 0.45f);

            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(_root.transform, false);
            RectTransform fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);
            _fillImage = fillGO.AddComponent<Image>();
            _fillImage.sprite = GetCircleSprite();
            _fillImage.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Radial360;
            _fillImage.fillOrigin = (int)Image.Origin360.Top;
            _fillImage.fillClockwise = true;
            _fillImage.fillAmount = 0f;

            BuildLabel(canvasGO.transform);
            _root.SetActive(false);
        }

        // 채집 중인 대상의 이름(예: "구리 광맥")과 요구 조건을 게이지 아래에 표시한다.
        private void BuildLabel(Transform canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _labelRoot = new GameObject("GatherLabel", typeof(RectTransform), typeof(Image));
            _labelRoot.transform.SetParent(canvas, false);
            var rt = (RectTransform)_labelRoot.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(380f, 64f);
            _labelRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            _nameText = MakeText("Name", _labelRoot.transform, font, 24, FontStyle.Bold, new Vector2(0f, 0.45f), new Vector2(1f, 1f));
            _subText = MakeText("Sub", _labelRoot.transform, font, 15, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(1f, 0.48f));
            _subText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            _labelRoot.SetActive(false);
        }

        private static Text MakeText(string name, Transform parent, Font font, int size, FontStyle style, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            return t;
        }

private void Update()
        {
            if (_root == null)
                return;

            bool gathering = _playerInteract != null && _playerInteract.IsHolding;
            bool eating = _eat != null && _eat.IsHolding;
            bool trapping = _trap != null && _trap.IsHolding;
            bool holding = gathering || eating || trapping;

            if (_root.activeSelf != holding)
                _root.SetActive(holding);

            UpdateLabel();

            if (holding)
            {
                float progress = gathering ? _playerInteract.HoldProgress01
                    : eating ? _eat.HoldProgress01
                    : _trap.HoldProgress01;
                _fillImage.fillAmount = progress;
            }
        }

        [Header("채집 라벨 위치")]
        [Tooltip("플레이어가 대상(프리팹)에서 이 거리(m) 안으로 들어오면 이름 라벨을 띄운다.")]
        [SerializeField] private float labelShowDistance = 1.3f;
        [SerializeField] private float labelHideExtra = 0.5f;
        [Tooltip("대상이 이 키(m)보다 크면 꼭대기가 아니라 줄기 높이(treeLabelHeight)에 라벨을 단다.")]
        [SerializeField] private float tallObjectHeight = 4.5f;
        [SerializeField] private float treeLabelHeight = 3.2f;
        [SerializeField] private float labelWorldLift = 0.6f;

        private Transform _labelTarget;
        private Bounds _labelBounds;      // 렌더러 전체 bounds
        private Bounds _labelCore;        // 거리 판정용(키 큰 대상은 가지/잎 말고 밑동 쪽만)
        private bool _labelTall;
        private bool _labelNear;
        private Vector2 _labelPos;
        private bool _labelPosValid;

        private void CacheLabelTarget(Transform t)
        {
            _labelTarget = t;
            var rs = t.GetComponentsInChildren<Renderer>();
            bool any = false; _labelBounds = new Bounds(t.position, Vector3.zero);
            foreach (var r in rs) { if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue; if (!any) { _labelBounds = r.bounds; any = true; } else _labelBounds.Encapsulate(r.bounds); }
            _labelTall = _labelBounds.size.y > tallObjectHeight;
            _labelCore = _labelBounds;
            if (_labelTall) _labelCore.size = new Vector3(_labelBounds.size.x * 0.35f, _labelBounds.size.y, _labelBounds.size.z * 0.35f);
            _labelNear = false;
        }

        // 플레이어와 대상(프리팹) 사이의 수평 거리. 대상 안쪽이면 0.
        private float DistanceToTarget()
        {
            Vector3 pp = _playerInteract.transform.position;
            Vector3 d = pp - _labelCore.center; Vector3 e = _labelCore.extents;
            float dx = Mathf.Max(0f, Mathf.Abs(d.x) - e.x), dz = Mathf.Max(0f, Mathf.Abs(d.z) - e.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // 라벨은 항상 채집 대상 프리팹에 붙는다: 작은 대상은 머리 위, 키 큰 대상(나무)은 밑동에서 treeLabelHeight 높이.
        private Vector3 LabelAnchor()
        {
            if (_labelTall) return new Vector3(_labelCore.center.x, _labelBounds.min.y + treeLabelHeight, _labelCore.center.z);
            return new Vector3(_labelBounds.center.x, _labelBounds.max.y + labelWorldLift, _labelBounds.center.z);
        }

        private void UpdateLabel()
        {
            if (_labelRoot == null) return;
            var comp = _playerInteract != null ? _playerInteract.CurrentInteractable as Component : null;
            var label = comp as LastTruck.IInteractLabel;
            // 채집 대상이 방금 파괴됐으면(C# 참조만 남은 상태) 접근하지 않는다.
            bool show = comp != null && label != null && !string.IsNullOrEmpty(label.InteractLabel);
            Camera cam = Camera.main;
            Vector3 sp = Vector3.zero;
            if (show)
            {
                Transform t = comp.transform;
                if (t.name == "GatherTrigger" && t.parent != null) t = t.parent;      // 일반 돌/나무: 프롭 루트 기준
                if (t != _labelTarget) CacheLabelTarget(t);

                // 대상에 충분히 가까이 가야 이름이 뜬다(한번 뜬 뒤에는 조금 멀어져도 유지해서 깜빡이지 않게).
                float dist = DistanceToTarget();
                _labelNear = _labelNear ? dist <= labelShowDistance + labelHideExtra : dist <= labelShowDistance;
                show = _labelNear && cam != null;
                if (show)
                {
                    sp = cam.WorldToScreenPoint(LabelAnchor());
                    if (sp.z <= 0.1f) show = false;
                }
            }
            if (_labelRoot.activeSelf != show) _labelRoot.SetActive(show);
            if (!show) { if (comp == null) _labelTarget = null; _labelPosValid = false; return; }

            _nameText.text = label.InteractLabel;
            _nameText.color = label.InteractLabelColor;
            _subText.text = label.InteractSubLabel;

            var rt = (RectTransform)_labelRoot.transform;
            // 이름만 표시(부가 설명이 없으면 작은 한 줄 라벨)
            bool hasSub = !string.IsNullOrEmpty(label.InteractSubLabel);
            _subText.gameObject.SetActive(hasSub);
            ((RectTransform)_nameText.transform).anchorMin = new Vector2(0f, hasSub ? 0.45f : 0f);
            rt.sizeDelta = new Vector2(Mathf.Max(110f, _nameText.preferredWidth + 40f), hasSub ? 64f : 42f);
            Vector2 target = new Vector2(sp.x, sp.y + rt.sizeDelta.y * 0.5f + 8f);
            target.x = Mathf.Clamp(target.x, rt.sizeDelta.x * 0.5f + 8f, Screen.width - rt.sizeDelta.x * 0.5f - 8f);
            target.y = Mathf.Clamp(target.y, rt.sizeDelta.y * 0.5f + 70f, Screen.height - rt.sizeDelta.y * 0.5f - 8f);
            _labelPos = _labelPosValid ? Vector2.Lerp(_labelPos, target, 1f - Mathf.Exp(-18f * Time.deltaTime)) : target;
            _labelPosValid = true;
            rt.position = new Vector3(_labelPos.x, _labelPos.y, 0f);

            // 원형 게이지는 라벨 바로 아래에 붙인다.
            if (_root != null && _root.activeSelf) ((RectTransform)_root.transform).position = new Vector3(_labelPos.x, _labelPos.y - rt.sizeDelta.y * 0.5f - 34f, 0f);
        }

        private static Sprite GetCircleSprite()
        {
            if (_cachedCircleSprite != null)
                return _cachedCircleSprite;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedCircleSprite;
        }
    }
}
