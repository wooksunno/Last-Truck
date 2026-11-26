using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastTruck
{
    /// <summary>
    /// 먼 배경만 부드럽게 흐리는 심도 (Gaussian Depth of Field).
    /// 흐림이 시작되는 거리를 "카메라 ↔ 내 캐릭터 거리 + 여유"로 매 프레임 맞춘다.
    /// (예전에는 카메라에서 30m로 고정이라, 카메라를 멀리 빼면 캐릭터와 주변까지 뿌옇게 보였다)
    ///  - 캐릭터와 그 주변 FocusMargin m 까지는 항상 선명하다.
    ///  - 내 캐릭터가 없으면(메뉴/로비) 끈다.
    /// 낮/밤 Volume보다 우선순위가 높은 Volume 하나를 따로 둔다 (프로필 에셋을 실행 중에 바꾸지 않는다).
    /// </summary>
    public class DynamicFocus : MonoBehaviour
    {
        private const float FocusMargin = 22f;   // 캐릭터 너머 이만큼은 선명
        private const float BlurRange = 90f;     // 그 뒤 이 거리에 걸쳐 점점 흐려진다
        private const float MaxRadius = 0.6f;

        private static DynamicFocus _instance;
        private Volume _volume;
        private DepthOfField _dof;
        private float _start = 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("DynamicFocus");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DynamicFocus>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private void Awake()
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 60f;   // 낮/밤(0~1)/파스텔(50~51) Volume보다 위
            _volume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "DynamicFocus";
            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianMaxRadius.Override(MaxRadius);
            _dof.highQualitySampling.Override(true);
            _volume.profile = profile;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            CraftingSystem.PlayerInventory me = Networking.TruckInventorySync.LocalPlayerInventory;
            if (cam == null || me == null)
            {
                _volume.weight = 0f;
                return;
            }

            float dist = Vector3.Distance(cam.transform.position, me.transform.position);
            float target = dist + FocusMargin;
            _start = Mathf.Lerp(_start, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));   // 줌할 때 부드럽게 따라간다
            _dof.gaussianStart.Override(_start);
            _dof.gaussianEnd.Override(_start + BlurRange);
            _volume.weight = 1f;
        }
    }
}
