using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PotionShop.Art
{
    internal sealed class UiTypography : IDisposable
    {
        private static readonly int UnderlayColor = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilate = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftness = Shader.PropertyToID("_UnderlaySoftness");
        private readonly Dictionary<(Material Source, bool LightText), Material> shadows = new();

        internal void Apply(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                Apply(text);
            foreach (var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                ApplyLegacyShadow(text);
        }

        internal void Apply(TMP_Text text)
        {
            TMP_FontAsset font = text.font;
            if (font != null && font.faceInfo.pointSize > 0)
            {
                var face = font.faceInfo;
                // TMP spacing is expressed in hundredths of an em. Use a Hangul glyph as the tracking reference.
                float units = face.scale * 100f / face.pointSize;
                float advance = font.characterLookupTable.TryGetValue('가', out var reference)
                    ? reference.glyph.metrics.horizontalAdvance * reference.glyph.scale * reference.scale * units
                    : 90f;
                text.characterSpacing -= (advance + font.normalSpacingOffset + text.characterSpacing) * .08f;
                text.lineSpacing += (face.lineHeight * units + text.lineSpacing) * .10f;
            }

            Material source = text.fontSharedMaterial;
            bool lightText = text.color.grayscale > .48f;
            if (source == null || !source.HasProperty(UnderlayColor))
            {
                ApplyLegacyShadow(text);
                return;
            }

            var key = (source, lightText);
            if (!shadows.TryGetValue(key, out var material))
            {
                material = new Material(source) { name = $"{source.name} UI Shadow", hideFlags = HideFlags.DontSave };
                material.DisableKeyword("UNDERLAY_INNER");
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor(UnderlayColor, ShadowColor(lightText));
                material.SetFloat(UnderlayOffsetX, lightText ? .37f : .30f);
                material.SetFloat(UnderlayOffsetY, lightText ? -.52f : -.42f);
                material.SetFloat(UnderlayDilate, lightText ? .10f : .02f);
                material.SetFloat(UnderlaySoftness, lightText ? .22f : .30f);
                shadows.Add(key, material);
            }

            text.fontSharedMaterial = material;
            text.UpdateMeshPadding();
        }

        private static Color ShadowColor(bool lightText) => lightText
            ? new Color(.10f, .06f, .035f, .86f)
            : new Color(.18f, .085f, .045f, .52f);

        private static void ApplyLegacyShadow(UnityEngine.UI.Graphic text)
        {
            UnityEngine.UI.Shadow shadow = null;
            foreach (var effect in text.GetComponents<UnityEngine.UI.Shadow>())
            {
                if (effect is UnityEngine.UI.Outline) continue;
                shadow = effect;
                break;
            }

            bool lightText = text.color.grayscale > .48f;
            if (shadow == null) shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = ShadowColor(lightText);
            shadow.effectDistance = lightText ? new Vector2(1.5f, -2.2f) : new Vector2(1f, -1.6f);
            shadow.useGraphicAlpha = true;
            shadow.enabled = true;
        }

        public void Dispose()
        {
            foreach (var material in shadows.Values)
                UnityEngine.Object.Destroy(material);
            shadows.Clear();
        }
    }
}
