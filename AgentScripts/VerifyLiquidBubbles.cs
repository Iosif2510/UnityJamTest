using System;
using UnityEngine;
using PotionShop;

/// <summary>Behaviour checks for emission, ascent, surface events, and renderer coordinates.</summary>
public static class VerifyLiquidBubbles
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static object Run()
    {
        var positions = new Vector4[LiquidBubbleSimulation.Capacity];
        var shapes = new Vector4[LiquidBubbleSimulation.Capacity];
        var events = new Vector4[LiquidBubbleSimulation.RippleCapacity];
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        int ascending = 0, surface = 0;
        float maxRadius = 0f;
        var sim = new LiquidBubbleSimulation(6174);
        for (int frame = 0; frame < 1200; frame++)
        {
            sim.Step(1f / 60f, 0.5f, 0.6f, 6f, -0.017f, Vector2.zero,
                bounds, Matrix4x4.identity, Vector3.zero, Vector2.one * 0.5f,
                10f, 0.22f, 0.45f, 1.25f);
            sim.CopyTo(positions, shapes, events);
            Require(sim.Count <= positions.Length && sim.RippleCount <= events.Length, "Capacity overflow.");
            for (int i = 0; i < sim.Count; i++)
            {
                Vector4 p = positions[i];
                Require(!float.IsNaN(p.x + p.y + p.z + p.w), "Invalid position.");
                Require(new Vector3(p.x, p.y, p.z).sqrMagnitude < 0.251f, "Bubble left the volume.");
                maxRadius = Mathf.Max(maxRadius, p.w);
                if (shapes[i].x > 0.5f)
                {
                    surface++;
                    Require(shapes[i].y == 1f, "Default surface bubble was flattened.");
                    Require(Mathf.Abs(p.y + 0.017f) < 0.02f, "Cap detached from the water plane.");
                }
                else if (p.y < -0.2f) ascending++;
            }
            for (int i = 0; i < sim.RippleCount; i++)
            {
                Require(sim.Time >= events[i].z && sim.Time - events[i].z <= 1.27f,
                    "Ripple lifetime is invalid.");
                Require(events[i].w > 0f, "Ripple lost its source radius.");
            }
        }
        Require(ascending > 0 && surface > 0 && sim.TotalPopped > 20,
            "The emission/ascent/surface/pop cycle did not complete.");
        float time = sim.Time;
        int count = sim.Count;
        sim.Step(0f, 0.5f, 0.6f, 6f, 0f, Vector2.zero, bounds, Matrix4x4.identity,
            Vector3.zero, Vector2.one * 0.5f, 10f, 0.22f, 0.45f, 1.25f);
        Require(time == sim.Time && count == sim.Count, "Paused simulation advanced.");
        sim.Step(0.02f, 0f, 0.6f, 6f, 0f, Vector2.zero, bounds, Matrix4x4.identity,
            Vector3.zero, Vector2.one * 0.5f, 10f, 0.22f, 0.45f, 1.25f);
        Require(sim.Count == 0 && sim.RippleCount == 0, "Bubble Amount = 0 did not clear the effect.");

        int transformedPops = 0;
        foreach (float level in new[] { -0.49f, -0.2f, 0f, 0.3f, 0.55f })
        {
            var transformed = new LiquidBubbleSimulation(42);
            var matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(23f, 40f, -12f),
                new Vector3(0.8f, 1f, 1.2f));
            var tilt = new Vector2(0.12f, -0.08f);
            for (int frame = 0; frame < 900; frame++)
            {
                transformed.Step(1f / 60f, 1f, 0.6f, 6f, level, tilt, bounds,
                    matrix.inverse, Vector3.zero, new Vector2(0.65f, 0.7f),
                    20f, 0.22f, 0.45f, 1.25f);
                transformed.CopyTo(positions, shapes, events);
                for (int i = 0; i < transformed.Count; i++)
                {
                    Vector3 p = matrix.inverse.MultiplyPoint3x4(positions[i]);
                    Require(p.sqrMagnitude < 0.251f, "Scaled/rotated bubble left the ellipsoid.");
                }
            }
            if (level >= 0.55f)
                Require(transformed.TotalPopped == 0, "A submerged boundary generated a fake surface pop.");
            transformedPops += transformed.TotalPopped;
        }
        Require(transformedPops > 0, "Tilted/scaled liquid never produced surface events.");
        return new { passed = true, simulatedSeconds = 95, ascendingSamples = ascending,
            capSamples = surface, transformedPops, maxRadius };
    }
}
