using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 총에서 튀어나온 탄피. 물리로 떨어져 바닥에 닿으면 잠시 굴러다니다가 줄어들며 사라진다.
    /// (공중에서 오래 머무는 경우를 대비해 최대 수명도 둔다)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShellCasing : MonoBehaviour
    {
        [Tooltip("바닥에 처음 닿은 뒤 사라지기 시작할 때까지(초)")]
        [SerializeField] private float lingerSeconds = 2.5f;
        [Tooltip("사라질 때 줄어드는 시간(초)")]
        [SerializeField] private float shrinkSeconds = 0.4f;
        [Tooltip("바닥에 닿지 않아도 이 시간이 지나면 정리한다(초)")]
        [SerializeField] private float maxLifetime = 8f;

        private float _spawnTime;
        private float _landedTime = -1f;
        private Vector3 _baseScale;

        private void Awake()
        {
            _spawnTime = Time.time;
            _baseScale = transform.localScale;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_landedTime < 0f)
                _landedTime = Time.time;
        }

        private void Update()
        {
            float now = Time.time;
            float deadline = _landedTime >= 0f
                ? Mathf.Min(_landedTime + lingerSeconds, _spawnTime + maxLifetime)
                : _spawnTime + maxLifetime;

            if (now < deadline)
                return;

            float t = shrinkSeconds > 0f ? (now - deadline) / shrinkSeconds : 1f;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = _baseScale * (1f - t);
        }
    }
}
