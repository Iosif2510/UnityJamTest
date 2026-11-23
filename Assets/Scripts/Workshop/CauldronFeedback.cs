using LitMotion;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The cauldron's extra feedback, built in code so the scene stays untouched: a splash of droplets and a splash
    /// sound where an ingredient lands, and a puff of sparkles with a chime when the brew is done.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CauldronFeedback : MonoBehaviour
    {
        private static readonly Color Liquid = new(0.55f, 0.85f, 0.55f, 0.9f);
        private static readonly Color Spark = new(1f, 0.85f, 0.45f, 1f);

        private ParticleSystem splash;
        private ParticleSystem sparkle;
        private ParticleSystem embers;
        private FireLightFlicker fire;
        private bool fireSearched;
        private static readonly Color Ember = new(1f, 0.55f, 0.18f, 1f);

        private void Awake()
        {
            splash = Build("Splash", Liquid, 0.06f, 0.12f, 2.2f, 3.8f, 0.55f, 18, gravity: 2.4f, cone: 28f);
            sparkle = Build("BrewSparkle", Spark, 0.04f, 0.09f, 0.8f, 2.0f, 1.1f, 40, gravity: -0.25f, cone: 55f);
            embers = Build("Embers", Ember, 0.02f, 0.045f, 0.6f, 1.6f, 0.9f, 40, gravity: -0.4f, cone: 35f);
        }

        /// <summary>An ingredient hit the liquid at <paramref name="point"/>.</summary>
        public void Splash(Vector3 point, Color? tint = null)
        {
            splash.transform.position = point;
            var main = splash.main;
            main.startColor = tint ?? Liquid;
            splash.Emit(16);
            GameSfx.Play("Splash", 0.55f, 1f, 0.12f);
        }

        /// <summary>
        /// The fire under the pot answers: its light flares, embers fly up and it roars softly. Stronger for the last
        /// ingredient, when brewing starts.
        /// </summary>
        public void FireFlare(float strength)
        {
            if (!fireSearched)
            {
                fireSearched = true;
                float best = 2f; // only a fire right under this pot
                foreach (var light in FindObjectsByType<FireLightFlicker>(FindObjectsSortMode.None))
                {
                    float d = Vector3.Distance(light.transform.position, transform.position);
                    if (d < best) { best = d; fire = light; }
                }
            }
            if (fire == null) return;
            fire.Flare(strength, 0.6f + strength * 0.6f);
            embers.transform.position = fire.transform.position;
            embers.Emit(Mathf.RoundToInt(8 + strength * 16));
            GameSfx.Play("FireFlare", 0.45f + strength * 0.3f, 1f, 0.08f);
        }

        /// <summary>The brew finished.</summary>
        public void BrewDone(Vector3 surface)
        {
            sparkle.transform.position = surface;
            sparkle.Emit(36);
            GameSfx.Play("BrewDone", 0.8f);
            // The pot gives a small hop.
            var pot = transform;
            var scale = pot.localScale;
            LMotion.Create(1.06f, 1f, 0.35f).WithEase(Ease.OutElastic).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(s => pot.localScale = new Vector3(scale.x * (2f - s), scale.y * s, scale.z * (2f - s)))
                .AddTo(gameObject);
        }

        private static Texture2D dot;

        /// <summary>A soft round dot for every particle here.</summary>
        private static Texture2D Dot
        {
            get
            {
                if (dot != null) return dot;
                const int size = 32;
                dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                        float a = Mathf.Clamp01((1f - d) * 3f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                dot.SetPixels32(px);
                dot.Apply();
                return dot;
            }
        }

        private ParticleSystem Build(string name, Color color, float minSize, float maxSize, float minSpeed,
            float maxSpeed, float life, int max, float gravity, float cone)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone;
            shape.radius = 0.12f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Dot); // round droplets and sparks instead of squares
            // The particle shader starts opaque, which ignores the dot's alpha: make it alpha-blended.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
    }
}
