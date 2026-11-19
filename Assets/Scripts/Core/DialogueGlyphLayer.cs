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
            text.text = line.DisplayText;
            text.ForceMeshUpdate();
        }

        internal void Reflow(double time, double skipTime)
        {
            elapsed = time;
            skippedAt = skipTime;
            text.text = timeline.DisplayText;
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
                if (copy == 0 && cue.Accent != DialogueAccent.None)
                {
                    double accentStart = elapsed < skippedAt ? cue.Start : cue.LastAccent ? skippedAt : double.PositiveInfinity;
                    pose = pose.Accentuate(cue.Accent, (float)(elapsed - accentStart));
                }
                var midpoint = (baseline.vertices[vertex] + baseline.vertices[vertex + 2]) * .5f;
                var rotation = Quaternion.Euler(0, 0, pose.Angle);
                for (int j = vertex; j < vertex + 4; j++)
                {
                    var point = baseline.vertices[j] - midpoint;
                    point.x *= pose.Scale.x;
                    point.y *= pose.Scale.y;
                    mesh.vertices[j] = midpoint + rotation * point + (Vector3)(pose.Offset * (ch.pointSize / 22f));
                    Color color = pose.GetColor(baseline.colors32[j]);
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
            private readonly Color accentTint;
            private readonly float accentMix;

            private Pose(float opacity, Color? tint = null, Vector2 offset = default, float angle = 0,
                Vector2? scale = null, Color accentTint = default, float accentMix = 0f)
            {
                Opacity = opacity;
                Tint = tint;
                Offset = offset;
                Angle = angle;
                Scale = scale ?? Vector2.one;
                this.accentTint = accentTint;
                this.accentMix = accentMix;
            }

            internal Color GetColor(Color source)
            {
                Color color = Tint ?? source;
                return accentMix > 0 ? Color.Lerp(color, accentTint, accentMix) : color;
            }

            internal Pose Accentuate(DialogueAccent accent, float age)
            {
                if (age < 0 || age >= DialogueEmphasis.PulseSeconds) return this;
                float progress = age / DialogueEmphasis.PulseSeconds;
                float pulse = Key(progress, (0, .65f), (.22f, 1f), (.58f, .54f), (1f, 0f));
                Color color = accent switch
                {
                    DialogueAccent.Relief => new Color(.09f, .43f, .26f),
                    DialogueAccent.Concern => new Color(.52f, .22f, .10f),
                    DialogueAccent.Alarm => new Color(.61f, .12f, .19f),
                    _ => new Color(.43f, .13f, .52f)
                };
                float shake = accent == DialogueAccent.Alarm ? Mathf.Sin(progress * Mathf.PI * 6f) * 2.5f : 0;
                return new Pose(Opacity, Tint, Offset + new Vector2(shake, 3f) * pulse, Angle,
                    Scale * (1 + pulse * .17f), color, pulse * .82f);
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
                        float lift = Mathf.Sin(t * Mathf.PI) * 9;
                        return new Pose(1, new Color(.13f, .32f, .45f),
                            new Vector2(Mathf.Sin(t * Mathf.PI * 2) * 4, lift), Mathf.Sin(t * Mathf.PI * 2) * 4.5f);
                    }
                    if (cue.Effect == SymptomEffect.Frost)
                    {
                        float cold = Mathf.Clamp01(age / .62f);
                        float t = (age / .22f + jitter) * Mathf.PI * 2;
                        return new Pose(1, Color.Lerp(new Color(.29f, .18f, .11f), new Color(.12f, .31f, .48f), cold),
                            new Vector2(Mathf.Sin(t) * 1.35f, Mathf.Cos(t) * .65f));
                    }
                    return new Pose(1);
                }

                if (!cue.EchoTail)
                {
                    switch (cue.Effect)
                    {
                        case SymptomEffect.ShadowLag when copy == 1:
                            if (age < .72f)
                            {
                                float t = EaseUtility.Evaluate(Mathf.InverseLerp(.07f, .72f, age), Ease.OutQuad);
                                return new Pose(t * .73f, new Color(.20f, .09f, .30f),
                                    new Vector2(Mathf.Lerp(-23, 6, t), -4.5f));
                            }
                            float loop = Mathf.Repeat((age - .72f) / 2.4f, 1);
                            return new Pose(Key(loop, (0, .73f), (.35f, .46f), (.55f, 0), (.75f, 0), (1, .73f)),
                                new Color(.20f, .09f, .30f),
                                new Vector2(Key(loop, (0, 6), (.35f, -15), (.55f, -15), (.75f, 6), (1, 6)), -4.5f));
                        case SymptomEffect.Echo:
                            float delayed = age - (copy - 1) * .09f - phase % 6 * .025f;
                            float echo = Mathf.Repeat(delayed / 1.25f, 1);
                            return new Pose(delayed < 0 ? 0 : Key(echo, (0, 0), (.13f, .74f), (.50f, .27f), (.78f, .05f), (1, 0)) / copy,
                                new Color(.57f, .26f, .78f), new Vector2(echo * 20 * copy, 2.5f * copy));
                        case SymptomEffect.MirrorDoze when copy == 1:
                            float doze = Mathf.Repeat(Mathf.Max(0f, age - phase % 6 * .035f) / 2.2f, 1);
                            return new Pose(.64f, new Color(.26f, .52f, .80f), new Vector2(4, -4),
                                Key(doze, (0, 3), (.12f, 27), (.29f, 7), (.48f, 20), (.76f, 2), (1, 3)),
                                new Vector2(-1, 1));
                        case SymptomEffect.Frost when copy == 1:
                            return new Pose(Mathf.Clamp01(age / .58f) * .5f, new Color(.31f, .70f, .96f),
                                scale: new Vector2(1.28f, 1.21f));
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
