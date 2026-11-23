using UnityEditor;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>Imports the drawing-only memo sheets (Resources/MemoText/Sheets) as UI sprites, sharp enough to read.</summary>
    public sealed class MemoTextImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/MemoText/Sheets";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = true; // cards show the sheet at a quarter size
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
