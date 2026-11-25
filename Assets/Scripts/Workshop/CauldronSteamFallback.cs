using UnityEngine;
using UnityEngine.VFX;

namespace PotionShop
{
    /// <summary>
    /// The cauldron's steam is a VFX Graph, which needs compute shaders; a browser has none, so there the steam was
    /// simply missing. Where VFX Graph cannot run, soft puffs from a regular particle system rise from the same spot
    /// instead (the fire is drawn by its own flame models and needs no stand-in).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CauldronSteamFallback : MonoBehaviour
    {
        private void Start()
        {
            if (SystemInfo.supportsComputeShaders) return;
            foreach (var vfx in FindObjectsByType<VisualEffect>(FindObjectsSortMode.None))
                if (vfx.name.Contains("Steam"))
                    Puffs(vfx.transform);
        }

        private void Puffs(Transform at)
        {
            var steam = CellarAtmosphere.NewSystem("Steam (fallback)", transform);
            steam.transform.SetPositionAndRotation(at.position, Quaternion.identity);
            var main = steam.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.92f, 0.88f, 0.84f, 0.16f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = steam.emission;
            emission.rateOverTime = 9f;
            var shape = steam.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // the circle lies flat, puffs go up
            var size = steam.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var fade = steam.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var drift = steam.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            steam.Play();
        }
    }
}
