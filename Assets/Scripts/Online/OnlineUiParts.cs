using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The designer's UI parts for the online screens (Resources/OnlineUi, see Docs/UI_디자인_핸드오프/부품_규격표.md),
    /// looked up by file name, plus the lettering colours the spec gives for each kind of background.
    /// Note: the Maplestory font has no ✓ ▸ ● glyphs, so screen text must not use them.
    /// </summary>
    internal static class OnlineUiParts
    {
        private const string Folder = "OnlineUi";
        private static Dictionary<string, Sprite> sprites;

        /// <summary>On yellow buttons, parchment, grey buttons and chips.</summary>
        internal static readonly Color Ink = new(0.290f, 0.180f, 0.118f);      // #4A2E1E
        internal static readonly Color InkSoft = new(0.471f, 0.345f, 0.251f);  // #785840
        /// <summary>On the red name plate, the desk, the brown toast and the bottom strip.</summary>
        internal static readonly Color Cream = new(1f, 0.965f, 0.886f);        // #FFF6E2
        internal static readonly Color StampRed = new(0.659f, 0.157f, 0.125f); // #A82820
        internal static readonly Color OutlineBrown = new(0.20f, 0.11f, 0.06f, 0.9f);
        internal static readonly Color OutlineRed = new(0.36f, 0.06f, 0.05f, 0.9f);
        /// <summary>Warms the grey small button so it no longer reads as the grey disabled state.</summary>
        internal static readonly Color SmallButtonTint = new(1f, 0.90f, 0.74f);

        internal static Sprite Get(string key)
        {
            if (sprites == null)
            {
                sprites = new Dictionary<string, Sprite>();
                foreach (var sprite in Resources.LoadAll<Sprite>(Folder))
                    sprites[sprite.name] = sprite;
            }
            if (!sprites.TryGetValue(key, out var found))
                Debug.LogWarning($"[Online UI] Missing part '{key}' in Resources/{Folder}.");
            return found;
        }

        internal static bool Has(string key)
        {
            Get("UI_Timer_Hourglass"); // loads the table
            return sprites.ContainsKey(key);
        }

        /// <summary>Draws a part on the rect: sliced when it has a border, stretched otherwise.</summary>
        internal static Image Draw(RectTransform rect, string key, bool raycast = false)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = Get(key);
            image.color = Color.white;
            image.raycastTarget = raycast;
            image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        /// <summary>
        /// A button drawn from a part family: "&lt;family&gt;_Normal", "_Hover", "_Pressed" and, if present, "_Disabled".
        /// </summary>
        internal static Button Button(RectTransform rect, string family, string text, int size, Font font,
            UnityAction onClick, Color? textColor = null)
        {
            var image = Draw(rect, family + "_Normal", true);
            if (family == "UI_SmallButton")
                image.color = SmallButtonTint;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Get(family + "_Hover"),
                pressedSprite = Has(family + "_Pressed") ? Get(family + "_Pressed") : Get(family + "_Hover"),
                selectedSprite = Get(family + "_Normal"),
                disabledSprite = Has(family + "_Disabled") ? Get(family + "_Disabled") : Get(family + "_Normal"),
            };
            button.onClick.AddListener(onClick);
            if (!string.IsNullOrEmpty(text))
                OnlineUi.Label(OnlineUi.Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(12f, 6f),
                    new Vector2(-12f, -2f)), text, size, font, textColor ?? Ink);
            return button;
        }

        /// <summary>Cream lettering with a dark rim, for text sitting straight on a picture.</summary>
        internal static Text Outlined(Text label, Color? rim = null)
        {
            label.color = Cream;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = rim ?? OutlineBrown;
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        /// <summary>Full-screen picture behind a screen, covering it whatever the window's aspect.</summary>
        internal static Image Backdrop(RectTransform parent, string key, Color? tint = null)
        {
            var holder = OnlineUi.Rect("Backdrop", parent, Vector2.zero, Vector2.one);
            OnlineUi.Fill(holder, new Color(0.12f, 0.07f, 0.05f), true);
            var picture = OnlineUi.Rect("Picture", holder, Vector2.zero, Vector2.one);
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            var image = Draw(picture, key);
            image.color = tint ?? Color.white;
            return image;
        }

        /// <summary>A row of page dots (LOFT_Dot) that light up one after another: "waiting" without a ● glyph.</summary>
        internal static Image[] WaitingDots(RectTransform parent, Vector2 centre, float size = 24f)
        {
            var dots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var dot = OnlineUi.Box("Dot" + i, parent, new Vector2(0f, 1f), Vector2.zero, new Vector2(size, size));
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.anchoredPosition = centre + new Vector2((i - 1) * (size + 8f), 0f);
                dots[i] = Draw(dot, "LOFT_Dot_Unseen");
            }
            return dots;
        }

        /// <summary>Call every frame for <see cref="WaitingDots"/>.</summary>
        internal static void AnimateDots(Image[] dots)
        {
            int lit = (int)(Time.unscaledTime * 3f) % 4;
            for (int i = 0; i < dots.Length; i++)
                dots[i].sprite = Get(i < lit ? "LOFT_Dot_Current" : "LOFT_Dot_Unseen");
        }

        /// <summary>A small green "connected" lamp.</summary>
        internal static Image Lamp(RectTransform rect, bool on = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Generated("Disc", 32, (x, y) => Mathf.Clamp01(15.5f - Mathf.Sqrt(x * x + y * y)));
            image.color = on ? new Color(0.36f, 0.72f, 0.38f) : new Color(0.78f, 0.36f, 0.30f);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A small solid triangle pointing right (the font has no triangle glyphs).</summary>
        internal static Image Triangle(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Generated("Triangle", 32, // vertices (-10, ±12) and (12, 0)
                (x, y) => Mathf.Clamp01(Mathf.Min(x + 10.5f, (12f - x) * 12f / 22f - Mathf.Abs(y) + 0.5f)));
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A soft-edged dark pad that quiets a busy picture behind content.</summary>
        internal static Image Shade(RectTransform rect, float alpha)
        {
            var image = rect.gameObject.AddComponent<Image>();
            const int size = 96, edge = 40;
            image.sprite = Generated("Shade", size, (x, y) =>
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x) - (size / 2f - edge)), dy = Mathf.Max(0f, Mathf.Abs(y) - (size / 2f - edge));
                return Mathf.SmoothStep(1f, 0f, Mathf.Sqrt(dx * dx + dy * dy) / edge);
            }, edge);
            image.type = Image.Type.Sliced;
            image.color = new Color(0.10f, 0.05f, 0.02f, alpha);
            image.raycastTarget = false;
            return image;
        }

        private static readonly Dictionary<string, Sprite> generated = new();

        /// <summary>A white sprite whose alpha comes from <paramref name="alpha"/>(x, y), centred coordinates.</summary>
        private static Sprite Generated(string key, int size, System.Func<float, float, float> alpha, int border = 0)
        {
            if (generated.TryGetValue(key, out var sprite) && sprite != null)
                return sprite;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(x - size / 2f + 0.5f, y - size / 2f + 0.5f));
            texture.SetPixels32(pixels);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = "Generated " + key;
            generated[key] = sprite;
            return sprite;
        }
    }
}
