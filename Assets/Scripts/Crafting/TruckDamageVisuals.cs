using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 내구도가 떨어지면 부품(TruckDetachablePart: 문, 사이드미러, 범퍼, 적재함 아치 등)이 하나씩 떨어져 나간다.
    ///
    /// - 부품은 내구도 비율이 부품의 detachBelow 아래로 내려가는 순간 몸체에서 사라지고, 같은 모양의 파편이
    ///   트럭이 달리던 속도를 이어받아 튕겨 나가 땅에 구른다 (파편은 몇 초 뒤 줄어들며 사라진다).
    /// - 수리해서 비율이 다시 올라가면 부품이 다시 붙는다.
    /// - 처음 상태(게임 시작 / 멀티플레이 접속 직후)는 파편 없이 이미 떨어진 채로 시작한다.
    /// - 내구도 변화는 TruckInformation.OnDurabilityChanged로 받는다. 멀티플레이에서는 클라이언트도 호스트의 내구도를
    ///   그대로 받아서 같은 이벤트가 오므로, 각자 화면에서 똑같이 떨어진다 (파편은 보기용이라 네트워크로 보내지 않는다).
    /// </summary>
    public class TruckDamageVisuals : MonoBehaviour
    {
        [Tooltip("떨어진 파편이 사라지기까지 (초).")]
        [SerializeField] private float debrisLifetime = 12f;
        [Tooltip("동시에 남아 있을 수 있는 파편 수. 넘으면 오래된 것부터 지운다.")]
        [SerializeField] private int maxDebris = 30;
        [Tooltip("다시 붙을 때 커지며 나타나는 시간 (초).")]
        [SerializeField] private float reattachPopSeconds = 0.25f;

        private class Slot
        {
            public TruckDetachablePart part;
            public Renderer[] renderers;
            public Vector3 baseScale;
            public bool gone;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private static readonly Queue<GameObject> LiveDebris = new Queue<GameObject>();
        private LastTruck.TruckInformation _info;
        private Rigidbody _rb;
        private bool _initialized;

        /// <summary>떨어진(숨겨진) 부품 수.</summary>
        public int DetachedCount
        {
            get
            {
                int n = 0;
                foreach (Slot s in _slots) if (s.gone) n++;
                return n;
            }
        }

        private void Awake()
        {
            _info = GetComponent<LastTruck.TruckInformation>();
            _rb = GetComponent<Rigidbody>();

            foreach (TruckDetachablePart part in GetComponentsInChildren<TruckDetachablePart>(true))
            {
                _slots.Add(new Slot
                {
                    part = part,
                    renderers = part.GetComponentsInChildren<Renderer>(true),
                    baseScale = part.transform.localScale,
                });
            }

            if (_info != null)
                _info.OnDurabilityChanged += OnDurabilityChanged;
        }

        private void OnDestroy()
        {
            if (_info != null)
                _info.OnDurabilityChanged -= OnDurabilityChanged;
        }

        private void OnDurabilityChanged(float current, float max)
        {
            float fraction = max > 0f ? current / max : 0f;
            bool silent = !_initialized;   // 첫 값은 "이미 이런 상태였다": 파편을 만들지 않는다
            _initialized = true;

            foreach (Slot slot in _slots)
            {
                if (slot.part == null) continue;
                bool shouldBeGone = fraction < slot.part.detachBelow;
                if (shouldBeGone && !slot.gone) Detach(slot, silent);
                else if (!shouldBeGone && slot.gone) Reattach(slot, silent);
            }
        }

        // ---------------------------------------------------------------- 떨어지기

        private void Detach(Slot slot, bool silent)
        {
            slot.gone = true;
            if (!silent) SpawnDebris(slot);
            SetVisible(slot, false);
        }

        private void Reattach(Slot slot, bool silent)
        {
            slot.gone = false;
            SetVisible(slot, true);
            if (!silent && isActiveAndEnabled) StartCoroutine(Pop(slot));
        }

        private static void SetVisible(Slot slot, bool visible)
        {
            foreach (Renderer r in slot.renderers)
                if (r != null) r.enabled = visible;
        }

        private IEnumerator Pop(Slot slot)
        {
            Transform t = slot.part.transform;
            float time = 0f;
            while (time < reattachPopSeconds && t != null && !slot.gone)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / reattachPopSeconds);
                t.localScale = slot.baseScale * Mathf.Lerp(0.6f, 1f, k * (2f - k));
                yield return null;
            }
            if (t != null) t.localScale = slot.baseScale;
        }

        private void SpawnDebris(Slot slot)
        {
            var filter = slot.part.GetComponent<MeshFilter>();
            var source = slot.part.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || source == null) return;

            var go = new GameObject("Debris_" + slot.part.name);
            go.layer = LastTruck.TruckMove.PassLayer;   // 트럭 바퀴/몸체와는 부딪히지 않고 땅에만 구른다
            go.transform.SetPositionAndRotation(slot.part.transform.position, slot.part.transform.rotation);
            go.transform.localScale = slot.part.transform.lossyScale;

            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;

            Bounds b = filter.sharedMesh.bounds;
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, Vector3.one * 0.08f);

            var body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(1f, slot.part.debrisMass);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Vector3 inherited = _rb != null ? _rb.linearVelocity : Vector3.zero;
            Vector3 kick = transform.TransformDirection(slot.part.kickLocal) + Random.insideUnitSphere * 0.5f;
            body.linearVelocity = inherited + kick;
            body.angularVelocity = Random.insideUnitSphere * slot.part.spin;

            // 지나가던 플레이어가 파편에 걸려 막히지 않게 한다
            foreach (CharacterController cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                Physics.IgnoreCollision(box, cc, true);
            foreach (LastTruck.PlayerMove pm in FindObjectsByType<LastTruck.PlayerMove>(FindObjectsSortMode.None))
                foreach (Collider c in pm.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(box, c, true);

            go.AddComponent<DebrisFader>().lifetime = debrisLifetime;

            LiveDebris.Enqueue(go);
            while (LiveDebris.Count > Mathf.Max(1, maxDebris))
            {
                GameObject oldest = LiveDebris.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
        }

        /// <summary>파편: lifetime초 뒤 마지막 1초 동안 줄어들며 사라진다.</summary>
        private class DebrisFader : MonoBehaviour
        {
            public float lifetime = 12f;
            private float _age;
            private Vector3 _scale;

            private void Start() => _scale = transform.localScale;

            private void Update()
            {
                _age += Time.deltaTime;
                float left = lifetime - _age;
                if (left <= 1f) transform.localScale = _scale * Mathf.Clamp01(left);
                if (left <= 0f) Destroy(gameObject);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LiveDebris.Clear();
    }
}
