using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Gives 02_Workshop the "cozy twilight" look: a smaller room, cool violet ambient with warm practical
    /// lights, soft Lit materials tinted to the reference palette, candle clusters and a matching volume.
    /// Every step sets absolute values, so running it again re-applies the same result.
    /// The original toon materials are kept; cozy copies live in Assets/Arts/Materials/Cozy.
    /// </summary>
    public static class CozyWorkshopLook
    {
        private const string ScenePath = "Assets/Scenes/02_Workshop.unity";
        private const string MaterialFolder = "Assets/Arts/Materials/Cozy";
        private const string ProfilePath = "Assets/Settings/Workshop_CozyVolume.asset";
        private const string CandleModel = "Assets/Arts/Models/gg/Candle/Candle.fbx";
        private const string CandleTexture = "Assets/Arts/Models/gg/Candle/Candle_BaseColor.png";
        private const string LightsRoot = "CozyLights";
        private const string CandlesRoot = "CozyCandles";
        private const string CompactMarker = "CozyRoomCompacted";

        // Room ("plan B", chosen 2026-11-23): a long hall 2 × 3 wall segments (≈ 9.3 × 13.9 m) instead of the
        // original 18 m square. The door is in the middle of the near short wall (−z), the fireplace in the
        // middle of the far one (+z); the six ingredient shelves stand along the long walls facing the hall,
        // and the cauldron sits in the middle of the hall. Values are wall centre lines; walls are 0.62 m thick.
        public const float WallX = 4.65f, WallZ = 6.95f;
        public const float FaceX = WallX - 0.31f, FaceZ = WallZ - 0.31f; // inner wall faces
        public const float ShelfX = 3.45f;                                // shelf centre, back 0.29 m off the wall
        public static readonly float[] ShelfRows = { -4f, 0f, 4f };
        public const float CauldronZ = 1f;
        public static readonly Vector3 PlayerStart = new(0f, 0f, -5.3f);
        private const float OriginalWall = 9f;                            // the 18 m room the scene was built as
        private const float SegmentSpacing = 4.64f;

        // name fragment → (reference colour, smoothness, metallic). Texture is kept and multiplied by a
        // lightened version of the colour so grain and bricks stay readable.
        private static readonly (string key, string hex, float smooth, float metal)[] Palette =
        {
            ("roof18_brass", "#e0b46a", 0.55f, 0.6f), ("roof18_pegs", "#d9b071", 0.35f, 0f),
            ("roof18_timber", "#6b4a72", 0.2f, 0f), ("roof18_deck", "#7d6496", 0.2f, 0f),
            ("roof18_lining", "#8c7cb2", 0.2f, 0f), ("roof18_gable", "#7f86c4", 0.15f, 0f),
            ("roof18_endgrain", "#b48a96", 0.2f, 0f), ("roof_interior", "#7d6496", 0.2f, 0f),
            ("roof", "#6b4a72", 0.2f, 0f), ("shelf", "#9a6684", 0.25f, 0f), ("table", "#a0707e", 0.25f, 0f),
            ("floor", "#a87a86", 0.2f, 0f), ("wall", "#8a86c2", 0.15f, 0f), ("corner", "#8a86c2", 0.15f, 0f),
            ("lantern", "#e0b46a", 0.5f, 0.6f), ("campfire", "#7a5260", 0.15f, 0f),
            ("firstpersonarms", "#f0c3b0", 0.3f, 0f), ("ivy", "#5c8f93", 0.25f, 0f),
            ("moss", "#6e9a8c", 0.15f, 0f), ("potioncork", "#c49a7a", 0.2f, 0f),
        };

        // Textures whose own hue fights the palette (red shelf paint, pink-beige bricks) get a cool
        // multiplier instead, which pulls them toward mauve wood and lavender-blue stone.
        private static readonly Dictionary<string, string> DirectTints = new()
        {
            ["shelf"] = "#b4a4e6", ["table"] = "#b4a4e6", ["wall"] = "#9ea6e8", ["corner"] = "#9ea6e8",
        };

        // Simple box furniture whose painted texture stays orange-red under any tint: use a flat warm
        // brown wood colour (keeping the normal map for grain), like the clay furniture in the references.
        // Chosen over mauve (#9a6c82) so the ingredients on the shelves stand out (user, 2026-11-23).
        private static readonly Dictionary<string, string> FlatColors = new()
        {
            ["shelf"] = "#a0704a", ["table"] = "#a0704a",
        };

        [MenuItem("Potion Shop/Art/Apply Cozy Workshop Look")]
        public static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before applying the cozy workshop look.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Debug.Log(Apply());
        }

        public static string Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new System.Text.StringBuilder("[Cozy workshop] ");
            Directory.CreateDirectory(MaterialFolder);

            log.Append(RefreshCozyMaterials()).Append(' ');
            log.Append(RemapPrefabs()).Append(' ');
            log.Append(CompactRoom()).Append(' ');
            log.Append(RemapScene(scene)).Append(' ');
            log.Append(SetupLighting()).Append(' ');
            log.Append(PlaceCandles()).Append(' ');
            log.Append(SetupVolume());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ---------- materials ----------

        private static Material Cozy(Material source)
        {
            if (source == null || source.shader == null || !source.shader.name.Contains("Toon") ||
                source.name.Contains("Cauldron") || source.name.Contains("Glass"))
                return source;

            string path = $"{MaterialFolder}/{source.name}_Cozy.mat";
            var cozy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (cozy == null)
            {
                cozy = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(cozy, path);
            }

            var texture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (texture == null && source.HasProperty("_BaseMap"))
                texture = source.GetTexture("_BaseMap");
            cozy.SetTexture("_BaseMap", texture);

            var normal = source.HasProperty("_NormalMap") ? source.GetTexture("_NormalMap") : null;
            cozy.SetTexture("_BumpMap", normal);
            cozy.SetFloat("_BumpScale", 0.5f);
            if (normal != null) cozy.EnableKeyword("_NORMALMAP");
            else cozy.DisableKeyword("_NORMALMAP");

            string key = source.name.ToLowerInvariant().Replace("m_", "").Replace("_toon", "");
            var entry = System.Array.Find(Palette, e => key.Contains(e.key));
            if (entry.key != null)
            {
                if (FlatColors.TryGetValue(entry.key, out string flat))
                {
                    cozy.SetTexture("_BaseMap", null);
                    cozy.SetColor("_BaseColor", Hex(flat));
                }
                else
                    cozy.SetColor("_BaseColor", DirectTints.TryGetValue(entry.key, out string direct)
                        ? Hex(direct) : Color.Lerp(Hex(entry.hex), Color.white, 0.35f) * 1.15f);
                cozy.SetFloat("_Smoothness", entry.smooth);
                cozy.SetFloat("_Metallic", entry.metal);
            }
            else
            {
                cozy.SetColor("_BaseColor", Color.white * 0.95f);
                cozy.SetFloat("_Smoothness", 0.3f);
                cozy.SetFloat("_Metallic", 0f);
            }
            cozy.SetFloat("_SpecularHighlights", 1f);
            cozy.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(cozy);
            return cozy;
        }

        /// <summary>Re-derives every existing cozy copy from its toon original so palette edits apply on re-run.</summary>
        private static string RefreshCozyMaterials()
        {
            int refreshed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (!name.EndsWith("_Cozy")) continue;
                string original = name[..^"_Cozy".Length];
                foreach (string sourceGuid in AssetDatabase.FindAssets($"{original} t:Material"))
                {
                    var source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(sourceGuid));
                    if (source == null || source.name != original || !source.shader.name.Contains("Toon")) continue;
                    Cozy(source);
                    refreshed++;
                    break;
                }
            }
            return $"refreshed {refreshed};";
        }

        private static bool RemapRenderers(GameObject root)
        {
            bool changed = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var materials = renderer.sharedMaterials;
                bool rendererChanged = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var cozy = Cozy(materials[i]);
                    if (cozy == materials[i]) continue;
                    materials[i] = cozy;
                    rendererChanged = true;
                }
                if (!rendererChanged) continue;
                renderer.sharedMaterials = materials;
                changed = true;
            }
            return changed;
        }

        private static string RemapPrefabs()
        {
            // Innermost first so outer prefabs pick up already-converted nested prefabs.
            string[] folders = { "Assets/Prefabs/Decorations", "Assets/Prefabs/Workshop/IngredientS", "Assets/Prefabs/Workshop" };
            int count = 0;
            var done = new HashSet<string>();
            foreach (string folder in folders)
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!done.Add(path)) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (RemapRenderers(contents))
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        count++;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            return $"prefabs {count};";
        }

        private static string RemapScene(Scene scene)
        {
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                if (RemapRenderers(root)) count++;
            return $"scene roots {count};";
        }

        // ---------- room ----------

        private static string CompactRoom()
        {
            var room = GameObject.Find("Environment/Room")?.transform;
            var shelves = GameObject.Find("Stations/IngredientShelves")?.transform;
            if (room == null || shelves == null) return "room not found;";

            foreach (Transform child in room)
            {
                if (child.name.StartsWith("FrontWall"))
                    LayoutWall(child);
                else if (child.name.StartsWith("GableRoof"))
                    ScaleRoof(child, child.name == "GableRoof" ? 5.5f : 1f);
            }

            // Shelves line the long walls with their open (−z local) side towards the hall.
            int index = 0;
            foreach (Transform shelf in shelves)
            {
                float side = index < 3 ? -1f : 1f;
                shelf.position = new Vector3(side * ShelfX, 0f, ShelfRows[index % 3]);
                shelf.rotation = Quaternion.Euler(0f, side < 0 ? -90f : 90f, 0f);
                index++;
            }

            var cauldron = GameObject.Find("Stations/Cauldron")?.transform;
            if (cauldron != null) cauldron.position = new Vector3(0f, cauldron.position.y, CauldronZ);
            foreach (string name in new[] { "Player", "PlayerCamera", "FirstPersonCinemachineCamera" })
            {
                var t = GameObject.Find(name)?.transform;
                if (t == null) continue;
                t.SetPositionAndRotation(new Vector3(PlayerStart.x, t.position.y, PlayerStart.z), Quaternion.identity);
            }

            // Decorations were scattered for the 18 m square. The marker's scale remembers the factors last
            // applied, so re-running (or changing the room) rescales from the original layout, not compounded.
            var environment = GameObject.Find("Environment").transform;
            var marker = environment.Find(CompactMarker);
            if (marker == null)
            {
                marker = new GameObject(CompactMarker).transform;
                marker.SetParent(environment, false);
            }
            Vector3 previous = marker.localScale;
            var factor = new Vector3(WallX / OriginalWall, 1f, WallZ / OriginalWall);
            var decorations = environment.Find("Decorations_Toon");
            if (decorations != null)
                foreach (Transform group in decorations)
                foreach (Transform item in group)
                {
                    Vector3 p = item.position;
                    item.position = new Vector3(p.x / previous.x * factor.x, p.y, p.z / previous.z * factor.z);
                }
            marker.localScale = factor;
            Physics.SyncTransforms();
            int stuck = SnapIvyToWalls(environment.Find("Decorations_Toon/Ivy"));
            PlaceShelfLanterns(environment);
            return $"room {2 * WallX:F1}×{2 * WallZ:F1} m, ivy snapped {stuck};";
        }

        /// <summary>
        /// Rebuilds one wall from its four segments, three pillars and three lanterns.
        /// Short walls (door / fireplace in the middle): two segments, end pillars only, no lanterns.
        /// Long walls: three seamless segments without pillars; one lantern above the crate gap.
        /// </summary>
        private static void LayoutWall(Transform wall)
        {
            Vector3 inward = wall.rotation * Vector3.forward;
            bool longWall = Mathf.Abs(inward.x) > 0.5f;
            wall.position = new Vector3(inward.x * WallX, 0f, inward.z * WallZ);
            foreach (Transform part in wall)
            {
                Vector3 p = part.localPosition;
                bool on = true;
                switch (part.name)
                {
                    case "Wall (1)": p.x = longWall ? -SegmentSpacing : -SegmentSpacing / 2; break;
                    case "Wall (4)": p.x = longWall ? 0f : SegmentSpacing / 2; break;
                    case "Wall (2)": p.x = SegmentSpacing; on = longWall; break;
                    case "Wall (3)": on = false; break;
                    case "Corner (1)": p.x = -SegmentSpacing; on = !longWall; break;
                    case "Corner (3)": p.x = SegmentSpacing; on = !longWall; break;
                    case "Corner (2)": on = false; break;
                    // The wall's own three lanterns are replaced by PlaceShelfLanterns (one in every gap).
                    case "Lantern": case "Lantern (1)": case "Lantern (2)": on = false; break;
                }
                if (part.name.StartsWith("Wall"))
                    part.localScale = new Vector3(longWall ? 1.04f : 0.91f, part.localScale.y, part.localScale.z);
                part.localPosition = p;
                part.gameObject.SetActive(on);
            }
        }

        // Wall lantern prefab and its mounting relative to the wall's centre line (taken from FrontWall).
        private const string LanternPrefab = "Assets/Prefabs/Workshop/Lantern.prefab";
        private const float LanternHeight = 1.46f, LanternOffset = 0.53f;
        private const string LanternsRoot = "CozyLanterns";

        /// <summary>
        /// A wall lantern in every empty gap beside the shelves, so the ingredients on each shelf are lit from
        /// both ends: the two middle gaps (z ±2) and the two ends (z ±6) of each long wall. In the middle gaps
        /// the lantern sits beside the bookcase (z ±1.55) or above the crate stack (z ±2).
        /// </summary>
        private static void PlaceShelfLanterns(Transform environment)
        {
            var root = Recreate(LanternsRoot, environment);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefab);
            if (prefab == null) return;
            var spots = new (float side, float z)[]
            {
                (-1f, -6.0f), (-1f, -1.55f), (-1f, 2.0f), (-1f, 6.0f),   // west: workbench gap at z −2
                (1f, -6.0f), (1f, -2.0f), (1f, 1.55f), (1f, 6.0f),       // east: crate gaps
            };
            int i = 0;
            foreach (var (side, z) in spots)
            {
                var lantern = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                lantern.name = $"Shelf Lantern {++i}";
                Vector3 inward = new Vector3(-side, 0f, 0f);
                // Match the wall's own lanterns: the FrontWall's forward points out of the room and its lanterns
                // sit at local yaw 270 (west wall lanterns face yaw 180, east 0). Anything else turns the bracket.
                lantern.transform.SetPositionAndRotation(
                    new Vector3(side * WallX + inward.x * LanternOffset, LanternHeight, z),
                    Quaternion.LookRotation(-inward) * Quaternion.Euler(0f, 270f, 0f));
            }
        }

        /// <summary>Scales the gable roof so it spans the room; height follows the short span to keep its pitch.</summary>
        private static void ScaleRoof(Transform roof, float baseScale)
        {
            float fx = WallX / OriginalWall, fz = WallZ / OriginalWall;
            var scale = new Vector3();
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = roof.rotation * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
                float f = Mathf.Abs(axis.z) > 0.7f ? fz : fx; // world x and height use the short span
                scale[i] = baseScale * f;
            }
            roof.localScale = scale;
        }

        /// <summary>Ivy hung on the old walls; after compaction, cast outward and stick it onto the new wall face.</summary>
        private static int SnapIvyToWalls(Transform ivy)
        {
            if (ivy == null) return 0;
            int snapped = 0;
            foreach (Transform item in ivy)
            {
                Vector3 p = item.position;
                Vector3 outward = Mathf.Abs(p.x) > Mathf.Abs(p.z) ? new Vector3(Mathf.Sign(p.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(p.z));
                Vector3 origin = new Vector3(outward.x != 0 ? 0f : p.x, p.y, outward.z != 0 ? 0f : p.z);
                foreach (var hit in Physics.RaycastAll(origin, outward, 12f))
                {
                    if (!hit.collider.transform.IsChildOf(GameObject.Find("Environment/Room").transform)) continue;
                    item.position = hit.point - outward * 0.03f;
                    snapped++;
                    break;
                }
            }
            return snapped;
        }

        // ---------- lighting ----------

        private static string SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#7d86c4");
            RenderSettings.ambientEquatorColor = Hex("#5d5494");
            RenderSettings.ambientGroundColor = Hex("#7a4f6a");
            RenderSettings.ambientIntensity = 1.2f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Hex("#3f467d");
            RenderSettings.fogDensity = 0.03f;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.transform.parent != null && light.transform.parent.name == LightsRoot) continue;
                if (light.type == LightType.Directional)
                {
                    light.color = Hex("#b9b4ff"); light.intensity = 0.45f;
                    light.shadows = LightShadows.Soft; light.shadowStrength = 0.6f;
                }
                else if (light.type == LightType.Spot && light.color.g > 0.9f && light.color.r < 0.1f)
                    light.enabled = false; // stray green test spot under the cauldron
                else if (light.type == LightType.Point && light.name == "Fire Light")
                {
                    light.color = Hex("#ff9a4a"); light.intensity = 6f; light.range = 6.5f;
                    light.shadows = LightShadows.Soft;
                }
            }

            var root = Recreate(LightsRoot, GameObject.Find("Environment").transform);
            int lanterns = 0;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.gameObject.activeInHierarchy || !UsesMaterial(renderer, "Lantern")) continue;
                Vector3 inward = -renderer.bounds.center; inward.y = 0f;
                AddLight(root, $"Lantern Light {++lanterns}", renderer.bounds.center + inward.normalized * 0.25f,
                    "#ffc27a", 2.4f, 4.5f, 0.08f);
            }
            AddLight(root, "Cauldron Glow", new Vector3(0f, 1.35f, CauldronZ), "#8dffb5", 1.4f, 3.2f, 0f);
            return $"lights {lanterns} lanterns;";
        }

        private static string PlaceCandles()
        {
            var root = Recreate(CandlesRoot, GameObject.Find("Environment").transform);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CandleModel);
            if (model == null) return "candle model missing;";

            var wax = MaterialAsset("Candle_Cozy", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CandleTexture));
                m.SetColor("_BaseColor", Hex("#fff1e0")); m.SetFloat("_Smoothness", 0.35f);
            });
            var flame = MaterialAsset("CandleFlame_Cozy", "Universal Render Pipeline/Unlit", m =>
                m.SetColor("_BaseColor", new Color(4f, 2.1f, 0.8f, 1f)));

            // One cluster in each corner, clear of the walls. None by the cauldron: candles next to an open
            // fire read as a hazard, not as cosy.
            float cx = FaceX - 0.4f, cz = FaceZ - 0.4f;
            var clusters = new[]
            {
                new Vector3(-cx, 0f, cz), new Vector3(cx, 0f, cz), new Vector3(-cx, 0f, -cz), new Vector3(cx, 0f, -cz),
            };
            float[] heights = { 0.32f, 0.22f, 0.15f };
            Vector3[] offsets = { Vector3.zero, new(0.13f, 0f, 0.05f), new(-0.05f, 0f, 0.14f) };
            int index = 0;
            foreach (var center in clusters)
            {
                var group = new GameObject($"Candle Cluster {++index}").transform;
                group.SetParent(root, false);
                group.position = center;
                for (int i = 0; i < heights.Length; i++)
                {
                    var candle = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                    candle.name = $"Candle {i + 1}";
                    candle.transform.localPosition = offsets[i];
                    candle.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    // Scale relative to the model's own import scale, then stand it on the floor: the candle's
                    // pivot is at its middle, which buried half of every candle before.
                    Bounds b = Encapsulate(candle);
                    candle.transform.localScale *= heights[i] / Mathf.Max(0.001f, b.size.y);
                    b = Encapsulate(candle);
                    candle.transform.position += Vector3.up * (center.y - b.min.y);
                    b = Encapsulate(candle);
                    foreach (var r in candle.GetComponentsInChildren<Renderer>()) r.sharedMaterial = wax;

                    var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(tip.GetComponent<Collider>());
                    tip.name = "Flame";
                    tip.transform.SetParent(group, false);
                    tip.transform.position = new Vector3(b.center.x, b.max.y + 0.03f, b.center.z);
                    tip.transform.localScale = new Vector3(0.028f, 0.05f, 0.028f);
                    var tipRenderer = tip.GetComponent<MeshRenderer>();
                    tipRenderer.sharedMaterial = flame;
                    tipRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                AddLight(group, "Candle Light", center + Vector3.up * 0.45f, "#ffb466", 1.1f, 2.4f, 0.12f);
            }
            return $"candles {clusters.Length} clusters;";
        }

        // ---------- post ----------

        private static string SetupVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tonemapping = GetOrAdd<Tonemapping>(profile); tonemapping.mode.Override(TonemappingMode.Neutral);
            var adjustments = GetOrAdd<ColorAdjustments>(profile);
            adjustments.postExposure.Override(0.55f); adjustments.contrast.Override(-6f); adjustments.saturation.Override(6f);
            var split = GetOrAdd<SplitToning>(profile);
            split.shadows.Override(Hex("#4a4fa8")); split.highlights.Override(Hex("#ffc89a")); split.balance.Override(-15f);
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.Override(1.0f); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.75f);
            bloom.tint.Override(Hex("#ffd9b0"));
            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.3f); vignette.smoothness.Override(0.5f); vignette.color.Override(Hex("#1b1633"));
            EditorUtility.SetDirty(profile);

            int volumes = 0;
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!volume.isGlobal) continue;
                volume.sharedProfile = profile;
                volumes++;
            }
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                    data.renderPostProcessing = true;
            return $"volume on {volumes};";
        }

        // ---------- helpers ----------

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
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

        private static void AddLight(Transform parent, string name, Vector3 position, string hex, float intensity,
            float range, float flicker)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Hex(hex);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            if (flicker <= 0f) return;
            var flickerComponent = go.AddComponent<FireLightFlicker>();
            var serialized = new SerializedObject(flickerComponent);
            serialized.FindProperty("amplitude").floatValue = flicker;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Recreate(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static bool UsesMaterial(Renderer renderer, string fragment)
        {
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.name.Contains(fragment) && !material.name.Contains("Glass"))
                    return true;
            return false;
        }

        private static Bounds Encapsulate(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
