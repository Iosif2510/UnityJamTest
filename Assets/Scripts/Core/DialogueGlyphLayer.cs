using System;
using LitMotion;
using PotionShop.Data;
using TMPro;
using UnityEngine;

namespace PotionShop
{
    internal sealed class DialogueGlyphLayer : IDisposable
    {
        private readonly TMP_Text text;
        private readonly int copy;
        private TMP_MeshInfo[] original;
        private DialogueTimeline timeline;
        private double elapsed;
        private double skippedAt = double.PositiveInfinity;
        private float fade;

        internal DialogueGlyphLayer(TMP_Text text, int copy = 0)
        {
            this.text = text;
            this.copy = copy;
            text.OnPreRenderText += OnTextRebuilt;
        }

        internal void SetLine(DialogueTimeline line, float fadeSeconds)
        {
            timeline = line;
            elapsed = 0;
            skippedAt = double.PositiveInfinity;
            fade = fadeSeconds;
            original = null;
            text.maxVisibleCharacters = int.MaxValue;
            text.text = line.Text;
            text.ForceMeshUpdate();
        }

        internal void Clear()
        {
            timeline = null;
            original = null;
        }

        internal void Render(double time, double skipTime)
        {
            elapsed = time;
            skippedAt = skipTime;
            if (original == null || timeline == null) return;
            Apply(text.textInfo);
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        private void OnTextRebuilt(TMP_TextInfo info)
        {
            if (timeline == null) return;
            // TMP keeps its own reusable copy buffer. A rebuild refreshes it before any offsets are applied.
            original = info.CopyMeshInfoVertexData();
            Apply(info);
        }

        private void Apply(TMP_TextInfo info)
        {
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible || !timeline.TryGetCue(ch.index, out var cue)) continue;
                int meshIndex = ch.materialReferenceIndex;
                int vertex = ch.vertexIndex;
                var baseline = original[meshIndex];
                var mesh = info.meshInfo[meshIndex];
                float age = (float)(elapsed - Math.Min(cue.Start, skippedAt));
                float opacity = age < 0 ? 0 : elapsed >= skippedAt || fade <= 0 ? 1 :
                    EaseUtility.Evaluate(Mathf.Clamp01(age / fade), Ease.OutQuad);
                var pose = Pose.Evaluate(cue, Mathf.Max(0, age), copy);
                var midpoint = (baseline.vertices[vertex] + baseline.vertices[vertex + 2]) * .5f;
                var rotation = Quaternion.Euler(0, 0, pose.Angle);
                for (int j = vertex; j < vertex + 4; j++)
                {
                    var point = baseline.vertices[j] - midpoint;
                    point.x *= pose.Scale.x;
                    point.y *= pose.Scale.y;
                    mesh.vertices[j] = midpoint + rotation * point + (Vector3)(pose.Offset * (ch.pointSize / 22f));
                    Color color = pose.Tint ?? (Color)baseline.colors32[j];
                    color.a = baseline.colors32[j].a / 255f * opacity * pose.Opacity;
                    mesh.colors32[j] = color;
                }
            }
        }

        public void Dispose() => text.OnPreRenderText -= OnTextRebuilt;

        private readonly struct Pose
        {
            internal readonly Vector2 Offset;
            internal readonly Vector2 Scale;
            internal readonly float Angle;
            internal readonly Color? Tint;
            internal readonly float Opacity;

            private Pose(float opacity, Color? tint = null, Vector2 offset = default, float angle = 0,
                Vector2? scale = null)
            {
                Opacity = opacity;
                Tint = tint;
                Offset = offset;
                Angle = angle;
                Scale = scale ?? Vector2.one;
            }

            internal static Pose Evaluate(DialogueTimeline.Cue cue, float age, int copy)
            {
                float phase = cue.PhaseIndex;
                float jitter = (cue.PhaseIndex * 37 % 17) / 17f;
                if (copy == 0)
                {
                    if (cue.EchoTail)
                        return new Pose(Key(age / 2.6f, (0, 0), (.15f, .65f), (.38f, .1f), (.55f, .45f), (.78f, .06f), (1, .28f)),
                            new Color(.37f, .19f, .48f));
                    if (cue.Effect == SymptomEffect.Drift)
                    {
                        float t = Mathf.Repeat((age + phase * .12f) / 3.2f, 1);
                        float lift = Mathf.Sin(t * Mathf.PI) * 6;
                        return new Pose(1, new Color(.13f, .32f, .45f),
                            new Vector2(Mathf.Sin(t * Mathf.PI * 2) * 2.5f, lift), Mathf.Sin(t * Mathf.PI * 2) * 3);
                    }
                    if (cue.Effect == SymptomEffect.Frost)
                    {
                        float cold = Mathf.Clamp01(age / 1.4f);
                        float t = (age / .22f + jitter) * Mathf.PI * 2;
                        return new Pose(1, Color.Lerp(new Color(.29f, .18f, .11f), new Color(.12f, .31f, .48f), cold),
                            new Vector2(Mathf.Sin(t) * .5f, Mathf.Cos(t) * .25f));
                    }
                    return new Pose(1);
                }

                if (!cue.EchoTail)
                {
                    switch (cue.Effect)
                    {
                        case SymptomEffect.ShadowLag when copy == 1:
                            if (age < 1.6f)
                            {
                                float t = EaseUtility.Evaluate(Mathf.InverseLerp(.48f, 1.6f, age), Ease.OutQuad);
                                return new Pose(t * .52f, new Color(.13f, .05f, .22f),
                                    new Vector2(Mathf.Lerp(-18, 3, t), -3));
                            }
                            float loop = Mathf.Repeat((age - 1.6f) / 3.4f, 1);
                            return new Pose(Key(loop, (0, .52f), (.35f, .32f), (.55f, 0), (.75f, 0), (1, .52f)),
                                new Color(.13f, .05f, .22f), new Vector2(Key(loop, (0, 3), (.35f, -10), (.55f, -10), (.75f, 3), (1, 3)), -3));
                        case SymptomEffect.Echo:
                            float delayed = age - phase * .045f;
                            float echo = Mathf.Repeat(delayed / 1.8f, 1);
                            return new Pose(delayed < 0 ? 0 : Key(echo, (0, 0), (.15f, .46f), (.6f, .12f), (.85f, .04f), (1, 0)) / copy,
                                new Color(.83f, .65f, 1f), new Vector2(echo * 14 * copy, 0));
                        case SymptomEffect.MirrorDoze when copy == 1:
                            float doze = Mathf.Repeat(Mathf.Max(0f, age - phase * .06f) / 3.4f, 1);
                            return new Pose(.45f, new Color(.52f, .73f, .96f), new Vector2(2, -2),
                                Key(doze, (0, 0), (.45f, 0), (.58f, 18), (.66f, 4), (.78f, 15), (1, 0)),
                                new Vector2(-1, 1));
                        case SymptomEffect.Frost when copy == 1:
                            return new Pose(Mathf.Clamp01(age / 1.4f) * .24f, new Color(.64f, .87f, 1),
                                scale: new Vector2(1.19f, 1.14f));
                    }
                }
                return new Pose(0);
            }

            private static float Key(float time, (float Time, float Value) a, (float Time, float Value) b,
                (float Time, float Value) c, (float Time, float Value) d, (float Time, float Value)? e = null,
                (float Time, float Value)? f = null)
            {
                if (time <= b.Time) return Between(a, b);
                if (time <= c.Time) return Between(b, c);
                if (time <= d.Time || !e.HasValue) return Between(c, d);
                if (time <= e.Value.Time || !f.HasValue) return Between(d, e.Value);
                return Between(e.Value, f.Value);

                float Between((float Time, float Value) start, (float Time, float Value) end) =>
                    Mathf.Lerp(start.Value, end.Value, Mathf.InverseLerp(start.Time, end.Time, time));
            }
        }
    }
}
