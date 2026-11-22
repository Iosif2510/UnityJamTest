using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// Easy mode: highlighter strokes over the notes the narrator actually needs. On each potion memo that is
    /// the three symptoms the customer really has and, for each ingredient, the two notes that tell it apart
    /// from its look-alikes. Normal mode shows nothing, so finding those notes stays part of the puzzle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SheetMarks : MonoBehaviour
    {
        // Boxes were measured on the print sheets (Docs/PotionMemos) viewed at 1417 x 2000.
        private const float MeasuredWidth = 1417f;
        private const float MeasuredHeight = 2000f;
        // The in-game sheets shrink the print sheet onto paper with this margin (Tools/MemoPaper/bake_memos.py).
        private const float PaperMargin = 0.07f;
        private const float SheetAspect = 1440f / 2032f;

        private static readonly Color Symptom = new(1f, 0.78f, 0.16f, 0.42f);
        private static readonly Color Ingredient = new(0.42f, 0.80f, 0.36f, 0.40f);

        /// <summary>Per sheet (cover, six memos, counter manual): three symptom boxes, then six ingredient boxes.</summary>
        private static readonly Rect[][] Boxes =
        {
            null,
            Sheet(B(85, 120, 740, 235), B(810, 95, 1225, 200), B(195, 255, 760, 345),
                B(95, 1085, 250, 1160), B(60, 905, 190, 1020), B(1160, 800, 1360, 850), B(1160, 910, 1320, 960),
                B(400, 1340, 530, 1385), B(60, 1465, 175, 1570)),
            Sheet(B(105, 115, 865, 200), B(1015, 90, 1345, 225), B(195, 215, 670, 285),
                B(55, 855, 250, 905), B(80, 965, 270, 1015), B(720, 870, 870, 960), B(1115, 765, 1280, 820),
                B(275, 1465, 465, 1530), B(270, 1605, 425, 1700)),
            Sheet(B(70, 95, 1065, 190), B(75, 225, 565, 290), B(760, 190, 1190, 300),
                B(385, 745, 610, 800), B(40, 840, 175, 930), B(1110, 755, 1310, 810), B(1225, 850, 1335, 940),
                B(395, 1445, 540, 1495), B(905, 1340, 1070, 1410)),
            Sheet(B(45, 135, 415, 255), B(585, 95, 900, 215), B(535, 235, 1130, 350),
                B(360, 785, 520, 860), B(55, 900, 205, 990), B(790, 885, 965, 935), B(1150, 930, 1275, 1010),
                B(560, 1390, 770, 1440), B(65, 1495, 210, 1590)),
            Sheet(B(60, 95, 1120, 175), B(65, 195, 680, 265), B(785, 205, 1355, 275),
                B(40, 835, 225, 925), B(375, 735, 575, 785), B(735, 825, 875, 915), B(1155, 720, 1320, 810),
                B(140, 1415, 295, 1465), B(645, 1355, 835, 1405)),
            Sheet(B(125, 125, 605, 200), B(735, 95, 1355, 215), B(85, 225, 545, 330),
                B(120, 900, 235, 985), B(90, 1035, 230, 1125), B(800, 920, 900, 1005), B(1160, 845, 1320, 900),
                B(100, 1535, 245, 1640), B(160, 1435, 290, 1490)),
            null
        };

        private const int SymptomBoxes = 3;

        private RectTransform page;
        private readonly List<Image> strokes = new();

        /// <summary>Call once with the rect the sheet image fills; the strokes follow its pan and zoom.</summary>
        public void Bind(RectTransform content)
        {
            page = OnlineUi.Rect("Marks", content, Vector2.zero, Vector2.one);
            // Matches the area a preserve-aspect sheet image actually covers inside the content rect.
            var fitter = page.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = SheetAspect;
        }

        public void Show(int sheetIndex, bool easyMode)
        {
            var boxes = easyMode && sheetIndex >= 0 && sheetIndex < Boxes.Length ? Boxes[sheetIndex] : null;
            int count = boxes?.Length ?? 0;
            while (strokes.Count < count)
                strokes.Add(OnlineUi.Fill(OnlineUi.Rect("Stroke", page, Vector2.zero, Vector2.zero), Color.clear, false));

            for (int i = 0; i < strokes.Count; i++)
            {
                bool used = i < count;
                strokes[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                var box = boxes[i];
                var rect = strokes[i].rectTransform;
                rect.anchorMin = new Vector2(box.xMin, 1f - box.yMax);
                rect.anchorMax = new Vector2(box.xMax, 1f - box.yMin);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                strokes[i].color = i < SymptomBoxes ? Symptom : Ingredient;
            }
        }

        private static Rect[] Sheet(params Rect[] boxes) => boxes;

        /// <summary>A box measured on the print sheet, mapped onto the margined in-game sheet (0–1, y down).</summary>
        private static Rect B(float left, float top, float right, float bottom)
        {
            float scale = 1f - 2f * PaperMargin;
            float x0 = PaperMargin + left / MeasuredWidth * scale;
            float x1 = PaperMargin + right / MeasuredWidth * scale;
            float y0 = PaperMargin + top / MeasuredHeight * scale;
            float y1 = PaperMargin + bottom / MeasuredHeight * scale;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
