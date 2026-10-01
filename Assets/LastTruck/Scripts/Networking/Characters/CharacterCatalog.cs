using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 고를 수 있는 캐릭터 목록. 목록 순서가 곧 "캐릭터 번호"이고, 네트워크로는 이 번호만 주고받는다.
    ///
    /// - 에셋 위치: Assets/LastTruck/Multiplayer/Resources/CharacterCatalog.asset (Resources라서 어느 씬에서든 불러올 수 있다)
    /// - 캐릭터를 추가하려면: CharacterStatsData 에셋을 만들고 이 목록 맨 뒤에 넣은 다음,
    ///   메뉴 "LastTruck > Multiplayer > 1. 네트워크 프리팹 생성"과 "2. 캐릭터 초상화 촬영"을 다시 실행하면 된다.
    /// - 게임 도중에는 순서를 바꾸지 말 것 (접속한 사람끼리 목록이 다르면 서로 다른 캐릭터로 보인다).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "LastTruck/Character Catalog")]
    public class CharacterCatalog : ScriptableObject
    {
        #region 캐릭터 목록 조회

        public const string ResourcesPath = "CharacterCatalog";

        [SerializeField] private List<CharacterStatsData> characters = new List<CharacterStatsData>();

        public IReadOnlyList<CharacterStatsData> Characters => characters;
        public int Count => characters.Count;

        public CharacterStatsData Get(int index)
        {
            return index >= 0 && index < characters.Count ? characters[index] : null;
        }

        /// <summary>목록 범위 안으로 맞춘 번호. 목록이 비어 있으면 0.</summary>
        public int ClampIndex(int index)
        {
            if (characters.Count == 0) return 0;
            return Mathf.Clamp(index, 0, characters.Count - 1);
        }

        /// <summary>해당 번호의 네트워크 프리팹. 없으면 목록에서 처음으로 프리팹이 있는 캐릭터로 대신한다.</summary>
        public NetworkObject GetNetworkPrefab(int index)
        {
            CharacterStatsData data = Get(ClampIndex(index));
            if (data != null && data.networkPrefab != null) return data.networkPrefab;

            foreach (CharacterStatsData other in characters)
            {
                if (other != null && other.networkPrefab != null) return other.networkPrefab;
            }
            return null;
        }

        /// <summary>
        /// 스폰할 프리팹을 고른다. 고른 캐릭터에 프리팹이 없으면 프리팹이 있는 첫 캐릭터로 바꾸고 index도 그 번호로 맞춘다.
        /// </summary>
        public NetworkObject ResolveSpawnPrefab(ref int index)
        {
            index = ClampIndex(index);
            CharacterStatsData data = Get(index);
            if (data != null && data.networkPrefab != null) return data.networkPrefab;

            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null && characters[i].networkPrefab != null)
                {
                    index = i;
                    return characters[i].networkPrefab;
                }
            }
            return null;
        }

        public string GetDisplayName(int index)
        {
            CharacterStatsData data = Get(ClampIndex(index));
            return data != null ? data.DisplayName : "캐릭터 없음";
        }

        public Sprite GetPortrait(int index)
        {
            CharacterStatsData data = Get(ClampIndex(index));
            return data != null ? data.portrait : null;
        }

        #endregion

        #region 어디서든 쓰는 공용 인스턴스

        private static CharacterCatalog _instance;
        private static bool _warned;

        /// <summary>Resources/CharacterCatalog를 불러온다. 없으면 null (콘솔에 한 번 경고).</summary>
        public static CharacterCatalog Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CharacterCatalog>(ResourcesPath);
                    if (_instance == null && !_warned)
                    {
                        _warned = true;
                        Debug.LogWarning("[CharacterCatalog] Resources/CharacterCatalog 에셋이 없습니다. " +
                                         "메뉴 'LastTruck > Multiplayer > 1. 네트워크 프리팹 생성'을 실행하세요.");
                    }
                }
                return _instance;
            }
        }

        /// <summary>목록이 없어도 안전하게 번호를 맞춘다 (없으면 음수만 0으로).</summary>
        public static int SafeClamp(int index)
        {
            CharacterCatalog catalog = Instance;
            return catalog != null ? catalog.ClampIndex(index) : Mathf.Max(0, index);
        }

#if UNITY_EDITOR
        /// <summary>에디터 도구 전용: 목록 전체를 교체한다.</summary>
        public void EditorSetCharacters(List<CharacterStatsData> list)
        {
            characters = new List<CharacterStatsData>(list);
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _warned = false;
        }

        #endregion
    }
}
