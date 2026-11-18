using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 몬스터 종류별 프리팹(Monster_Walker, Monster_Tank ...)의 모습 설정. 프리팹 인스펙터에서 바로 고친다.
    ///  - 크기: 프리팹 루트의 Scale을 그대로 쓴다.
    ///  - 몸 메시: 같은 오브젝트의 ZombieVariant "Allowed Bodies" 목록 중에서 고른다 (모델을 통째로 바꿔도 된다).
    ///  - 색 / 애니메이션 속도: 아래 값.
    /// 체력/공격력/속도 등 수치는 MonsterCatalog(종류 표)에서 kind로 읽는다.
    /// 이 컴포넌트가 없는 프리팹(기본 Monster_Network)은 종류 표의 모습 값을 대신 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterAppearance : MonoBehaviour
    {
        [Tooltip("이 프리팹의 몬스터 종류 (수치는 MonsterCatalog에서 이 종류로 읽는다).")]
        public MonsterKind kind = MonsterKind.Walker;

        [Tooltip("몸 색에 곱하는 색 (흰색 = 원래 색).")]
        public Color tint = Color.white;

        [Tooltip("애니메이션 재생 속도 배율 (공격 타이밍도 같이 빨라진다).")]
        [Min(0.1f)] public float animationSpeed = 1f;
    }
}
