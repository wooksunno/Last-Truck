using UnityEngine;

namespace LastTruck
{
    public interface IInteractable
    {
        void Interact(GameObject player);
    }

    /// <summary>
    /// 여러 상호작용 대상이 겹칠 때의 우선순위. 값이 클수록 먼저 선택된다 (구현하지 않으면 0).
    /// </summary>
    public interface IInteractPriority
    {
        int InteractPriority { get; }
    }
}
