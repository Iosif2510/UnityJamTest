using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PotionShop.Data;

namespace PotionShop
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DialogueRainGraphic : MaskableGraphic
    {
        private TMP_Text body;
        private DialogueTimeline timeline;
        private double elapsed;
        private double skippedAt;

        internal void Draw(TMP_Text text, DialogueTimeline line, double seconds, double skipTime)
        {
            body = text;
            timeline = line;
            elapsed = seconds;
            skippedAt = skipTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (body == null || timeline == null) return;
            var info = body.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible || !timeline.TryGetCue(ch.index, out var cue) || cue.Effect != SymptomEffect.RainDrip)
                    continue;
                float age = (float)(elapsed - System.Math.Min(cue.Start, skippedAt));
                if (age < 0) continue;
                float delayed = (cue.PhaseIndex * 37 % 17) / 17f;
                float cycle = Mathf.Repeat(age / 2.8f + delayed, 1);
                if (cycle <= .7f) continue;
                float fall = Mathf.InverseLerp(.76f, 1, cycle);
                float alpha = cycle < .76f ? Mathf.InverseLerp(.7f, .76f, cycle) : 1 - fall;
                float size = ch.pointSize;
                var center = new Vector2((ch.bottomLeft.x + ch.bottomRight.x) * .5f,
                    ch.baseLine - fall * fall * size * .75f);
                var tint = (Color32)new Color(.40f, .76f, 1, alpha * .85f);
                int start = vertices.currentVertCount;
                vertices.AddVert(center, tint, Vector2.zero);
                const int sides = 8;
                for (int k = 0; k < sides; k++)
                {
                    float angle = k * Mathf.PI * 2 / sides;
                    var offset = new Vector2(Mathf.Sin(angle) * .075f, Mathf.Cos(angle) * .11f) * size;
                    vertices.AddVert(center + offset, tint, Vector2.zero);
                    vertices.AddTriangle(start, start + k + 1, start + (k + 1) % sides + 1);
                }
            }
        }
    }
}
