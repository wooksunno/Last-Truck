using UnityEngine;

/// <summary>
/// 몬스터 오브젝트가 파괴(Destroy)될 때 GameManager에 "몬스터 한 마리 처치됨"을 알리는 컴포넌트.
///
/// 사용 방식: MonsterSpawner가 몬스터를 스폰한 직후 이 컴포넌트를 자동으로 부착함
/// (몬스터 프리팹을 직접 수정할 필요 없음).
///
/// [주의] 이 방식은 몬스터가 죽을 때 실제로 Destroy()되는 것을 전제로 한다.
/// 나중에 오브젝트 풀링(재사용)을 도입하면 OnDestroy가 호출되지 않으므로,
/// 그때는 몬스터의 체력 스크립트에서 명시적으로 NotifyDeath()를 호출하는 방식으로 바꿔야 함.
/// </summary>
public class MonsterKillNotifier : MonoBehaviour
{
    // 같은 프레임에 중복 호출되는 것을 방지하기 위한 플래그
    // (예: 폭발 데미지 등으로 동시에 여러 번 사망 처리가 시도되는 경우 대비)
    private bool _hasReported = false;

    private void OnDestroy()
    {
        // 씬 전환/애플리케이션 종료 시에도 OnDestroy가 호출되므로,
        // 그 경우까지 킬로 잘못 집계되지 않도록 재생 중일 때만 처리
        if (!Application.isPlaying)
            return;

        NotifyDeath();
    }

    /// <summary>
    /// 몬스터의 체력 스크립트가 사망을 더 확실한 시점에 알리고 싶다면
    /// Destroy(gameObject) 직전에 이 함수를 직접 호출해도 된다 (선택 사항).
    /// OnDestroy에서도 자동으로 호출되므로 중복 호출은 내부에서 방지됨.
    /// </summary>
    public void NotifyDeath()
    {
        if (_hasReported)
            return;

        _hasReported = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyMonsterKilled();
        }
        else
        {
            Debug.LogWarning("[MonsterKillNotifier] GameManager.Instance를 찾을 수 없어 킬 카운트를 반영하지 못했습니다.");
        }
    }
}
