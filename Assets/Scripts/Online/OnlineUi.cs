using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>Small uGUI builders for the online screens, which are assembled in code so existing scenes stay untouched.</summary>
    internal static class OnlineUi
    {
        internal static readonly Color Wood = new(0.17f, 0.11f, 0.08f);
        internal static readonly Color WoodLight = new(0.30f, 0.20f, 0.13f);
        internal static readonly Color Parchment = new(0.94f, 0.89f, 0.77f);
        internal static readonly Color Ink = new(0.20f, 0.13f, 0.09f);
        internal static readonly Color Accent = new(0.80f, 0.42f, 0.20f);
        internal static readonly Color Faded = new(0.20f, 0.13f, 0.09f, 0.45f);

        internal static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
        {
            var root = new GameObject(name, typeof(RectTransform));
            if (parent != null) root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Creates a child rect. Anchors are in 0–1 parent space; offsets are pixel insets from those anchors.</summary>
        internal static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>Creates a fixed-size child positioned relative to one anchor point.</summary>
        internal static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent, anchor, anchor);
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        internal static Image Fill(RectTransform rect, Color color, bool raycast = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static Text Label(RectTransform rect, string text, int size, Font font, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        internal static Button Button(RectTransform rect, string text, int size, Font font, UnityAction onClick,
            Color? background = null, Color? foreground = null)
        {
            var image = Fill(rect, background ?? Accent);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            if (!string.IsNullOrEmpty(text))
                Label(Rect("Label", rect, Vector2.zero, Vector2.one), text, size, font, foreground ?? Color.white);
            return button;
        }

        internal static InputField Input(RectTransform rect, string placeholder, int size, Font font, int limit)
        {
            Fill(rect, Color.white);
            var field = rect.gameObject.AddComponent<InputField>();
            var inset = new Vector2(14f, 4f);
            var text = Label(Rect("Text", rect, Vector2.zero, Vector2.one, inset, -inset), string.Empty, size, font, Ink,
                TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var hint = Label(Rect("Placeholder", rect, Vector2.zero, Vector2.one, inset, -inset), placeholder, size, font,
                Faded, TextAnchor.MiddleLeft);
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }
    }
}
