using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The potion inside a bottle picture: liquid in the brew's colour (the one made in the workshop, see
    /// <see cref="CauldronColor.LastBrew"/>), clipped to the bottle's shape, with the glass drawn faintly over it.
    /// It fills with a slosh when the bottle is picked and drains when it is put back or drunk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PotionFill : MonoBehaviour
    {
        /// <summary>The fill line when full, as a fraction of the picture's height (below the neck and cork).</summary>
        public float FullLine { get; set; } = 0.6f;

        private static readonly Color Fallback = new(0.55f, 0.85f, 0.3f);

        private RectTransform liquid;
        private Image liquidImage;
        private Image surface;
        private float level;
        private float bottleTilt;
        private float slosh;
        private MotionHandle motion;

        /// <summary>The fill on this bottle picture, made on first use.</summary>
        public static PotionFill On(RectTransform bottle, Sprite sprite)
        {
            var fill = bottle.GetComponent<PotionFill>();
            if (fill != null) return fill;
            fill = bottle.gameObject.AddComponent<PotionFill>();
            fill.Build(sprite, bottle.GetComponent<Image>()?.preserveAspect ?? false);
            return fill;
        }

        /// <summary>The colour liquid reads as inside glass, from the brew colour.</summary>
        public static Color BrewDisplay
        {
            get
            {
                if (CauldronColor.LastBrew is not { } brew) return Fallback;
                Color.RGBToHSV(brew, out float h, out float s, out _);
                return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.05f), 0.8f);
            }
        }

        private void Build(Sprite sprite, bool preserveAspect)
        {
            var shape = Child("Potion", transform);
            var shapeImage = shape.gameObject.AddComponent<Image>();
            shapeImage.sprite = sprite;
            shapeImage.preserveAspect = preserveAspect;
            shapeImage.raycastTarget = false;
            shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            shape.SetSiblingIndex(0);

            // Wider and deeper than the bottle, so tilting the surface never shows its corners.
            liquid = Child("Liquid", shape);
            liquid.anchorMin = new Vector2(-1f, -1f);
            liquid.anchorMax = new Vector2(2f, 0f);
            liquid.pivot = new Vector2(0.5f, 1f);
            liquidImage = liquid.gameObject.AddComponent<Image>();
            liquidImage.raycastTarget = false;

            var top = Child("Surface", liquid);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = Vector2.one;
            top.offsetMin = new Vector2(0f, -5f);
            surface = top.gameObject.AddComponent<Image>();
            surface.raycastTarget = false;

            // the glass's shine and edges over the liquid
            var glass = Child("Glass", transform);
            var glassImage = glass.gameObject.AddComponent<Image>();
            glassImage.sprite = sprite;
            glassImage.preserveAspect = preserveAspect;
            glassImage.color = new Color(1f, 1f, 1f, 0.4f);
            glassImage.raycastTarget = false;
            glass.SetSiblingIndex(1);

            Colour = BrewDisplay;
            Apply();
        }

        public Color Colour
        {
            set
            {
                var c = value;
                c.a = 0.88f;
                liquidImage.color = c;
                var light = Color.Lerp(value, Color.white, 0.45f);
                light.a = 0.9f;
                surface.color = light;
            }
        }

        /// <summary>Pours in up to <paramref name="target"/> (0 empty, 1 full), sloshing as it settles.</summary>
        public void FillTo(float target, float seconds, bool slosh = true)
        {
            motion.TryCancel();
            float from = level;
            motion = LMotion.Create(0f, 1f, seconds).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    level = Mathf.Lerp(from, target, 1f - (1f - t) * (1f - t) * (1f - t));
                    this.slosh = slosh ? Mathf.Sin(t * 17f) * (1f - t) * 9f : 0f;
                    Apply();
                }).AddTo(gameObject);
        }

        /// <summary>Sets the level at once.</summary>
        public void SetLevel(float value)
        {
            motion.TryCancel();
            level = Mathf.Clamp01(value);
            slosh = 0f;
            Apply();
        }

        /// <summary>
        /// The bottle picture is tipped by <paramref name="degrees"/>: the surface turns the other way so it stays
        /// level, as liquid does.
        /// </summary>
        public void KeepLevel(float degrees)
        {
            bottleTilt = degrees;
            Apply();
        }

        private void Apply()
        {
            if (liquid == null) return;
            liquid.anchorMax = new Vector2(2f, level * FullLine);
            liquid.offsetMin = liquid.offsetMax = Vector2.zero;
            liquid.localRotation = Quaternion.Euler(0f, 0f, slosh - bottleTilt);
            liquid.gameObject.SetActive(level > 0.001f);
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
