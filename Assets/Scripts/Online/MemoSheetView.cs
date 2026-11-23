using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// One memo on screen: the drawing-only sheet, with every phrase written into its box in the handwriting font in
    /// the chosen language (<see cref="MemoText"/>). Easy mode puts a highlighter stroke behind the phrases the
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
                rect.anchorMin = phrase.Box.min;
                rect.anchorMax = phrase.Box.max;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                text.text = MemoText.Text(sheetIndex, phrase.Key);
                // Start at the size it was written at (a little larger is fine), shrink to fit the box.
                int written = Mathf.Max(2, Mathf.RoundToInt(phrase.RowHeight * Mathf.Max(height, 1f) * 1.5f));
                text.resizeTextMaxSize = written;
                text.fontSize = written;
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

        private (Text, Image) NewSlot()
        {
            var box = OnlineUi.Rect("Phrase", page, Vector2.zero, Vector2.zero);
            var mark = OnlineUi.Fill(OnlineUi.Rect("Highlight", box, Vector2.zero, Vector2.one, new Vector2(-4f, -2f),
                new Vector2(4f, 2f)), Color.clear, false);
            var text = OnlineUi.Label(OnlineUi.Rect("Text", box, Vector2.zero, Vector2.one), string.Empty, 20, MemoText.Font,
                InkColor, TextAnchor.UpperLeft);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 1;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 0.92f;
            return (text, mark);
        }
    }
}
