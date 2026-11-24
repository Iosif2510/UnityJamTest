using LitMotion;
using PotionShop.Data;
using PotionShop.Portraits;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The customer at the counter notices the clock. Under a minute they start to fidget and tilt their head; under
    /// half a minute they turn cross (their angry face), sweat drops run down beside their head and now and then they
    /// mutter "흠…" in a small bubble. Everything relaxes when the clock stops.
    /// </summary>
    internal sealed class CustomerImpatience
    {
        private const float FidgetSeconds = 60f;
        private const float CrossSeconds = 30f;
        private const float SweatEvery = 1.3f;
        private const float MutterEvery = 11f;

        private static readonly Color Ink = new(0.204f, 0.118f, 0.071f);
        private static readonly Color Paper = new(1f, 0.965f, 0.894f);
        private static Sprite dropSprite, bubbleSprite;

        private readonly PortraitView portrait;
        private readonly PortraitColorCatalog palette;
        private readonly TMP_FontAsset font;
        private bool cross;
        private bool sweatLeft;
        private float nextSweat, nextMutter;
        private RectTransform bubble;
        private TextMeshProUGUI bubbleText;
        private float huffUntil;
        private MotionHandle bubbleMotion;

        public CustomerImpatience(PortraitView portrait, PortraitColorCatalog palette, TMP_FontAsset font)
        {
            this.portrait = portrait;
            this.palette = palette;
            this.font = font;
        }

        /// <summary>The portrait was composed again (a new phase): its face is the calm one once more.</summary>
        public void Reset()
        {
            cross = false;
            HideBubble();
            if (portrait != null) portrait.SetImpatience(0f);
        }

        public void Show(float remaining, bool running, GeneratedOrder order)
        {
            if (portrait == null || !portrait.isActiveAndEnabled) return;
            if (!running || remaining <= 0f || order == null)
            {
                portrait.SetImpatience(0f);
                HideBubble();
                return;
            }

            portrait.SetImpatience(remaining <= CrossSeconds ? 1f : remaining <= FidgetSeconds ? .5f : 0f);
            if (remaining > CrossSeconds) return;

            float now = Time.unscaledTime;
            if (!cross)
            {
                cross = true;
                var angry = order.Customer.AngryExpression;
                if (angry != null && palette != null) portrait.Show(order, palette, angry);
                portrait.Speak(.7f);
                Mutter();
                nextSweat = now + .3f;
                nextMutter = now + MutterEvery;
            }
            if (now >= nextSweat)
            {
                Sweat();
                nextSweat = now + SweatEvery * Random.Range(.8f, 1.2f);
            }
            if (now >= nextMutter)
            {
                Mutter();
                nextMutter = now + MutterEvery;
            }
        }

        private RectTransform Rect => (RectTransform)portrait.transform;

        /// <summary>A point on the portrait given as fractions of its rect (0,0 bottom-left, 1,1 top-right).</summary>
        private Vector2 At(float fx, float fy)
        {
            var r = Rect.rect;
            return new Vector2(r.xMin + r.width * fx, r.yMin + r.height * fy);
        }

        private void Sweat()
        {
            sweatLeft = !sweatLeft;
            var go = new GameObject("SweatDrop", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(portrait.transform, false);
            rect.SetAsLastSibling();
            var image = go.GetComponent<Image>();
            image.sprite = DropSprite;
            image.raycastTarget = false;
            float size = Mathf.Clamp(Rect.rect.width * .05f, 18f, 34f);
            rect.sizeDelta = new Vector2(size, size * 1.35f);
            Vector2 start = At(sweatLeft ? .32f : .68f, .73f) + new Vector2(Random.Range(-8f, 8f), 0f);
            rect.anchoredPosition = start;
            rect.localScale = Vector3.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, sweatLeft ? 12f : -12f);
            float fall = size * 1.6f;
            LMotion.Create(0f, 1f, 1.1f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (go != null) Object.Destroy(go); })
                .Bind(t =>
                {
                    if (rect == null) return;
                    float pop = Mathf.Clamp01(t / .15f);
                    rect.localScale = Vector3.one * (pop < 1f ? Mathf.Sin(pop * Mathf.PI * .6f) / .81f : 1f);
                    rect.anchoredPosition = start + Vector2.down * (fall * t * t);
                    image.color = new Color(1f, 1f, 1f, t < .7f ? 1f : 1f - (t - .7f) / .3f);
                });
        }

        /// <summary>
        /// Time ran out: the customer turns cross at once and huffs "흥!" in the bubble, which stays up through the
        /// change of phase.
        /// </summary>
        public void Huff(GeneratedOrder order)
        {
            if (portrait == null || !portrait.isActiveAndEnabled) return;
            var angry = order?.Customer.AngryExpression;
            if (angry != null && palette != null) portrait.Show(order, palette, angry);
            portrait.Speak(1f);
            huffUntil = Time.unscaledTime + 1.8f;
            Mutter(Loc.T("흥!"), 1.8f);
        }

        private void Mutter(string text = null, float seconds = 2.6f)
        {
            text ??= Loc.T("흠…");
            if (bubble == null) bubble = MakeBubble();
            bubbleText.text = text;
            bubble.SetAsLastSibling();
            bubble.anchoredPosition = At(.76f, .8f);
            bubbleMotion.TryCancel();
            var b = bubble;
            bubbleMotion = LMotion.Create(0f, 1f, seconds).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    if (b == null) return;
                    float s = t < .12f ? EaseOutBack(t / .12f) : t > .9f ? 1f - (t - .9f) / .1f : 1f;
                    b.localScale = Vector3.one * s;
                });
            GameSfx.Play("Paper", .35f, .8f, .05f);
        }

        private void HideBubble()
        {
            if (Time.unscaledTime < huffUntil) return;
            bubbleMotion.TryCancel();
            if (bubble != null) bubble.localScale = Vector3.zero;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private RectTransform MakeBubble()
        {
            var go = new GameObject("ImpatientBubble", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(portrait.transform, false);
            rect.sizeDelta = new Vector2(150f, 96f);
            rect.pivot = new Vector2(.15f, .1f);
            rect.localScale = Vector3.zero;
            var image = go.GetComponent<Image>();
            image.sprite = BubbleSprite;
            image.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = new Vector2(0f, .22f);
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = bubbleText = label.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = Loc.T("흠…");
            text.fontSize = 38;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Ink;
            text.raycastTarget = false;
            return rect;
        }

        private static Sprite DropSprite => dropSprite != null ? dropSprite : dropSprite = MakeDrop();
        private static Sprite BubbleSprite => bubbleSprite != null ? bubbleSprite : bubbleSprite = MakeBubbleSprite();

        /// <summary>A sweat drop: round bottom, pointed top, pale blue with an inked edge and a glint.</summary>
        private static Sprite MakeDrop()
        {
            const int w = 48, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var fill = new Color(.62f, .86f, 1f);
            var edge = new Color(.2f, .38f, .62f);
            var px = new Color[w * h];
            const float cx = .5f, cy = .36f, r = .3f, tip = .97f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    // distance inside the shape (in units of width), via the drop's half-width at this height
                    float half = v <= cy ? Mathf.Sqrt(Mathf.Max(0f, r * r - (v - cy) * (v - cy)))
                        : r * Mathf.Pow(Mathf.Max(0f, 1f - (v - cy) / (tip - cy)), .85f);
                    float inside = half - Mathf.Abs(u - cx);
                    if (v < cy - r) inside = -1f;
                    float a = Mathf.Clamp01(inside * w * .9f + .5f);
                    var c = inside * w < 4f ? edge : fill;
                    float glint = Mathf.Clamp01(1f - new Vector2((u - .4f) * 3.2f, (v - .3f) * 2.4f).magnitude * 3f);
                    c = Color.Lerp(c, Color.white, glint * .9f);
                    c.a = a;
                    px[y * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
        }

        /// <summary>A small cream thought bubble with a sepia outline and two trailing dots towards the speaker.</summary>
        private static Sprite MakeBubbleSprite()
        {
            const int w = 150, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // ellipse body in the upper part, two dots at the lower left
                    float d = Ellipse(x, y, 82f, 58f, 64f, 34f);
                    d = Mathf.Min(d, Ellipse(x, y, 26f, 18f, 11f, 9f));
                    d = Mathf.Min(d, Ellipse(x, y, 12f, 6f, 6f, 5f));
                    float a = Mathf.Clamp01(.5f - d);
                    var c = d > -4.5f ? Ink : Paper;
                    c.a = a;
                    px[y * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
        }

        /// <summary>Approximate signed distance in pixels to an axis-aligned ellipse (negative inside).</summary>
        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x + .5f - cx) / rx, ny = (y + .5f - cy) / ry;
            float k = Mathf.Sqrt(nx * nx + ny * ny);
            return (k - 1f) * Mathf.Min(rx, ry);
        }
    }
}
