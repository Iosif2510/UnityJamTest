using System;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The memo sheets lying on the painted loft desk. They sit where the desk picture leaves bare wood, each a
    /// little askew; hovering lifts one and clicking picks it up. What happens after the pick is the listener's job.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeskSheets : MonoBehaviour
    {
        // Positions are fractions of the desk picture (x from the left, y from the top), so the sheets stay on the
        // bare wood at any screen shape. Two rows of four between the props along the top and the floor below.
        private static readonly float[] Columns = { 0.110f, 0.262f, 0.414f, 0.566f };
        private static readonly float[] Rows = { 0.452f, 0.742f };
        private const float SheetWidth = 0.108f;
        private const float SheetAspect = 1440f / 2032f;
        private const float PictureAspect = 16f / 9f;

        private RectTransform[] sheets = Array.Empty<RectTransform>();
        private int hovered = -1;

        public int Hovered => hovered;

        /// <summary>Raised with the sheet index when the narrator picks a sheet up.</summary>
        public event Action<int> Picked;

        /// <param name="picture">A rect that covers exactly the desk picture.</param>
        public void Build(RectTransform picture, Sprite[] sprites)
        {
            sheets = new RectTransform[sprites.Length];
            var random = new System.Random(11);
            float halfWidth = SheetWidth * 0.5f;
            float halfHeight = SheetWidth * PictureAspect / SheetAspect * 0.5f;
            for (int i = 0; i < sprites.Length; i++)
            {
                float x = Columns[i % Columns.Length] + (float)(random.NextDouble() - 0.5) * 0.012f;
                float y = Rows[i / Columns.Length % Rows.Length] + (float)(random.NextDouble() - 0.5) * 0.02f;
                var rect = OnlineUi.Rect("DeskSheet" + i, picture, new Vector2(x - halfWidth, 1f - y - halfHeight),
                    new Vector2(x + halfWidth, 1f - y + halfHeight));
                rect.localRotation = Quaternion.Euler(0f, 0f, (float)(random.NextDouble() - 0.5) * 11f);

                // A soft dark copy underneath stands in for the shadow the lantern would cast.
                var shadow = OnlineUi.Fill(OnlineUi.Rect("Shadow", rect, Vector2.zero, Vector2.one, new Vector2(5f, -9f),
                    new Vector2(5f, -9f)), new Color(0f, 0f, 0f, 0.38f), false);
                shadow.sprite = sprites[i];
                var paper = OnlineUi.Fill(OnlineUi.Rect("Paper", rect, Vector2.zero, Vector2.one), Color.white);
                paper.sprite = sprites[i];

                var handle = paper.gameObject.AddComponent<SheetHandle>();
                handle.Bind(this, i);
                sheets[i] = rect;
            }
        }

        private void Hover(int index, bool inside)
        {
            if (inside)
            {
                if (hovered >= 0 && hovered != index) Settle(hovered);
                hovered = index;
                sheets[index].SetAsLastSibling();
                Scale(sheets[index], 1.08f);
            }
            else if (hovered == index)
            {
                hovered = -1;
                Settle(index);
            }
        }

        private void Pick(int index)
        {
            if (hovered == index) hovered = -1;
            Settle(index);
            Picked?.Invoke(index);
        }

        private void Settle(int index) => Scale(sheets[index], 1f);

        private void Scale(RectTransform sheet, float target)
        {
            float from = sheet.localScale.x;
            LMotion.Create(from, target, 0.12f).WithEase(Ease.OutQuad)
                .Bind(value => sheet.localScale = new Vector3(value, value, 1f)).AddTo(sheet.gameObject);
        }

        /// <summary>Forwards pointer events from one sheet to the desk.</summary>
        private sealed class SheetHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            private DeskSheets desk;
            private int index;

            internal void Bind(DeskSheets owner, int sheetIndex)
            {
                desk = owner;
                index = sheetIndex;
            }

            public void OnPointerEnter(PointerEventData eventData) => desk.Hover(index, true);
            public void OnPointerExit(PointerEventData eventData) => desk.Hover(index, false);

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                    desk.Pick(index);
            }
        }
    }
}
