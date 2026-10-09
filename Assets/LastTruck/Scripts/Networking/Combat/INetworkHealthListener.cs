using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// NetworkHealth(동기화된 체력)의 변화를 화면에 반영하는 쪽이 구현한다.
    /// 예) PlayerStats: HP바 갱신 / 피격·사망 애니메이션,  Damageable(몬스터): 빨간색 깜빡임 + 데미지 숫자.
    /// 모든 컴퓨터(호스트/클라이언트)에서 똑같이 호출된다.
    /// </summary>
    public interface INetworkHealthListener
    {
        /// <summary>스폰될 때 최대 체력으로 쓸 값 (호스트가 한 번 읽는다).</summary>
        float NetworkMaxHealth { get; }

        /// <summary>체력이 바뀔 때마다.</summary>
        void OnNetworkHealthChanged(float current, float max);

        /// <summary>피해를 받은 순간 (빨간색 깜빡임, 데미지 숫자, 피격 애니메이션 등).</summary>
        void OnNetworkHit(float amount, Vector3 hitPoint);

        /// <summary>체력이 0이 된 순간 (사망 애니메이션 등).</summary>
        void OnNetworkDeath();
    }
}
