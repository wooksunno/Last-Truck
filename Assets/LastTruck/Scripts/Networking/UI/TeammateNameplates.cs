using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 동료 머리 위 이름표 + HP바 (내 캐릭터는 표시하지 않는다).
    ///  - 화면 위 캔버스에 그리고 매 프레임 캐릭터 머리 위치로 옮긴다 → 거리와 상관없이 글자가 또렷하다.
    ///  - 트럭에 탄 동료, 죽은 동료, 카메라 뒤에 있는 동료는 숨긴다.
    ///  - HP는 NetworkHealth(호스트가 계산한 체력)를 그대로 쓴다.
    /// </summary>
    public class TeammateNameplates : MonoBehaviour
    {
        #region 설정

        [SerializeField] private float headHeight = 2.3f;
        [SerializeField] private float maxDistance = 60f;
        [SerializeField] private Vector2 plateSize = new Vector2(140f, 34f);

        private static readonly Color BarBackground = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color BarHigh = new Color(0.35f, 0.85f, 0.4f);
        private static readonly Color BarLow = new Color(0.9f, 0.3f, 0.25f);

        #endregion

        #region 상태

        private sealed class Plate
        {
            public RectTransform Root;
            public TextMeshProUGUI Name;
            public Image Fill;
            public string ShownName;
        }

        private readonly Dictionary<NetworkPlayer, Plate> _plates = new Dictionary<NetworkPlayer, Plate>();
        private readonly List<NetworkPlayer> _removeBuffer = new List<NetworkPlayer>();
        private Canvas _canvas;

        #endregion

        #region 갱신

        private void Awake()
        {
            _canvas = InGameHud.CreateOverlayCanvas(transform, "NameplateCanvas", 30);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            float scale = Mathf.Max(0.5f, Screen.height / 1080f);

            foreach (NetworkPlayer player in NetworkPlayer.All)
            {
                if (player == null || player.IsLocal) continue;
                if (!_plates.TryGetValue(player, out Plate plate))
                {
                    plate = CreatePlate();
                    _plates[player] = plate;
                }
                UpdatePlate(player, plate, cam, scale);
            }

            // 사라진 캐릭터의 이름표 정리
            _removeBuffer.Clear();
            foreach (KeyValuePair<NetworkPlayer, Plate> pair in _plates)
            {
                if (pair.Key == null || pair.Key.Object == null || !pair.Key.Object.IsValid) _removeBuffer.Add(pair.Key);
            }
            foreach (NetworkPlayer player in _removeBuffer)
            {
                if (_plates[player].Root != null) Destroy(_plates[player].Root.gameObject);
                _plates.Remove(player);
            }
        }

        private void UpdatePlate(NetworkPlayer player, Plate plate, Camera cam, float scale)
        {
            bool visible = cam != null && player.IsAlive && !player.IsSeated;
            Vector3 screen = Vector3.zero;
            if (visible)
            {
                Vector3 head = player.transform.position + Vector3.up * headHeight;
                screen = cam.WorldToScreenPoint(head);
                visible = screen.z > 0f && screen.z < maxDistance;
            }

            if (plate.Root.gameObject.activeSelf != visible) plate.Root.gameObject.SetActive(visible);
            if (!visible) return;

            plate.Root.position = new Vector3(screen.x, screen.y, 0f);
            plate.Root.localScale = Vector3.one * scale;

            string nickname = player.Nickname.ToString();
            if (plate.ShownName != nickname)
            {
                plate.ShownName = nickname;
                plate.Name.text = nickname;
            }

            NetworkHealth health = player.Health;
            float ratio = health != null && health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 1f;
            plate.Fill.fillAmount = ratio;
            plate.Fill.color = Color.Lerp(BarLow, BarHigh, ratio);
        }

        #endregion

        #region UI 만들기

        private Plate CreatePlate()
        {
            var rootGo = new GameObject("Nameplate", typeof(RectTransform));
            rootGo.transform.SetParent(_canvas.transform, false);
            var root = rootGo.GetComponent<RectTransform>();
            root.sizeDelta = plateSize;
            root.pivot = new Vector2(0.5f, 0f);

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(root, false);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.4f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
            if (registry != null && registry.tmpFont != null) nameText.font = registry.tmpFont;
            nameText.fontSize = 18f;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            nameText.outlineWidth = 0.2f;
            nameText.outlineColor = new Color32(0, 0, 0, 200);
            nameText.raycastTarget = false;

            var barBack = new GameObject("HpBack", typeof(RectTransform), typeof(Image));
            barBack.transform.SetParent(root, false);
            var backRect = barBack.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0.1f, 0f);
            backRect.anchorMax = new Vector2(0.9f, 0.3f);
            backRect.offsetMin = Vector2.zero;
            backRect.offsetMax = Vector2.zero;
            Image backImage = barBack.GetComponent<Image>();
            backImage.color = BarBackground;
            backImage.raycastTarget = false;

            var fillGo = new GameObject("HpFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(backRect, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            Image fill = fillGo.GetComponent<Image>();
            fill.sprite = InGameHud.CircleSprite; // fillAmount를 쓰려면 스프라이트가 필요하다 (모양은 가로로 늘어남)
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.raycastTarget = false;

            return new Plate { Root = root, Name = nameText, Fill = fill };
        }

        #endregion
    }
}
