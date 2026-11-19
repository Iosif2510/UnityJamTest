using UnityEngine;

namespace PotionShop.Portraits
{
    internal sealed class SlimeBurstGraphic : UnityEngine.UI.MaskableGraphic
    {
        private float progress;

        internal void SetProgress(float value)
        {
            progress = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            float radius = size * Mathf.Lerp(.10f, .55f, 1 - (1 - progress) * (1 - progress));
            for (int i = 0; i < 14; i++)
            {
                float angle = i * 2.399963f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var center = direction * radius * (.65f + i % 4 * .15f);
                center.y -= size * .27f * progress * progress;
                float droplet = size * (.012f + i % 4 * .005f) * (1 - progress * .72f);
                var tint = Color.Lerp(new Color(.19f, .69f, .23f), new Color(.76f, 1, .38f), (i % 3) * .5f);
                tint.a = .88f * (1 - progress);
                int first = mesh.currentVertCount;
                mesh.AddVert(center, tint, Vector2.zero);
                for (int edge = 0; edge < 8; edge++)
                {
                    float theta = edge * Mathf.PI / 4f;
                    var offset = direction * (Mathf.Cos(theta) * 1.55f) +
                        new Vector2(-direction.y, direction.x) * Mathf.Sin(theta);
                    mesh.AddVert(center + offset * droplet, tint, Vector2.zero);
                    mesh.AddTriangle(first, first + edge + 1, first + (edge + 1) % 8 + 1);
                }
            }
        }
    }
}
