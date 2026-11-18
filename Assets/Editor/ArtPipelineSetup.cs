using System;
using System.IO;
using System.Linq;
using PotionShop.Art;
using PotionShop.Data;
using PotionShop.Portraits;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PotionShop.Editor
{
    public static class ArtPipelineSetup
    {
        public const string TestScene = "Assets/Scenes/Tests/PortraitTest.unity";
        public const string ViewPrefab = "Assets/Prefabs/Portrait/PortraitView.prefab";
        public static void Set(Object target, string property, Object value)
        { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        public static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        // One-time migration, run deliberately; does not recreate existing artwork/test scenes.
        public static string Install()
        {
            if (EditorApplication.isPlaying || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and exit Play before installing art connections.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var colors = CreateColors();
                var customers = AssetDatabase.LoadAssetAtPath<CustomerCatalog>("Assets/GameData/Customers.asset");
                CreateProfiles(customers);
                var catalog = AssetDatabase.LoadAssetAtPath<PotionCatalog>("Assets/GameData/PotionCatalog.asset");
                Set(catalog, "portraitColors", colors);
                var theme = Asset<UITheme>("Assets/GameData/UITheme.asset");
                Asset<ArtTestSettings>("Assets/Editor/ArtTestSettings.asset");
                var ingredients = catalog.Ingredients;
                foreach (var variant in ingredients.Kinds.SelectMany(k => k.Variants)) NormalizeIngredient(variant);
                CreateView();
                var workshop = EditorSceneManager.OpenScene("Assets/Scenes/02_Workshop.unity", OpenSceneMode.Single);
                foreach (var item in Object.FindObjectsByType<WarehouseIngredient>(FindObjectsSortMode.None))
                {
                    if (item == null) continue;
                    if (item.GetComponentInParent<WorkshopShelf>() == null) continue;
                    if (item.GetComponents<WarehouseIngredient>().Length > 1 && PrefabUtility.IsAddedComponentOverride(item))
                    { PrefabUtility.RevertAddedComponent(item, InteractionMode.AutomatedAction); continue; }
                    var slot = item.transform.parent.GetComponent<IngredientSlot>() ?? item.transform.parent.gameObject.AddComponent<IngredientSlot>();
                    Set(slot, "catalog", ingredients);
                    var so = new SerializedObject(slot); so.FindProperty("variantId").stringValue = item.VariantId; so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
                }
                var interaction = Object.FindFirstObjectByType<WorkshopInteraction>(); Set(interaction, "uiTheme", theme);
                ExtractCanvas(Object.FindFirstObjectByType<Canvas>().gameObject, "Assets/Prefabs/UI/WorkshopCanvas.prefab");
                EditorSceneManager.SaveScene(workshop);
                var order = EditorSceneManager.OpenScene("Assets/Scenes/01_Order.unity", OpenSceneMode.Single);
                var canvas = Object.FindFirstObjectByType<Canvas>();
                var view = canvas.GetComponentInChildren<PortraitView>(true);
                if (view == null)
                {
                    view = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ViewPrefab), canvas.transform)).GetComponent<PortraitView>();
                    var rt = (RectTransform)view.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.55f);
                    rt.sizeDelta = new Vector2(288, 432); rt.anchoredPosition = Vector2.zero; rt.SetAsFirstSibling();
                }
                var orderView = Object.FindFirstObjectByType<OrderSceneView>();
                Set(orderView, "portraitView", view); Set(orderView, "portraitColors", colors);
                ExtractCanvas(canvas.gameObject, "Assets/Prefabs/UI/OrderCanvas.prefab");
                EditorSceneManager.SaveScene(order);
                if (!File.Exists(TestScene)) CreateTest(customers, colors, canvas.GetComponent<CanvasScaler>().referenceResolution);
                AssetDatabase.SaveAssets();
                return "Art connections installed: 18 ingredient prefabs, scene slots, 22 profiles, portrait prefab/test scene, UI prefabs/theme.";
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void ExtractCanvas(GameObject canvas, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (PrefabUtility.IsAnyPrefabInstanceRoot(canvas)) return;
            if (File.Exists(path)) throw new InvalidOperationException("Existing UI prefab needs manual review: " + path);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas, path, InteractionMode.AutomatedAction);
        }

        private static void NormalizeIngredient(IngredientVariant variant)
        {
            var path = AssetDatabase.GetAssetPath(variant.Prefab);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException(variant.Id + ": no prefab");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ingredient = root.GetComponent<WarehouseIngredient>() ?? root.AddComponent<WarehouseIngredient>();
                var so = new SerializedObject(ingredient); so.FindProperty("variantId").stringValue = variant.Id; so.ApplyModifiedPropertiesWithoutUndo();
                if (root.transform.Find("VisualRoot") == null)
                {
                    var children = root.transform.Cast<Transform>().ToArray();
                    var visual = new GameObject("VisualRoot").transform; visual.SetParent(root.transform, false);
                    var collision = new GameObject("InteractionColliders").transform; collision.SetParent(root.transform, false);
                    foreach (var shape in root.GetComponentsInChildren<Collider>(true))
                    {
                        var node = new GameObject(shape.name + "_Hit"); node.layer = shape.gameObject.layer;
                        node.transform.SetParent(collision, false);
                        node.transform.position = shape.transform.position; node.transform.rotation = shape.transform.rotation;
                        node.transform.localScale = shape.transform.lossyScale;
                        UnityEditorInternal.ComponentUtility.CopyComponent(shape);
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(node);
                        Object.DestroyImmediate(shape);
                    }
                    foreach (var child in children) child.SetParent(visual, false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static PortraitColorCatalog CreateColors()
        {
            var asset = Asset<PortraitColorCatalog>("Assets/GameData/PortraitColors.asset");
            var so = new SerializedObject(asset); var list = so.FindProperty("colors");
            if (list.arraySize != 0) return asset;
            string[] values = { "Black:2B2A33", "Brown:7A4A2A", "Red:B8352C", "Gold:D9B34A", "Silver:C9CCD6", "White:EEEAE2", "Blue:3F74C8", "Green:3F9A4A", "Purple:8A4FC0", "Yellow:E0B02C", "Orange:E07A2C" };
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                var pair = values[i].Split(':'); var record = list.GetArrayElementAtIndex(i);
                record.FindPropertyRelative("value").intValue = (int)Enum.Parse<AppearanceValue>(pair[0]);
                ColorUtility.TryParseHtmlString("#" + pair[1], out var color); record.FindPropertyRelative("color").colorValue = color;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); return asset;
        }

        private static void CreateProfiles(CustomerCatalog asset)
        {
            var so = new SerializedObject(asset); var list = so.FindProperty("profiles");
            for (int i = 0; i < list.arraySize; i++)
                if (!list.GetArrayElementAtIndex(i).FindPropertyRelative("fixedAppearance").boolValue)
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("status").intValue = (int)ContentStatus.Retired;
            string[] jobs = { "대장간 견습생", "등불지기", "뱃사공", "도서관 사서", "파이 가게 주인", "양봉가", "떠돌이 음유시인", "시계공", "숲지기", "꽃집 직원", "재단사", "마부", "우편배달부", "행상", "목수", "찻집 직원", "빵집 주인", "경비대원", "정원사", "다리 지킴이", "광산 길잡이", "짐꾼" };
            int job = 0;
            foreach (var race in new[] { "HUM", "ELF", "BST", "GOL" })
            foreach (var variation in race == "GOL" ? new[] { "G1", "G2", "G3", "G4" } : new[] { "M1", "M2", "M3", "F1", "F2", "F3" })
            {
                string id = race + "_" + variation; string occupation = jobs[job++];
                if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)) continue;
                int index = list.arraySize++; var p = list.GetArrayElementAtIndex(index);
                p.FindPropertyRelative("id").stringValue = id; p.FindPropertyRelative("status").intValue = (int)ContentStatus.Active;
                p.FindPropertyRelative("fixedAppearance").boolValue = true;
                p.FindPropertyRelative("parts").arraySize = 0;
                foreach (var field in new[] { "basePortrait", "eyeClosed", "hairFront", "hairBack", "rigPrefab" }) p.FindPropertyRelative(field).objectReferenceValue = null;
                p.FindPropertyRelative("job").stringValue = occupation;
                p.FindPropertyRelative("speciesId").stringValue = "species." + (race == "HUM" ? "human" : race == "ELF" ? "elf" : race == "BST" ? "beastfolk" : "golem");
                p.FindPropertyRelative("gender").intValue = (int)(race == "GOL" ? CustomerGender.Genderless : variation[0] == 'M' ? CustomerGender.Male : CustomerGender.Female);
                p.FindPropertyRelative("earType").intValue = (int)(race == "HUM" ? AppearanceValue.RoundEars : race == "ELF" ? AppearanceValue.PointedEars : race == "BST" ? AppearanceValue.AnimalEars : AppearanceValue.None);
                p.FindPropertyRelative("hasFang").boolValue = new[] { "BST_M1", "BST_M3", "BST_F3", "GOL_G2", "GOL_G4" }.Contains(id);
                SetValues(p.FindPropertyRelative("hairColors"), race == "GOL" ? Array.Empty<AppearanceValue>() : race == "HUM" ? new[] { AppearanceValue.Black, AppearanceValue.Brown, AppearanceValue.Red } : race == "ELF" ? new[] { AppearanceValue.Gold, AppearanceValue.Silver, AppearanceValue.Black } : new[] { AppearanceValue.Brown, AppearanceValue.White, AppearanceValue.Red });
                SetValues(p.FindPropertyRelative("eyeColors"), race == "HUM" ? new[] { AppearanceValue.Brown, AppearanceValue.Blue, AppearanceValue.Green } : race == "ELF" ? new[] { AppearanceValue.Blue, AppearanceValue.Green, AppearanceValue.Purple } : race == "BST" ? new[] { AppearanceValue.Yellow, AppearanceValue.Green } : new[] { AppearanceValue.Blue, AppearanceValue.Orange });
                p.FindPropertyRelative("irisRegions").arraySize = 0; // Artist-specific positions; never guess eye locations.
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetValues(SerializedProperty list, AppearanceValue[] values)
        { list.arraySize = values.Length; for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).intValue = (int)values[i]; }

        private static Image Image(string name, Transform parent)
        {
            var image = new GameObject(name, typeof(RectTransform),
                name is "Base" or "EyeClosed" or "HairFront" or "HairBack" ? typeof(PortraitSkinnedImage) : typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            image.raycastTarget = false; image.enabled = false; return image;
        }
        private static void CreateView()
        {
            if (File.Exists(ViewPrefab)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ViewPrefab));
            var root = new GameObject("PortraitView", typeof(RectTransform));
            try
            {
                var layers = root.AddComponent<PortraitLayers>();
                layers.hairBack = Image("HairBack", root.transform);
                layers.irisLeft = Image("IrisLeft", root.transform); layers.irisRight = Image("IrisRight", root.transform);
                layers.baseImage = Image("Base", root.transform); layers.eyeClosed = Image("EyeClosed", root.transform);
                layers.hairFront = Image("HairFront", root.transform); layers.slime = Image("Slime", root.transform);
                var color = root.AddComponent<PortraitColorModule>(); Set(color, "layers", layers);
                var blink = root.AddComponent<PortraitBlink>(); Set(blink, "layers", layers);
                var rig = root.AddComponent<PortraitRigModule>(); Set(rig, "layers", layers);
                var view = root.AddComponent<PortraitView>(); Set(view, "layers", layers); Set(view, "colors", color); Set(view, "blink", blink);
                Set(view, "rig", rig);
                ((RectTransform)root.transform).sizeDelta = new Vector2(432, 648);
                PrefabUtility.SaveAsPrefabAsset(root, ViewPrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void CreateTest(CustomerCatalog customers, PortraitColorCatalog colors, Vector2 resolution)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TestScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = new GameObject("PortraitTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = resolution;
            var background = Image("Background", canvas.transform); background.enabled = true; background.color = new Color32(39, 49, 50, 255);
            var view = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ViewPrefab), canvas.transform)).GetComponent<PortraitView>();
            var rect = (RectTransform)view.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(resolution.y * .4f, resolution.y * .6f);
            var driver = new GameObject("PortraitTestDriver").AddComponent<PortraitTestDriver>(); driver.customers = customers; driver.palette = colors; driver.view = view;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EditorSceneManager.SaveScene(scene, TestScene);
        }
    }
}
