using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// The browser build (WebGL), for Unity Play and for a quick page of our own. Potion Shop/Build/Web Settings puts
    /// the player settings a browser needs in place (also before "Publish to Play"); Potion Shop/Build/WebGL Build
    /// does that and builds to Build/WebGL next to the project. Switching the editor to WebGL re-imports every asset
    /// once, which takes a while.
    /// </summary>
    public static class WebBuild
    {
        public static string OutputFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Build", "WebGL"));

        [MenuItem("Potion Shop/Build/Web Settings")]
        public static void ApplySettings()
        {
            // Brotli files that unpack themselves when the server does not say they are compressed
            // (Unity Play does; a plain file host such as Cloudflare R2 may not)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.runInBackground = true; // the friend keeps hearing and seeing the round in a background tab
            // static batching was on for the desktop only; the storeroom's fixed props need it in a browser too
            SetStaticBatching(BuildTarget.WebGL, true);
            Debug.Log("[WebBuild] Web player settings applied.");
        }

        /// <summary>The per-platform static batching switch (Player Settings > Other), set through its internal API.</summary>
        private static void SetStaticBatching(BuildTarget target, bool on)
        {
            const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var get = typeof(PlayerSettings).GetMethod("GetBatchingForPlatform", any);
            var set = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform", any);
            if (get == null || set == null)
            {
                Debug.LogWarning("[WebBuild] Could not reach the static batching setting; set it by hand for WebGL.");
                return;
            }
            var args = new object[] { target, 0, 0 };
            get.Invoke(null, args);
            set.Invoke(null, new object[] { target, on ? 1 : 0, (int)args[2] });
        }

        [MenuItem("Potion Shop/Build/WebGL Build")]
        public static void Build() => Build(false);

        /// <summary>
        /// Builds from scratch: needed after a change the incremental build does not notice, such as a .jslib plugin
        /// newly marked for WebGL.
        /// </summary>
        [MenuItem("Potion Shop/Build/WebGL Build (Clean)")]
        public static void BuildClean() => Build(true);

        public static void Build(bool clean)
        {
            ApplySettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Could not switch the editor to WebGL (is the WebGL module installed?).");

            var scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputFolder,
                target = BuildTarget.WebGL,
                options = clean ? BuildOptions.CleanBuildCache : BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"[WebBuild] {summary.result} in {summary.totalTime}: {summary.totalSize / (1024 * 1024)} MB at {OutputFolder}");
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL build {summary.result} ({summary.totalErrors} errors)");
        }
    }
}
