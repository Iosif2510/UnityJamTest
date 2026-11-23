using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Portraits
{
    /// <summary>
    /// A code rig for the slime portrait (customers have Spine rigs; the slime is a single picture). The picture is
    /// split into a grid whose bottom stays planted on the counter while the body breathes (squash and stretch, wider
    /// when lower) and sways like jelly, more towards the top. <see cref="Jiggle"/> adds a wobble that rings out, for
    /// speech beats and the moment it appears.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SlimeJelly : BaseMeshEffect
    {
        private const int Columns = 8, Rows = 10;
        private const float BreathSpeed = 1.6f, BreathAmount = 0.035f;
        private const float SwaySpeed = 1.1f, SwayAmount = 0.025f;

        private float jiggle;
        private float jiggleSince = -10f;
        private readonly List<UIVertex> quad = new();
        private readonly List<UIVertex> grid = new();

        /// <summary>A wobble of the given strength (0–1) that dies out over about a second.</summary>
        public void Jiggle(float strength)
        {
            jiggle = Mathf.Clamp01(strength);
            jiggleSince = Time.unscaledTime;
        }

        private void Update()
        {
            if (graphic != null && graphic.enabled) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount != 4) return;
            quad.Clear();
            vh.GetUIVertexStream(quad);
            if (quad.Count < 6) return;
            // corners of the simple quad: find min/max position and their uvs
            UIVertex bl = quad[0], tr = quad[0];
            foreach (var v in quad)
            {
                if (v.position.x <= bl.position.x && v.position.y <= bl.position.y) bl = v;
                if (v.position.x >= tr.position.x && v.position.y >= tr.position.y) tr = v;
            }
            Vector2 p0 = bl.position, p1 = tr.position, uv0 = bl.uv0, uv1 = tr.uv0;
            float w = p1.x - p0.x, h = p1.y - p0.y;
            if (w <= 0f || h <= 0f) return;

            float t = Time.unscaledTime;
            float breath = Mathf.Sin(t * BreathSpeed * Mathf.PI * 2f * 0.5f);
            float age = t - jiggleSince;
            float ring = age < 1.2f ? jiggle * Mathf.Exp(-age * 3.2f) * Mathf.Sin(age * 22f) : 0f;
            float centreX = p0.x + w * 0.5f;

            grid.Clear();
            for (int r = 0; r <= Rows; r++)
            {
                float fy = r / (float)Rows;                 // 0 at the planted bottom, 1 at the top
                for (int c = 0; c <= Columns; c++)
                {
                    float fx = c / (float)Columns;
                    float x = p0.x + w * fx;
                    float y = p0.y + h * fy;
                    // breathe: taller and narrower on the in-breath, the bottom row never moves vertically
                    float stretch = breath * BreathAmount + ring * 0.08f;
                    y = p0.y + (y - p0.y) * (1f + stretch);
                    x = centreX + (x - centreX) * (1f - stretch * 0.7f * fy);
                    // jelly sway: grows towards the top, a little wave through the body
                    float sway = Mathf.Sin(t * SwaySpeed * Mathf.PI * 2f * 0.5f + fy * 1.4f) * SwayAmount * fy * fy;
                    x += (sway + ring * 0.06f * fy * fy * Mathf.Sin(fy * 3f)) * w;
                    var v = bl;
                    v.position = new Vector3(x, y, 0f);
                    v.uv0 = new Vector2(Mathf.Lerp(uv0.x, uv1.x, fx), Mathf.Lerp(uv0.y, uv1.y, fy));
                    grid.Add(v);
                }
            }
            vh.Clear();
            foreach (var v in grid) vh.AddVert(v);
            int stride = Columns + 1;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    int i = r * stride + c;
                    vh.AddTriangle(i, i + stride, i + stride + 1);
                    vh.AddTriangle(i, i + stride + 1, i + 1);
                }
        }
    }
}
