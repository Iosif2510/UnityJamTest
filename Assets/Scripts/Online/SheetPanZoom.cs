using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PotionShop.Online
{
    /// <summary>
    /// Reading view for a memo sheet inside its masked viewport. The sheet starts as wide as the viewport with its
    /// top edge showing, because a whole portrait sheet squeezed into a landscape screen is too small to read.
    /// The wheel moves down the page, dragging moves it freely, and Ctrl+wheel or the zoom buttons resize it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SheetPanZoom : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private const float MaxZoom = 2.5f;
        private const float ZoomStep = 1.2f;
        private const float ScrollPixels = 130f;

        private RectTransform content;
        private RectTransform viewport;
        private float aspect = 1f;
        /// <summary>1 means the sheet is exactly as wide as the viewport.</summary>
        private float zoom = 1f;
        private Vector2 offset;
        private Vector2 appliedViewport;

        /// <summary>Smallest zoom: the whole sheet fits in the viewport.</summary>
        private float WholeSheetZoom =>
            viewport.rect.width > 0f ? Mathf.Min(1f, viewport.rect.height * aspect / viewport.rect.width) : 1f;

        public bool ShowsWholeSheet => zoom <= WholeSheetZoom + 0.001f;

        /// <param name="sheetAspect">Width divided by height of the sheet.</param>
        public void Bind(RectTransform viewportRect, RectTransform contentRect, float sheetAspect)
        {
            viewport = viewportRect;
            content = contentRect;
            aspect = sheetAspect;
            // The sheet hangs from the top centre of the viewport; offset.y is how far the page has been moved up.
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1f);
        }

        public void ResetView()
        {
            zoom = 1f;
            offset = Vector2.zero;
            Apply();
        }

        /// <summary>When set, the plain wheel zooms (drag still pans); otherwise it scrolls and Ctrl+wheel zooms.</summary>
        public bool WheelZooms { get; set; }

        /// <summary>Shows the whole sheet, centred. Safe to call before the canvas has laid out.</summary>
        public void ShowWholeSheet()
        {
            zoom = 0f; // clamped up to the whole-sheet zoom when applied
            offset = Vector2.zero;
            Apply();
        }

        public void ZoomIn() => SetZoom(zoom * ZoomStep);
        public void ZoomOut() => SetZoom(zoom / ZoomStep);

        /// <summary>Switches between the whole sheet and the reading width.</summary>
        public void ToggleWholeSheet()
        {
            if (ShowsWholeSheet) ResetView();
            else SetZoom(WholeSheetZoom);
        }

        public void OnScroll(PointerEventData eventData)
        {
            float wheel = eventData.scrollDelta.y;
            if (Mathf.Approximately(wheel, 0f))
                return;
            var keyboard = Keyboard.current;
            bool ctrl = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            if (WheelZooms || ctrl)
            {
                SetZoom(wheel > 0f ? zoom * ZoomStep : zoom / ZoomStep);
                return;
            }
            offset.y -= Mathf.Sign(wheel) * ScrollPixels;
            Apply();
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = viewport.lossyScale.x;
            offset += eventData.delta / (scale > 0f ? scale : 1f);
            Apply();
        }

        private void LateUpdate()
        {
            // The viewport has no size until the canvas lays out, and it changes when the window is resized.
            if (viewport != null && viewport.rect.size != appliedViewport)
                Apply();
        }

        private void SetZoom(float value)
        {
            float before = zoom;
            zoom = Mathf.Clamp(value, WholeSheetZoom, MaxZoom);
            // Keep the part of the page at the middle of the viewport in place while resizing.
            float middle = viewport.rect.height * 0.5f;
            if (before > 0f)
                offset.y = (offset.y + middle) * zoom / before - middle;
            Apply();
        }

        private void Apply()
        {
            if (viewport == null || viewport.rect.width <= 0f)
                return;
            appliedViewport = viewport.rect.size;
            zoom = Mathf.Clamp(zoom, WholeSheetZoom, MaxZoom);
            float width = appliedViewport.x * zoom;
            float height = width / aspect;
            content.sizeDelta = new Vector2(width, height);

            float sideways = Mathf.Max(0f, (width - appliedViewport.x) * 0.5f);
            offset.x = Mathf.Clamp(offset.x, -sideways, sideways);
            float downward = height - appliedViewport.y;
            // A sheet shorter than the viewport sits in the middle instead of clinging to the top.
            offset.y = downward > 0f ? Mathf.Clamp(offset.y, 0f, downward) : downward * 0.5f;
            content.anchoredPosition = offset;
        }
    }
}
