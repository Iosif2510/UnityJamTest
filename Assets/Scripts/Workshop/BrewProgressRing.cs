using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// A small ring on the screen while the cauldron brews, filling clockwise in the brew's own colour. It pops and
    /// disappears when the potion is done. Kept on screen rather than over the pot: looking down into the cauldron
    /// puts anything above its rim behind the time bar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BrewProgressRing : MonoBehaviour
    {
        private const float Pixels = 92f;

        private static Sprite discSprite, ringSprite;

        private WorkshopCauldron cauldron;
        private CauldronColor colour;
        private Transform root;
        private Image fill;
        private bool showing;
        private MotionHandle motion;

        internal void Bind(WorkshopCauldron owner, CauldronColor liquidColour)
        {
            cauldron = owner;
            colour = liquidColour;

            var canvasObject = new GameObject("BrewProgressCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("BrewProgressRing", typeof(RectTransform));
            root = go.transform;
            var rect = (RectTransform)root;
            rect.SetParent(canvasObject.transform, false);
            // under the crosshair, above the cauldron's name and prompt
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -160f);
            rect.sizeDelta = new Vector2(Pixels, Pixels);

            Layer("Back", DiscSprite, new Color(1f, 0.965f, 0.886f, 0.9f), 0f);
            Layer("Track", RingSprite, new Color(0.204f, 0.118f, 0.071f, 0.25f), 0f);
            fill = Layer("Fill", RingSprite, Color.white, 0f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            Layer("Rim", RimSprite, new Color(0.204f, 0.118f, 0.071f, 0.9f), 0f);

            go.SetActive(false);
        }

        private Image Layer(string name, Sprite sprite, Color color, float inset)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            var image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void LateUpdate()
        {
            if (cauldron == null || root == null) return;
            float progress = cauldron.BrewProgress;
            bool brewing = cauldron.RemainingSeconds > 0d;

            if (brewing && !showing)
            {
                showing = true;
                root.gameObject.SetActive(true);
                motion.TryCancel();
                motion = LMotion.Create(0f, 1f, 0.3f).WithEase(Ease.OutBack)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).Bind(Scale);
            }
            else if (!brewing && showing)
            {
                showing = false;
                fill.fillAmount = 1f;
                motion.TryCancel();
                var go = root.gameObject;
                motion = LMotion.Create(0f, 1f, 0.45f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithOnComplete(() => { if (go != null) go.SetActive(false); })
                    .Bind(t => Scale(t < 0.35f ? 1f + 0.3f * Mathf.Sin(t / 0.35f * Mathf.PI * 0.5f) : 1.3f * (1f - (t - 0.35f) / 0.65f)));
            }
            if (!root.gameObject.activeSelf) return;

            if (showing) fill.fillAmount = progress;
            var c = colour != null ? colour.Current : Color.green;
            // the brew colour is dark for a liquid; brighten it to read as a fill
            Color.RGBToHSV(c, out float h, out float s, out _);
            fill.color = Color.HSVToRGB(h, Mathf.Clamp01(s * 0.9f), 0.85f);


        }

        private void Scale(float s)
        {
            if (root == null) return;
            root.localScale = Vector3.one * Mathf.Max(0f, s);
        }

        private void OnDisable()
        {
            motion.TryCancel();
            showing = false;
            if (root != null) root.gameObject.SetActive(false);
        }

        private static Sprite DiscSprite => discSprite != null ? discSprite : discSprite = Draw(r => 60f - r);
        private static Sprite RingSprite => ringSprite != null ? ringSprite : ringSprite = Draw(r => 11f - Mathf.Abs(r - 44f));
        private static Sprite rimSprite;
        private static Sprite RimSprite => rimSprite != null ? rimSprite : rimSprite = Draw(r => 3f - Mathf.Abs(r - 59f));

        private static Sprite Draw(System.Func<float, float> inside)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float a = Mathf.Clamp01(inside(Mathf.Sqrt(dx * dx + dy * dy)) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
