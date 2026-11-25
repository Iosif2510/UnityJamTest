using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The chosen ink's label slapped onto the bottle: the label is cut from the ink pot's own picture, comes down
    /// big and lands with a thud and a few flecks of ink, finishing the prescription.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabelStamp : MonoBehaviour
    {
        // The cream label on the front of every ink pot picture, as fractions of the picture.
        private static readonly Rect LabelArea = new(0.255f, 0.275f, 0.5f, 0.255f);

        private static Sprite dotSprite;

        private RectTransform stamp;
        private Image image;
        private Sprite shownFrom;
        private MotionHandle motion;

        public static LabelStamp On(RectTransform bottle)
        {
            var s = bottle.GetComponent<LabelStamp>();
            if (s != null) return s;
            s = bottle.gameObject.AddComponent<LabelStamp>();
            s.Build();
            return s;
        }

        private void Build()
        {
            var bottle = (RectTransform)transform;
            stamp = new GameObject("Stamp", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            stamp.SetParent(bottle, false);
            stamp.anchorMin = stamp.anchorMax = new Vector2(0.5f, 0.33f);
            image = stamp.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            // after the potion and glass, before the name label
            var fill = bottle.Find("Glass");
            stamp.SetSiblingIndex(fill != null ? fill.GetSiblingIndex() + 1 : 0);
            stamp.gameObject.SetActive(false);
        }

        /// <summary>Stamps the label of <paramref name="inkPot"/> on; <paramref name="animate"/> false shows it at once.</summary>
        public void Show(Sprite inkPot, Color ink, bool animate = true)
        {
            if (inkPot == null) { Clear(); return; }
            if (shownFrom == inkPot && stamp.gameObject.activeSelf && animate) return;
            shownFrom = inkPot;
            // just the ink's symbol (Resources/Crafting/Symbols, cut out of the pot's label with a clear background);
            // the old square cut of the label is the fallback
            var symbol = Symbol(inkPot);
            image.sprite = symbol != null ? symbol : Cut(inkPot);
            var size = ((RectTransform)transform).rect.size;
            // inside the glass: no wider than the room this bottle shape has at its label spot
            var (centre, room) = BottleLabelArea.For(TryGetComponent(out UnityEngine.UI.Image picture) ? picture.sprite : null);
            stamp.anchorMin = stamp.anchorMax = centre;
            float width = size.x * Mathf.Min(symbol != null ? 0.3f : 0.44f, room);
            stamp.sizeDelta = new Vector2(width, width * image.sprite.rect.height / image.sprite.rect.width);
            stamp.gameObject.SetActive(true);
            motion.TryCancel();
            float tilt = Random.Range(-3f, 3f);
            if (!animate)
            {
                stamp.localScale = Vector3.one;
                stamp.localRotation = Quaternion.Euler(0f, 0f, tilt);
                image.color = Color.white;
                return;
            }
            motion = LMotion.Create(0f, 1f, 0.16f).WithEase(Ease.InQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => Land(ink))
                .Bind(t =>
                {
                    stamp.localScale = Vector3.one * Mathf.Lerp(1.9f, 1f, t);
                    stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(tilt - 14f, tilt, t));
                    image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 2.5f));
                }).AddTo(gameObject);
        }

        public void Clear()
        {
            motion.TryCancel();
            shownFrom = null;
            if (stamp != null) stamp.gameObject.SetActive(false);
        }

        private void Land(Color ink)
        {
            GameSfx.Play("StampThud", 0.45f, 1.35f, 0.05f);
            OrderHudMotion.Bounce(transform, 1.08f);
            for (int i = 0; i < 6; i++) Fleck(ink, i);
        }

        /// <summary>A small fleck of ink flying out from under the label.</summary>
        private void Fleck(Color ink, int i)
        {
            var rect = new GameObject("Fleck", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = stamp.anchorMin;
            var fleck = rect.GetComponent<Image>();
            fleck.sprite = Dot;
            fleck.raycastTarget = false;
            float size = Random.Range(5f, 10f);
            rect.sizeDelta = new Vector2(size, size);
            float angle = (i / 6f + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f);
            float reach = stamp.sizeDelta.x * Random.Range(0.55f, 0.8f);
            var go = rect.gameObject;
            LMotion.Create(0f, 1f, 0.35f).WithEase(Ease.OutCubic).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (go != null) Destroy(go); })
                .Bind(t =>
                {
                    rect.anchoredPosition = dir * reach * t;
                    var c = ink;
                    c.a = 1f - t * t;
                    fleck.color = c;
                }).AddTo(go);
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> symbols = new();

        private static Sprite Symbol(Sprite pot)
        {
            if (symbols.TryGetValue(pot.name, out var known) && known != null) return known;
            var tex = Resources.Load<Texture2D>("Crafting/Symbols/" + pot.name);
            if (tex == null) return null;
            return symbols[pot.name] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite Cut(Sprite pot)
        {
            var r = pot.textureRect;
            var area = new Rect(r.x + r.width * LabelArea.x, r.y + r.height * LabelArea.y,
                r.width * LabelArea.width, r.height * LabelArea.height);
            return Sprite.Create(pot.texture, area, new Vector2(0.5f, 0.5f), pot.pixelsPerUnit);
        }

        private static Sprite Dot
        {
            get
            {
                if (dotSprite != null) return dotSprite;
                const int size = 24;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude;
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(size * 0.5f - d) * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return dotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
