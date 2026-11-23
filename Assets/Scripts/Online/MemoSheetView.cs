using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// One memo on screen. In Korean it is the handwritten original; in other languages the drawing-only sheet with
    /// every phrase written into its box in the handwriting font (<see cref="MemoText"/>). Easy mode puts a highlighter stroke behind the phrases the
    /// narrator actually needs: yellow for the symptoms, green for the notes that tell an ingredient apart.
    /// </summary>
    internal sealed class MemoSheetView
    {
        private static readonly Color InkColor = new(0.13f, 0.11f, 0.10f, 0.95f);
        private static readonly Color Symptom = new(1f, 0.78f, 0.16f, 0.45f);
        private static readonly Color Ingredient = new(0.42f, 0.80f, 0.36f, 0.42f);

        private readonly RectTransform page;
        private readonly Image image;
        private readonly List<(Text text, Image mark)> slots = new();
        private int shown = -1;
        private bool shownEasy;
        private string shownLanguage;
        private float shownHeight;

        /// <summary>Fits a 16:9 sheet inside <paramref name="parent"/>.</summary>
        public MemoSheetView(RectTransform parent)
        {
            page = OnlineUi.Rect("Memo", parent, Vector2.zero, Vector2.one);
            var fitter = page.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;
            image = OnlineUi.Fill(page, Color.white, false);
        }

        public RectTransform Rect => page;

        public void Show(int sheetIndex, bool easy)
        {
            float height = page.rect.height;
            if (sheetIndex == shown && easy == shownEasy && MemoText.Language == shownLanguage &&
                Mathf.Approximately(height, shownHeight))
                return;
            shown = sheetIndex;
            shownEasy = easy;
            shownLanguage = MemoText.Language;
            shownHeight = height;
            image.sprite = MemoText.Sheet(sheetIndex);
            bool original = MemoText.ShowsOriginal(sheetIndex);

            var phrases = MemoText.Phrases(sheetIndex);
            while (slots.Count < phrases.Count)
                slots.Add(NewSlot());
            for (int i = 0; i < slots.Count; i++)
            {
                var (text, mark) = slots[i];
                bool used = i < phrases.Count;
                var rect = (RectTransform)text.transform.parent;
                rect.gameObject.SetActive(used);
                if (!used) continue;
                var phrase = phrases[i];
                // Boxes already clear the drawing's ink and have room for a wider font (Tools/MemoText/build_layout.py).
                var area = original ? phrase.Written : phrase.Box;
                rect.anchorMin = area.min;
                rect.anchorMax = area.max;
                // The original already has the words written; only the easy-mode stroke goes on it.
                text.enabled = !original;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                text.font = MemoText.Font;
                text.text = MemoText.Text(sheetIndex, phrase.Key);
                // Start at the size it was written at (a little larger is fine), shrink to fit the box.
                int written = Mathf.Max(2, Mathf.RoundToInt(phrase.RowHeight * Mathf.Max(height, 1f) * 1.5f));
                var size = new Vector2(page.rect.width * area.width, page.rect.height * area.height);
                text.fontSize = Fit(text, size, written);
                bool marked = easy && phrase.Mark.Length > 0;
                mark.enabled = marked;
                if (marked) mark.color = phrase.Mark == "symptom" ? Symptom : Ingredient;
            }
        }

        /// <summary>Call when the sheet may have been resized (first layout, window change).</summary>
        public void Refresh()
        {
            if (shown >= 0 && !Mathf.Approximately(page.rect.height, shownHeight))
                Show(shown, shownEasy);
        }

        private static readonly TextGenerator Measure = new();

        /// <summary>The largest size up to <paramref name="max"/> whose wrapped text fits <paramref name="box"/>.</summary>
        private static int Fit(Text text, Vector2 box, int max)
        {
            if (box.x <= 1f || box.y <= 1f || string.IsNullOrEmpty(text.text)) return max;
            var settings = text.GetGenerationSettings(box);
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            float scale = Mathf.Max(text.pixelsPerUnit, 0.0001f);
            for (int size = max; size > 4; size--)
            {
                // Lay it out at this size, wrapped to the box width, and see whether the lines fit its height.
                settings.fontSize = size;
                Measure.Populate(text.text, settings);
                if (Measure.lineCount * size * text.lineSpacing * 1.12f <= box.y * 1.06f ||
                    Measure.rectExtents.height / scale <= box.y * 1.06f && Measure.lineCount <= 1)
                    return size;
            }
            return 4;
        }

        private (Text, Image) NewSlot()
        {
            var box = OnlineUi.Rect("Phrase", page, Vector2.zero, Vector2.zero);
            var mark = OnlineUi.Fill(OnlineUi.Rect("Highlight", box, Vector2.zero, Vector2.one, new Vector2(-4f, -2f),
                new Vector2(4f, 2f)), Color.clear, false);
            var text = OnlineUi.Label(OnlineUi.Rect("Text", box, Vector2.zero, Vector2.one), string.Empty, 20, MemoText.Font,
                InkColor, TextAnchor.UpperLeft);
            // Sized by Fit, not Unity's best fit: best fit only wraps at spaces, so Chinese and Japanese notes
            // would shrink onto a single tiny line.
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // A line a hair over the box still shows instead of vanishing.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 0.92f;
            return (text, mark);
        }
    }
}
