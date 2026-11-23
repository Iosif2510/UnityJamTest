using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The cat from the guide memo ("밥 주지 마. 이미 먹었음"), asleep in the basket on the loft desk. It breathes and
    /// lets a "z" drift up now and then; click it and it purrs, gives a little squish and a heart floats up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftCat : MonoBehaviour, IPointerClickHandler
    {
        // Where the cat sits in the 1920×1080 BG_Loft picture (the basket at the lower right).
        private static readonly Rect InPicture = new(1577f, 751f, 315f, 215f);
        private const float PurrSeconds = 1.8f;
        private const float SnoreEvery = 3.2f;

        private static Sprite heartSprite;

        private RectTransform body;
        private Font font;
        private float nextSnore;
        private float purringUntil;

        /// <summary>Puts the cat into the basket of the backdrop picture (the image <see cref="OnlineUiParts.Backdrop"/> made).</summary>
        internal static LoftCat Place(Image backdrop, Font font)
        {
            if (!OnlineUiParts.Has("LOFT_Cat")) return null;
            var picture = backdrop.rectTransform;
            var rect = OnlineUi.Rect("Cat", picture,
                new Vector2(InPicture.xMin / 1920f, 1f - InPicture.yMax / 1080f),
                new Vector2(InPicture.xMax / 1920f, 1f - InPicture.yMin / 1080f));
            var image = OnlineUiParts.Draw(rect, "LOFT_Cat", raycast: true);
            var cat = rect.gameObject.AddComponent<LoftCat>();
            cat.body = rect;
            cat.font = font;
            rect.pivot = new Vector2(0.5f, 0.3f); // breathes up from where it lies
            return cat;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            bool purring = t < purringUntil;
            // slow sleeping breath; quicker and deeper while purring
            float breath = Mathf.Sin(t * (purring ? 5.5f : 1.9f)) * (purring ? 0.02f : 0.014f);
            body.localScale = new Vector3(1f + breath * 0.4f, 1f + breath, 1f);
            if (t >= nextSnore)
            {
                nextSnore = t + SnoreEvery;
                if (!purring) Float(Snore(), new Vector2(0.72f, 0.62f), 1.8f, 55f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime < purringUntil) return;
            purringUntil = Time.unscaledTime + PurrSeconds;
            GameSfx.Play("Purr", 0.8f, 1f, 0.06f);
            OrderHudMotion.Bounce(body, 1.06f);
            Float(Heart(), new Vector2(0.7f, 0.66f), 1.8f, 80f);
        }

        /// <summary>A small "z" in the loft's ink colour.</summary>
        private RectTransform Snore()
        {
            var rect = OnlineUi.Rect("Z", Overlay, Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(56f, 56f);
            var label = OnlineUi.Label(rect, "z", 40, font, OnlineUiParts.Cream, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            return rect;
        }

        private RectTransform Heart()
        {
            var rect = OnlineUi.Rect("Heart", Overlay, Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(42f, 39f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = HeartSprite;
            image.color = new Color(0.93f, 0.45f, 0.42f);
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>Marks float over everything on the loft canvas, memos included.</summary>
        private Transform Overlay => body.GetComponentInParent<Canvas>().rootCanvas.transform;

        /// <summary>Lets a small mark rise from a point on the cat (fractions of its rect), sway and fade.</summary>
        private void Float(RectTransform mark, Vector2 at, float seconds, float rise)
        {
            var r = body.rect;
            Vector2 local = new(r.xMin + r.width * at.x, r.yMin + r.height * at.y);
            Vector2 start = (Vector2)mark.parent.InverseTransformPoint(body.TransformPoint(local));
            mark.SetAsLastSibling();
            mark.localPosition = start;
            var graphic = mark.GetComponent<Graphic>();
            var colour = graphic.color;
            var go = mark.gameObject;
            LMotion.Create(0f, 1f, seconds).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (go != null) Destroy(go); })
                .Bind(t =>
                {
                    if (mark == null) return;
                    mark.localPosition = start + new Vector2(Mathf.Sin(t * 5f) * 8f + t * 14f, t * rise);
                    float s = Mathf.Lerp(0.6f, 1.1f, Mathf.Clamp01(t * 2.5f));
                    mark.localScale = new Vector3(s, s, 1f);
                    colour.a = t < 0.6f ? Mathf.Clamp01(t * 5f) : 1f - (t - 0.6f) / 0.4f;
                    graphic.color = colour;
                }).AddTo(go);
        }

        private static Sprite HeartSprite => heartSprite != null ? heartSprite : heartSprite = MakeHeart();

        private static Sprite MakeHeart()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // two round lobes over a point: distance-based so the edge stays soft
                    float u = (x + 0.5f) / size * 2.2f - 1.1f, v = (y + 0.5f) / size * 2.1f - 1.15f;
                    float lobe = Mathf.Min(Vector2.Distance(new Vector2(Mathf.Abs(u), v), new Vector2(0.48f, 0.38f)) - 0.5f, 99f);
                    float point = v <= 0.38f && v >= -0.95f ? Mathf.Abs(u) - (v + 0.95f) * 0.72f : 99f;
                    float d = Mathf.Min(lobe, point) * size / 2.2f;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
