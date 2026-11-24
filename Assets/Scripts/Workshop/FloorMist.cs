using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// A cellar haze lying low along the floor: broad, barely-there puffs creeping slowly about. By the fireplace and
    /// the cauldron the warmth lifts it away, so the hearth and the brew stand in clear air.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorMist : MonoBehaviour
    {
        private static readonly Vector3 Area = new(8.0f, 0.15f, 12.4f);
        private const float Alpha = 0.035f, WarmRadius = 1.9f;

        private ParticleSystem mist;
        private ParticleSystem.Particle[] buffer;
        private Vector3[] warm = System.Array.Empty<Vector3>();

        private void Start()
        {
            var scene = gameObject.scene;
            var list = new System.Collections.Generic.List<Vector3>();
            var fire = CellarAtmosphere.Find(scene, "Fireplace Light");
            if (fire != null) list.Add(fire.position);
            var cauldron = FindAnyObjectByType<WorkshopCauldron>();
            if (cauldron != null) list.Add(cauldron.transform.position);
            warm = list.ToArray();

            mist = CellarAtmosphere.NewSystem("Floor Mist", transform);
            mist.transform.position = new Vector3(0f, 0.12f, 0f);
            var main = mist.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.4f, 4.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // dusty and warm like the room, not a cold white (that read as a snowy map)
            main.startColor = new Color(0.7f, 0.6f, 0.52f, Alpha);
            main.maxParticles = 60;
            var emission = mist.emission;
            emission.rateOverTime = 3.2f;
            var shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Area;
            shape.rotation = Vector3.zero;
            var velocity = mist.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            var fade = mist.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = mist.GetComponent<ParticleSystemRenderer>();
            // lying flat on the floor rather than facing the camera
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sortingFudge = 10f;
            buffer = new ParticleSystem.Particle[main.maxParticles];
            mist.Simulate(20f, true, true);
            mist.Play();
        }

        private void LateUpdate()
        {
            if (mist == null || warm.Length == 0) return;
            int n = mist.GetParticles(buffer);
            for (int i = 0; i < n; i++)
            {
                var p = buffer[i].position;
                float clear = 1f;
                foreach (var w in warm)
                {
                    float d = new Vector2(p.x - w.x, p.z - w.z).magnitude;
                    clear = Mathf.Min(clear, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WarmRadius * 0.5f, WarmRadius, d)));
                }
                var c = buffer[i].startColor;
                c.a = (byte)(Alpha * 255f * clear);
                buffer[i].startColor = c;
                // the warmth also nudges it away
                if (clear < 1f)
                    foreach (var w in warm)
                    {
                        var away = new Vector3(p.x - w.x, 0f, p.z - w.z);
                        if (away.sqrMagnitude < WarmRadius * WarmRadius && away.sqrMagnitude > 0.0001f)
                            buffer[i].position = p + away.normalized * 0.05f * Time.deltaTime;
                    }
            }
            mist.SetParticles(buffer, n);
        }
    }
}
