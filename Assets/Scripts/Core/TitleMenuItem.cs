using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// One title menu entry that clearly shows it is pointed at (by the mouse or the arrow keys): it lifts and grows
    /// and the other entries fade back. Pressing squashes it. <see cref="TitleMenu"/> owns the group.
    /// </summary>
    [RequireComponent(typeof(Button)), DisallowMultipleComponent]
    public sealed class TitleMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler,
        IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.1f, PressScale = 0.94f, Lift = 6f;

        internal TitleMenu Menu;
        private RectTransform rect;
        private Vector2 rest;
        private Vector3 restScale;
        private CanvasGroup group;
        private float hover, hoverTarget, press = 1f, pressTarget = 1f, fade = 1f;
        private float appear = 1f;
        private MotionHandle appearing;
        private bool pointed, selected;

        internal bool Pointed => pointed || selected;

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = TryGetComponent(out CanvasGroup g) ? g : gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Where it rests, once the menu has placed it.</summary>
        internal void Settle()
        {
            rest = rect.anchoredPosition;
            restScale = rect.localScale;
        }

        /// <summary>Rises into place and fades in after <paramref name="delay"/>, once the logo has landed.</summary>
        internal void Appear(float delay)
        {
            appear = 0f;
            appearing.TryCancel();
            appearing = LMotion.Create(0f, 1f, 0.35f).WithDelay(delay).WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).Bind(t => appear = t).AddTo(gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointed = true;
            // the mouse takes over from the arrow keys: one entry lit at a time
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject != gameObject)
                events.SetSelectedGameObject(null);
            Point();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointed = false;
            pressTarget = 1f;
            // a click leaves the button selected; moving away should still let it go
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == gameObject) events.SetSelectedGameObject(null);
            Menu?.Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            Point();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            Menu?.Refresh();
        }

        public void OnPointerDown(PointerEventData eventData) => pressTarget = PressScale;
        public void OnPointerUp(PointerEventData eventData) => pressTarget = 1f;

        private void Point()
        {
            if (hoverTarget < 1f) GameSfx.Play("Hover", 0.55f, 1f + rect.GetSiblingIndex() * 0.015f, 0.04f);
            Menu?.Refresh();
        }

        /// <summary>Set by the menu: whether this entry is the pointed one, and whether another one is.</summary>
        internal void SetState(bool on, bool another)
        {
            hoverTarget = on ? 1f : 0f;
            fadeTarget = another && !on ? 0.7f : 1f;
        }

        private float fadeTarget = 1f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            hover = Mathf.MoveTowards(hover, hoverTarget, dt * 6f);
            press = Mathf.Lerp(press, pressTarget, 1f - Mathf.Exp(-dt * 30f));
            fade = Mathf.Lerp(fade, fadeTarget, 1f - Mathf.Exp(-dt * 12f));
            float eased = EaseOutBack(hover);

            rect.localScale = restScale * (Mathf.LerpUnclamped(1f, HoverScale, eased) * press);
            rect.anchoredPosition = rest + Vector2.up * (Lift * eased - 24f * (1f - appear));
            group.alpha = fade * appear;
        }

        private void OnDisable()
        {
            pointed = selected = false;
            hover = hoverTarget = 0f;
            press = pressTarget = 1f;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
