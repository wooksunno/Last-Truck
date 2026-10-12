using UnityEngine;

/// <summary>
/// 몬스터 애니메이션 클립과 타이밍 (몬스터 프리팹에 붙인다, 싱글 MonsterAI / 멀티 NetworkMonster 공용).
///
/// FBX 애니메이션은 클립 이름이 모두 "Take 001"이라 이름으로 찾을 수 없어서, 여기서 직접 연결한다.
///  - 공격: 클립 길이 동안 제자리에서 휘두르고, 타격 순간(이벤트 OnAttackHit, 없으면 attackHitTiming)에 피해.
///  - 피격(경직): 클립 길이만큼 멈춘다. 끝난 뒤 stunImmunitySeconds 동안은 다시 경직되지 않는다.
///  - 사망: 클립을 끝까지 재생한 뒤 사라진다.
/// 애니메이터 파라미터: Speed(float), Attack(trigger), Hit(trigger), Dead(bool)  → Monster Animator2
/// </summary>
public class MonsterAnimationSet : MonoBehaviour
{
    #region 인스펙터

    [Header("클립")]
    public AnimationClip attackClip;
    public AnimationClip getHitClip;
    public AnimationClip deathClip;

    [Header("타이밍")]
    [Tooltip("공격 클립에 OnAttackHit 이벤트가 없을 때: 클립의 몇 % 지점에서 피해가 들어가는가 (0~1).")]
    [Range(0f, 1f)] public float attackHitTiming = 0.45f;

    [Tooltip("경직이 끝난 뒤 이 시간(초) 동안은 다시 경직되지 않는다 (연사로 계속 묶이지 않게).")]
    public float stunImmunitySeconds = 1f;

    [Tooltip("사망 애니메이션이 끝난 뒤 사라지기까지 더 기다리는 시간(초).")]
    public float corpseLingerSeconds = 0.5f;

    #endregion

    #region 조회

    public float AttackDuration => attackClip != null ? attackClip.length : 0.8f;

    /// <summary>공격 시작부터 피해가 들어가기까지 시간.</summary>
    public float AttackHitDelay
    {
        get
        {
            if (attackClip == null) return AttackDuration * attackHitTiming;
            foreach (AnimationEvent e in attackClip.events)
            {
                if (e.functionName == "OnAttackHit") return e.time;
            }
            return attackClip.length * attackHitTiming;
        }
    }

    public float StunDuration => getHitClip != null ? getHitClip.length : 0.4f;

    public float DeathDuration => deathClip != null ? deathClip.length : 1.5f;

    #endregion
}
