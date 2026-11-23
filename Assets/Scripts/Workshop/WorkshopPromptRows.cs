using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The interaction prompt rows at the bottom of the workshop screen. A shortcut that has an icon (the mouse
    /// buttons) is drawn as that icon instead of text. While an ingredient is held, its two actions sit side by
    /// side on one line so nothing covers the ingredient in the middle of the screen.
    /// </summary>
    internal sealed class WorkshopPromptRows
    {
        private const float IconSize = 34f;

        private sealed class Row
        {
            private readonly RectTransform rect;
            private readonly TextMeshProUGUI action;
            private readonly TextMeshProUGUI key;
            private readonly UnityEngine.UI.HorizontalLayoutGroup layout;
            private readonly UnityEngine.UI.LayoutElement keySize;
            private readonly UnityEngine.UI.Image keycap;
            private readonly UnityEngine.UI.Image icon;
            private readonly IReadOnlyDictionary<string, Sprite> icons;
            private readonly Vector2 position;
            private readonly Vector2 size;

            internal Row(TextMeshProUGUI action, TextMeshProUGUI key, IReadOnlyDictionary<string, Sprite> icons)
            {
                this.action = action;
                this.key = key;
                this.icons = icons;
                rect = (RectTransform)action.transform.parent;
                layout = rect.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                var cap = key.transform.parent;
                keySize = cap.GetComponent<UnityEngine.UI.LayoutElement>() ?? cap.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                keycap = cap.GetComponent<UnityEngine.UI.Image>();
                key.rectTransform.anchorMin = Vector2.zero;
                key.rectTransform.anchorMax = Vector2.one;
                key.rectTransform.offsetMin = key.rectTransform.offsetMax = Vector2.zero;
                key.enableAutoSizing = true;
                key.fontSizeMin = 12f;
                key.fontSizeMax = 17f;

                var iconObject = cap.Find("Icon");
                if (iconObject == null)
                {
                    iconObject = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform;
                    iconObject.SetParent(cap, false);
                }
                var iconRect = (RectTransform)iconObject;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                icon = iconObject.GetComponent<UnityEngine.UI.Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                position = rect.anchoredPosition;
                size = rect.sizeDelta;
            }

            internal void Show(string caption, string shortcut, int heldColumn = -1)
            {
                bool shown = !string.IsNullOrEmpty(caption);
                rect.gameObject.SetActive(shown);
                if (!shown) return;

                bool held = heldColumn >= 0;
                rect.anchoredPosition = held ? new Vector2(heldColumn == 0 ? -140f : 140f, position.y) : position;
                rect.sizeDelta = held ? new Vector2(260f, IconSize + 4f) : size;
                layout.childAlignment = TextAnchor.MiddleCenter;
                action.text = caption;

                bool hasIcon = shortcut != null && icons.TryGetValue(shortcut, out var sprite) && sprite != null;
                icon.enabled = hasIcon;
                if (hasIcon) icon.sprite = icons[shortcut];
                if (keycap != null) keycap.enabled = !hasIcon;
                key.text = hasIcon ? string.Empty : shortcut ?? string.Empty;
                keySize.preferredWidth = hasIcon ? IconSize : key.text.Length > 1 ? 59f : 24f;
                keySize.preferredHeight = hasIcon ? IconSize : 24f;
                key.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(shortcut));
            }
        }

        private readonly Row primary;
        private readonly Row restore;

        internal WorkshopPromptRows(TextMeshProUGUI action, TextMeshProUGUI key, IReadOnlyDictionary<string, Sprite> icons)
        {
            primary = new Row(action, key, icons);
            restore = CopyRow("Return Row");

            Row CopyRow(string name)
            {
                var source = action.transform.parent;
                var copy = Object.Instantiate(source.gameObject, source.parent, false);
                copy.name = name;
                var result = new Row(copy.transform.Find(action.name).GetComponent<TextMeshProUGUI>(),
                    copy.transform.Find(key.transform.parent.name + "/" + key.name).GetComponent<TextMeshProUGUI>(), icons);
                result.Show(null, null);
                return result;
            }
        }

        internal void Show(string action, string key)
        {
            primary.Show(action, key);
            restore.Show(null, null);
        }

        /// <summary>Held ingredient: the left-button action (turn it, or put it in the cauldron) and right-click to put it down.</summary>
        internal void ShowHeld(string leftAction, string leftKey)
        {
            primary.Show(leftAction, leftKey, 0);
            restore.Show("내려놓기", "우클릭", 1);
        }
    }
}
