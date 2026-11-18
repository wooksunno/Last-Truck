using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 관심 영역(AOI) 도우미.
    /// 프로젝트 설정(NetworkProjectConfig)의 Replication Features에 Interest Management가 켜져 있을 때만 동작한다.
    /// 몬스터만 AreaOfInterest로 두고, 나머지(게임 상태/캐릭터/트럭 등)는 Global로 고정해 항상 동기화한다.
    /// </summary>
    public static class NetworkInterest
    {
        #region 조회

        /// <summary>이 러너에서 AOI가 켜져 있는가.</summary>
        public static bool Enabled(NetworkRunner runner)
        {
            if (runner == null || runner.Config == null) return false;
            return ((int)runner.Config.Simulation.ReplicationFeatures & 2) != 0;
        }

        #endregion

        #region 적용

        /// <summary>스폰 직전(onBeforeSpawned)에 부른다. areaOfInterest가 false면 전역.</summary>
        public static void Apply(NetworkRunner runner, NetworkObject obj, bool areaOfInterest)
        {
            if (obj == null || !Enabled(runner)) return;
            obj.ObjectInterest = areaOfInterest ? NetworkObjectInterestModes.AreaOfInterest : NetworkObjectInterestModes.Global;
        }

        #endregion
    }
}
