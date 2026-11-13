using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// Synty City Zombies 모델의 외형(옷/캐릭터)을 몬스터마다 다르게 고른다.
    ///
    /// Synty 좀비 프리팹은 한 뼈대 아래에 "Zombie_*" 몸 메시가 전부 들어 있고 하나만 켜져 있다.
    /// 그래서 모델을 새로 만들지 않고, 하나만 켜고 나머지를 끄는 것으로 외형을 바꾼다 (애니메이션/충돌체는 그대로).
    /// 멀티플레이: 호스트가 만든 NetworkObject 번호로 고르기 때문에 모든 화면에서 같은 외형이 나온다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZombieVariant : MonoBehaviour
    {
        [Tooltip("몸 메시 오브젝트 이름이 이 글자로 시작하는 것들 중에서 고른다.")]
        [SerializeField] private string bodyPrefix = "Zombie_";

        [Tooltip("0 이상이면 이 번호의 외형으로 고정한다 (-1 = 무작위/네트워크 번호).")]
        [SerializeField] private int forcedIndex = -1;

        [Tooltip("비어 있지 않으면 이 이름의 몸 메시들 중에서만 고른다 (몬스터 종류별 프리팹에서 설정).")]
        [SerializeField] private string[] allowedBodies = new string[0];

        /// <summary>고를 수 있는 몸 메시 이름 목록 (에디터 도구가 종류별 프리팹을 만들 때 넣는다).</summary>
        public string[] AllowedBodies
        {
            get => allowedBodies;
            set => allowedBodies = value ?? new string[0];
        }

        private bool _appliedByType;

        private void Start()
        {
            if (_appliedByType) return; // 몬스터 종류가 이미 외형을 정했다
            if (forcedIndex < 0 && allowedBodies != null && allowedBodies.Length > 0)
            {
                var no = GetComponent<Fusion.NetworkObject>();
                ApplyAllowed(no != null && no.IsValid ? (int)(no.Id.Raw % 100000) : Random.Range(0, 100000));
                return;
            }
            Apply();
        }

        /// <summary>인스펙터의 Allowed Bodies 목록 중에서 seed로 고른다 (목록이 비면 전체).</summary>
        public void ApplyAllowed(int seed) => ApplyNames(allowedBodies, seed);

        /// <summary>
        /// 몬스터 종류(NetworkMonster)가 부른다: names 목록에 있는 몸 메시 중에서만 seed로 고른다 (모든 화면에서 같은 결과).
        /// 목록이 비었거나 맞는 메시가 없으면 전체에서 고른다.
        /// </summary>
        public void ApplyNames(System.Collections.Generic.IList<string> names, int seed)
        {
            _appliedByType = true;
            var bodies = new System.Collections.Generic.List<Renderer>();
            var allowed = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r == null || !r.name.StartsWith(bodyPrefix)) continue;
                bodies.Add(r);
                if (names != null && names.Contains(r.name)) allowed.Add(r);
            }
            if (bodies.Count == 0) return;

            var pool = allowed.Count > 0 ? allowed : bodies;
            Renderer chosen = pool[((seed % pool.Count) + pool.Count) % pool.Count];
            foreach (Renderer body in bodies)
                body.gameObject.SetActive(body == chosen);

            var damageable = GetComponent<Combat.Damageable>();
            if (damageable != null) damageable.RefreshRenderers();
        }

        public void Apply()
        {
            var bodies = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r != null && r.name.StartsWith(bodyPrefix)) bodies.Add(r);
            }
            if (bodies.Count == 0) return;

            int index;
            if (forcedIndex >= 0)
            {
                index = forcedIndex;
            }
            else
            {
                var no = GetComponent<Fusion.NetworkObject>();
                index = no != null && no.IsValid
                    ? (int)(no.Id.Raw % (uint)bodies.Count)
                    : Random.Range(0, bodies.Count);
            }
            index = ((index % bodies.Count) + bodies.Count) % bodies.Count;

            for (int i = 0; i < bodies.Count; i++)
                bodies[i].gameObject.SetActive(i == index);

            // 피격 깜빡임이 보이는 몸에 적용되도록 렌더러 목록을 갱신한다
            var damageable = GetComponent<Combat.Damageable>();
            if (damageable != null) damageable.RefreshRenderers();
        }
    }
}
