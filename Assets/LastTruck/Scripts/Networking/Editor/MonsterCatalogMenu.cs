using UnityEditor;
using UnityEngine;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 몬스터 종류/웨이브 표 에셋(Assets/Resources/MonsterCatalog.asset)을 기획표 기본값으로 만든다.
    /// 에셋이 없어도 게임은 같은 기본값으로 돌아간다. 수치를 인스펙터에서 바꾸고 싶을 때 실행한다.
    /// </summary>
    public static class MonsterCatalogMenu
    {
        private const string AssetPath = "Assets/Resources/MonsterCatalog.asset";

        [MenuItem("LastTruck/Multiplayer/2. 몬스터 종류·웨이브 표 만들기", priority = 102)]
        public static void CreateOrSelect()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(AssetPath);
            if (existing != null)
            {
                bool reset = EditorUtility.DisplayDialog("몬스터 종류·웨이브 표",
                    "이미 표가 있습니다. 기획표 기본값으로 되돌릴까요?\n(아니오: 기존 표를 선택만 합니다)", "기본값으로", "아니오");
                if (reset)
                {
                    Undo.RecordObject(existing, "Reset Monster Catalog");
                    existing.FillDefaults();
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                Selection.activeObject = existing;
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
            catalog.FillDefaults();
            AssetDatabase.CreateAsset(catalog, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log($"[MonsterCatalog] 만들었습니다: {AssetPath}");
        }
    }
}
