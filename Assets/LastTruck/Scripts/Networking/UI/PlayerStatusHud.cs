using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 내 캐릭터 상태 화면 (싱글/멀티 공용, 처음 필요할 때 자동 생성 → 씬이 바뀌면 같이 사라짐).
    ///  - 피격: 화면 가장자리가 붉게 번쩍 (HP가 낮으면 약하게 계속 남음) + 카메라 살짝 흔들림
    ///  - 기절: "기절했습니다" 안내 + 동료가 살리는 진행도, (설정 시) 화면 흑백
    ///  - 동료 살리기: 기절한 동료 근처에서 "E 길게: ○○ 살리기" + 진행도
    /// </summary>
    public class PlayerStatusHud : MonoBehaviour
    {
        #region 설정

        private const float FlashPeak = 0.65f;
        private const float FlashFadeSpeed = 1.6f;
        private const float LowHealthRatio = 0.3f;
        private const float LowHealthAlpha = 0.28f;
        private const float HitShake = 0.08f;

        private static readonly Color VignetteColor = new Color(0.85f, 0.05f, 0.05f, 1f);

        #endregion

        #region 상태

        private static PlayerStatusHud _instance;

        private Image _vignette;
        private float _flash;

        private GameObject _downedPanel;
        private Text _downedText;
        private Image _downedBarFill;

        private GameObject _revivePanel;
        private Text _reviveText;
        private Image _reviveBarFill;

        private Volume _grayVolume;

        #endregion

        #region 외부에서 부르는 함수

        /// <summary>HUD가 없으면 만든다.</summary>
        public static PlayerStatusHud Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("PlayerStatusHud");
            _instance = go.AddComponent<PlayerStatusHud>();
            return _instance;
        }

        /// <summary>내 캐릭터가 맞았을 때: 가장자리 붉게 + 카메라 흔들림.</summary>
        public static void PlayLocalHit()
        {
            PlayerStatusHud hud = Ensure();
            hud._flash = FlashPeak;
            Combat.CameraShake.Shake(HitShake);
        }

        /// <summary>캐릭터 머리 위에 받은 피해 숫자 (모든 화면).</summary>
        public static void ShowDamageNumber(Transform character, float amount)
        {
            if (character == null || amount <= 0f) return;
            Combat.DamagePopup.Spawn(character.position + Vector3.up * 2.2f, Mathf.Max(1, Mathf.RoundToInt(amount)));
        }

        #endregion

        #region 생명주기

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            BuildUI();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            PlayerStats stats = FindLocalStats(out NetworkPlayer networkPlayer);
            bool downed = IsLocalDowned(stats, networkPlayer);
            float ratio = stats != null && stats.MaxHealth > 0f ? Mathf.Clamp01(stats.currentHealth / stats.MaxHealth) : 1f;

            UpdateVignette(downed, ratio);
            UpdateDownedPanel(downed, networkPlayer);
            UpdateRevivePanel(downed);
            UpdateGrayscale(downed && (networkPlayer == null || networkPlayer.GrayscaleWhenDowned));
        }

        #endregion

        #region 갱신

        private void UpdateVignette(bool downed, float ratio)
        {
            _flash = Mathf.MoveTowards(_flash, 0f, FlashFadeSpeed * Time.deltaTime);
            float lowHealth = !downed && ratio < LowHealthRatio ? LowHealthAlpha * (1f - ratio / LowHealthRatio) : 0f;
            float alpha = Mathf.Max(_flash, lowHealth);
            Color c = VignetteColor;
            c.a = alpha;
            _vignette.color = c;
            _vignette.enabled = alpha > 0.001f;
        }

        private void UpdateDownedPanel(bool downed, NetworkPlayer networkPlayer)
        {
            if (_downedPanel.activeSelf != downed) _downedPanel.SetActive(downed);
            if (!downed) return;

            if (networkPlayer == null)
            {
                _downedText.text = "기절했습니다\n<size=22>살려줄 동료가 없어 게임 오버입니다\n[R] 다시 시작</size>";
                _downedBarFill.transform.parent.gameObject.SetActive(false);
                // 싱글플레이 게임 오버: R로 지금 씬을 다시 시작한다
                if (Input.GetKeyDown(KeyCode.R))
                {
                    UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
                }
                return;
            }

            float progress = networkPlayer.ReviveProgress01;
            bool beingRevived = networkPlayer.Reviver != Fusion.PlayerRef.None;
            if (beingRevived)
            {
                NetworkPlayer reviver = NetworkPlayer.Find(networkPlayer.Reviver);
                string name = reviver != null ? reviver.Nickname.ToString() : "동료";
                _downedText.text = $"기절했습니다\n<size=22>{name}이(가) 살리는 중...</size>";
            }
            else
            {
                _downedText.text = "기절했습니다\n<size=22>동료를 기다리는 중...</size>";
            }
            _downedBarFill.transform.parent.gameObject.SetActive(beingRevived);
            _downedBarFill.fillAmount = progress;
        }

        private void UpdateRevivePanel(bool downed)
        {
            PlayerReviver reviver = PlayerReviver.Local;
            NetworkPlayer target = !downed && reviver != null ? (reviver.Reviving != null ? reviver.Reviving : reviver.NearbyDowned) : null;
            bool show = target != null;
            if (_revivePanel.activeSelf != show) _revivePanel.SetActive(show);
            if (!show) return;

            string name = target.Nickname.ToString();
            bool reviving = reviver.Reviving == target;
            _reviveText.text = reviving ? $"{name} 살리는 중..." : $"[E] 길게 눌러 {name} 살리기";
            _reviveBarFill.transform.parent.gameObject.SetActive(reviving);
            _reviveBarFill.fillAmount = reviving ? target.ReviveProgress01 : 0f;
        }

        private void UpdateGrayscale(bool on)
        {
            if (_grayVolume == null)
            {
                if (!on) return;
                CreateGrayVolume();
            }
            _grayVolume.weight = Mathf.MoveTowards(_grayVolume.weight, on ? 1f : 0f, 2f * Time.deltaTime);
            _grayVolume.enabled = _grayVolume.weight > 0.001f;
        }

        #endregion

        #region 도우미

        private static PlayerStats FindLocalStats(out NetworkPlayer networkPlayer)
        {
            networkPlayer = null;
            if (GameLauncher.IsOnlineSession)
            {
                networkPlayer = NetworkPlayer.Local;
                return networkPlayer != null ? networkPlayer.GetComponent<PlayerStats>() : null;
            }
            return PlayerStats.OfflineLocal;
        }

        private static bool IsLocalDowned(PlayerStats stats, NetworkPlayer networkPlayer)
        {
            if (networkPlayer != null) return networkPlayer.IsDowned;
            return stats != null && stats.IsDownedOffline;
        }

        private void CreateGrayVolume()
        {
            var go = new GameObject("DownedGrayscaleVolume");
            go.transform.SetParent(transform, false);
            _grayVolume = go.AddComponent<Volume>();
            _grayVolume.isGlobal = true;
            _grayVolume.priority = 1000f;
            _grayVolume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-100f);
            _grayVolume.sharedProfile = profile;

            // 카메라 후처리가 꺼져 있으면 흑백이 보이지 않으므로 켠다.
            Camera cam = Camera.main;
            if (cam != null)
            {
                UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        #endregion

        #region UI 만들기

        private void BuildUI()
        {
            Canvas canvas = InGameHud.CreateOverlayCanvas(transform, "PlayerStatusCanvas", 40);
            canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

            // 가장자리 붉은 효과
            _vignette = CreateImage(canvas.transform, "HitVignette", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vignette.sprite = CreateVignetteSprite();
            _vignette.enabled = false;

            // 기절 안내 (화면 가운데 아래)
            _downedPanel = CreatePanel(canvas.transform, "DownedPanel", new Vector2(0f, -120f), new Vector2(620f, 150f),
                out _downedText, out _downedBarFill, 34);
            _downedPanel.SetActive(false);

            // 동료 살리기 안내 (화면 아래)
            _revivePanel = CreatePanel(canvas.transform, "RevivePanel", new Vector2(0f, -330f), new Vector2(520f, 90f),
                out _reviveText, out _reviveBarFill, 24);
            _revivePanel.SetActive(false);
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size,
            out Text text, out Image barFill, int fontSize)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(root.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.3f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            text = textGo.GetComponent<Text>();
            text.font = InGameFonts.LegacyFont;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            textGo.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            Image back = CreateImage(root.transform, "BarBack", new Vector2(0.1f, 0f), new Vector2(0.9f, 0.18f), Vector2.zero, Vector2.zero);
            back.color = new Color(0f, 0f, 0f, 0.6f);
            barFill = CreateImage(back.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            barFill.color = new Color(0.35f, 0.85f, 0.4f);
            barFill.sprite = WhiteSprite; // Filled 이미지는 스프라이트가 있어야 채워진다
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillAmount = 0f;
            return root;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Sprite _whiteSprite;

        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null) return _whiteSprite;
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply();
                _whiteSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                return _whiteSprite;
            }
        }

        /// <summary>가운데는 투명, 가장자리로 갈수록 진해지는 텍스처 (흰색, 색은 Image.color로).</summary>
        private static Sprite CreateVignetteSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v) / 1.41421f; // 0(가운데) ~ 1(모서리)
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        #endregion
    }
}
