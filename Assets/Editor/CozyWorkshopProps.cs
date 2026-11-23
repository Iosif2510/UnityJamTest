using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Imports the cozy Meshy props (Build/meshy/&lt;Name&gt; → Assets/Arts/Models/Cozy/&lt;Name&gt;) and lays them out
    /// in 02_Workshop under Environment/CozyProps. Re-running rebuilds that root from scratch.
    /// Layout assumes the ~13 m room from <see cref="CozyWorkshopLook"/> (walls ±6.4, shelves ±3.8).
    /// </summary>
    public static class CozyWorkshopProps
    {
        private const string ScenePath = "Assets/Scenes/02_Workshop.unity";
        private const string SourceFolder = "../Build/meshy";
        private const string ModelFolder = "Assets/Arts/Models/Cozy";
        private const string MaterialFolder = "Assets/Arts/Materials/Cozy";
        private const string Root = "CozyProps";

        // name → height (or diameter for the rug) the model is normalised to, in metres.
        private static readonly Dictionary<string, float> Sizes = new()
        {
            ["BookStack"] = 0.26f, ["PotionTrio"] = 0.3f, ["HerbBundle"] = 0.55f, ["Pumpkin"] = 0.45f,
            ["WoodenCrate"] = 0.62f, ["ClayJar"] = 0.42f, ["RoundRug"] = 2.6f, ["ArchedWindow"] = 1.7f,
            ["Broom"] = 1.45f, ["HangingPlant"] = 0.7f,
        };

        private struct Spot
        {
            public string Prop; public Vector3 Position; public float Yaw; public float Scale; public Vector3 Tilt;
            public Spot(string prop, Vector3 position, float yaw = 0f, float scale = 1f, Vector3 tilt = default)
            { Prop = prop; Position = position; Yaw = yaw; Scale = scale; Tilt = tilt; }
        }

        [MenuItem("Potion Shop/Art/Place Cozy Workshop Props")]
        public static void PlaceFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Debug.Log(Place());
        }

        public static string Place()
        {
            string imported = Import();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var environment = GameObject.Find("Environment").transform;
            var old = environment.Find(Root);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(Root).transform;
            root.SetParent(environment, false);

            int placed = 0, missing = 0;
            foreach (var spot in Layout())
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{spot.Prop}/{spot.Prop}.fbx");
                if (model == null) { missing++; continue; }
                // Meshy FBX roots carry a 100x unit scale, so scale a wrapper instead of the model itself.
                var go = new GameObject($"{spot.Prop} {++placed}");
                go.transform.SetParent(root, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
                instance.transform.localPosition = Vector3.zero;
                Normalise(go, spot.Prop, spot.Scale);
                go.transform.position = spot.Position;
                go.transform.rotation = Quaternion.Euler(spot.Tilt.x, spot.Yaw, spot.Tilt.z);
                var material = PropMaterial(spot.Prop);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, _ => material);
                    if (spot.Prop == "RoundRug") r.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (spot.Prop is "WoodenCrate" or "Pumpkin" or "ClayJar" or "Broom") AddCollider(go);
            }

            AddWindowLight(root);
            AddPotionGlows(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"[Cozy props] {imported} placed {placed}, missing {missing}";
        }

        // Room: walls ±6.4 (inner face ≈ ±6.15), shelves at x ±3.8, z −3.8/0/3.8, cauldron (0, 0, 2), player starts (0, 0, −3) facing +z.
        private static IEnumerable<Spot> Layout()
        {
            const float w = 5.75f;
            // Rug under the cauldron and a broom leaning beside it.
            yield return new Spot("RoundRug", new Vector3(0f, 0.005f, 2f));
            yield return new Spot("Broom", new Vector3(1.45f, 0f, 3.05f), 200f, 1f, new Vector3(-12f, 0f, 6f));

            // Pumpkin heaps in the four corners, by the candle clusters.
            foreach (var (x, z) in new[] { (-1f, 1f), (1f, 1f), (-1f, -1f), (1f, -1f) })
            {
                var c = new Vector3(x * (w - 0.1f), 0f, z * (w - 0.1f));
                yield return new Spot("Pumpkin", c + new Vector3(-x * 0.55f, 0f, 0f), 30f * x, 1.25f);
                yield return new Spot("Pumpkin", c + new Vector3(0f, 0f, -z * 0.6f), -50f, 0.8f);
                yield return new Spot("Pumpkin", c + new Vector3(-x * 0.2f, 0f, -z * 0.95f), 110f, 0.6f);
            }

            // Crate stacks with jars and books between the shelf backs and the side walls.
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1.9f, 1.9f })
            {
                var c = new Vector3(x * w, 0f, z);
                yield return new Spot("WoodenCrate", c, 90f * x + 8f * z);
                yield return new Spot("WoodenCrate", c + new Vector3(0f, 0.62f, 0f), 90f * x - 12f, 0.8f);
                yield return new Spot("ClayJar", c + new Vector3(-x * 0.1f, 0f, z > 0 ? -0.62f : 0.62f), 20f, z > 0 ? 1f : 0.8f);
                yield return new Spot("BookStack", c + new Vector3(0f, 0f, z > 0 ? 0.65f : -0.65f), 70f * z);
            }

            // Arched windows on the far wall either side of the lantern, with a potted herb sill.
            float face = FarWallFace();
            yield return new Spot("ArchedWindow", new Vector3(-2.3f, 1.0f, face - 0.12f), 180f);
            yield return new Spot("ArchedWindow", new Vector3(2.3f, 1.0f, face - 0.12f), 180f);

            // Herb bundles hanging along the side walls, hanging plants in the near corners.
            foreach (float x in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++)
                    yield return new Spot("HerbBundle", new Vector3(x * (w + 0.15f), 2.25f, -3.9f + i * 2.6f), 90f * x + i * 25f, 1f + (i % 2) * 0.2f);
            yield return new Spot("HangingPlant", new Vector3(-w + 0.2f, 2.1f, -w + 0.2f), 40f);
            yield return new Spot("HangingPlant", new Vector3(w - 0.2f, 2.1f, -w + 0.2f), -40f);

            // Potion trios and books by the cauldron and on the floor along the far wall.
            yield return new Spot("PotionTrio", new Vector3(-1.25f, 0f, 3.2f), 160f);
            yield return new Spot("BookStack", new Vector3(-1.6f, 0f, 2.6f), 35f, 1.3f);
            yield return new Spot("PotionTrio", new Vector3(-4.6f, 0f, w - 0.1f), 190f);
            yield return new Spot("PotionTrio", new Vector3(4.6f, 0f, w - 0.1f), 170f, 0.9f);
            yield return new Spot("BookStack", new Vector3(0.9f, 0f, w - 0.1f), 10f, 1.4f);
        }

        // ---------- import ----------

        private static string Import()
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourceFolder));
            int copied = 0;
            foreach (string name in Sizes.Keys)
            {
                string from = Path.Combine(source, name);
                if (!Directory.Exists(from)) continue;
                string to = Path.Combine(ModelFolder, name);
                Directory.CreateDirectory(to);
                foreach (string file in new[] { $"{name}.fbx", $"{name}_base_color.png", $"{name}_normal.png" })
                {
                    string src = Path.Combine(from, file), dst = Path.Combine(to, file);
                    if (!File.Exists(src) || File.Exists(dst)) continue;
                    File.Copy(src, dst);
                    copied++;
                }
            }
            AssetDatabase.Refresh();

            foreach (string name in Sizes.Keys)
            {
                if (AssetImporter.GetAtPath($"{ModelFolder}/{name}/{name}.fbx") is ModelImporter model &&
                    (model.materialImportMode != ModelImporterMaterialImportMode.None || model.importAnimation))
                {
                    model.materialImportMode = ModelImporterMaterialImportMode.None;
                    model.importAnimation = false;
                    model.SaveAndReimport();
                }
                if (AssetImporter.GetAtPath($"{ModelFolder}/{name}/{name}_normal.png") is TextureImporter normal &&
                    normal.textureType != TextureImporterType.NormalMap)
                {
                    normal.textureType = TextureImporterType.NormalMap;
                    normal.maxTextureSize = 1024;
                    normal.SaveAndReimport();
                }
                if (AssetImporter.GetAtPath($"{ModelFolder}/{name}/{name}_base_color.png") is TextureImporter color &&
                    color.maxTextureSize != 1024)
                {
                    color.maxTextureSize = 1024;
                    color.SaveAndReimport();
                }
            }
            return $"copied {copied} files,";
        }

        private static Material PropMaterial(string prop)
        {
            string path = $"{MaterialFolder}/Prop_{prop}_Cozy.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelFolder}/{prop}/{prop}_base_color.png"));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelFolder}/{prop}/{prop}_normal.png");
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.5f);
            if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", prop == "PotionTrio" ? 0.6f : 0.25f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- helpers ----------

        /// <summary>Z of the far (+z) wall's inner face, found by casting from the room centre.</summary>
        private static float FarWallFace()
        {
            Physics.SyncTransforms();
            var room = GameObject.Find("Environment/Room").transform;
            foreach (var hit in Physics.RaycastAll(new Vector3(2.3f, 1.8f, 0f), Vector3.forward, 12f))
                if (hit.collider.transform.IsChildOf(room) && hit.point.z > 4f)
                    return hit.point.z;
            return 6.1f;
        }

        private static void Normalise(GameObject go, string prop, float scale)
        {
            go.transform.localScale = Vector3.one;
            Bounds b = Bounds(go);
            float measured = prop == "RoundRug" ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
            go.transform.localScale = Vector3.one * (Sizes[prop] * scale / Mathf.Max(0.001f, measured));
            // Sit the model on the wrapper's origin (Meshy pivots are at the bottom, but not always exactly).
            var model = go.transform.GetChild(0);
            Bounds placed = Bounds(go);
            model.position += new Vector3(go.transform.position.x - placed.center.x, go.transform.position.y - placed.min.y, go.transform.position.z - placed.center.z);
        }

        private static void AddCollider(GameObject go)
        {
            Bounds b = Bounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            box.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
        }

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Cool moonlight through the far-wall windows: an emissive pane and a soft spot shining in.</summary>
        private static void AddWindowLight(Transform root)
        {
            var pane = MaterialAsset("WindowMoonlight_Cozy", "Universal Render Pipeline/Unlit",
                m => m.SetColor("_BaseColor", new Color(0.55f, 0.6f, 1.5f, 1f)));
            foreach (float x in new[] { -2.3f, 2.3f })
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = "Window Moonlight";
                quad.transform.SetParent(root, false);
                quad.transform.position = new Vector3(x, 1.85f, FarWallFace() - 0.02f);
                quad.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                quad.transform.localScale = new Vector3(0.95f, 1.45f, 1f);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = pane; r.shadowCastingMode = ShadowCastingMode.Off;

                var light = new GameObject("Moon Spot").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.position = new Vector3(x, 2.6f, 6.0f);
                light.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.75f, -1f));
                light.type = LightType.Spot; light.spotAngle = 60f; light.range = 7f;
                light.color = new Color(0.62f, 0.66f, 1f); light.intensity = 3f; light.shadows = LightShadows.Soft;
            }
        }

        /// <summary>Faint pastel glow beside each potion trio so the bottles read as magic.</summary>
        private static void AddPotionGlows(Transform root)
        {
            var colors = new[] { new Color(1f, 0.55f, 0.85f), new Color(0.55f, 1f, 0.8f), new Color(0.75f, 0.6f, 1f) };
            int i = 0;
            foreach (Transform child in root)
            {
                if (!child.name.StartsWith("PotionTrio")) continue;
                var light = new GameObject("Potion Glow").AddComponent<Light>();
                light.transform.SetParent(child, true);
                light.transform.position = child.position + Vector3.up * 0.22f;
                light.type = LightType.Point; light.range = 1.4f; light.intensity = 0.9f;
                light.color = colors[i++ % colors.Length]; light.shadows = LightShadows.None;
            }
        }

        private static Material MaterialAsset(string name, string shader, System.Action<Material> configure)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
