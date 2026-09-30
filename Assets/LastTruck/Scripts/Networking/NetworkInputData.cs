using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 클라이언트가 매 네트워크 틱마다 서버(호스트)로 보내는 입력값.
    /// 이동 방향은 "카메라 기준으로 이미 계산된 월드 방향 벡터"로 보낸다.
    /// 호스트가 시뮬레이션을 돌릴 때는 각 클라이언트의 카메라 Transform을 알 수 없기 때문에,
    /// 카메라 relative 계산은 반드시 입력을 만드는 쪽(로컬 클라이언트, GameLauncher.OnInput)에서 끝내야 한다.
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        public Vector3 MoveDirection;
        public NetworkButtons Buttons;
    }

    /// <summary>NetworkButtons 비트에 매핑되는 입력 종류.</summary>
    public enum NetworkInputButton
    {
        Walk,
        Attack,
        Interact,
    }
}
