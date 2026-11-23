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

        private void Awake()
        {
            splash = Build("Splash", Liquid, 0.06f, 0.12f, 2.2f, 3.8f, 0.55f, 18, gravity: 2.4f, cone: 28f);
            sparkle = Build("BrewSparkle", Spark, 0.04f, 0.09f, 0.8f, 2.0f, 1.1f, 40, gravity: -0.25f, cone: 55f);
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
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.SetColor("_BaseColor", Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
    }
}
