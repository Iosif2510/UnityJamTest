using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// Dust drifting in the lamplight: a slow cloud of motes around each lantern and under the chandelier, catching
    /// the warm light and fading out away from it. Walking through a cloud pushes the motes aside.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DustMotes : MonoBehaviour
    {
        private const float PushRadius = 0.9f, PushStrength = 0.6f;

        private readonly System.Collections.Generic.List<ParticleSystem> clouds = new();
        private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];

        private void Start()
        {
            var scene = gameObject.scene;
            var lights = CellarAtmosphere.Find(scene, "CozyLights");
            if (lights != null)
                foreach (Transform lamp in lights)
                    if (lamp.name.StartsWith("Lantern Light") || lamp.name == "Cauldron Glow")
                        clouds.Add(Cloud(lamp, lamp.name == "Cauldron Glow" ? 1.3f : 1.1f, 26));
            var chandelier = CellarAtmosphere.Find(scene, "Chandelier Light");
            if (chandelier != null) clouds.Add(Cloud(chandelier, 2.2f, 50, -1.8f));
        }

        private ParticleSystem Cloud(Transform lamp, float radius, int count, float drop = 0f)
        {
            var ps = CellarAtmosphere.NewSystem("Dust " + lamp.name, transform);
            ps.transform.position = lamp.position + Vector3.up * drop;
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.032f);
            main.startColor = new Color(1f, 0.9f, 0.72f, 0.75f);
            main.maxParticles = count;
            main.gravityModifier = 0.002f;
            var emission = ps.emission;
            emission.rateOverTime = count / 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            // a lazy wander, as dust does in still air
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.05f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ps.Simulate(10f, true, true); // already hanging in the air when you come down
            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            var eyes = CellarAtmosphere.Eyes;
            if (eyes == null) return;
            // the body moving through: from the knees to the eyes
            var body = eyes.position;
            foreach (var cloud in clouds)
            {
                if ((cloud.transform.position - body).sqrMagnitude > 9f) continue;
                if (buffer.Length < cloud.main.maxParticles) buffer = new ParticleSystem.Particle[cloud.main.maxParticles];
                int n = cloud.GetParticles(buffer);
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    var p = buffer[i].position;
                    var away = new Vector3(p.x - body.x, 0f, p.z - body.z);
                    float d = away.magnitude;
                    if (d > PushRadius || p.y > body.y + 0.3f || p.y < body.y - 1.4f) continue;
                    float push = (1f - d / PushRadius) * PushStrength * Time.deltaTime;
                    buffer[i].position = p + (d > 0.001f ? away / d : Vector3.right) * push + Vector3.up * push * 0.4f;
                    moved = true;
                }
                if (moved) cloud.SetParticles(buffer, n);
            }
        }
    }
}
