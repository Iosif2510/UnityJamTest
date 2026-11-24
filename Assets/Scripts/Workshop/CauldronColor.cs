using System.Collections.Generic;
using LitMotion;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The brew takes on each ingredient's colour as it lands: the liquid leans towards it, and when the last one
    /// goes in the colours swirl through each other before settling on the finished potion's colour. Works through
    /// the liquid's property block, like <see cref="LiquidMotion"/>, so the shared material is never touched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CauldronColor : MonoBehaviour
    {
        private const float LeanSeconds = 0.5f;
        private const float SwirlSeconds = 1.6f;

        private static readonly int LiquidColor = Shader.PropertyToID("_LiquidColor");
        private static readonly int TopColor = Shader.PropertyToID("_LiquidTopColor");
        private static readonly Dictionary<string, Color> sampled = new();

        private Renderer liquid;
        private MaterialPropertyBlock block;
        private Color baseLiquid;
        private float topBrightness = 1f;
        private readonly List<Color> added = new();
        private MotionHandle motion;

        /// <summary>The finished colour of the last brew made, carried to the counter to fill the bottle.</summary>
        public static Color? LastBrew { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LastBrew = null; // no domain reload between play sessions

        /// <summary>The brew a friend made downstairs (co-op replay), so the bottle fills with its colour here too.</summary>
        internal static void ShowBrew(Color? brew) => LastBrew = brew is { a: > 0f } ? brew : null;

        /// <summary>The liquid's colour right now (the progress ring borrows it).</summary>
        public Color Current { get; private set; }

        /// <summary>The colour the last ingredient brought (the splash takes it), brightened to read as droplets.</summary>
        public Color? LastTint => added.Count == 0 ? null : Bright(added[^1]);

        private static Color Bright(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out _);
            var b = Color.HSVToRGB(h, s * 0.8f, 0.9f);
            b.a = 0.9f;
            return b;
        }

        internal void Bind(Renderer renderer)
        {
            liquid = renderer;
            block ??= new MaterialPropertyBlock();
            var material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null || !material.HasProperty(LiquidColor)) { liquid = null; return; }
            baseLiquid = material.GetColor(LiquidColor);
            topBrightness = material.HasProperty(TopColor) ? Max(material.GetColor(TopColor)) : Max(baseLiquid) * 4f;
            Current = baseLiquid;
            // Sample every ingredient up front, while they all sit on the shelves (a thrown one is already hidden).
            foreach (var ingredient in FindObjectsByType<WarehouseIngredient>(FindObjectsSortMode.None))
                Sample(ingredient);
        }

        /// <summary>An ingredient landed; <paramref name="landed"/> of <paramref name="count"/> are in now.</summary>
        internal void Add(WarehouseIngredient ingredient, int landed, int count)
        {
            if (liquid == null) return;
            var tint = AsPotion(ingredient != null ? Sample(ingredient) : Current);
            added.Add(tint);
            motion.TryCancel();
            if (landed < count)
            {
                Color from = Current, to = Color.Lerp(Current, tint, 0.75f);
                motion = LMotion.Create(0f, 1f, LeanSeconds).WithEase(Ease.OutCubic)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t => Apply(Color.Lerp(from, to, t)));
                return;
            }
            Swirl();
        }

        /// <summary>The colours chase each other round, then the finished potion's colour settles.</summary>
        private void Swirl()
        {
            var colours = added.ToArray();
            var start = Current;
            var final = Finished(colours);
            LastBrew = final;
            motion = LMotion.Create(0f, 1f, SwirlSeconds).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    float phase = t * colours.Length * 1.75f;
                    int i = Mathf.FloorToInt(phase);
                    var chase = Color.Lerp(colours[i % colours.Length], colours[(i + 1) % colours.Length],
                        Mathf.SmoothStep(0f, 1f, phase - i));
                    var c = Color.Lerp(start, chase, Mathf.Clamp01(t / 0.12f));
                    // a bright flash in the middle of the swirl, then the settled colour
                    float glow = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t / 0.8f) * Mathf.PI);
                    Apply(Color.Lerp(c * glow, final, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t))));
                });
        }

        /// <summary>Back to the plain base liquid (a pour was cancelled or a new batch starts).</summary>
        internal void ResetColor()
        {
            motion.TryCancel();
            added.Clear();
            if (liquid != null) Apply(baseLiquid);
        }

        private void OnDisable() => motion.TryCancel();

        private void Apply(Color c)
        {
            c.a = 1f;
            Current = c;
            if (liquid == null) return;
            liquid.GetPropertyBlock(block);
            block.SetColor(LiquidColor, c);
            // The surface is an HDR glow of nearly pure colour (the base is lime at 2.0); scaling the whole colour up
            // instead would wash every channel out to white under bloom.
            Color.RGBToHSV(c, out float h, out float sat, out _);
            var top = Color.HSVToRGB(h, Mathf.Lerp(sat, 1f, 0.45f), 1f, true) * topBrightness;
            top.a = 1f;
            block.SetColor(TopColor, top);
            liquid.SetPropertyBlock(block);
        }

        /// <summary>The ingredients' mix, a little richer than a plain average so it reads as a finished potion.</summary>
        private Color Finished(Color[] colours)
        {
            var sum = Color.black;
            foreach (var c in colours) sum += c;
            var mix = sum / Mathf.Max(1, colours.Length);
            Color.RGBToHSV(mix, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.25f + 0.1f), Mathf.Max(v, Max(baseLiquid)));
        }

        /// <summary>Turns an ingredient's surface colour into a liquid colour at the base liquid's brightness.</summary>
        private Color AsPotion(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s < 0.12f)
            {
                // a grey or white ingredient: keep the brew's own hue, just paler
                Color.RGBToHSV(Current, out h, out _, out _);
                s = 0.3f;
            }
            else s = Mathf.Max(0.6f, Mathf.Lerp(s, 0.9f, 0.5f));
            return Color.HSVToRGB(h, s, Max(baseLiquid) * 1.05f);
        }

        /// <summary>
        /// The average colour of the ingredient's main surface: its base texture shrunk on the GPU (textures are not
        /// readable on the CPU) times its base colour. Cached per variant.
        /// </summary>
        private static Color Sample(WarehouseIngredient ingredient)
        {
            if (sampled.TryGetValue(ingredient.VariantId, out var known)) return known;
            Renderer main = null;
            float biggest = -1f;
            foreach (var r in ingredient.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.sharedMaterial == null) continue;
                float size = r.bounds.size.sqrMagnitude;
                if (size > biggest) { biggest = size; main = r; }
            }
            var colour = new Color(0.5f, 0.5f, 0.5f);
            if (main != null)
            {
                var material = main.sharedMaterial;
                var tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                    : material.HasProperty("_Color") ? material.color : Color.white;
                var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
                colour = (texture != null ? Average(texture) : Color.white) * tint;
            }
            sampled[ingredient.VariantId] = colour;
            return colour;
        }

        private static Color Average(Texture texture)
        {
            const int size = 16;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Color sum = Color.black;
            float weight = 0f;
            foreach (var p in read.GetPixels())
            {
                // vivid pixels count more, so a red cap on a pale stalk still reads as red
                Color.RGBToHSV(p, out _, out float s, out _);
                float w = p.a * (0.25f + s);
                sum += p * w;
                weight += w;
            }
            Destroy(read);
            return weight > 0f ? sum / weight : Color.gray;
        }

        private static float Max(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));
    }
}
