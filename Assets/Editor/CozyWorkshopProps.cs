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
            ["WoodenCrate"] = 0.62f, ["ClayJar"] = 0.42f, ["RoundRug"] = 2.6f,
            ["Broom"] = 1.45f, ["HangingPlant"] = 0.7f,
            ["Workbench"] = 0.95f, ["Bookcase"] = 2.05f, ["Bench"] = 0.5f, ["ApothecaryCabinet"] = 1.25f,
            ["WoodenDoor"] = 2.15f, ["Fireplace"] = 1.65f, ["Chandelier"] = 0.75f, ["DryingRack"] = 0.65f,
        };

        private const float RopeTop = 6.5f; // above the roof framing, so ropes disappear into it

        private struct Spot
        {
            public string Prop; public Vector3 Position; public float Yaw; public float Scale; public Vector3 Tilt;
            public string Key;     // name that other spots can sit on
            public string On;      // sit on top of this keyed prop; Position is then a local XZ offset on its top
            public Vector3 Inward; // non-zero: Position is on a wall face; push the prop in until its back touches it
            public bool Hang;      // add a rope from the prop up into the roof
            public Spot(string prop, Vector3 position, float yaw = 0f, float scale = 1f, Vector3 tilt = default)
            {
                Prop = prop; Position = position; Yaw = yaw; Scale = scale; Tilt = tilt;
                Key = null; On = null; Inward = default; Hang = false;
            }
        }

        private static Spot OnWall(string prop, Vector3 inward, float along, float y, string key = null, float scale = 1f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 face = WallFace(-inward, side * along, y);
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
            return new Spot(prop, face, yaw, scale) { Inward = inward, Key = key };
        }

        private static Spot Top(string on, string prop, float x, float z, float yaw, float scale = 1f) =>
            new(prop, new Vector3(x, 0f, z), yaw, scale) { On = on };

        private static Spot Hanging(string prop, Vector3 position, float yaw, float scale = 1f) =>
            new(prop, position, yaw, scale) { Hang = true };

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
            var keyed = new Dictionary<string, GameObject>();
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
                go.transform.rotation = Quaternion.Euler(spot.Tilt.x, spot.Yaw, spot.Tilt.z);
                if (spot.On != null && keyed.TryGetValue(spot.On, out var under))
                {
                    Bounds ub = Bounds(under);
                    Vector3 local = under.transform.rotation * spot.Position;
                    go.transform.position = new Vector3(ub.center.x + local.x, ub.max.y, ub.center.z + local.z);
                }
                else go.transform.position = spot.Position;
                if (spot.Inward != Vector3.zero)
                {
                    // How far the prop reaches behind its pivot along the wall normal, so its back sits on the face.
                    Bounds pb = Bounds(go);
                    var axis = new Vector3(Mathf.Abs(spot.Inward.x), 0f, Mathf.Abs(spot.Inward.z));
                    float back = Vector3.Dot(pb.center - go.transform.position, -spot.Inward) + Vector3.Dot(pb.extents, axis);
                    go.transform.position += spot.Inward * (back + 0.02f);
                }
                if (spot.Key != null) keyed[spot.Key] = go;
                if (spot.Hang) AddRope(go);
                var material = PropMaterial(spot.Prop);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, _ => material);
                    if (spot.Prop == "RoundRug") r.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (spot.On == null && spot.Prop is "WoodenCrate" or "Pumpkin" or "ClayJar" or "Broom" or "Workbench" or
                        "Bookcase" or "Bench" or "ApothecaryCabinet" or "Fireplace")
                    AddCollider(go);
            }

            AddPotionGlows(root);
            string lights = FeatureLights(root, keyed);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"[Cozy props] {imported} placed {placed}, missing {missing}, {lights}";
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

            // The workshop is a cellar: no windows. Herbs hang above the far-wall bench and workbench instead.
            float face = FarWallFace();
            foreach (float x in new[] { -2.75f, -1.85f, 1.85f, 2.75f })
                yield return new Spot("HerbBundle", new Vector3(x, 1.95f, face - 0.1f), 180f + x * 20f, 0.9f);

            // Herb bundles hanging along the side walls, hanging plants in the near corners.
            foreach (float x in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++)
                    yield return new Spot("HerbBundle", new Vector3(x * (w + 0.15f), 2.25f, new[] { -4.3f, -2.6f, 2.6f, 4.3f }[i]), 90f * x + i * 25f, 1f + (i % 2) * 0.2f);
            yield return new Spot("HangingPlant", new Vector3(-w + 0.2f, 2.1f, -w + 0.2f), 40f);
            yield return new Spot("HangingPlant", new Vector3(w - 0.2f, 2.1f, -w + 0.2f), -40f);

            // Potion trio and books by the cauldron.
            yield return new Spot("PotionTrio", new Vector3(-1.25f, 0f, 3.2f), 160f);
            yield return new Spot("BookStack", new Vector3(-1.6f, 0f, 2.6f), 35f, 1.3f);

            // Each wall gets its own feature so the four walls stop repeating.
            Vector3 fromFar = Vector3.back, fromBack = Vector3.forward, fromEast = Vector3.left, fromWest = Vector3.right;
            // Far wall: a bench under one window, a workbench under the other.
            yield return OnWall("Bench", fromFar, 2.3f, 0f, "bench");
            yield return Top("bench", "BookStack", -0.35f, 0f, 20f, 1.1f);
            yield return Top("bench", "ClayJar", 0.4f, 0f, 0f, 0.8f);
            yield return OnWall("Workbench", fromFar, -2.3f, 0f, "workbench");
            yield return Top("workbench", "PotionTrio", -0.35f, 0f, 10f);
            yield return Top("workbench", "BookStack", 0.35f, 0.05f, -25f, 0.9f);
            // Back wall (behind the starting point): the door and a drawer cabinet.
            yield return OnWall("WoodenDoor", fromBack, 2.3f, 0f);
            yield return OnWall("ApothecaryCabinet", fromBack, -2.3f, 0f, "cabinet");
            yield return Top("cabinet", "PotionTrio", -0.2f, 0f, 200f, 0.9f);
            yield return Top("cabinet", "ClayJar", 0.3f, 0f, 30f, 0.7f);
            // East wall: a bookcase. West wall: the fireplace.
            yield return OnWall("Bookcase", fromEast, 0f, 0f, "bookcase");
            yield return OnWall("Fireplace", fromWest, 0f, 0f, "fireplace");
            yield return Top("fireplace", "PotionTrio", -0.35f, 0f, 0f, 0.8f);
            yield return Top("fireplace", "BookStack", 0.4f, 0f, 15f, 0.8f);

            // Overhead: a chandelier above the cauldron and drying racks over the aisles.
            yield return Hanging("Chandelier", new Vector3(0f, 2.75f, 2f), 0f);
            yield return Hanging("DryingRack", new Vector3(-1.9f, 2.7f, -1.2f), 90f);
            yield return Hanging("DryingRack", new Vector3(1.9f, 2.7f, -1.2f), 90f, 1.1f);
            yield return Hanging("DryingRack", new Vector3(0f, 2.85f, -4.6f), 0f);
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

        /// <summary>Point on the inner face of the wall that lies in <paramref name="outward"/>, cast from <paramref name="offset"/>.</summary>
        private static Vector3 WallFace(Vector3 outward, Vector3 offset, float y)
        {
            Physics.SyncTransforms();
            var room = GameObject.Find("Environment/Room").transform;
            var origin = new Vector3(offset.x, 1.2f, offset.z);
            foreach (var hit in Physics.RaycastAll(origin, outward, 12f))
                if (hit.collider.transform.IsChildOf(room) && Vector3.Dot(hit.point, outward) > 4f)
                    return new Vector3(hit.point.x, y, hit.point.z);
            var fallback = origin + outward * 6.1f;
            return new Vector3(fallback.x, y, fallback.z);
        }

        private static void AddRope(GameObject prop)
        {
            var rope = MaterialAsset("Rope_Cozy", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.36f, 0.29f, 0.27f));
                m.SetFloat("_Smoothness", 0.15f);
            });
            Bounds b = Bounds(prop);
            float length = RopeTop - b.max.y;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Rope";
            go.transform.SetParent(prop.transform, false);
            go.transform.position = new Vector3(b.center.x, b.max.y - 0.03f + length / 2f, b.center.z);
            go.transform.rotation = Quaternion.identity;
            Vector3 s = prop.transform.lossyScale;
            go.transform.localScale = new Vector3(0.018f / s.x, length / 2f / s.y, 0.018f / s.z);
            go.GetComponent<MeshRenderer>().sharedMaterial = rope;
        }

        /// <summary>Strong warm pools at the fireplace and chandelier; the side-wall lanterns they cover are switched off.</summary>
        private static string FeatureLights(Transform root, Dictionary<string, GameObject> keyed)
        {
            int off = 0;
            var room = GameObject.Find("Environment/Room").transform;
            var cozyLights = GameObject.Find("Environment/CozyLights")?.transform;
            foreach (string wall in new[] { "FrontWall (2)", "FrontWall (3)" })
            {
                var lantern = room.Find(wall)?.Find("Lantern");
                if (lantern == null) continue;
                Vector3 at = Bounds(lantern.gameObject).center;
                lantern.gameObject.SetActive(false);
                if (cozyLights != null)
                    foreach (Transform light in cozyLights)
                        if (Vector3.Distance(light.position, at) < 1.5f) light.gameObject.SetActive(false);
                off++;
            }
            if (keyed.TryGetValue("fireplace", out var fireplace))
            {
                Bounds b = Bounds(fireplace);
                AddPointLight(root, "Fireplace Light", new Vector3(b.max.x + 0.4f, 0.55f, b.center.z),
                    new Color(1f, 0.55f, 0.24f), 6f, 6.5f, 0.22f);
            }
            foreach (Transform child in root)
            {
                if (!child.name.StartsWith("Chandelier")) continue;
                AddPointLight(root, "Chandelier Light", Bounds(child.gameObject).center, new Color(1f, 0.78f, 0.5f), 3.5f, 7f, 0.06f);
                break;
            }
            return $"side lanterns off {off}";
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position, Color color, float intensity,
            float range, float flicker)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            if (flicker <= 0f) return;
            var serialized = new SerializedObject(go.AddComponent<FireLightFlicker>());
            serialized.FindProperty("amplitude").floatValue = flicker;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

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
