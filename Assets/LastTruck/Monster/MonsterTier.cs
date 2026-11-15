using UnityEngine;
using UnityEngine.AI;

namespace LastTruck
{
    /// <summary>
    /// 몬스터의 강함 등급 (밤이 깊어질수록 강해지는 일반 좀비 / 중간보스).
    /// SurvivalDirector가 스폰 직후 붙인다. 체력/피해 배율은 호스트(싱글)가 적용하고,
    /// 크기는 모든 화면이 같은 값으로 모델(자식 "Model")만 키운다 (루트는 NetworkTransform이 동기화하므로 건드리지 않는다).
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterTier : MonoBehaviour
    {
        public bool IsBoss;
        public float DamageScale = 1f;
        public float SizeScale = 1f;

        private bool _sizeApplied;

        /// <summary>이 몬스터의 피해 배율 (없으면 1).</summary>
        public static float DamageOf(GameObject monster)
        {
            MonsterTier tier = monster != null ? monster.GetComponent<MonsterTier>() : null;
            return (tier != null ? tier.DamageScale : 1f) * WeatherDirector.MonsterDamageScale;   // 폭풍우에서는 더 아프다
        }

        /// <summary>크기를 한 번만 적용한다 (모델 + 충돌체 + 길찾기 반경).</summary>
        public void ApplySize(float size)
        {
            SizeScale = size;
            if (_sizeApplied || size <= 0.01f || Mathf.Approximately(size, 1f)) return;
            _sizeApplied = true;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.transform != transform) animator.transform.localScale *= size;

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius *= size;
                capsule.height *= size;
                capsule.center *= size;
            }

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius *= Mathf.Min(size, 1.6f);   // 너무 넓으면 좁은 길을 못 지나간다
                agent.height *= size;
            }
        }
    }
}
