using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Potion Shop/Build/Optimize Assets For Web: import settings that only the browser build uses, so the desktop
    /// builds look exactly as before.
    /// - Textures and sprite atlases get a WebGL override with DXT5 (DXT1 without alpha), named outright. Their
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
                    // set by an earlier run with an "automatic" format, which still followed the high-quality default
                    bool redo = web.overridden && web.format == TextureImporterFormat.Automatic;
                    if (web.overridden && !redo) continue;
                    if (!redo && importer.textureCompression == TextureImporterCompression.Compressed) continue; // DXT already
                    if (importer.textureCompression == TextureImporterCompression.Uncompressed && IsSmall(path)) continue;
                    web.overridden = true;
                    web.maxTextureSize = importer.maxTextureSize;
                    // named outright: "automatic" here means the importer's own quality, i.e. BC7, which browsers lack
                    web.format = importer.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
                    web.textureCompression = TextureImporterCompression.Compressed;
                    web.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(web);
                    importer.SaveAndReimport();
                    textures++;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not SpriteAtlasImporter importer) continue;
                    var web = importer.GetPlatformSettings(WebGL);
                    if (web.overridden && web.format != TextureImporterFormat.Automatic)
                        continue;
                    var standard = importer.GetPlatformSettings("DefaultTexturePlatform");
                    web.overridden = true;
                    web.maxTextureSize = standard.maxTextureSize;
                    web.format = TextureImporterFormat.DXT5; // sprites have alpha
                    web.textureCompression = TextureImporterCompression.Compressed;
                    web.crunchedCompression = false;
                    importer.SetPlatformSettings(web);
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
