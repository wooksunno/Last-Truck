using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 개발용 단축키. 게임이 시작되면 자동으로 만들어진다 (씬에 둘 필요 없음). 개발 빌드(에디터 포함)에서만 동작한다.
    ///   Ctrl+A : 설비 전체 + 트럭에 붙일 수 있는 부착물(포탑/엔진/전면/장갑) 전체를 받는다 (CraftingCheats.GrantFacilitiesAndParts).
    /// (Ctrl+1~7은 유니티 에디터가 창 열기에 쓰는 키라 피했다.)
    /// 정식 빌드에서 빼려면 이 파일을 지우면 된다.
    /// </summary>
    public class CraftingCheatHotkeys : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild) return;
            if (FindFirstObjectByType<CraftingCheatHotkeys>() != null) return;
            var go = new GameObject("CraftingCheatHotkeys");
            DontDestroyOnLoad(go);
            go.AddComponent<CraftingCheatHotkeys>();
        }

        private void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.A))
            {
                string message = CraftingCheats.GrantFacilitiesAndParts();
                Debug.Log("[Cheat Ctrl+A] " + message);
                LastTruck.HudToast.Show(message);
            }
        }
    }
}
