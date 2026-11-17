using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 떠오르며 사라지는 데미지 숫자. 애니메이션과 파괴를 팝업 오브젝트 스스로 처리하므로
    /// 피격 대상이 먼저 파괴되어도 숫자가 화면에 남지 않는다.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class DamagePopup : MonoBehaviour
    {
        private const float Duration = 0.8f;

        private TextMesh _textMesh;
        private TextMesh[] _texts;      // 본 글자 + (있으면) 그림자 글자
        private float[] _baseAlpha;
        private Vector3 _startPosition;
        private float _elapsed;

        public static void Spawn(Vector3 worldPos, int amount)
        {
            var go = new GameObject("DamagePopup");
            go.transform.position = worldPos + Vector3.up * 0.3f;

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = amount.ToString();
            tm.characterSize = 0.15f;
            tm.fontSize = 64;
            tm.color = Color.red;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            go.AddComponent<DamagePopup>();
        }

        /// <summary>
        /// 숫자 대신 글자를 띄운다 (고유 능력 발동 표시: "강화!", "기절!" 등). 데미지 숫자와 겹치지 않게 조금 위에 띄운다.
        /// </summary>
        public static void SpawnText(Vector3 worldPos, string text, Color color, float sizeScale = 1f)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var go = new GameObject("AbilityPopup");
            go.transform.position = worldPos + Vector3.up * 0.7f;

            TextMesh tm = go.AddComponent<TextMesh>();
            // 한글이 선명하게 나오도록 게임 UI 폰트를 쓴다 (없으면 기본 폰트)
            Font font = LastTruck.Networking.InGameFonts.LegacyFont;
            if (font != null)
            {
                tm.font = font;
                MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.sharedMaterial = font.material;
            }
            tm.text = text;
            tm.characterSize = 0.12f * Mathf.Max(0.1f, sizeScale);
            tm.fontSize = 64;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            // 밝은 배경에서도 읽히도록 뒤에 검은 그림자 글자를 하나 깐다
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.transform.localPosition = new Vector3(0.025f, -0.025f, 0.01f) * Mathf.Max(0.1f, sizeScale);
            TextMesh shadow = shadowGo.AddComponent<TextMesh>();
            shadow.font = tm.font;
            MeshRenderer shadowRenderer = shadowGo.GetComponent<MeshRenderer>();
            MeshRenderer mainRenderer = go.GetComponent<MeshRenderer>();
            if (shadowRenderer != null && mainRenderer != null) shadowRenderer.sharedMaterial = mainRenderer.sharedMaterial;
            shadow.text = text;
            shadow.characterSize = tm.characterSize;
            shadow.fontSize = tm.fontSize;
            shadow.color = new Color(0f, 0f, 0f, 0.85f);
            shadow.anchor = tm.anchor;
            shadow.alignment = tm.alignment;

            go.AddComponent<DamagePopup>();
        }

        private void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
            _texts = null;
            _startPosition = transform.position;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / Duration;

            transform.position = _startPosition + Vector3.up * t;
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            if (_texts == null)
            {
                // 그림자는 SpawnText에서 Awake 뒤에 붙으므로 첫 Update에서 모은다
                _texts = GetComponentsInChildren<TextMesh>();
                _baseAlpha = new float[_texts.Length];
                for (int i = 0; i < _texts.Length; i++) _baseAlpha[i] = _texts[i].color.a;
            }
            for (int i = 0; i < _texts.Length; i++)
            {
                if (_texts[i] == null) continue;
                Color c = _texts[i].color;
                c.a = _baseAlpha[i] * (1f - t);
                _texts[i].color = c;
            }

            if (_elapsed >= Duration)
                Destroy(gameObject);
        }
    }
}
