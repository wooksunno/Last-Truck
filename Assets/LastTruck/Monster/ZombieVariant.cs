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

        private void Start()
        {
            Apply();
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
