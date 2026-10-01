using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 게임 씬에서 호스트가 스폰하는 네트워크 프리팹 모음 (캐릭터 제외).
    /// 에셋 위치: Assets/LastTruck/Multiplayer/Resources/NetworkPrefabRegistry.asset
    /// 에디터 메뉴 "LastTruck > Multiplayer > 1. 네트워크 프리팹 생성"이 자동으로 만들고 채운다.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkPrefabRegistry", menuName = "LastTruck/Network Prefab Registry")]
    public class NetworkPrefabRegistry : ScriptableObject
    {
        public const string ResourcesPath = "NetworkPrefabRegistry";

        [Tooltip("게임 진행 상태(낮/밤, 준비 대기, 게임 오버)를 동기화하는 오브젝트.")]
        public NetworkObject gameStatePrefab;

        [Tooltip("몬스터 (NetworkObject + NetworkTransform + NetworkHealth + NetworkMonster).")]
        public NetworkObject monsterPrefab;

        [Tooltip("게임 씬의 기본 UI 텍스트(legacy Text)에 쓸 한글 폰트. 기본 Arial/LegacyRuntime 대신 이 폰트로 바꿔서 선명하게 보이게 한다.")]
        public Font uiFont;

        [Tooltip("동료 이름표 등 TextMeshPro 글자에 쓸 한글 폰트 (Pretendard SDF).")]
        public TMPro.TMP_FontAsset tmpFont;

        private static NetworkPrefabRegistry _instance;

        public static NetworkPrefabRegistry Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<NetworkPrefabRegistry>(ResourcesPath);
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }
    }
}
