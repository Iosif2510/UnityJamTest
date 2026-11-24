using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// One title menu entry that clearly shows it is pointed at (by the mouse or the arrow keys): it lifts and grows,
    /// a warm glow comes up behind it, the quill slides in at its right end and writes in the air, and the other entries
    /// fade back a little. Pressing squashes it. <see cref="TitleMenu"/> owns the group.
    /// </summary>
    [RequireComponent(typeof(Button)), DisallowMultipleComponent]
    public sealed class TitleMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler,
        IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.1f, PressScale = 0.94f, Lift = 6f;

        private static Sprite glowSprite, quillSprite;

        internal TitleMenu Menu;
        private RectTransform rect;
        private Vector2 rest;
        private Vector3 restScale;
        private Image glow;
        private RectTransform quill;
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

            // the glow sits behind the button: a sibling just before it, following it about
            glow = new GameObject(name + " Glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            glow.rectTransform.SetParent(rect.parent, false);
            glow.rectTransform.SetSiblingIndex(rect.GetSiblingIndex());
            glow.sprite = GlowSprite;
            glow.raycastTarget = false;
            glow.color = new Color(1f, 0.72f, 0.3f, 0f);

            quill = new GameObject("Quill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            quill.SetParent(rect, false);
            // on the right end, mirrored so the nib points back at the words
            quill.anchorMin = quill.anchorMax = new Vector2(1f, 0.5f);
            quill.pivot = new Vector2(0.85f, 0.15f); // near the nib
            quill.sizeDelta = new Vector2(52f, 83f);
            quill.localScale = new Vector3(-1f, 1f, 1f);
            var quillImage = quill.GetComponent<Image>();
            quillImage.sprite = QuillSprite;
            quillImage.raycastTarget = false;
            quill.gameObject.SetActive(false);
        }

        /// <summary>Where it rests, once the menu has placed it.</summary>
        internal void Settle()
        {
            rest = rect.anchoredPosition;
            restScale = rect.localScale;
            glow.rectTransform.anchorMin = rect.anchorMin;
            glow.rectTransform.anchorMax = rect.anchorMax;
            glow.rectTransform.sizeDelta = rect.sizeDelta + new Vector2(150f, 110f);
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
            fadeTarget = another && !on ? 0.72f : 1f;
            if (on && !quill.gameObject.activeSelf)
            {
                quill.gameObject.SetActive(true);
                quillSince = Time.unscaledTime;
            }
        }

        private float fadeTarget = 1f;
        private float quillSince;

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

            var c = glow.color;
            c.a = 0.75f * Mathf.SmoothStep(0f, 1f, hover) * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f));
            glow.color = c;
            glow.rectTransform.anchoredPosition = rect.anchoredPosition;
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, hover);

            if (quill.gameObject.activeSelf)
            {
                // slides in from the right, then writes little loops in the air while pointed
                float t = Time.unscaledTime - quillSince;
                float slide = Mathf.SmoothStep(0f, 1f, hover);
                quill.anchoredPosition = new Vector2(Mathf.Lerp(30f, -52f, slide) + Mathf.Sin(t * 7f) * 3f,
                    Mathf.Cos(t * 7f) * 2.5f - 8f);
                quill.localRotation = Quaternion.Euler(0f, 0f, -30f - Mathf.Sin(t * 3.5f) * 6f);
                quill.GetComponent<Image>().color = new Color(1f, 1f, 1f, slide);
                if (hover <= 0f) quill.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            pointed = selected = false;
            hover = hoverTarget = 0f;
            press = pressTarget = 1f;
            if (glow != null) glow.color = new Color(1f, 0.72f, 0.3f, 0f);
            if (quill != null) quill.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (glow != null) Destroy(glow.gameObject);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static Sprite QuillSprite
        {
            get
            {
                if (quillSprite != null) return quillSprite;
                var tex = Resources.Load<Texture2D>("Crafting/Quill");
                return quillSprite = tex != null
                    ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
            }
        }

        /// <summary>A soft rounded glow, brightest in the middle.</summary>
        private static Sprite GlowSprite
        {
            get
            {
                if (glowSprite != null) return glowSprite;
                const int w = 128, h = 64;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.2f);
                        float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return glowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
