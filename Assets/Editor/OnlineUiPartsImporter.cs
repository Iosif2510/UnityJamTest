using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Imports the online / loft UI parts (Assets/Resources/OnlineUiB) as UI sprites. The slice border comes from the
    /// file name, as the parts spec asks: <c>_9slice_64</c> is a 64 px border on all sides, <c>_3slice_24</c> a 24 px
    /// border on the left and right only. A designer's file with the same name pattern drops straight in.
    /// </summary>
    public sealed class OnlineUiPartsImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/OnlineUiB/";
        private static readonly Regex Slice = new(@"_(9|3)slice_(\d+)$");

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var match = Slice.Match(name);
            if (!match.Success)
            {
                importer.spriteBorder = Vector4.zero;
                return;
            }
            float border = float.Parse(match.Groups[2].Value);
            // Vector4 order is left, bottom, right, top.
            importer.spriteBorder = match.Groups[1].Value == "9"
                ? new Vector4(border, border, border, border)
                : new Vector4(border, 0f, border, 0f);
        }
    }
}
