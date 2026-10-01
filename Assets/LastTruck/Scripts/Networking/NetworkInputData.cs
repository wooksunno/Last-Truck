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
        /// <summary>이동 방향 (월드 기준, y=0, 길이 0~1).</summary>
        public Vector3 MoveDirection;

        /// <summary>
        /// 이번 틱에 캐릭터가 바라봐야 할 방향 (무기 조준 등). 길이가 0이면 "요청 없음" → 이동 방향을 따라 회전한다.
        /// </summary>
        public Vector3 FaceDirection;

        public NetworkButtons Buttons;

        /// <summary>트럭 운전석에 앉아 있을 때: 앞(+1)/뒤(-1) 가속.</summary>
        public float TruckThrottle;

        /// <summary>트럭 운전석에 앉아 있을 때: 핸들 왼쪽(-1)/오른쪽(+1).</summary>
        public float TruckSteer;
    }

    /// <summary>NetworkButtons 비트에 매핑되는 입력 종류.</summary>
    public enum NetworkInputButton
    {
        Walk,
        Attack,
        Interact,
        TruckBrake,
    }
}
