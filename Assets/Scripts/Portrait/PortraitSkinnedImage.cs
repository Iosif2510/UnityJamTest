using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D.Animation;

namespace PotionShop.Portraits
{
    // SpriteSkin owns deformation; UI owns clipping, tint and drawing.
    public sealed class PortraitSkinnedImage : Image
    {
        private SpriteSkin source;
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
        private void LateUpdate() { if (source != null) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
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
    }
}
