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
                float delay = (cue.PhaseIndex * 37 % 17) * .016f;
                if (age < delay) continue;
                float cycle = Mathf.Repeat(age - delay, 1.18f);
                if (cycle > .62f) continue;
                float fall = Mathf.InverseLerp(.11f, .62f, cycle);
                float alpha = cycle < .11f ? Mathf.InverseLerp(0, .11f, cycle) : 1 - fall;
                float size = ch.pointSize;
                var center = new Vector2((ch.bottomLeft.x + ch.bottomRight.x) * .5f,
                    ch.baseLine - fall * fall * size * .86f);
                var tint = (Color32)new Color(.12f, .49f, .81f, alpha);
                int start = vertices.currentVertCount;
                vertices.AddVert(center, tint, Vector2.zero);
                const int sides = 8;
                for (int k = 0; k < sides; k++)
                {
                    float angle = k * Mathf.PI * 2 / sides;
                    var offset = new Vector2(Mathf.Sin(angle) * .105f, Mathf.Cos(angle) * .145f) * size;
                    vertices.AddVert(center + offset, tint, Vector2.zero);
                    vertices.AddTriangle(start, start + k + 1, start + (k + 1) % sides + 1);
                }
            }
        }
    }
}
