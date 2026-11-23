using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// A row of small buttons that sets the memo language (<see cref="MemoText.Language"/>, saved in PlayerPrefs).
    /// Each language is named in its own script. Sheets on screen follow on their next refresh.
    /// </summary>
    internal sealed class MemoLanguagePicker
    {
        private static readonly (string Code, string Name)[] Languages =
        {
            ("ko", "한국어"), ("en", "English"), ("ja", "日本語"), ("zh-Hans", "简体中文"), ("zh-Hant", "繁體中文"),
        };

        private readonly List<(string code, Image image, Text label)> buttons = new();

        public MemoLanguagePicker(RectTransform row, Font latinFont)
        {
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var cjk = Resources.Load<Font>("MemoText/Xiaolai-Regular");
            foreach (var (code, name) in Languages)
            {
                string chosen = code;
                var rect = OnlineUi.Rect(code, row, Vector2.zero, Vector2.zero);
                bool cjkName = code == "ja" || code.StartsWith("zh");
                var button = OnlineUiParts.Button(rect, "UI_SmallButton", name, 22, cjkName ? cjk : latinFont, () =>
                {
                    MemoText.Language = chosen;
                    Refresh();
                });
                buttons.Add((code, (Image)button.targetGraphic, rect.GetComponentInChildren<Text>()));
            }
            Refresh();
        }

        /// <summary>The chosen language is the bright one; the rest are dimmed.</summary>
        public void Refresh()
        {
            string current = MemoText.Language;
            foreach (var (code, image, label) in buttons)
            {
                bool on = code == current;
                image.color = on ? new Color(1f, 0.82f, 0.42f) : new Color(0.93f, 0.9f, 0.86f);
                label.color = on ? OnlineUiParts.Ink : new Color(0.29f, 0.18f, 0.12f, 0.8f);
            }
        }
    }
}
