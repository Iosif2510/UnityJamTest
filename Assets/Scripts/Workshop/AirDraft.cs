using System.Collections.Generic;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The air the player stirs: hanging herbs, plants and ivy swing from where they hang as you walk past, and the
    /// chandelier's candle flames lean away from your movement and flicker back upright. Each one is a little damped
    /// pendulum pushed by how fast and how close you go by.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AirDraft : MonoBehaviour
    {
        private sealed class Swinger
        {
            public Transform T;
            public Vector3 BasePos;
            public Quaternion BaseRot;
            public Vector3 Pivot;        // world point it hangs from (or stands on, for flames)
            public float Reach;          // how close the player must pass
            public float Give;           // how far a push swings it
            public float Spring, Damping;
            public Vector2 Angle, Speed; // degrees about world x and z
            public bool Flame;
        }

        private readonly List<Swinger> swingers = new();
        private Vector3 lastEyes, moving;

        private void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.name;
                    var b = r.bounds;
                    if (n == "Candle Flame")
                        Add(r.transform, new Vector3(b.center.x, b.min.y, b.center.z), 2.2f, 45f, 60f, 5f, true);
                    else if (n == "HerbBundle" || n == "HangingPlant")
                        Add(r.transform, new Vector3(b.center.x, b.max.y, b.center.z), 1.9f, 32f, 18f, 2.2f, false);
                    else if (n == "HangingIvy")
                        Add(r.transform, new Vector3(b.center.x, b.max.y, b.center.z), 1.5f, 14f, 22f, 3f, false);
                }
        }

        private void Add(Transform t, Vector3 pivot, float reach, float give, float spring, float damping, bool flame)
        {
            swingers.Add(new Swinger
            {
                T = t, BasePos = t.position, BaseRot = t.rotation, Pivot = pivot, Reach = reach, Give = give,
                Spring = spring, Damping = damping, Flame = flame,
            });
        }

        private void LateUpdate()
        {
            var eyes = CellarAtmosphere.Eyes;
            if (eyes == null || swingers.Count == 0) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            // how the player is actually moving, from where the eyes went (smoothed; a jump such as a teleport is ignored)
            var step = eyes.position - lastEyes;
            lastEyes = eyes.position;
            step.y = 0f;
            var now = step.magnitude > 1f ? Vector3.zero : step / Mathf.Max(Time.deltaTime, 1e-4f);
            moving = Vector3.Lerp(moving, Vector3.ClampMagnitude(now, 4f), 1f - Mathf.Exp(-Time.deltaTime * 12f));
            var velocity = moving;

            foreach (var s in swingers)
            {
                if (s.T == null) continue;
                float d = new Vector2(s.Pivot.x - eyes.position.x, s.Pivot.z - eyes.position.z).magnitude;
                if (d < s.Reach && velocity.sqrMagnitude > 0.01f)
                {
                    // the air moves with the player: hanging things swing the way they walk, and a flame (turning
                    // about its base) leans its tip back from it; about world x then z, both come out the same
                    float push = (1f - d / s.Reach) * s.Give * dt * 6f;
                    s.Speed += new Vector2(-velocity.z, velocity.x) * push;
                }
                // a damped spring back to rest
                s.Speed += (-s.Spring * s.Angle - s.Damping * s.Speed) * dt;
                s.Angle += s.Speed * dt;
                s.Angle = Vector2.ClampMagnitude(s.Angle, s.Flame ? 25f : 14f);
                var swing = Quaternion.Euler(s.Angle.x, 0f, s.Angle.y);
                s.T.SetPositionAndRotation(s.Pivot + swing * (s.BasePos - s.Pivot), swing * s.BaseRot);
            }
        }
    }
}
