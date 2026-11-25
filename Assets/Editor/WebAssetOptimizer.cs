using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Potion Shop/Build/Optimize Assets For Web: import settings that only the browser build uses, so the desktop
    /// builds look exactly as before.
    /// - Textures and sprite atlases get a WebGL override with crunched DXT5 (DXT1 without alpha; normal maps plain
    ///   DXT5), named outright, and the UI atlases lose their unneeded mipmaps. Their
    ///   "high quality" compression is BC7, which browsers do not take, so they went into the web build uncompressed:
    ///   a 1920x1080 memo sheet was 10.6 MB instead of about 2 MB. An "automatic" override is not enough, as it still
    ///   follows that high-quality default. Small uncompressed images (icons, under 256 px) stay sharp.
    /// - The ingredient models get Mesh LODs: about 20,000 triangles each, half of the storeroom's, and mostly
    ///   seen small on the shelves. Held up close they are full detail again.
    /// Running it again changes nothing that is already set.
    /// </summary>
    public static class WebAssetOptimizer
    {
        private const string WebGL = "WebGL";
        private const int SmallSide = 256;
        private const int CrunchQuality = 50;

        /// <summary>
        /// Crunched DXT (DXT5 with alpha, DXT1 without): DXT in the browser's memory but far smaller to download.
        /// Normal maps stay plain DXT5, as crunching them shows in the lighting.
        /// </summary>
        private static TextureImporterFormat WebFormat(TextureImporter importer) =>
            importer.textureType == TextureImporterType.NormalMap ? TextureImporterFormat.DXT5
            : importer.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.DXT5Crunched
            : TextureImporterFormat.DXT1Crunched;

        [MenuItem("Potion Shop/Build/Optimize Assets For Web")]
        public static string Run()
        {
            int textures = 0, atlases = 0, models = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                    var web = importer.GetPlatformTextureSettings(WebGL);
                    var wanted = WebFormat(importer);
                    if (web.overridden && web.format == wanted) continue;
                    if (!web.overridden && importer.textureCompression == TextureImporterCompression.Uncompressed && IsSmall(path))
                        continue;
                    web.overridden = true;
                    web.maxTextureSize = importer.maxTextureSize;
                    web.format = wanted;
                    web.textureCompression = TextureImporterCompression.Compressed;
                    web.crunchedCompression = wanted is TextureImporterFormat.DXT5Crunched or TextureImporterFormat.DXT1Crunched;
                    web.compressionQuality = CrunchQuality;
                    importer.SetPlatformTextureSettings(web);
                    importer.SaveAndReimport();
                    textures++;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not SpriteAtlasImporter importer) continue;
                    bool changed = false;
                    // UI is drawn at its own size: its mipmaps were a third more memory and download for nothing
                    var texture = importer.textureSettings;
                    if (Path.GetFileName(path).StartsWith("UI_") && texture.generateMipMaps)
                    {
                        texture.generateMipMaps = false;
                        importer.textureSettings = texture;
                        changed = true;
                    }
                    var web = importer.GetPlatformSettings(WebGL);
                    if (!web.overridden || web.format != TextureImporterFormat.DXT5Crunched)
                    {
                        var standard = importer.GetPlatformSettings("DefaultTexturePlatform");
                        web.overridden = true;
                        web.maxTextureSize = standard.maxTextureSize;
                        web.format = TextureImporterFormat.DXT5Crunched; // sprites have alpha
                        web.textureCompression = TextureImporterCompression.Compressed;
                        web.crunchedCompression = true;
                        web.compressionQuality = CrunchQuality;
                        importer.SetPlatformSettings(web);
                        changed = true;
                    }
                    if (!changed) continue;
                    importer.SaveAndReimport();
                    atlases++;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Arts/Models/Ingredients" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not ModelImporter importer || importer.generateMeshLods) continue;
                    importer.generateMeshLods = true;
                    importer.SaveAndReimport();
                    models++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            string report = $"[WebAssetOptimizer] web compression on {textures} textures and {atlases} atlases, Mesh LODs on {models} models";
            Debug.Log(report);
            return report;
        }

        private static bool IsSmall(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return texture != null && texture.width <= SmallSide && texture.height <= SmallSide;
        }
    }
}
