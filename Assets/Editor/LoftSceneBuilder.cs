using System.IO;
using PotionShop.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Rebuilds the greybox of the narrator's loft in 03_Loft: a room, a desk, the eight memo sheets lying on it,
    /// a seated camera and warm light. Everything it makes lives under one "Loft3D" root, so running it again
    /// replaces only its own objects. Art passes should swap the boxes for models and keep the sheet objects.
    /// </summary>
    public static class LoftSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/03_Loft.unity";
        private const string AssetFolder = "Assets/Arts/Loft";
        private const string RootName = "Loft3D";

        // Sheet size in metres: the memos are 1440 x 2032, shown a little larger than A4 so titles read from the seat.
        private const float SheetWidth = 0.27f;
        private const float SheetHeight = SheetWidth * 2032f / 1440f;
        private const float DeskTop = 0.80f;

        [MenuItem("Potion Shop/Online/Rebuild Loft Greybox")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var config = Resources.Load<OnlineCoopConfig>(OnlineCoopConfig.ResourcePath);
            if (config == null || config.MemoSheets == null || config.MemoSheets.Length == 0)
            {
                Debug.LogError("[Loft] OnlineCoopConfig with memo sheets is required.");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old);
            Directory.CreateDirectory(AssetFolder);

            var root = new GameObject(RootName);
            var wood = Paint("LoftWood", new Color(0.42f, 0.27f, 0.16f));
            var darkWood = Paint("LoftWoodDark", new Color(0.24f, 0.15f, 0.10f));
            var plaster = Paint("LoftPlaster", new Color(0.62f, 0.52f, 0.42f));

            // Room: a small attic. The narrator faces +Z across the desk toward the back wall.
            Box("Floor", root.transform, new Vector3(0f, -0.05f, 0.6f), new Vector3(5f, 0.1f, 5f), darkWood);
            Box("BackWall", root.transform, new Vector3(0f, 1.4f, 1.9f), new Vector3(5f, 2.9f, 0.1f), plaster);
            Box("LeftWall", root.transform, new Vector3(-2.5f, 1.4f, 0.6f), new Vector3(0.1f, 2.9f, 5f), plaster);
            Box("RightWall", root.transform, new Vector3(2.5f, 1.4f, 0.6f), new Vector3(0.1f, 2.9f, 5f), plaster);
            Box("RoofLeft", root.transform, new Vector3(-1.45f, 2.75f, 0.6f), new Vector3(3.4f, 0.1f, 5f), wood)
                .transform.rotation = Quaternion.Euler(0f, 0f, 32f);
            Box("RoofRight", root.transform, new Vector3(1.45f, 2.75f, 0.6f), new Vector3(3.4f, 0.1f, 5f), wood)
                .transform.rotation = Quaternion.Euler(0f, 0f, -32f);

            // Desk.
            var desk = new GameObject("Desk");
            desk.transform.SetParent(root.transform, false);
            Box("Top", desk.transform, new Vector3(0f, DeskTop - 0.03f, 0.75f), new Vector3(2.3f, 0.06f, 1.15f), wood);
            foreach (var corner in new[] { new Vector2(-1.05f, 0.25f), new Vector2(1.05f, 0.25f), new Vector2(-1.05f, 1.25f), new Vector2(1.05f, 1.25f) })
                Box("Leg", desk.transform, new Vector3(corner.x, (DeskTop - 0.06f) * 0.5f, corner.y),
                    new Vector3(0.08f, DeskTop - 0.06f, 0.08f), darkWood);

            // Sheets: two rows of four, each a little askew, as if left in a hurry.
            var sheetsRoot = new GameObject("Sheets");
            sheetsRoot.transform.SetParent(root.transform, false);
            var sheets = new Transform[config.MemoSheets.Length];
            var random = new System.Random(7);
            for (int i = 0; i < sheets.Length; i++)
            {
                int column = i % 4, row = i / 4;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Sheet" + i;
                quad.transform.SetParent(sheetsRoot.transform, false);
                Object.DestroyImmediate(quad.GetComponent<MeshCollider>());
                quad.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.02f);
                quad.transform.localScale = new Vector3(SheetWidth, SheetHeight, 1f);
                float x = (column - 1.5f) * 0.5f + (float)(random.NextDouble() - 0.5) * 0.04f;
                float z = 0.98f - row * 0.46f + (float)(random.NextDouble() - 0.5) * 0.03f;
                // Each sheet sits a hair above the previous one so overlapping corners never flicker.
                quad.transform.position = new Vector3(x, DeskTop + 0.002f + i * 0.0006f, z);
                quad.transform.rotation = Quaternion.Euler(90f, (float)(random.NextDouble() - 0.5) * 16f, 0f);
                quad.GetComponent<MeshRenderer>().sharedMaterial = SheetMaterial(i, config.MemoSheets[i]);
                quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                sheets[i] = quad.transform;
            }

            // Seat: looking down across the desk.
            var cameraObject = GameObject.Find("LoftCamera");
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            if (camera == null)
            {
                Debug.LogError("[Loft] LoftCamera is missing from the scene.");
                return;
            }
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.04f, 0.04f);
            // Sits a little right of centre so the desk shows in the part of the screen the status board leaves free.
            camera.transform.SetPositionAndRotation(new Vector3(0.36f, 1.98f, -0.34f), Quaternion.Euler(52f, 0f, 0f));

            var hold = new GameObject("HoldAnchor").transform;
            hold.SetParent(camera.transform, false);
            hold.localPosition = new Vector3(0f, 0f, 0.42f);
            // A quad faces -Z, so turned to the camera its front is read the right way round.
            hold.localRotation = Quaternion.identity;

            // Light: a lantern above the desk and a little cool fill from a window.
            var lantern = new GameObject("LanternLight").AddComponent<Light>();
            lantern.transform.SetParent(root.transform, false);
            lantern.transform.position = new Vector3(-0.9f, 1.9f, 1.3f);
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.78f, 0.52f);
            lantern.intensity = 1.9f;
            lantern.range = 5f;
            lantern.shadows = LightShadows.Soft;
            var moon = new GameObject("WindowLight").AddComponent<Light>();
            moon.transform.SetParent(root.transform, false);
            moon.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.70f, 0.90f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.16f, 0.14f);

            var loftDesk = root.AddComponent<LoftDesk>();
            var serialized = new SerializedObject(loftDesk);
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("holdAnchor").objectReferenceValue = hold;
            var list = serialized.FindProperty("sheets");
            list.arraySize = sheets.Length;
            for (int i = 0; i < sheets.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = sheets[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Loft] Greybox rebuilt with {sheets.Length} sheets.");
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        private static Material Paint(string name, Color color)
        {
            var material = LoadOrCreate(name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SheetMaterial(int index, Sprite sprite)
        {
            var material = LoadOrCreate("LoftSheet" + index);
            material.SetTexture("_BaseMap", sprite.texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name)
        {
            string path = $"{AssetFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
