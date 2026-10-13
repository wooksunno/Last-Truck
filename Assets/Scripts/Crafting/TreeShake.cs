using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 채집 중 주먹으로 맞은 나무가 밑동을 축으로 흔들렸다가 제자리로 돌아오는 연출.
    /// 필요할 때 Shake()가 나무 루트에 자동으로 붙인다 (씬에 미리 달아 둘 필요 없음).
    /// 나무 콜라이더/채집 트리거는 움직이지 않는다: 흔드는 동안 원래 메쉬를 숨기고 "보이기만 하는 복사본"을 흔든다.
    /// (나무 자체를 돌리면 콜라이더가 플레이어를 밀어내 채집 범위에서 벗어나 채집이 끊겼다)
    /// </summary>
    public class TreeShake : MonoBehaviour
    {
        [Tooltip("처음 맞았을 때 기울어지는 각도(도).")]
        [SerializeField] private float amplitude = 4f;
        [Tooltip("흔들리는 빠르기 (초당 왕복 횟수).")]
        [SerializeField] private float frequency = 3.5f;
        [Tooltip("흔들림이 잦아드는 속도. 클수록 빨리 멈춘다.")]
        [SerializeField] private float damping = 4f;
        [Tooltip("이 시간(초)이 지나면 원래 모습으로 돌려 놓고 멈춘다.")]
        [SerializeField] private float duration = 1.2f;

        private MeshRenderer _sourceRenderer;
        private Transform _visual;
        private Vector3 _pivot;
        private Vector3 _axis;
        private float _startTime;
        private bool _shaking;

        private float _amplitudeScale = 1f;
        private float _frequencyScale = 1f;

        /// <summary>
        /// root를 pivot(밑동)을 축으로, hitFrom(때린 사람 위치) 반대쪽으로 흔든다.
        /// 돌처럼 짧게 떨리게 하려면 amplitudeScale을 줄이고 frequencyScale을 키운다.
        /// </summary>
        public static void Shake(GameObject root, Vector3 pivot, Vector3 hitFrom, float amplitudeScale = 1f, float frequencyScale = 1f)
        {
            if (root == null)
                return;
            TreeShake shake = root.GetComponent<TreeShake>();
            if (shake == null)
                shake = root.AddComponent<TreeShake>();
            shake._amplitudeScale = amplitudeScale;
            shake._frequencyScale = frequencyScale;
            shake.Begin(pivot, hitFrom);
        }

        private void Begin(Vector3 pivot, Vector3 hitFrom)
        {
            if (!EnsureVisual())
                return;

            _pivot = pivot;
            Vector3 away = _pivot - hitFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = transform.forward;
            _axis = Vector3.Cross(Vector3.up, away.normalized);
            _startTime = Time.time;

            // 흔들리는 도중에 또 맞으면 처음부터 다시 흔든다
            _shaking = true;
            _sourceRenderer.enabled = false;
            _visual.gameObject.SetActive(true);
            enabled = true;
        }

        /// <summary>원래 메쉬와 똑같이 보이는 복사본(콜라이더 없음)을 한 번만 만든다.</summary>
        private bool EnsureVisual()
        {
            if (_visual != null)
                return true;

            _sourceRenderer = GetComponent<MeshRenderer>();
            MeshFilter sourceFilter = GetComponent<MeshFilter>();
            if (_sourceRenderer == null || sourceFilter == null || sourceFilter.sharedMesh == null)
                return false;

            var go = new GameObject("ShakeVisual");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = _sourceRenderer.sharedMaterials;
            r.shadowCastingMode = _sourceRenderer.shadowCastingMode;
            r.receiveShadows = _sourceRenderer.receiveShadows;
            r.lightProbeUsage = _sourceRenderer.lightProbeUsage;
            r.reflectionProbeUsage = _sourceRenderer.reflectionProbeUsage;
            go.SetActive(false);
            _visual = go.transform;
            return true;
        }

        private void Update()
        {
            if (!_shaking)
            {
                enabled = false;
                return;
            }

            float t = Time.time - _startTime;
            if (t >= duration)
            {
                Restore();
                return;
            }

            float angle = amplitude * _amplitudeScale * Mathf.Exp(-damping * _frequencyScale * t) *
                          Mathf.Sin(t * frequency * _frequencyScale * 2f * Mathf.PI);
            Quaternion tilt = Quaternion.AngleAxis(angle, _axis);
            _visual.SetPositionAndRotation(_pivot + tilt * (transform.position - _pivot), tilt * transform.rotation);
        }

        private void Restore()
        {
            _shaking = false;
            enabled = false;
            if (_visual != null)
                _visual.gameObject.SetActive(false);
            if (_sourceRenderer != null)
                _sourceRenderer.enabled = true;
        }

        private void OnDisable()
        {
            if (_shaking)
                Restore();
        }
    }
}
