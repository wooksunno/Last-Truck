using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 도장(ToonWorld 셰이더의 Military Weathering)이 트럭을 따라 움직이게 한다.
    /// 위장 무늬/진흙은 3D 좌표로 계산하므로, 트럭 기준 좌표를 매 프레임 재질에 넣어 줘야 달릴 때 무늬가 미끄러지지 않는다.
    /// TruckInformation이 자동으로 붙인다.
    /// </summary>
    public class VehicleWeathering : MonoBehaviour
    {
        private static readonly int Row0 = Shader.PropertyToID("_VehRow0");
        private static readonly int Row1 = Shader.PropertyToID("_VehRow1");
        private static readonly int Row2 = Shader.PropertyToID("_VehRow2");

        private readonly List<Material> _materials = new List<Material>();

        private void Start() => Collect();

        private void Collect()
        {
            _materials.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.IsKeywordEnabled("_WEATHERING_ON") && !_materials.Contains(m))
                        _materials.Add(m);
        }

        private void LateUpdate()
        {
            if (_materials.Count == 0) return;
            Matrix4x4 w2l = transform.worldToLocalMatrix;
            Vector4 r0 = w2l.GetRow(0), r1 = w2l.GetRow(1), r2 = w2l.GetRow(2);
            foreach (var m in _materials)
            {
                if (m == null) continue;
                m.SetVector(Row0, r0);
                m.SetVector(Row1, r1);
                m.SetVector(Row2, r2);
            }
        }

        private void OnDestroy()
        {
            // 에디터에서 재질 에셋에 트럭 위치가 남지 않도록 되돌린다
            foreach (var m in _materials)
            {
                if (m == null) continue;
                m.SetVector(Row0, Vector4.zero);
                m.SetVector(Row1, Vector4.zero);
                m.SetVector(Row2, Vector4.zero);
            }
        }
    }
}
