using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// Dust drifting through the whole room: sparse motes hanging everywhere at standing height and wandering in every
    /// direction (no settling), with a few more around each lantern where they catch the light. Walking through
    /// them pushes them aside.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DustMotes : MonoBehaviour
    {
        private const float PushRadius = 0.9f, PushStrength = 0.6f;

        private readonly System.Collections.Generic.List<ParticleSystem> clouds = new();
        private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];

        private void Start()
        {
            // the hall, from knee to above head height
            clouds.Add(Cloud("Room", new Vector3(0f, 1.6f, 0f), ParticleSystemShapeType.Box, new Vector3(8.2f, 2.8f, 12.6f), 170));
            var lights = CellarAtmosphere.Find(gameObject.scene, "CozyLights");
            if (lights != null)
                foreach (Transform lamp in lights)
                    if (lamp.name.StartsWith("Lantern Light"))
                        clouds.Add(Cloud(lamp.name, lamp.position, ParticleSystemShapeType.Sphere, Vector3.one * 1.1f, 5));
        }

        private ParticleSystem Cloud(string name, Vector3 at, ParticleSystemShapeType shapeType, Vector3 size, int count)
        {
            var ps = CellarAtmosphere.NewSystem("Dust " + name, transform);
            ps.transform.position = at;
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.024f);
            // amber specks catching the lamplight (white ones read as snow)
            main.startColor = new Color(1f, 0.78f, 0.5f, 0.6f);
            main.maxParticles = count;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.rateOverTime = count / 14f;
            var shape = ps.shape;
            shape.shapeType = shapeType;
            if (shapeType == ParticleSystemShapeType.Box) shape.scale = size;
            else shape.radius = size.x;
            // a lazy wander, up as much as down, as dust does in still air
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.04f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ps.Simulate(16f, true, true); // already hanging in the air when you come down
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
