using UnityEditor;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>Background music (Resources/Music) streams from disk instead of sitting decompressed in memory.</summary>
    public sealed class MusicImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Music/")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
        }
    }
}
