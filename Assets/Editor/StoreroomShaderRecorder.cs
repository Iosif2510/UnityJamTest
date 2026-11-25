using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Potion Shop/Build/Record Storeroom Shaders: while the storeroom is open in play mode (on the web quality level),
    /// saves the shader variants rendered so far to Resources/Shaders/StoreroomVariants for <see cref="ShaderWarmup"/>.
    /// Clear the tracked list first (Clear Tracked Shaders), walk around the storeroom, then record.
    /// </summary>
    public static class StoreroomShaderRecorder
    {
        private const string AssetPath = "Assets/Resources/Shaders/StoreroomVariants.shadervariants";
        private const BindingFlags Internal = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        [MenuItem("Potion Shop/Build/Clear Tracked Shaders")]
        public static void Clear() =>
            typeof(ShaderUtil).GetMethod("ClearCurrentShaderVariantCollection", Internal)?.Invoke(null, null);

        [MenuItem("Potion Shop/Build/Record Storeroom Shaders")]
        public static string Record()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/Shaders");
            var save = typeof(ShaderUtil).GetMethod("SaveCurrentShaderVariantCollection", Internal);
            if (save == null) return "SaveCurrentShaderVariantCollection is not available in this Unity version";
            save.Invoke(null, new object[] { AssetPath });
            AssetDatabase.ImportAsset(AssetPath);
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(AssetPath);
            string report = $"[ShaderRecorder] {collection?.shaderCount} shaders, {collection?.variantCount} variants -> {AssetPath}";
            Debug.Log(report);
            return report;
        }
    }
}
