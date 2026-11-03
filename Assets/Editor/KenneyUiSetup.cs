using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastTruck.EditorTools
{
    /// <summary>
    /// Kenney "UI Pack - Adventure"(PNG/Double, 2배 크기)를 UI 스프라이트로 쓸 수 있게 가져오기 설정을 맞춘다.
    ///  - 텍스처 타입 Sprite, 부드러운 확대(Bilinear), 압축/밉맵 끔
    ///  - panel_* / button_* 는 9-slice 테두리를 넣어서 크기를 늘려도 모서리가 안 깨지게 한다
    /// 메뉴: LastTruck/UI/Kenney 스프라이트 설정
    /// </summary>
    public static class KenneyUiSetup
    {
        public const string Folder = "Assets/Free_Assets/kenney_ui-pack-adventure/PNG/Double";

        [MenuItem("LastTruck/UI/Kenney 스프라이트 설정")]
        public static void Run()
        {
            int n = Apply();
            Debug.Log("[KenneyUiSetup] 스프라이트 설정 " + n + "개");
        }

        public static int Apply()
        {
            int count = 0;
            foreach (string path in Directory.GetFiles(Folder, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                string name = Path.GetFileNameWithoutExtension(assetPath);
                Vector4 border = BorderFor(name);

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
                if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.spritePixelsPerUnit != 100f) { importer.spritePixelsPerUnit = 100f; changed = true; }

                // 9-slice로 늘리는 스프라이트는 Full Rect여야 모서리에 틈/얼룩이 생기지 않는다
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType != SpriteMeshType.FullRect)
                {
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }
            return count;
        }

        /// <summary>스프라이트 이름으로 9-slice 테두리(왼, 아래, 오른, 위)를 정한다 (2배 크기 기준 픽셀).</summary>
        private static Vector4 BorderFor(string name)
        {
            if (name.StartsWith("panel_") || name.StartsWith("round_"))
                return name.StartsWith("round_") ? Vector4.zero : new Vector4(24, 24, 24, 24);
            if (name.StartsWith("button_"))
                return new Vector4(20, 18, 20, 18);
            if (name.StartsWith("progress_"))
                return new Vector4(0, 12, 0, 12);
            return Vector4.zero;
        }
    }
}
