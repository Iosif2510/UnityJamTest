using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Replaces the cauldron's flipbook fire with a stylized one: a ring of large toon flame cards
    /// (PotionShop/StylizedFlame), a warm glow pool on the floor and rising embers (PotionShop/SoftGlow).
    /// The old VFX component is only disabled, so its audio source keeps playing. Re-running rebuilds it.
    /// </summary>
    public static class CozyCauldronFire
    {
        private const string ScenePath = "Assets/Scenes/02_Workshop.unity";
        private const string MaterialFolder = "Assets/Arts/Materials/Cozy";
        private const string Root = "StylizedFire";

        [MenuItem("Potion Shop/Art/Rebuild Cauldron Fire")]
        public static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Debug.Log(Rebuild());
        }

        public static string Rebuild()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cauldron = GameObject.Find("Stations/Cauldron").transform;
            var oldFire = cauldron.Find("CauldronFire");
            if (oldFire != null && oldFire.TryGetComponent<VisualEffect>(out var vfx)) vfx.enabled = false;

            var existing = cauldron.Find(Root);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var root = new GameObject(Root).transform;
            root.SetParent(cauldron, false);
            root.position = new Vector3(cauldron.position.x, 0f, oldFire != null ? oldFire.position.z : cauldron.position.z);

            // Mostly orange with a small warm core: bright enough for a touch of bloom without washing to white.
            var flame = MaterialAsset("StylizedFlame_Cozy", "PotionShop/StylizedFlame", m =>
            {
                m.SetColor("_CoreColor", new Color(1.55f, 1.15f, 0.55f));
                m.SetColor("_MidColor", new Color(1.35f, 0.52f, 0.14f));
                m.SetColor("_EdgeColor", new Color(0.95f, 0.2f, 0.08f));
                m.SetColor("_TipColor", new Color(0.8f, 0.14f, 0.28f));
                m.SetFloat("_Erosion", 0.6f);
                m.SetFloat("_Sway", 0.35f);
                m.SetFloat("_Fps", 10f);
            });
            var glow = MaterialAsset("FireGlow_Cozy", "PotionShop/SoftGlow", m =>
            {
                m.SetColor("_Color", new Color(1.2f, 0.5f, 0.2f, 0.45f));
                m.SetFloat("_Softness", 2.2f);
            });
            var ember = MaterialAsset("FireEmber_Cozy", "PotionShop/SoftGlow", m =>
            {
                m.SetColor("_Color", new Color(2.4f, 1.3f, 0.5f, 1f));
                m.SetFloat("_Softness", 1.2f);
            });

            // Measured: the pot's bottom is at 0.18 m, its body about 0.69 m in radius, and the liquid's interior
            // volume 0.64 m in radius from 0.25 m up; a flame inside that volume shows through the open top.
            // So: low flames under the pot, between the logs, stay below 0.25 m; licking flames hug the belly
            // just outside 0.64 m instead of standing out in a wide ring around it.
            int cards = 0;
            const int under = 7;
            for (int i = 0; i < under; i++)
            {
                float angle = i * Mathf.PI * 2f / under + 0.15f;
                AddFlame(root, flame, new Vector3(Mathf.Cos(angle) * 0.42f, 0.02f, Mathf.Sin(angle) * 0.42f),
                    0.3f, 0.22f, 40f + i * 1.91f);
                cards++;
            }
            const int ring = 8;
            for (int i = 0; i < ring; i++)
            {
                float angle = i * Mathf.PI * 2f / ring + 0.3f;
                bool big = i % 2 == 0;
                float height = big ? 0.42f : 0.3f;
                float width = big ? 0.28f : 0.22f;
                AddFlame(root, flame, new Vector3(Mathf.Cos(angle) * 0.68f, 0.03f, Mathf.Sin(angle) * 0.68f),
                    width, height, i * 1.37f);
                cards++;
            }

            AddGlowPool(root, glow);
            AddEmbers(root, ember);
            string hearth = BuildHearthFire(flame);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"[Cauldron fire] {cards} flame cards, glow pool, embers; old VFX disabled: {oldFire != null}; {hearth}";
        }

        /// <summary>Three flame cards on the logs in the fireplace mouth (the fireplace comes from CozyWorkshopProps).</summary>
        private static string BuildHearthFire(Material flame)
        {
            var props = GameObject.Find("Environment/CozyProps")?.transform;
            if (props == null) return "no props for a hearth fire";
            Transform fireplace = null;
            // "Fireplace Light" shares the prefix; the prop is the one with renderers.
            foreach (Transform t in props)
                if (t.name.StartsWith("Fireplace ") && t.GetComponentInChildren<Renderer>() != null) fireplace = t;
            if (fireplace == null) return "no fireplace";
            var old = fireplace.Find("HearthFire");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var renderers = fireplace.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var root = new GameObject("HearthFire").transform;
            root.SetParent(fireplace, true);
            // The mouth is centred on the fireplace, 0.3 m in from its front face (most of the model is sunk into
            // the wall, so its bounds centre lies behind the mouth), above the hearth stones. Any further forward
            // and the flame tips stick out past the stone face when seen from the side.
            float inward = -Mathf.Sign(b.center.z);
            float frontZ = b.center.z + inward * b.extents.z;
            root.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.13f, frontZ - inward * 0.3f);
            root.rotation = Quaternion.identity;
            float s = b.size.y / 1.4f;
            AddFlame(root, flame, new Vector3(-0.22f * s, 0f, 0f), 0.36f * s, 0.5f * s, 21.3f);
            AddFlame(root, flame, new Vector3(0f, 0f, 0.05f), 0.46f * s, 0.68f * s, 23.9f);
            AddFlame(root, flame, new Vector3(0.24f * s, 0f, 0f), 0.34f * s, 0.46f * s, 27.1f);
            return "hearth fire 3 cards";
        }

        private static void AddFlame(Transform root, Material material, Vector3 local, float width, float height, float seed)
        {
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(card.GetComponent<Collider>());
            card.name = "Flame";
            card.transform.SetParent(root, false);
            // The quad is centred on its pivot, so lift it by half its height to stand on the logs.
            card.transform.localPosition = local + Vector3.up * height * 0.5f;
            card.transform.localScale = new Vector3(width, height, 1f);
            var renderer = card.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock(); // each card flickers on its own phase
            block.SetFloat("_Seed", seed);
            renderer.SetPropertyBlock(block);
        }

        private static void AddGlowPool(Transform root, Material material)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(pool.GetComponent<Collider>());
            pool.name = "Glow Pool";
            pool.transform.SetParent(root, false);
            pool.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pool.transform.localScale = Vector3.one * 2.4f;
            var renderer = pool.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void AddEmbers(Transform root, Material material)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            main.duration = 4f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.85f, 0.45f));
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;

            var emission = system.emission;
            emission.rateOverTime = 11f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.62f;
            shape.radiusThickness = 0.35f; // from the rim, where the flames are

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.9f;
            noise.scrollSpeed = 0.6f;

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material MaterialAsset(string name, string shader, System.Action<Material> configure = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = Shader.Find(shader);
            configure?.Invoke(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
