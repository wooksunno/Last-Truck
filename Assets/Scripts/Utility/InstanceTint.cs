using UnityEngine;

namespace Utility
{
    /// <summary>
    /// 공유 머티리얼(에셋)은 전혀 건드리지 않고, 이 오브젝트(와 자식)의 렌더러에만
    /// 색을 덮어씌운다. MaterialPropertyBlock을 쓰기 때문에 새 머티리얼 파일도 안 만든다.
    /// 인스펙터에서 Add Component로 붙이고 Tint 색만 바꾸면 된다.
    /// </summary>
    [ExecuteAlways]
    public class InstanceTint : MonoBehaviour
    {
        [Tooltip("이 색으로 _BaseColor를 덮어쓴다. 원래 텍스처/버텍스컬러에 곱해지므로, " +
                 "흰색보다 어둡게 하면 어두워지고 밝게 하면 밝아진다.")]
        public Color tint = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Tooltip("켜져 있는 동안에만 적용된다. 꺼면 원래 머티리얼 색으로 돌아간다.")]
        public bool apply = true;

        [Tooltip("자식 렌더러에도 같이 적용할지")]
        public bool includeChildren = false;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            // 컴포넌트 자체를 끄거나(체크박스) 제거할 때도 덮어쓴 색을 반드시 지운다.
            ClearAll();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Apply()
        {
            // includeChildren을 껐다 켰다 하면서 생길 수 있는 "지워지지 않은 과거 범위"를
            // 막기 위해, 매번 전체를 먼저 지우고 나서 현재 설정대로 다시 칠한다.
            ClearAll();

            if (!apply)
                return;

            if (_block == null)
                _block = new MaterialPropertyBlock();

            Renderer[] renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                r.SetPropertyBlock(_block);
            }

            RepaintViews();
        }

        private void ClearAll()
        {
            // apply/includeChildren 둘 중 뭐가 바뀌었든, 과거에 켜져있던 범위까지 포함해서
            // 이 오브젝트와 모든 자식 렌더러의 덮어쓰기를 지운다(includeChildren을 껐다가 꺼도 자식에 남지 않도록).
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
            }

            RepaintViews();
        }

        private static void RepaintViews()
        {
#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }
    }
}
