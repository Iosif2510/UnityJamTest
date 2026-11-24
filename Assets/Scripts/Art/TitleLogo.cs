using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The shop's logo on the waiting screen: the first time it drops in from above and lands with a squash and a
    /// thud (later it just pops up), then floats gently while a glint sweeps across it every few seconds.
    /// </summary>
    [RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public sealed class TitleLogo : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float floatAmplitude = 6f;
        [SerializeField, Min(0.1f)] private float floatPeriod = 2.8f;
        [SerializeField, Min(0.5f)] private float shineEvery = 4.5f;

        private static bool droppedOnce;
        private static Sprite shineSprite;

        private RectTransform rect;
        private RectTransform shine;
        private Image shineImage;
        private Vector2 rest;
        private float drop;           // extra height while falling in
        private Vector2 squash = Vector2.one;
        private float floatWeight;    // 0 while landing, then eases up to 1
        private float shownAt;
        private float nextShine;
        private MotionHandle entrance;
        private MotionHandle shineMotion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => droppedOnce = false;

        private void Awake()
        {
            rect = (RectTransform)transform;
            rest = rect.anchoredPosition;
            // the title written in the game's language (Resources/Localization/Logo), Korean as drawn otherwise
            if (!Loc.IsKorean && TryGetComponent(out Image logo))
            {
                var art = Resources.Load<Texture2D>("Localization/Logo/logo_" + Loc.Language);
                if (art != null)
                {
                    logo.sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(0.5f, 0.5f), 100f);
                    logo.preserveAspect = true;
                }
            }
            // the glint only shows over the logo's own shape
            if (!TryGetComponent(out Mask _)) gameObject.AddComponent<Mask>().showMaskGraphic = true;
            shine = new GameObject("Shine", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            shine.SetParent(rect, false);
            shine.anchorMin = shine.anchorMax = new Vector2(0f, 0.5f);
            shine.sizeDelta = new Vector2(rect.rect.width * 0.09f, rect.rect.height * 1.8f);
            shine.localRotation = Quaternion.Euler(0f, 0f, -22f);
            shineImage = shine.GetComponent<Image>();
            shineImage.sprite = ShineSprite;
            shineImage.raycastTarget = false;
            shine.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            entrance.TryCancel();
            shownAt = Time.unscaledTime;
            nextShine = shownAt + (droppedOnce ? 1.2f : 1.6f);
            if (!droppedOnce)
            {
                droppedOnce = true;
                Drop();
            }
            else
                Pop();
        }

        private void OnDisable()
        {
            entrance.TryCancel();
            shineMotion.TryCancel();
            if (shine != null) shine.gameObject.SetActive(false);
            drop = 0f;
            squash = Vector2.one;
            if (rect != null)
            {
                rect.anchoredPosition = rest;
                rect.localScale = Vector3.one;
            }
        }

        /// <summary>Falls in from above, lands with a squash and a thud, and wobbles back into shape.</summary>
        private void Drop()
        {
            floatWeight = 0f;
            float height = rect.rect.height * 1.4f;
            entrance = LMotion.Create(1f, 0f, 0.42f).WithEase(Ease.InQuad).WithDelay(0.15f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(Land)
                .Bind(t =>
                {
                    drop = height * t;
                    squash = new Vector2(1f - 0.06f * t, 1f + 0.1f * t); // stretched while it falls
                }).AddTo(gameObject);
            drop = height;
        }

        private void Land()
        {
            drop = 0f;
            GameSfx.Play("StampThud", 0.55f, 0.72f, 0.03f);
            entrance = LMotion.Create(0f, 1f, 0.7f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => squash = Vector2.one)
                .Bind(t =>
                {
                    // a damped wobble: wide and flat on impact, then tall, then settling
                    float wobble = Mathf.Cos(t * Mathf.PI * 3.2f) * Mathf.Pow(1f - t, 2.2f);
                    squash = new Vector2(1f + 0.14f * wobble, 1f - 0.16f * wobble);
                    floatWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
                }).AddTo(gameObject);
        }

        /// <summary>Coming back between customers: a quick pop instead of the full drop.</summary>
        private void Pop()
        {
            floatWeight = 1f;
            drop = 0f;
            entrance = LMotion.Create(0f, 1f, 0.4f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => squash = Vector2.one)
                .Bind(t =>
                {
                    float s = Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(t));
                    squash = new Vector2(s, s);
                }).AddTo(gameObject);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float bob = floatAmplitude * Mathf.Sin((now - shownAt) * 2f * Mathf.PI / floatPeriod) * floatWeight;
            rect.anchoredPosition = rest + new Vector2(0f, drop + bob);
            // pivot is the centre, so squash about the bottom by shifting with the height change
            rect.localScale = new Vector3(squash.x, squash.y, 1f);
            rect.anchoredPosition += new Vector2(0f, (squash.y - 1f) * rect.rect.height * 0.5f);

            if (floatWeight > 0.99f && now >= nextShine)
            {
                nextShine = now + shineEvery;
                Glint();
            }
        }

        /// <summary>A soft band of light passing across the logo.</summary>
        private void Glint()
        {
            shineMotion.TryCancel();
            shine.gameObject.SetActive(true);
            float w = rect.rect.width;
            shineMotion = LMotion.Create(0f, 1f, 0.75f).WithEase(Ease.InOutSine)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (shine != null) shine.gameObject.SetActive(false); })
                .Bind(t =>
                {
                    shine.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.1f, w * 1.1f, t), 0f);
                    shineImage.color = new Color(1f, 0.98f, 0.9f, 0.55f * Mathf.Sin(t * Mathf.PI));
                }).AddTo(gameObject);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>White, brightest along its middle and fading to both sides.</summary>
        private static Sprite ShineSprite
        {
            get
            {
                if (shineSprite != null) return shineSprite;
                const int w = 32, h = 4;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs((x + 0.5f) / w - 0.5f) * 2f;
                    byte a = (byte)(Mathf.Pow(1f - d, 1.6f) * 255f);
                    for (int y = 0; y < h; y++) px[y * w + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                return shineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
