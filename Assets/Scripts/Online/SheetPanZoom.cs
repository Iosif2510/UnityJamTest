using UnityEngine;
using UnityEngine.EventSystems;

namespace PotionShop.Online
{
    /// <summary>Wheel zoom and drag pan for a memo sheet inside its masked viewport.</summary>
    [DisallowMultipleComponent]
    public sealed class SheetPanZoom : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private const float MinZoom = 1f;
        private const float MaxZoom = 3f;

        private RectTransform content;
        private RectTransform viewport;
        private float zoom = 1f;

        public void Bind(RectTransform viewportRect, RectTransform contentRect)
        {
            viewport = viewportRect;
            content = contentRect;
        }

        public void ResetView()
        {
            zoom = 1f;
            content.localScale = Vector3.one;
            content.anchoredPosition = Vector2.zero;
        }

        public void OnScroll(PointerEventData eventData)
        {
            zoom = Mathf.Clamp(zoom * (eventData.scrollDelta.y > 0f ? 1.15f : 1f / 1.15f), MinZoom, MaxZoom);
            content.localScale = Vector3.one * zoom;
            Clamp();
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = viewport.lossyScale.x;
            content.anchoredPosition += eventData.delta / (scale > 0f ? scale : 1f);
            Clamp();
        }

        private void Clamp()
        {
            Vector2 limit = Vector2.Max(Vector2.zero, (content.rect.size * zoom - viewport.rect.size) * 0.5f);
            Vector2 position = content.anchoredPosition;
            content.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -limit.x, limit.x),
                Mathf.Clamp(position.y, -limit.y, limit.y));
        }
    }
}
