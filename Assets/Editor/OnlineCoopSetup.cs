using System.Linq;
using PotionShop.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PotionShop.Editor
{
    /// <summary>Generates the online co-op assets: memo sprites, the shared config, and the loft scene.</summary>
    public static class OnlineCoopSetup
    {
        private const string MemoFolder = "Assets/Arts/Memos";
        private const string ConfigPath = "Assets/Resources/" + OnlineCoopConfig.ResourcePath + ".asset";
        private const string LoftPath = "Assets/Scenes/" + OnlineCoopConfig.LoftScene + ".unity";
        private static readonly string[] MemoOrder =
            { "Cover", "POT_01", "POT_02", "POT_03", "POT_04", "POT_05", "POT_06", "CounterManual" };

        [MenuItem("Potion Shop/Online/Rebuild Co-op Assets")]
        public static void Rebuild()
        {
            var sprites = MemoOrder.Select(ImportMemo).ToArray();
            if (sprites.Any(sprite => sprite == null))
                throw new System.IO.FileNotFoundException("A memo sheet is missing from " + MemoFolder);

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            var config = AssetDatabase.LoadAssetAtPath<OnlineCoopConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<OnlineCoopConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            var serialized = new SerializedObject(config);
            serialized.FindProperty("bodyFont").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Font>("Assets/Arts/Fonts/Maplestory Light.ttf");
            serialized.FindProperty("boldFont").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Font>("Assets/Arts/Fonts/Maplestory Bold.ttf");
            var sheets = serialized.FindProperty("memoSheets");
            sheets.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                sheets.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            if (!System.IO.File.Exists(LoftPath))
                CreateLoftScene();
            RegisterScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Online] Co-op assets rebuilt.");
        }

        private static Sprite ImportMemo(string name)
        {
            string path = $"{MemoFolder}/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                importer.maxTextureSize != 2048)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateLoftScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("LoftCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.11f, 0.08f);
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            new GameObject("LoftRoot").AddComponent<LoftView>();
            EditorSceneManager.SaveScene(scene, LoftPath);
        }

        private static void RegisterScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == LoftPath))
                return;
            scenes.Add(new EditorBuildSettingsScene(LoftPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
