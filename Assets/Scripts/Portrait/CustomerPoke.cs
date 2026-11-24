using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop.Portraits
{
    /// <summary>
    /// Clicking the customer pokes them (<see cref="PortraitView.Poke"/>): an invisible hit area over the body, so the
    /// portrait answers a curious finger without any words. Ignored while the portrait is faded out.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PortraitView))]
    public sealed class CustomerPoke : MonoBehaviour, IPointerDownHandler
    {
        // The body, not the empty corners of the portrait rect.
        private static readonly Vector2 AreaMin = new(.2f, .05f), AreaMax = new(.8f, .92f);

        private PortraitView portrait;
        private RectTransform area;

        private void Awake()
        {
            portrait = GetComponent<PortraitView>();
            area = new GameObject("PokeArea", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            area.SetParent(transform, false);
            area.SetAsFirstSibling();
            area.anchorMin = AreaMin;
            area.anchorMax = AreaMax;
            area.offsetMin = area.offsetMax = Vector2.zero;
            var hit = area.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            // the portrait's world-space canvas had nothing clickable on it, so it has no raycaster of its own
            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null && !canvas.TryGetComponent(out GraphicRaycaster _))
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !Visible()) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position,
                eventData.pressEventCamera, out var local);
            var r = area.rect;
            float side = Mathf.Clamp((local.x - r.center.x) / (r.width * .5f), -1f, 1f);
            portrait.Poke(side);
            bool slime = portrait.Layers.slime != null && portrait.Layers.slime.enabled;
            if (slime) GameSfx.Play("SlimePop", .3f, 1.35f, .08f);
            else GameSfx.Play("Blip", .22f, .7f, .08f);
        }

        private bool Visible()
        {
            foreach (var group in GetComponentsInParent<CanvasGroup>())
                if (group.alpha < .5f) return false;
            return true;
        }
    }
}
