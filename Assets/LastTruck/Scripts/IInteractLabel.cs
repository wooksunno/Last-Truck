namespace LastTruck
{
    /// <summary>
    /// 상호작용 대상이 화면에 표시할 이름(예: "구리 광석")과 부가 설명(예: "필요 도구 등급 2")을 제공한다.
    /// </summary>
    public interface IInteractLabel
    {
        string InteractLabel { get; }
        string InteractSubLabel { get; }
        UnityEngine.Color InteractLabelColor { get; }
    }
}
