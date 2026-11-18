using System;
using UnityEngine;

namespace PotionShop
{
    /// <summary>Owns bubble lifetimes independently of their shader representation.</summary>
    public sealed class LiquidBubbleSimulation
    {
        // Keep these capacities in sync with LiquidBubbleEffects.hlsl.
        public const int Capacity = 24;
        public const int RippleCapacity = 16;

        private struct Bubble
        {
            public Vector2 anchor;
            public Vector3 position;
            public float radius, age, speed, phase, surfaceAge, hold;
            public bool onSurface;
        }

        private readonly Bubble[] bubbles = new Bubble[Capacity];
        private readonly Vector4[] ripples = new Vector4[RippleCapacity];
        private readonly System.Random random;
        private int count, rippleCount;
        private float emission;

        public int Count => count;
        public int RippleCount => rippleCount;
        public int TotalPopped { get; private set; }
        public float Time { get; private set; }

        public LiquidBubbleSimulation(int seed) => random = new System.Random(seed);

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        // The mesh bounds describe an ellipsoid. Solve a gravity-aligned column through it,
        // even when the renderer is rotated or scaled. Coordinates use the shader's height scale.
        private static bool Column(Vector2 xz, float margin, Bounds bounds, Matrix4x4 toObject,
            out float bottom, out float top)
        {
            Vector3 p = toObject.MultiplyPoint3x4(new Vector3(xz.x, 0f, xz.y)) - bounds.center;
            Vector3 up = toObject.MultiplyVector(Vector3.up);
            Vector3 e = bounds.extents;
            float mx = toObject.MultiplyVector(Vector3.right).magnitude * margin;
            float my = up.magnitude * margin;
            float mz = toObject.MultiplyVector(Vector3.forward).magnitude * margin;
            e = Vector3.Max(e - new Vector3(mx, my, mz), Vector3.one * 0.0001f);
            p = new Vector3(p.x / e.x, p.y / e.y, p.z / e.z);
            up = new Vector3(up.x / e.x, up.y / e.y, up.z / e.z);
            float a = Vector3.Dot(up, up);
            float b = Vector3.Dot(p, up);
            float discriminant = b * b - a * (Vector3.Dot(p, p) - 1f);
            bottom = top = 0f;
            if (a < 0.00001f || discriminant <= 0f) return false;
            float root = Mathf.Sqrt(discriminant);
            bottom = (-b - root) / a;
            top = (-b + root) / a;
            return true;
        }

        public void Step(float dt, float amount, float size, float scale, float level, Vector2 tilt,
            Bounds bounds, Matrix4x4 toObject, Vector3 center, Vector2 span,
            float emissionRate, float riseSpeed, float surfaceLifetime, float rippleLifetime)
        {
            if (dt <= 0f) return;
            Time += dt;
            bool emitting = amount > 0f && size > 0f;
            if (!emitting)
            {
                count = rippleCount = 0;
                emission = 0f;
                return;
            }

            for (int i = rippleCount - 1; i >= 0; i--)
            {
                if (Time - ripples[i].z > rippleLifetime)
                    ripples[i] = ripples[--rippleCount];
            }

            for (int i = count - 1; i >= 0; i--)
            {
                Bubble b = bubbles[i];
                b.age += dt;
                float drift = b.radius * 0.55f * Mathf.SmoothStep(0f, 1f, b.age * 2f);
                Vector2 xz = b.onSurface ? new Vector2(b.position.x, b.position.z) :
                    b.anchor + new Vector2(Mathf.Sin(b.age * 2.3f + b.phase),
                        Mathf.Cos(b.age * 1.8f + b.phase)) * drift;
                float surface = level + Vector2.Dot(xz, tilt);
                if (!Column(xz, b.radius, bounds, toObject, out float bottom, out float top)
                    || surface < bottom)
                {
                    bubbles[i] = bubbles[--count];
                    continue;
                }

                // A submerged bubble becomes a cap at the same position before it bursts.
                float target = Mathf.Min(surface - b.radius * 0.25f, top);
                b.position.x = xz.x;
                b.position.z = xz.y;
                if (!b.onSurface)
                {
                    b.position.y = Mathf.Max(bottom, b.position.y + riseSpeed * b.speed * dt);
                    if (b.position.y >= target)
                    {
                        // Don't create a surface event when the column ends below the liquid plane.
                        if (top < surface - b.radius)
                        {
                            bubbles[i] = bubbles[--count];
                            continue;
                        }
                        b.onSurface = true;
                    }
                }
                if (b.onSurface)
                {
                    b.position.y = target;
                    b.surfaceAge += dt;
                    if (b.surfaceAge >= b.hold)
                    {
                        Vector4 ripple = new Vector4(xz.x, xz.y, Time, b.radius);
                        if (rippleCount == RippleCapacity)
                        {
                            // The arrays stay packed; overwrite the oldest event if saturated.
                            int oldest = 0;
                            for (int j = 1; j < rippleCount; j++)
                                if (ripples[j].z < ripples[oldest].z) oldest = j;
                            ripples[oldest] = ripple;
                        }
                        else ripples[rippleCount++] = ripple;
                        TotalPopped++;
                        bubbles[i] = bubbles[--count];
                        continue;
                    }
                }
                bubbles[i] = b;
            }

            emission = Mathf.Min(emission + dt * emissionRate * Mathf.Clamp01(amount)
                * Mathf.Clamp(scale / 6f, 0.25f, 2f), Capacity);
            while (emission >= 1f && count < Capacity)
            {
                emission -= 1f;
                float radius = Mathf.Clamp(size / Mathf.Max(scale, 0.01f) * 0.5f, 0.003f, 0.12f)
                    * Range(0.65f, 1.05f);
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float angle = Range(0f, Mathf.PI * 2f);
                    float distance = Mathf.Sqrt(Range(0f, 1f)) * 0.52f;
                    Vector2 xz = new Vector2(center.x + Mathf.Cos(angle) * span.x * distance,
                        center.z + Mathf.Sin(angle) * span.y * distance);
                    float surface = level + Vector2.Dot(xz, tilt);
                    if (!Column(xz, radius, bounds, toObject, out float bottom, out float top)
                        || Mathf.Min(surface, top) - bottom < radius * 3f) continue;
                    bubbles[count++] = new Bubble
                    {
                        anchor = xz,
                        position = new Vector3(xz.x, bottom + radius * 0.15f, xz.y),
                        radius = radius,
                        speed = Range(0.75f, 1.3f),
                        phase = Range(0f, Mathf.PI * 2f),
                        hold = surfaceLifetime * Range(0.7f, 1.35f)
                    };
                    break;
                }
            }
        }

        public void CopyTo(Vector4[] positions, Vector4[] shapes, Vector4[] events,
            float surfaceFlattening = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                Bubble b = bubbles[i];
                float birth = Mathf.SmoothStep(0f, 1f, b.age / 0.22f);
                float surfaceProgress = b.onSurface ? Mathf.Clamp01(b.surfaceAge / b.hold) : 0f;
                float growth = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(b.age * 1.3f));
                float radius = b.radius * birth * growth;
                positions[i] = new Vector4(b.position.x, b.position.y, b.position.z, radius);
                shapes[i] = new Vector4(b.onSurface ? 1f : 0f,
                    1f - Mathf.Clamp(surfaceFlattening, 0f, 0.5f) * surfaceProgress, b.age, 0f);
            }
            Array.Copy(ripples, events, rippleCount);
        }
    }
}
