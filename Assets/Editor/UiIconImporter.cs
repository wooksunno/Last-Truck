using UnityEditor;
using UnityEngine;

namespace LastTruck.EditorTools
{
    /// <summary>Resources/UI/Icons 아래 PNG를 UI 아이콘(스프라이트)으로 자동 설정한다.</summary>
    public class UiIconImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/UI/Icons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.spritePixelsPerUnit = 100f;
        }
    }
}
