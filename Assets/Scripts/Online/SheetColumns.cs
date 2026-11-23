using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// A portrait memo sheet shown landscape as two columns: its top half on the left and its bottom half on the right,
    /// as the handoff's assembled screens do until landscape memos exist. Each column is a masked view of the whole
    /// sheet, so the easy-mode marks (<see cref="SheetMarks"/>) line up without any change.
    /// </summary>
    internal sealed class SheetColumns
    {
        private readonly Image[] pages = new Image[2];
        private readonly SheetMarks[] marks = new SheetMarks[2];
        private readonly OnlineCoopConfig config;

        /// <param name="area">Size of the parent area the two columns are centred in.</param>
        public SheetColumns(RectTransform parent, OnlineCoopConfig coopConfig, Vector2 area, float gap)
        {
            config = coopConfig;
            var first = config.MemoSheets[0];
            float halfAspect = first.rect.width / first.rect.height * 2f;
            float width = Mathf.Min((area.x - gap * 3f) / 2f, (area.y - gap * 2f) * halfAspect);
            float height = width / halfAspect;
            for (int half = 0; half < 2; half++)
            {
                var view = OnlineUi.Box(half == 0 ? "Top half" : "Bottom half", parent, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(width, height));
                view.pivot = new Vector2(0.5f, 0.5f);
                view.anchoredPosition = new Vector2((half == 0 ? -1f : 1f) * (width + gap) * 0.5f, 0f);
                view.gameObject.AddComponent<RectMask2D>();
                // The whole sheet, twice the view's height, hung from the top (first half) or the bottom (second).
                float edge = half == 0 ? 1f : 0f;
                var content = OnlineUi.Rect("Sheet", view, new Vector2(0f, edge), new Vector2(1f, edge));
                content.pivot = new Vector2(0.5f, edge);
                content.sizeDelta = new Vector2(0f, height * 2f);
                content.anchoredPosition = Vector2.zero;
                pages[half] = OnlineUi.Fill(content, Color.white, false);
                marks[half] = content.gameObject.AddComponent<SheetMarks>();
                marks[half].Bind(content);
            }
        }

        public void Show(int sheetIndex, bool easy)
        {
            for (int half = 0; half < 2; half++)
            {
                pages[half].sprite = config.MemoSheets[sheetIndex];
                marks[half].Show(sheetIndex, easy);
            }
        }
    }
}
