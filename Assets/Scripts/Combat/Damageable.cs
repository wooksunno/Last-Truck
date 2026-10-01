using System.Collections;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 피격 가능한 대상(허수아비, 몬스터 등). 데미지를 받으면 잠깐 빨갛게 변하고 데미지 숫자를 띄운다.
    ///
    /// 멀티플레이: 같은 오브젝트에 NetworkHealth가 있으면 체력은 호스트가 관리한다.
    ///  - TakeDamage는 호스트에게 피해를 "요청"만 하고,
    ///  - 빨간색 깜빡임/데미지 숫자는 호스트가 적용한 뒤 모든 컴퓨터에서 똑같이 나온다 (OnNetworkHit).
    ///  - 체력 0이면 호스트가 오브젝트를 없앤다 (Destroy 대신 네트워크 Despawn).
    /// </summary>
    public class Damageable : MonoBehaviour, LastTruck.Networking.INetworkHealthListener
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float hitFlashDuration = 0.15f;

        private int _currentHealth;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private Coroutine _flashRoutine;

        private LastTruck.Networking.NetworkHealth _networkHealth;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;

        /// <summary>멀티플레이에서 체력을 호스트가 관리하는 오브젝트인가.</summary>
        private bool IsNetworked => _networkHealth != null && _networkHealth.Object != null && _networkHealth.Object.IsValid;

        #region 생명주기

        private void Awake()
        {
            _currentHealth = maxHealth;
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalColors[i] = GetColor(_renderers[i]);
            }
            _networkHealth = GetComponent<LastTruck.Networking.NetworkHealth>();
        }

        #endregion

        #region 피해

        public void TakeDamage(int amount, Vector3 hitPoint)
        {
            if (amount <= 0)
                return;

            // 멀티플레이: 호스트에게 요청만 한다. 연출은 호스트가 적용한 뒤 OnNetworkHit으로 모두에게 나온다.
            if (IsNetworked)
            {
                _networkHealth.RequestDamage(amount, hitPoint, true);
                return;
            }

            _currentHealth -= amount;
            PlayHitEffect(hitPoint, amount);

            if (_currentHealth <= 0)
            {
                // Debug.Log($"[Damageable] {name} 체력 소진 - 초기화");
                // _currentHealth = maxHealth;
                Destroy(gameObject);
            }
        }

        #endregion

        #region 피격 연출 (빨간색 깜빡임 + 데미지 숫자)

        private void PlayHitEffect(Vector3 hitPoint, int amount)
        {
            SpawnDamagePopup(hitPoint, amount);

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            if (isActiveAndEnabled)
                _flashRoutine = StartCoroutine(FlashRed());
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

        #endregion

        #region 멀티플레이 (NetworkHealth가 호출)

        float LastTruck.Networking.INetworkHealthListener.NetworkMaxHealth => maxHealth;

        void LastTruck.Networking.INetworkHealthListener.OnNetworkHealthChanged(float current, float max)
        {
            _currentHealth = Mathf.CeilToInt(current);
        }

        void LastTruck.Networking.INetworkHealthListener.OnNetworkHit(float amount, Vector3 hitPoint)
        {
            PlayHitEffect(hitPoint, Mathf.RoundToInt(amount));
        }

        void LastTruck.Networking.INetworkHealthListener.OnNetworkDeath()
        {
            // 호스트가 곧 오브젝트를 없앤다 (NetworkHealth). 여기서는 따로 할 일 없음.
        }

        #endregion
    }
}
