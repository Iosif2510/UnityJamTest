using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;
using UnityEngine.U2D.Animation;

namespace PotionShop.Portraits
{
    // SpriteSkin or Spine supplies deformation; UI supplies clipping, tint and drawing.
    public sealed class PortraitSkinnedImage : Image
    {
        private SpriteSkin source;
        private PortraitSpineRig.MeshBinding spine;
        private readonly List<Vector3> positions = new();
        public void Bind(SpriteSkin skin)
        {
            source = skin;
            if (source != null)
            {
                source.alwaysUpdate = true; source.forceCpuDeformation = true;
                source.GetComponent<SpriteRenderer>().forceRenderingOff = true;
            }
            SetVerticesDirty();
        }
        internal void BindSpine(PortraitSpineRig.MeshBinding mesh)
        {
            spine = mesh;
            SetVerticesDirty();
        }

        private void LateUpdate() { if (source != null || spine != null) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (spine != null && sprite != null)
            {
                PopulateSpineMesh(vh);
                return;
            }
            if (source == null || sprite == null || source.GetComponent<SpriteRenderer>().sprite != sprite || !source.HasCurrentDeformedVertices())
            { base.OnPopulateMesh(vh); return; }
            positions.Clear();
            foreach (var p in source.GetDeformedVertexPositionData()) positions.Add(p);
            var uv = sprite.uv; var triangles = sprite.triangles;
            if (positions.Count != uv.Length) { base.OnPopulateMesh(vh); return; }
            var rect = GetPixelAdjustedRect(); vh.Clear();
            for (int i = 0; i < positions.Count; i++)
            {
                var pixel = (Vector2)positions[i] * sprite.pixelsPerUnit + sprite.pivot;
                vh.AddVert(new Vector3(rect.x + pixel.x / sprite.rect.width * rect.width,
                    rect.y + pixel.y / sprite.rect.height * rect.height), color, uv[i]);
            }
            for (int i = 0; i < triangles.Length; i += 3) vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }

        private void PopulateSpineMesh(VertexHelper vh)
        {
            var rect = GetPixelAdjustedRect();
            var bounds = spine.Bounds;
            var uv = DataUtility.GetOuterUV(sprite);
            var vertices = spine.Vertices;
            var sourceUVs = spine.UVs;
            var triangles = spine.Triangles;
            vh.Clear();
            for (int i = 0; i < vertices.Length; i += 2)
            {
                var position = new Vector3(
                    rect.x + (vertices[i] - bounds.x) / bounds.width * rect.width,
                    rect.y + (vertices[i + 1] - bounds.y) / bounds.height * rect.height);
                var texCoord = new Vector2(Mathf.Lerp(uv.x, uv.z, sourceUVs[i]),
                    Mathf.Lerp(uv.y, uv.w, 1f - sourceUVs[i + 1]));
                vh.AddVert(position, color, texCoord);
            }
            for (int i = 0; i < triangles.Length; i += 3)
                vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
