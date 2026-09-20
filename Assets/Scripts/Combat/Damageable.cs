using System.Collections;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 피격 가능한 대상(허수아비 등). 데미지를 받으면 잠깐 빨갛게 변하고 데미지 숫자를 띄운다.
    /// 체력이 0 이하로 떨어지면 다시 가득 채워 반복 연습이 가능하게 한다.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float hitFlashDuration = 0.15f;

        private int _currentHealth;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private Coroutine _flashRoutine;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalColors[i] = GetColor(_renderers[i]);
            }
        }

        public void TakeDamage(int amount, Vector3 hitPoint)
        {
            if (amount <= 0)
                return;

            _currentHealth -= amount;
            SpawnDamagePopup(hitPoint, amount);

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRed());

            if (_currentHealth <= 0)
            {
                Debug.Log($"[Damageable] {name} 체력 소진 - 초기화");
                _currentHealth = maxHealth;
            }
        }

        private IEnumerator FlashRed()
        {
            for (int i = 0; i < _renderers.Length; i++)
                SetColor(_renderers[i], Color.red);

            yield return new WaitForSeconds(hitFlashDuration);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    SetColor(_renderers[i], _originalColors[i]);
            }

            _flashRoutine = null;
        }

        private static Color GetColor(Renderer renderer)
        {
            Material mat = renderer.material;
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color"))
                return mat.GetColor("_Color");
            return Color.white;
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            Material mat = renderer.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }

        private void SpawnDamagePopup(Vector3 worldPos, int amount)
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

            StartCoroutine(AnimatePopup(go, tm));
        }

        private static IEnumerator AnimatePopup(GameObject go, TextMesh tm)
        {
            const float duration = 0.8f;
            float t = 0f;
            Vector3 start = go.transform.position;
            Camera cam = Camera.main;

            while (t < duration)
            {
                t += Time.deltaTime;
                go.transform.position = start + Vector3.up * (t / duration);
                if (cam != null)
                    go.transform.rotation = cam.transform.rotation;

                Color c = tm.color;
                c.a = 1f - (t / duration);
                tm.color = c;
                yield return null;
            }

            Object.Destroy(go);
        }
    }
}
