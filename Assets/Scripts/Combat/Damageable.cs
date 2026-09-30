using LastTruck;
using System.Collections;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 피격 가능한 대상(허수아비 등). 데미지를 받으면 잠깐 빨갛게 변하고 데미지 숫자를 띄운다.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float hitFlashDuration = 0.15f;

        private int _currentHealth;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private Coroutine _flashRoutine;
        private Rigidbody _rigidbody;
        private Coroutine _rootRoutine;
        private float _rootUntilTime;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;

        /// <summary>덫 등으로 이동이 묶인 상태인지. 이동/AI 스크립트는 이 값을 보고 움직임을 멈춰야 한다.</summary>
        public bool IsRooted => Time.time < _rootUntilTime;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalColors[i] = GetColor(_renderers[i]);
            }
            _rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>지정한 시간(초) 동안 이동을 묶는다. 겹쳐 걸리면 더 긴 쪽으로 갱신된다.</summary>
        public void Root(float duration)
        {
            if (duration <= 0f)
                return;

            _rootUntilTime = Mathf.Max(_rootUntilTime, Time.time + duration);
            if (_rootRoutine == null)
                _rootRoutine = StartCoroutine(RootRoutine());
        }

        private IEnumerator RootRoutine()
        {
            while (Time.time < _rootUntilTime)
            {
                if (_rigidbody != null && !_rigidbody.isKinematic)
                    _rigidbody.linearVelocity = Vector3.zero;
                yield return null;
            }

            _rootRoutine = null;
        }

        //public void TakeDamage(int amount, Vector3 hitPoint)
        //{
        //    if (amount <= 0)
        //        return;

        //    _currentHealth -= amount;
        //    SpawnDamagePopup(hitPoint, amount);

        //    if (_flashRoutine != null)
        //        StopCoroutine(_flashRoutine);
        //    _flashRoutine = StartCoroutine(FlashRed());

        //    if (_currentHealth <= 0)
        //    {
        //        // Debug.Log($"[Damageable] {name} 체력 소진 - 초기화");
        //        // _currentHealth = maxHealth;
        //        Destroy(gameObject);
        //    }
        //}

        public void TakeDamage(int amount, Vector3 hitPoint)
        {
            if (amount <= 0)
                return;

            int finalDamage = amount;

            var abilityController = FindObjectOfType<CharacterAbilityController>();
            if (abilityController != null)
            {
                finalDamage = abilityController.GetCalculatedDamage(amount, out bool isEnhanced);
            }

            _currentHealth -= finalDamage;
            SpawnDamagePopup(hitPoint, finalDamage);

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRed());

            if (_currentHealth <= 0)
            {
                Destroy(gameObject);
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
            // 팝업의 애니메이션/파괴는 DamagePopup이 스스로 처리한다.
            // (여기서 코루틴을 돌리면 이 오브젝트가 파괴될 때 코루틴도 멈춰 숫자가 남는다)
            DamagePopup.Spawn(worldPos, amount);
        }
    }
}
