using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The B-style UI parts (Resources/OnlineUiB), looked up by their name without the slice suffix:
    /// "LOFT_Tab_Normal_9slice_40" is found as "LOFT_Tab_Normal". Files that follow the parts spec's naming
    /// replace these one for one, whatever their border size.
    /// </summary>
    internal static class OnlineUiParts
    {
        private const string Folder = "OnlineUiB";
        private static readonly Regex Suffix = new(@"_(9|3)slice_\d+$");
        private static Dictionary<string, Sprite> sprites;

        // Lettering colours that sit on the parts.
        internal static readonly Color Ink = new(0.27f, 0.15f, 0.09f);
        internal static readonly Color Cream = new(1f, 0.95f, 0.84f);
        internal static readonly Color Muted = new(0.27f, 0.15f, 0.09f, 0.55f);

        internal static Sprite Get(string key)
        {
            if (sprites == null)
            {
                sprites = new Dictionary<string, Sprite>();
                foreach (var sprite in Resources.LoadAll<Sprite>(Folder))
                    sprites[Suffix.Replace(sprite.name, string.Empty)] = sprite;
            }
            if (!sprites.TryGetValue(key, out var found))
                Debug.LogWarning($"[Online UI] Missing part '{key}' in Resources/{Folder}.");
            return found;
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
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            var state = new SpriteState
            {
                highlightedSprite = Get(family + "_Hover"),
                pressedSprite = Get(family + "_Pressed"),
                selectedSprite = Get(family + "_Normal"),
                disabledSprite = Has(family + "_Disabled") ? Get(family + "_Disabled") : Get(family + "_Normal"),
            };
            button.spriteState = state;
            button.onClick.AddListener(onClick);
            if (!string.IsNullOrEmpty(text) || textColor != null)
                OnlineUi.Label(OnlineUi.Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(10f, 4f),
                    new Vector2(-10f, -2f)), text ?? string.Empty, size, font, textColor ?? Cream);
            return button;
        }

        internal static bool Has(string key)
        {
            Get("UI_Timer_Hourglass"); // make sure the table is loaded
            return sprites.ContainsKey(key);
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
    }
}
