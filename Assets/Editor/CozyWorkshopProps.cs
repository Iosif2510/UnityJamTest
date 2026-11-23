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
            // Books were 0.26 m tall (0.74 m wide, giant tomes); 0.18 m reads as ordinary books.
            ["BookStack"] = 0.18f, ["PotionTrio"] = 0.3f, ["HerbBundle"] = 0.55f, ["Pumpkin"] = 0.45f,
            ["WoodenCrate"] = 0.62f, ["ClayJar"] = 0.42f, ["RoundRug"] = 2.6f,
            ["Broom"] = 1.45f, ["HangingPlant"] = 0.7f,
            ["Workbench"] = 0.95f, ["Bookcase"] = 2.05f, ["Bench"] = 0.5f, ["ApothecaryCabinet"] = 1.6f,
            ["WoodenDoor"] = 2.45f, ["Fireplace"] = 1.65f, ["Chandelier"] = 0.75f, ["DryingRack"] = 0.65f,
        };

        // Meshy's smart-topology meshes ship vertex normals smoothed across hard edges (12–77 % of the surface
        // more than 60° off its faces in a 2026-11-23 check), which lights as dark streaks and smears.
        // Unity recalculates them on import: 30° keeps furniture edges crisp, 60° keeps organic props soft.
        // The pumpkin's own normals were already clean (0.7 %), so it keeps them.
        private static readonly Dictionary<string, float> RecalculateNormals = new()
        {
            ["WoodenDoor"] = 30f, ["ApothecaryCabinet"] = 30f, ["Bench"] = 30f, ["Bookcase"] = 30f,
            ["BookStack"] = 30f, ["WoodenCrate"] = 30f, ["Workbench"] = 30f, ["Fireplace"] = 30f,
            ["Chandelier"] = 30f, ["DryingRack"] = 30f, ["RoundRug"] = 30f,
            ["Broom"] = 60f, ["ClayJar"] = 60f, ["HerbBundle"] = 60f, ["HangingPlant"] = 60f, ["PotionTrio"] = 60f,
        };

        private const float RopeTop = 6.5f; // above the roof framing, so ropes disappear into it

        private struct Spot
        {
            public string Prop; public Vector3 Position; public float Yaw; public float Scale; public Vector3 Tilt;
            public string Key;     // name that other spots can sit on
            public string On;      // sit on top of this keyed prop; Position is then a local XZ offset on its top
            public Vector3 Inward; // non-zero: Position is on a wall face; push the prop in until its back touches it
            public bool Hang;      // add a rope from the prop up into the roof
            public float Protrude; // > 0: sink a deep wall prop into the wall so only this much sticks out
            public Spot(string prop, Vector3 position, float yaw = 0f, float scale = 1f, Vector3 tilt = default)
            {
                Prop = prop; Position = position; Yaw = yaw; Scale = scale; Tilt = tilt;
                Key = null; On = null; Inward = default; Hang = false; Protrude = 0f;
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
            var roofColliders = AddRoofColliders();
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
                    if (spot.Protrude > 0f)
                        go.transform.position -= spot.Inward * Mathf.Max(0f, Vector3.Dot(pb.size, axis) - spot.Protrude);
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
            foreach (var c in roofColliders) Object.DestroyImmediate(c);
            AddChandelierFlames(root);
            string lights = FeatureLights(root, keyed);
            lights += $", floor decor cleared {ClearFootprints(root)}";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"[Cozy props] {imported} placed {placed}, missing {missing}, {lights}";
        }

        // Long hall (plan B, see CozyWorkshopLook): inner faces x ±4.34, z ±6.64; shelves along the long walls at
        // x ±3.45, z −4 / 0 / +4 (gaps between them at z ±2), cauldron (0, 0, 1), player starts at (0, 0, −5.3) by
        // the door. Zones: entry (near wall: door, broom, cabinet, crates), hall (shelves, cauldron, racks overhead)
        // and hearth (far wall: fireplace between workbench and bench).
        private static IEnumerable<Spot> Layout()
        {
            const float fx = CozyWorkshopLook.FaceX, fz = CozyWorkshopLook.FaceZ;
            Vector3 fromFar = Vector3.back, fromBack = Vector3.forward, fromEast = Vector3.left, fromWest = Vector3.right;

            // Hearth wall. The fireplace is sunk into the wall (it is 1.3 m deep) and scaled down a little so
            // it sits between the workbench and the bench instead of filling the wall.
            var fireplace = OnWall("Fireplace", fromFar, 0f, 0f, "fireplace", 0.85f);
            fireplace.Protrude = 0.7f;
            yield return fireplace;
            yield return Top("fireplace", "PotionTrio", -0.4f, 0f, 0f, 0.8f);
            yield return Top("fireplace", "BookStack", 0.45f, 0f, 15f);
            // The one bookcase stands right beside the fireplace (a reading corner); the bench on the other
            // side is a real two-seater (seat about 0.6 m wide each) and nothing is put on it.
            yield return OnWall("Bookcase", fromFar, 1.55f, 0f, "bookcase", 1.15f); // x −1.55
            yield return OnWall("Bench", fromFar, -2.3f, 0f, "bench", 2.0f);        // x +2.3
            yield return new Spot("ClayJar", new Vector3(-2.3f, 0f, fz - 0.3f), 15f, 0.9f);
            float face = FarWallFace();
            foreach (float x in new[] { -3.1f, -2.4f, 1.8f, 2.8f })
                yield return new Spot("HerbBundle", new Vector3(x, 1.95f, face - 0.1f), 180f + x * 20f, 0.9f);
            // Pumpkins in the far corner pockets (x 2.85–4.34, z 5.35–6.64), clear of the bench and the
            // corner candles; the right pocket is narrower because of the bench.
            yield return new Spot("Pumpkin", new Vector3(-3.25f, 0f, 5.85f), -30f, 0.9f);
            yield return new Spot("Pumpkin", new Vector3(-3.95f, 0f, 5.6f), -50f, 0.55f);
            yield return new Spot("Pumpkin", new Vector3(3.65f, 0f, 5.8f), 30f, 0.85f);

            // Entry wall. The round door is sunk until only its stone ring stands proud; the player starts on
            // the round mat in front of it, looking down the hall.
            var door = OnWall("WoodenDoor", fromBack, 0f, 0f);
            door.Protrude = 0.5f;
            yield return door;
            yield return new Spot("RoundRug", new Vector3(0f, 0.005f, -5.3f), 0f, 0.6f);
            // Leaning on the wall beside the door, its foot far enough out that the handle rests on the wall.
            yield return new Spot("Broom", new Vector3(-1.55f, 0.03f, -fz + 0.55f), 0f, 1f, new Vector3(-14f, 0f, 0f));
            yield return OnWall("ApothecaryCabinet", fromBack, 2.5f, 0f, "cabinet");  // x +2.5
            yield return Top("cabinet", "PotionTrio", -0.2f, 0f, 200f, 0.9f);
            yield return Top("cabinet", "ClayJar", 0.3f, 0f, 30f, 0.7f);
            // The crate has an open lid with scrolls sticking out, so crates stand side by side, never stacked.
            yield return new Spot("WoodenCrate", new Vector3(-2.35f, 0f, -fz + 0.4f), 6f);
            yield return new Spot("WoodenCrate", new Vector3(-3.1f, 0f, -fz + 0.45f), -14f, 0.85f);
            yield return Hanging("HangingPlant", new Vector3(-3.75f, 2.1f, -6.0f), 40f);
            yield return Hanging("HangingPlant", new Vector3(3.75f, 2.1f, -6.0f), -40f);

            // Long walls: the gaps between the shelves (z ±2, 1.3 m wide) each sit under a lantern. The west
            // near gap holds the workbench (moved off the hearth wall for the bookcase); the others hold a
            // single crate with a jar beside it.
            yield return OnWall("Workbench", fromWest, 2.0f, 0f, "workbench");      // west, z −2
            yield return Top("workbench", "PotionTrio", -0.3f, 0f, 10f);
            yield return Top("workbench", "BookStack", 0.35f, 0.05f, -25f);
            foreach (var (x, z) in new[] { (-1f, 2f), (1f, -2f), (1f, 2f) })
            {
                var c = new Vector3(x * (fx - 0.38f), 0f, z - 0.2f);
                yield return new Spot("WoodenCrate", c, 90f * x + 8f * z);
                yield return new Spot("ClayJar", c + new Vector3(0f, 0f, 0.55f), 20f * z, 0.8f);
            }

            // Overhead: a chandelier above the cauldron (scaled to about 0.9 m across) and herb drying racks
            // over the hall on either side of it (about 1.1 m long).
            yield return Hanging("Chandelier", new Vector3(0f, 2.75f, CozyWorkshopLook.CauldronZ), 0f, 1.75f);
            // Racks hang above the shelf fronts, running across the hall (yaw 0) in the bays between the roof
            // trusses (whose tie beams come down to ~2.9 m at z −6, −4.8…−4, −2.2, −0.6…0.2, 1.8, 3.6…4.6, 5.6),
            // low enough (bottom 2.55 m) to stay under the purlins.
            foreach (float z in new[] { -3.1f, 2.7f })
            foreach (float x in new[] { -2.2f, 2.2f })
                yield return Hanging("DryingRack", new Vector3(x, 2.55f, z), 0f, 1.6f);
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
                if (AssetImporter.GetAtPath($"{ModelFolder}/{name}/{name}.fbx") is ModelImporter model)
                {
                    bool recalc = RecalculateNormals.TryGetValue(name, out float angle);
                    var normals = recalc ? ModelImporterNormals.Calculate : ModelImporterNormals.Import;
                    if (model.materialImportMode != ModelImporterMaterialImportMode.None || model.importAnimation ||
                        model.importNormals != normals || (recalc && !Mathf.Approximately(model.normalSmoothingAngle, angle)))
                    {
                        model.materialImportMode = ModelImporterMaterialImportMode.None;
                        model.importAnimation = false;
                        model.importNormals = normals;
                        if (recalc) model.normalSmoothingAngle = angle;
                        model.SaveAndReimport();
                    }
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
            // The short walls have a seam in the middle (its pillar is hidden behind the door and fireplace),
            // so also try 1 m to either side; the wall face is flat, so the depth found there holds here too.
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            foreach (float shift in new[] { 0f, 1f, -1f })
            foreach (var hit in Physics.RaycastAll(origin + along * shift, outward, 12f))
                if (hit.collider.transform.IsChildOf(room) && Vector3.Dot(hit.point, outward) > 4f)
                {
                    float depth = Vector3.Dot(hit.point, outward);
                    Vector3 p = origin - outward * Vector3.Dot(origin, outward) + outward * depth;
                    return new Vector3(p.x, y, p.z);
                }
            Debug.LogWarning($"[Cozy props] no wall found towards {outward} from {origin}");
            var fallback = origin + outward * 6.1f;
            return new Vector3(fallback.x, y, fallback.z);
        }

        /// <summary>Marks the temporary colliders added to the roof so rope casts stop only at the roof.</summary>
        private sealed class RoofProbe : MonoBehaviour { }

        private static List<Component> AddRoofColliders()
        {
            var added = new List<Component>();
            foreach (Transform child in GameObject.Find("Environment/Room").transform)
            {
                if (!child.name.StartsWith("GableRoof")) continue;
                foreach (var filter in child.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    added.Add(filter.gameObject.AddComponent<RoofProbe>());
                    added.Add(collider);
                }
            }
            Physics.SyncTransforms();
            return added;
        }

        // Candle tips of the Meshy chandelier in its mesh space, found from the cream wax texels of its texture.
        private static readonly Vector3[] ChandelierCandleTips =
        {
            new(-0.00186f, 0.00277f, 0f), new(-0.00101f, 0.0028f, -0.00139f), new(0.00006f, 0.00283f, -0.00171f),
            new(0.00097f, 0.0028f, -0.0013f), new(0.00111f, 0.00275f, 0.00119f), new(0.00013f, 0.00282f, 0.00155f),
            new(-0.00104f, 0.00274f, 0.00136f),
        };

        /// <summary>Lights the chandelier: a small glowing flame on each of its candles.</summary>
        private static void AddChandelierFlames(Transform root)
        {
            var flame = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/CandleFlame_Cozy.mat");
            foreach (Transform prop in root)
            {
                if (!prop.name.StartsWith("Chandelier ") || prop.childCount == 0) continue;
                var model = prop.GetChild(0);
                foreach (var tip in ChandelierCandleTips)
                {
                    var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(f.GetComponent<Collider>());
                    f.name = "Candle Flame";
                    f.transform.SetParent(prop, false);
                    f.transform.position = model.TransformPoint(tip) + Vector3.up * 0.035f;
                    f.transform.localScale = new Vector3(0.03f, 0.055f, 0.03f) / prop.lossyScale.x;
                    var r = f.GetComponent<MeshRenderer>();
                    r.sharedMaterial = flame;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        private static void AddRope(GameObject prop)
        {
            var rope = MaterialAsset("Rope_Cozy", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.36f, 0.29f, 0.27f));
                m.SetFloat("_Smoothness", 0.15f);
            });
            Bounds b = Bounds(prop);
            // Up to the underside of the roof right above (beam, purlin or plank), not through it.
            float top = RopeTop;
            foreach (var hit in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 0.02f, b.center.z), Vector3.up, 8f))
                if (hit.collider.GetComponent<RoofProbe>() != null) top = Mathf.Min(top, hit.point.y);
            float length = top - b.max.y + 0.03f;
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
        /// <summary>
        /// The mushroom and moss decorations were scattered before the furniture existed; hide the ones that
        /// land inside a wall feature or on the floor just in front of it (the fireplace mouth, the door).
        /// All are switched back on first, so moving a feature lets its old spot fill again.
        /// </summary>
        private static int ClearFootprints(Transform root)
        {
            var decorations = GameObject.Find("Environment/Decorations_Toon")?.transform;
            if (decorations == null) return 0;
            var zones = new List<Bounds>();
            foreach (Transform prop in root)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(prop.name,
                        "^(Fireplace|Bookcase|Bench|Workbench|ApothecaryCabinet|WoodenDoor|RoundRug) ")) continue;
                Bounds b = Bounds(prop.gameObject);
                // Reach 0.9 m out from the wall the prop stands against, towards the room centre.
                Vector3 inward = new Vector3(-Mathf.Sign(b.center.x) * (Mathf.Abs(b.center.x) > Mathf.Abs(b.center.z) ? 1 : 0), 0f,
                    -Mathf.Sign(b.center.z) * (Mathf.Abs(b.center.z) >= Mathf.Abs(b.center.x) ? 1 : 0));
                b.Encapsulate(b.center + inward * (Vector3.Dot(b.extents, new Vector3(Mathf.Abs(inward.x), 0, Mathf.Abs(inward.z))) + 0.9f));
                b.Expand(new Vector3(0.3f, 4f, 0.3f));
                zones.Add(b);
            }

            int hidden = 0;
            foreach (string group in new[] { "Mushrooms", "MossPatches" })
            {
                var parent = decorations.Find(group);
                if (parent == null) continue;
                foreach (Transform item in parent)
                {
                    bool blocked = zones.Exists(z => z.Contains(item.position));
                    item.gameObject.SetActive(!blocked);
                    if (blocked) hidden++;
                }
            }
            return hidden;
        }

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
                Vector3 inward = Mathf.Abs(b.center.x) > Mathf.Abs(b.center.z)
                    ? new Vector3(-Mathf.Sign(b.center.x), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(b.center.z));
                Vector3 front = b.center + inward * (Vector3.Dot(b.extents, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))) + 0.4f);
                AddPointLight(root, "Fireplace Light", new Vector3(front.x, 0.55f, front.z),
                    new Color(1f, 0.55f, 0.24f), 6f, 6.5f, 0.22f);
            }
            foreach (Transform child in root)
            {
                if (!child.name.StartsWith("Chandelier")) continue;
                // No shadows: each shadowed point light costs six atlas maps, and the cauldron fire and
                // fireplace already use twelve of them.
                var chandelier = AddPointLight(root, "Chandelier Light", Bounds(child.gameObject).center,
                    new Color(1f, 0.78f, 0.5f), 4.5f, 7.5f, 0.06f);
                chandelier.shadows = LightShadows.None;
                break;
            }
            return $"side lanterns off {off}";
        }

        private static Light AddPointLight(Transform parent, string name, Vector3 position, Color color, float intensity,
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
            if (flicker <= 0f) return light;
            var serialized = new SerializedObject(go.AddComponent<FireLightFlicker>());
            serialized.FindProperty("amplitude").floatValue = flicker;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return light;
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
