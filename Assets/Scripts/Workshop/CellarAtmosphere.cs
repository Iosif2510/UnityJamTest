using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionShop
{
    /// <summary>
    /// Makes the storage room feel like a real cellar around the player, without touching the scene: sounds that come
    /// from where they are and the cellar's own small noises (<see cref="CellarAmbience"/>), dust drifting in the
    /// lamplight (<see cref="DustMotes"/>), a cold mist along the floor that thins by the fire and the cauldron
    /// (<see cref="FloorMist"/>), the cauldron's light taking the brew's colour (<see cref="CauldronGlow"/>), and
    /// flames and hanging herbs stirring as the player walks past (<see cref="AirDraft"/>).
    /// </summary>
    internal static class CellarAtmosphere
    {
        private const string Scene = "02_Workshop";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (scene, _) => Add(scene);
            Add(SceneManager.GetActiveScene());
        }

        private static void Add(Scene scene)
        {
            if (scene.name != Scene) return;
            var root = new GameObject("Cellar Atmosphere");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<CellarAmbience>();
            root.AddComponent<DustMotes>();
            root.AddComponent<FloorMist>();
            root.AddComponent<CauldronGlow>();
            root.AddComponent<AirDraft>();
        }

        /// <summary>The player's eyes (and ears), or null before the player exists.</summary>
        internal static Transform Eyes
        {
            get
            {
                var cam = Camera.main;
                return cam != null ? cam.transform : null;
            }
        }

        /// <summary>A scene object by exact name, or null.</summary>
        internal static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        private static Material particleMaterial;
        private static Texture2D softDot;

        /// <summary>An alpha-blended particle material with a soft round dot (the default is opaque squares).</summary>
        internal static Material ParticleMaterial
        {
            get
            {
                if (particleMaterial != null) return particleMaterial;
                var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", SoftDot);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return particleMaterial = m;
            }
        }

        /// <summary>White, fully opaque in the middle and fading softly to the edge.</summary>
        internal static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                        float a = Mathf.Clamp01(1f - d);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return softDot = tex;
            }
        }

        /// <summary>A particle system with no emission shape, no shadows and the soft material, ready to configure.</summary>
        internal static ParticleSystem NewSystem(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }
    }
}
