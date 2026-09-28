namespace LastTruck
{
    /// <summary>
    /// E키를 일정 시간 꾹 눌러야 완료되는 상호작용. RequiredHoldSeconds가 0 이하이면
    /// PlayerInteract가 기존처럼 즉시(GetKeyDown) 상호작용으로 처리한다.
    /// </summary>
    public interface IHoldInteractable : IInteractable
    {
        float RequiredHoldSeconds { get; }
    }
}
