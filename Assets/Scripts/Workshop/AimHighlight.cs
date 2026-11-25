using System.Collections.Generic;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// Marks the ingredient under the crosshair: its colours brighten and it breathes slightly larger, so the player
    /// sees what a click will pick up before reading the prompt.
    /// </summary>
    internal sealed class AimHighlight
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private const float Brighten = 1.35f;

        private readonly MaterialPropertyBlock block = new();
        private readonly List<Renderer> renderers = new();
        private Transform current;
        private Vector3 currentScale;
        private float since;

        public void Focus(Transform target)
        {
            if (target != current)
            {
                Release();
                current = target;
                if (current == null) return;
                currentScale = current.localScale;
                since = Time.unscaledTime;
                current.GetComponentsInChildren(renderers);
                foreach (var r in renderers)
                {
                    var material = r.sharedMaterial;
                    if (material == null || !material.HasProperty(BaseColor)) continue;
                    var color = material.GetColor(BaseColor);
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColor, new Color(color.r * Brighten, color.g * Brighten, color.b * Brighten, color.a));
                    r.SetPropertyBlock(block);
                }
            }
            if (current == null) return;
            // A soft breathing: 4 % bigger at the top of each beat.
            float t = Time.unscaledTime - since;
            float breath = 1f + 0.04f * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f * 1.4f));
            current.localScale = currentScale * breath;
        }

        public void Release()
        {
            if (current != null)
            {
                current.localScale = currentScale;
                foreach (var r in renderers)
                    if (r != null) r.SetPropertyBlock(null);
            }
            renderers.Clear();
            current = null;
        }
    }
}
