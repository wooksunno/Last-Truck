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

        private void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
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

            Color c = _textMesh.color;
            c.a = 1f - t;
            _textMesh.color = c;

            if (_elapsed >= Duration)
                Destroy(gameObject);
        }
    }
}
