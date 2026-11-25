using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Imports the designer's online / loft UI parts (Assets/Resources/OnlineUi) as UI sprites. Slice borders come from
    /// the delivered <c>_borders.json</c>, which lists <c>[left, top, right, bottom]</c> per part name, as the parts
    /// spec (Docs/UI_디자인_핸드오프/부품_규격표.md) asks. A re-delivery with the same file names drops straight in.
    /// </summary>
    public sealed class OnlineUiPartsImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/OnlineUi/";
        private const string BordersFile = Folder + "_borders.json";
        private static readonly Regex Entry = new(@"""([^""]+)""\s*:\s*\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]");

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

            string name = Path.GetFileNameWithoutExtension(assetPath);
            // Unity's sprite border is (left, bottom, right, top).
            importer.spriteBorder = ReadBorders().TryGetValue(name, out var b) ? new Vector4(b.x, b.w, b.z, b.y) : Vector4.zero;
        }

        /// <summary>The JSON as (left, top, right, bottom) per part.</summary>
        private static Dictionary<string, Vector4> ReadBorders()
        {
            var borders = new Dictionary<string, Vector4>();
            if (!File.Exists(BordersFile))
                return borders;
            foreach (Match m in Entry.Matches(File.ReadAllText(BordersFile)))
                borders[m.Groups[1].Value] = new Vector4(float.Parse(m.Groups[2].Value), float.Parse(m.Groups[3].Value),
                    float.Parse(m.Groups[4].Value), float.Parse(m.Groups[5].Value));
            return borders;
        }

        /// <summary>Re-imports the parts when the border list itself changes.</summary>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path == BordersFile)
                {
                    foreach (var png in Directory.GetFiles(Folder, "*.png"))
                        AssetDatabase.ImportAsset(png.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                    return;
                }
        }
    }
}
