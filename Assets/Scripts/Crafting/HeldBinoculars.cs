using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 쌍안경을 1/2/3 키로 손에 들고 마우스 오른쪽 버튼을 누르고 있으면 멀리 본다.
    ///  - 시야각을 좁히고(약 3.5배) 카메라를 캐릭터 쪽으로 당긴다 (LastTruck.CameraEffects).
    ///  - 화면 가장자리를 쌍안경 모양으로 가린다.
    /// PlayerInventory가 자동으로 붙인다. 내 캐릭터(입력을 받는 쪽)에서만 동작한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class HeldBinoculars : MonoBehaviour
    {
        private static Texture2D _mask;

        private PlayerInventory _inventory;
        private Combat.WeaponController _weapon;
        private bool _zoom;
        private float _overlay;   // 0..1 가림막 진하기

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _weapon = GetComponent<Combat.WeaponController>();
        }

        private void OnDisable() => SetZoom(false);

        private bool IsLocal
        {
            get
            {
                if (_weapon != null && _weapon.IsRemoteView) return false;
                if (!LastTruck.Networking.GameLauncher.IsOnlineSession) return true;
                var local = LastTruck.Networking.NetworkPlayer.Local;
                return local != null && local.gameObject == gameObject;
            }
        }

        private void Update()
        {
            ItemData hand = _inventory != null ? _inventory.SelectedItem : null;
            bool holding = hand != null && hand.itemID == ItemIds.Binoculars;
            bool popup = GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;
            bool want = holding && IsLocal && !popup && Input.GetMouseButton(1);
            SetZoom(want);
            _overlay = Mathf.MoveTowards(_overlay, want ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        }

        private void SetZoom(bool on)
        {
            if (_zoom == on) return;
            _zoom = on;
            LastTruck.CameraEffects.SetZooming(on);
        }

        private void OnGUI()
        {
            if (_overlay <= 0.01f || Event.current.type != EventType.Repaint) return;
            if (_mask == null) _mask = BuildMask();

            // 쌍안경 두 원 모양 가림막 (화면 높이에 맞추고, 남는 좌우는 검게)
            float h = Screen.height, w = h * 2f;
            float x = (Screen.width - w) * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _overlay);
            GUI.DrawTexture(new Rect(x, 0f, w, h), _mask);
            if (x > 0f)
            {
                GUI.DrawTexture(new Rect(0f, 0f, x + 1f, h), Texture2D.blackTexture);
                GUI.DrawTexture(new Rect(x + w - 1f, 0f, x + 1f, h), Texture2D.blackTexture);
            }
            GUI.color = prev;
        }

        /// <summary>가로 2:1 텍스처에 겹친 두 원만 투명하고 나머지는 검정 (가장자리는 부드럽게).</summary>
        private static Texture2D BuildMask()
        {
            const int W = 512, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            Vector2 c1 = new Vector2(W * 0.36f, H * 0.5f), c2 = new Vector2(W * 0.64f, H * 0.5f);
            float r = H * 0.47f, soft = H * 0.05f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float d = Mathf.Min(Vector2.Distance(p, c1), Vector2.Distance(p, c2));
                    float a = Mathf.Clamp01((d - (r - soft)) / soft);
                    px[y * W + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
