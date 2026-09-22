using System;
using UnityEngine;

/// <summary>
/// 낮/밤 사이클 관리 및 밤 턴의 몬스터 킬 쿼터를 판정하는 싱글플레이용 매니저.
///
/// [설계 원칙]
/// - 몬스터 스폰/킬카운트 시스템은 이미 구현되어 있다고 가정하고,
///   이 매니저는 "언제 스폰을 시작/중단할지", "언제 낮/밤을 전환할지"만 판단한다.
///   실제 스폰 로직, 킬 판정 로직은 아래 이벤트/메서드를 통해 기존 시스템과 연결하면 된다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GamePhase
    {
        Day,
        Night
    }

    [Header("낮 설정 (Inspector 조절)")]
    [Tooltip("낮 턴의 지속 시간 (초). 실시간 타이머 기준.")]
    [SerializeField] private float dayDurationSeconds = 300f;

    /// <summary>외부(연출 스크립트 등)에서 낮 진행률을 계산할 때 참조할 수 있도록 공개.</summary>
    public float DayDurationSeconds => dayDurationSeconds;

    [Header("밤 설정 (Inspector 조절)")]
    [Tooltip("사이클(밤 회차)당 기본 몬스터 총 스폰 쿼터.")]
    [SerializeField] private int baseNightMonsterQuota = 10;

    [Tooltip("밤 사이클이 반복될수록 쿼터가 증가하는 값 (사이클당 가산). 필요 없으면 0으로 설정.")]
    [SerializeField] private int quotaIncreasePerCycle = 3;

    /// <summary>
    /// 씬에 하나만 존재한다고 가정하는 싱글톤 참조.
    /// 스포너/킬카운트 등 외부 스크립트가 인스펙터 연결 없이도 쉽게 접근할 수 있도록 제공.
    /// </summary>
    public static GameManager Instance { get; private set; }

    // ---------------------------------------------------------------
    // 현재 상태 (읽기 전용으로 외부에 노출 - UI 등에서 참조)
    // ---------------------------------------------------------------
    public GamePhase CurrentPhase { get; private set; }
    public int CurrentCycle { get; private set; } // 몇 번째 낮/밤인지 (1부터 시작)
    public int NightMonsterQuota { get; private set; } // 이번 밤의 총 스폰 쿼터
    public int NightMonstersKilled { get; private set; } // 이번 밤에 처치한 수
    public float DayTimeRemaining { get; private set; } // 낮 턴 남은 시간

    // ---------------------------------------------------------------
    // 외부(기존 스폰/킬카운트 시스템)에서 구독할 이벤트
    // ---------------------------------------------------------------

    /// <summary>밤이 시작될 때 발생. 인자는 이번 밤의 총 스폰 쿼터.
    /// 기존 몬스터 스포너 스크립트가 이 이벤트를 구독해서 스폰을 시작하면 됨.</summary>
    public event Action<int> OnNightStarted;

    /// <summary>낮이 시작될 때 발생. 인자는 새로 시작하는 낮의 사이클 번호.</summary>
    public event Action<int> OnDayStarted;

    /// <summary>밤이 끝났을 때(쿼터 달성) 발생. 스포너 쪽에서 잔여 스폰 취소 등에 사용 가능.</summary>
    public event Action OnNightEnded;

    /// <summary>낮/밤 페이즈가 바뀔 때마다 발생. UI/연출(하늘색 변화, 사운드 등) 연결용.</summary>
    public event Action<GamePhase> OnPhaseChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameManager] 씬에 GameManager가 이미 존재합니다. 중복된 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        CurrentCycle = 1;
        StartDay();
    }

    private void Update()
    {
        switch (CurrentPhase)
        {
            case GamePhase.Day:
                TickDay();
                break;

            case GamePhase.Night:
                TickNight();
                break;
        }
    }

    // ---------------------------------------------------------------
    // 낮 로직
    // ---------------------------------------------------------------
    private void StartDay()
    {
        CurrentPhase = GamePhase.Day;
        DayTimeRemaining = dayDurationSeconds;

        OnDayStarted?.Invoke(CurrentCycle);
        OnPhaseChanged?.Invoke(CurrentPhase);
        // TODO: 여기서 트럭 이동 가능 상태, 채집 가능 상태 등 낮 전용 시스템 활성화 트리거
    }

    private void TickDay()
    {
        DayTimeRemaining -= Time.deltaTime;

        if (DayTimeRemaining <= 0f)
        {
            StartNight();
        }
    }

    // ---------------------------------------------------------------
    // 밤 로직
    // ---------------------------------------------------------------
    private void StartNight()
    {
        CurrentPhase = GamePhase.Night;
        NightMonstersKilled = 0;
        NightMonsterQuota = CalculateNightQuota(CurrentCycle);

        OnNightStarted?.Invoke(NightMonsterQuota);
        OnPhaseChanged?.Invoke(CurrentPhase);
        // TODO: 여기서 플레이어 이동 제한(파밍 불가) 등 밤 전용 시스템 활성화 트리거
    }

    private void TickNight()
    {
        // 밤은 고정 시간이 아니라 "쿼터를 다 채웠는가"로 종료 판정.
        // 실제 몬스터 스폰/처치는 외부 시스템이 담당하고, 이 매니저는 판정만 함.
        if (NightMonstersKilled >= NightMonsterQuota)
        {
            EndNight();
        }
    }

    private void EndNight()
    {
        OnNightEnded?.Invoke();

        CurrentCycle++;
        StartDay();
    }

    /// <summary>
    /// 사이클(밤 회차)에 따른 몬스터 총 스폰 쿼터 계산.
    /// </summary>
    private int CalculateNightQuota(int cycle)
    {
        return baseNightMonsterQuota + quotaIncreasePerCycle * (cycle - 1);
    }

    // ---------------------------------------------------------------
    // 외부(기존 킬카운트 시스템)에서 호출할 공개 메서드
    // ---------------------------------------------------------------

    /// <summary>
    /// 몬스터 한 마리가 처치되었을 때 기존 킬카운트 시스템이 호출해주는 함수.
    /// </summary>
    public void NotifyMonsterKilled()
    {
        if (CurrentPhase != GamePhase.Night)
            return; // 밤이 아닐 때는 카운트하지 않음 (방어 로직)

        NightMonstersKilled = Mathf.Min(NightMonstersKilled + 1, NightMonsterQuota);
    }
}