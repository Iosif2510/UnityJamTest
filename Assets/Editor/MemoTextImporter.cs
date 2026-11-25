using UnityEditor;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>Imports the drawing-only memo sheets (Resources/MemoText/Sheets) as UI sprites, sharp enough to read.</summary>
    public sealed class MemoTextImporter : AssetPostprocessor
    {
        // Sheets, Sheets_ko and Shadows
        private const string Folder = "Assets/Resources/MemoText/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".png"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // no mipmaps: a browser leaves a mipmapped 1920x1080 sheet uncompressed (22 MB each instead of 2 MB)
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
