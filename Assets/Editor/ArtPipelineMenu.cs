using PotionShop.Art;
using PotionShop.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PotionShop.Editor
{
    public static class ArtPipelineMenu
    {
        private static void Select(string path)
        { Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path); EditorGUIUtility.PingObject(Selection.activeObject); }
        [MenuItem("Potion Shop/Art/Ingredients")] public static void Ingredients() => Select("Assets/GameData/Ingredients.asset");
        [MenuItem("Potion Shop/Art/Customers")] public static void Customers() => Select("Assets/GameData/Customers.asset");
        [MenuItem("Potion Shop/Art/Portrait Colors")] public static void Colors() => Select("Assets/GameData/PortraitColors.asset");
        [MenuItem("Potion Shop/Art/Shared UI Theme")] public static void Theme() => Select("Assets/GameData/UITheme.asset");
        [MenuItem("Potion Shop/Art/Test Settings")] public static void Settings() => Select("Assets/Editor/ArtTestSettings.asset");
        [MenuItem("Potion Shop/Art/Open Counter")] public static void Counter() => Open("Assets/Scenes/01_Order.unity");
        [MenuItem("Potion Shop/Art/Open Workshop")] public static void Workshop() => Open("Assets/Scenes/02_Workshop.unity");
        [MenuItem("Potion Shop/Art/Open Portrait Test")] public static void PortraitTest() => Open(ArtPipelineSetup.TestScene);
        private static void Open(string path)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play before changing editing scenes."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
