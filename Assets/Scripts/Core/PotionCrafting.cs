using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// Finishing the prescription when "건네기" is pressed, on a stage over the dimmed counter: the picked bottle
    /// and ink pot fly to the middle, the brew pours in and the bottle is corked, the ink pot is uncorked and a quill
    /// dips into it, a blank label is slapped on the bottle and the quill writes the potion's name and who it is for
    /// in a cursive hand, in that ink. Then the bottle flies to the customer.
    /// </summary>
    internal static class PotionCrafting
    {
        /// <summary>Where the cork ends, as a fraction of the picture's height from the top.</summary>
        private const float BottleCork = 0.19f, InkCork = 0.24f;
        private const float BottleHeight = 400f, InkHeight = 220f;

        internal readonly struct Order
        {
            public readonly RectTransform ShelfBottle, ShelfInk, Customer;
            public readonly Sprite Bottle, Ink;
            public readonly Color InkColour;
            public readonly string Potion, ForWhom;

            public Order(RectTransform shelfBottle, Sprite bottle, RectTransform shelfInk, Sprite ink, Color inkColour,
                string potion, string forWhom, RectTransform customer)
            {
                ShelfBottle = shelfBottle; Bottle = bottle; ShelfInk = shelfInk; Ink = ink; InkColour = inkColour;
                Potion = potion; ForWhom = forWhom; Customer = customer;
            }
        }

        private static Sprite quillSprite, labelSprite, dotSprite;

        /// <summary>True while the crafting scene plays (the camera keeps the packaging framing meanwhile).</summary>
        internal static bool Active { get; private set; }
        private static Font script;

        public static async UniTask Play(Order order, CancellationToken token)
        {
            if (order.Bottle == null || order.Ink == null || order.ShelfBottle == null || order.ShelfInk == null) return;
            var stage = Stage(out var canvas);
            var hidden = new[] { Hide(order.ShelfBottle), Hide(order.ShelfInk) };
            Active = true;
            try
            {
                await Run(order, stage, canvas, token);
            }
            finally
            {
                Active = false;
                foreach (var group in hidden) if (group != null) group.alpha = 1f;
                if (stage != null) UnityEngine.Object.Destroy(stage.gameObject);
            }
        }

        private static async UniTask Run(Order order, RectTransform stage, Canvas canvas, CancellationToken token)
        {
            // the dimmed counter behind
            var dim = Piece("Dim", stage, Vector2.zero);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.sizeDelta = Vector2.zero;
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = Color.clear;
            _ = Tween(0.35f, t => dimImage.color = new Color(0.05f, 0.02f, 0.01f, 0.55f * t), dim);

            // the bottle (body and cork apart) and the ink pot, lifted from the shelf to the middle
            var (bottle, bottleBody, bottleCork) = Split("Bottle", stage, order.Bottle, BottleHeight, BottleCork);
            var (inkPot, _, inkCork) = Split("InkPot", stage, order.Ink, InkHeight, InkCork);
            bottleCork.gameObject.SetActive(false);
            var fill = PotionFill.On(bottleBody, Body(order.Bottle, BottleCork));
            fill.FullLine = 0.74f;
            fill.Colour = PotionFill.BrewDisplay;
            fill.SetLevel(0f);

            Vector2 bottleSpot = new(-190f, -110f), inkSpot = new(230f, -250f);
            GameSfx.Play("Whoosh", 0.8f, 0.9f);
            await UniTask.WhenAll(
                Fly(bottle, From(order.ShelfBottle, stage, canvas), bottleSpot, Scale(order.ShelfBottle, BottleHeight, canvas), 0.5f, token),
                Fly(inkPot, From(order.ShelfInk, stage, canvas), inkSpot, Scale(order.ShelfInk, InkHeight, canvas), 0.55f, token));

            // pour the brew in
            float bottleW = bottle.sizeDelta.x;
            var neck = bottleSpot + new Vector2(0f, BottleHeight * (0.5f - BottleCork));
            var stream = Piece("Stream", stage, neck);
            stream.pivot = new Vector2(0.5f, 0f);
            stream.SetSiblingIndex(bottle.GetSiblingIndex()); // behind the bottle's glass
            var streamImage = stream.gameObject.AddComponent<Image>();
            streamImage.color = WithAlpha(PotionFill.BrewDisplay, 0.9f);
            GameSfx.Play("Pour", 0.8f);
            fill.FillTo(1f, 0.85f);
            await Tween(0.9f, t =>
            {
                float width = 16f * Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - t) * 5f);
                stream.sizeDelta = new Vector2(width, 560f * Mathf.Clamp01(t * 5f));
                stream.anchoredPosition = neck + Vector2.down * 30f + Vector2.up * (t > 0.8f ? (t - 0.8f) * 2000f : 0f);
            }, stream, token);
            UnityEngine.Object.Destroy(stream.gameObject);

            // cork it
            bottleCork.gameObject.SetActive(true);
            var corkRest = bottleCork.anchoredPosition;
            var corkImage = bottleCork.GetComponent<Image>();
            await Tween(0.2f, t =>
            {
                float e = t * t;
                bottleCork.anchoredPosition = corkRest + Vector2.up * (130f * (1f - e));
                corkImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 3f));
            }, bottleCork, token);
            GameSfx.Play("CorkIn", 0.9f);
            OrderHudMotion.Bounce(bottle, 1.06f);
            await Wait(0.2f, token);

            // uncork the ink
            GameSfx.Play("CorkPop", 0.9f);
            var inkCorkStart = inkCork.anchoredPosition;
            var inkCorkImage = inkCork.GetComponent<Image>();
            _ = Tween(0.5f, t =>
            {
                inkCork.anchoredPosition = inkCorkStart + new Vector2(120f * t, 170f * t - 220f * t * t);
                inkCork.localRotation = Quaternion.Euler(0f, 0f, -260f * t);
                inkCorkImage.color = new Color(1f, 1f, 1f, 1f - t * t);
            }, inkCork);
            var mouth = Piece("Mouth", inkPot, new Vector2(0f, InkHeight * (0.5f - InkCork) - 4f));
            mouth.sizeDelta = new Vector2(inkPot.sizeDelta.x * 0.3f, 12f);
            var mouthImage = mouth.gameObject.AddComponent<Image>();
            mouthImage.sprite = Dot;
            mouthImage.color = Color.Lerp(order.InkColour, Color.black, 0.6f);
            await Wait(0.2f, token);

            // the quill dips into the ink
            var quill = Piece("Quill", stage, new Vector2(900f, 420f));
            quill.sizeDelta = new Vector2(150f, 150f * 350f / 220f);
            quill.pivot = new Vector2(0.05f, 0.015f); // the nib's tip
            var quillImage = quill.gameObject.AddComponent<Image>();
            quillImage.sprite = Quill;
            var nibInk = Piece("NibInk", quill, Vector2.zero);
            nibInk.anchorMin = nibInk.anchorMax = quill.pivot;
            nibInk.sizeDelta = new Vector2(12f, 12f);
            var nibImage = nibInk.gameObject.AddComponent<Image>();
            nibImage.sprite = Dot;
            nibImage.color = Color.clear;
            var inkMouth = inkSpot + mouth.anchoredPosition;
            await Move(quill, inkMouth + new Vector2(10f, 90f), 0.4f, Ease.OutCubic, token);
            for (int dip = 0; dip < 2; dip++)
            {
                await Move(quill, inkMouth + new Vector2(4f, -6f), 0.14f, Ease.InQuad, token);
                GameSfx.Play("Splash", 0.15f, 1.9f, 0.1f);
                nibImage.color = order.InkColour;
                await Move(quill, inkMouth + new Vector2(10f, dip == 0 ? 60f : 110f), 0.16f, Ease.OutQuad, token);
            }

            // slap the blank label on
            var label = Piece("Label", bottle, new Vector2(520f, -420f));
            float labelW = bottleW * 0.8f;
            label.sizeDelta = new Vector2(labelW, labelW * 325f / 512f);
            var labelImage = label.gameObject.AddComponent<Image>();
            labelImage.sprite = Label;
            var labelRest = new Vector2(0f, -BottleHeight * 0.14f);
            var labelFrom = label.anchoredPosition;
            _ = Move(quill, new Vector2(520f, 260f), 0.3f, Ease.InOutQuad, token);
            await Tween(0.28f, t =>
            {
                float e = t * t;
                label.anchoredPosition = Vector2.Lerp(labelFrom, labelRest, e);
                label.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(28f, -2f, e));
                label.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, e);
            }, label, token);
            GameSfx.Play("StampThud", 0.55f, 1.2f);
            OrderHudMotion.Bounce(bottle, 1.05f);
            await Wait(0.15f, token);

            // write the potion and who it is for, in the chosen ink
            var inkColour = Color.Lerp(order.InkColour, Color.black, 0.25f);
            await Write(label, quill, stage, order.Potion, 0.12f, labelW * 0.78f, 54, inkColour, 0.05f, token);
            if (!string.IsNullOrEmpty(order.ForWhom))
                await Write(label, quill, stage, order.ForWhom, -0.2f, labelW * 0.6f, 36, inkColour, 0.04f, token);
            await Move(quill, new Vector2(980f, 560f), 0.3f, Ease.InQuad, token);
            await Wait(0.25f, token);

            // hand it over
            var inkGroup = inkPot.gameObject.AddComponent<CanvasGroup>();
            _ = Tween(0.3f, t => inkGroup.alpha = 1f - t, inkPot);
            _ = Tween(0.45f, t => dimImage.color = new Color(0.05f, 0.02f, 0.01f, 0.55f * (1f - t)), dim);
            var customerSpot = order.Customer != null ? From(order.Customer, stage, null) + new Vector2(0f, 40f)
                : bottleSpot + Vector2.up * 400f;
            var start = bottle.anchoredPosition;
            await Tween(0.5f, t =>
            {
                float e = 1f - (1f - t) * (1f - t);
                bottle.anchoredPosition = Vector2.Lerp(start, customerSpot, e) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 120f);
                bottle.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, e);
                bottle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 10f);
            }, bottle, token);
        }

        /// <summary>
        /// Writes one line across the label: the letters appear left to right as the quill's nib moves along them
        /// with a small up-and-down scrawl and a scratching sound.
        /// </summary>
        private static async UniTask Write(RectTransform label, RectTransform quill, RectTransform stage, string text,
            float lineY, float maxWidth, int size, Color ink, float perLetter, CancellationToken token)
        {
            var line = Piece("Line", label, new Vector2(0f, label.sizeDelta.y * lineY));
            line.gameObject.AddComponent<RectMask2D>();
            var words = Piece("Text", line, Vector2.zero);
            var label_ = words.gameObject.AddComponent<Text>();
            label_.font = Script;
            label_.text = text;
            label_.color = ink;
            label_.alignment = TextAnchor.MiddleLeft;
            label_.horizontalOverflow = HorizontalWrapMode.Overflow;
            label_.verticalOverflow = VerticalWrapMode.Overflow;
            label_.raycastTarget = false;
            label_.fontSize = size;
            float width = label_.preferredWidth;
            if (width > maxWidth)
            {
                label_.fontSize = Mathf.Max(18, Mathf.FloorToInt(size * maxWidth / width));
                width = label_.preferredWidth;
            }
            float height = label_.fontSize * 1.5f;
            words.sizeDelta = new Vector2(width + 10f, height);
            // the mask grows from the left edge of the centred line
            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = new Vector2(-width * 0.5f, label.sizeDelta.y * lineY);
            line.sizeDelta = new Vector2(0f, height);
            words.pivot = new Vector2(0f, 0.5f);
            words.anchorMin = words.anchorMax = new Vector2(0f, 0.5f);
            words.anchoredPosition = Vector2.zero;

            Vector2 NibAt(float x) => (Vector2)stage.InverseTransformPoint(line.TransformPoint(new Vector3(x, -height * 0.12f, 0f)));
            await Move(quill, NibAt(0f), 0.18f, Ease.OutQuad, token);
            float seconds = Mathf.Clamp(text.Length * perLetter, 0.5f, 1.5f);
            float nextScratch = 0f;
            await Tween(seconds, t =>
            {
                float x = width * t;
                line.sizeDelta = new Vector2(x + 2f, height);
                quill.anchoredPosition = NibAt(x) + new Vector2(0f, Mathf.Sin(t * seconds * 42f) * label_.fontSize * 0.16f);
                quill.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * seconds * 21f) * 3f);
                if (t * seconds >= nextScratch)
                {
                    nextScratch = t * seconds + 0.1f;
                    GameSfx.Play("Scribble", 0.6f, UnityEngine.Random.Range(0.9f, 1.15f));
                }
            }, line, token);
            line.sizeDelta = new Vector2(width + 10f, height);
        }

        // ---- building blocks ----

        private static RectTransform Stage(out Canvas canvas)
        {
            var go = new GameObject("PotionCrafting", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        private static RectTransform Piece(string name, Transform parent, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            return rect;
        }

        /// <summary>A picture split into its body and its cork, both under one root of the given height.</summary>
        private static (RectTransform root, RectTransform body, RectTransform cork) Split(string name, RectTransform stage,
            Sprite sprite, float height, float corkLine)
        {
            var root = Piece(name, stage, Vector2.zero);
            root.sizeDelta = new Vector2(height * sprite.rect.width / sprite.rect.height, height);
            var body = Piece("Body", root, Vector2.zero);
            body.anchorMin = Vector2.zero;
            body.anchorMax = new Vector2(1f, 1f - corkLine);
            body.sizeDelta = Vector2.zero;
            var bodyImage = body.gameObject.AddComponent<Image>();
            bodyImage.sprite = Body(sprite, corkLine);
            bodyImage.raycastTarget = false;
            var cork = Piece("Cork", root, Vector2.zero);
            cork.anchorMin = new Vector2(0f, 1f - corkLine);
            cork.anchorMax = Vector2.one;
            cork.sizeDelta = Vector2.zero;
            var corkImage = cork.gameObject.AddComponent<Image>();
            corkImage.sprite = Cut(sprite, 1f - corkLine, 1f);
            corkImage.raycastTarget = false;
            return (root, body, cork);
        }

        private static Sprite Body(Sprite sprite, float corkLine) => Cut(sprite, 0f, 1f - corkLine);

        private static readonly System.Collections.Generic.Dictionary<(Sprite, float, float), Sprite> cuts = new();

        /// <summary>A horizontal band of a sprite, between two heights given as fractions from its bottom.</summary>
        private static Sprite Cut(Sprite sprite, float from, float to)
        {
            if (cuts.TryGetValue((sprite, from, to), out var known) && known != null) return known;
            var r = sprite.textureRect;
            var band = new Rect(r.x, r.y + r.height * from, r.width, r.height * (to - from));
            return cuts[(sprite, from, to)] = Sprite.Create(sprite.texture, band, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
        }

        private static CanvasGroup Hide(RectTransform shelf)
        {
            if (shelf == null) return null;
            var group = (shelf.TryGetComponent(out CanvasGroup foundCanvasGroup) ? foundCanvasGroup : shelf.gameObject.AddComponent<CanvasGroup>());
            group.alpha = 0f;
            return group;
        }

        /// <summary>A shelf item's centre on the stage.</summary>
        private static Vector2 From(RectTransform item, RectTransform stage, Canvas _)
        {
            var source = item.GetComponentInParent<Canvas>().rootCanvas;
            var cam = source.renderMode == RenderMode.ScreenSpaceOverlay ? null
                : source.worldCamera != null ? source.worldCamera : Camera.main;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, item.TransformPoint(item.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, screen, null, out var local);
            return local;
        }

        /// <summary>How big the shelf item looks now, relative to its size on the stage.</summary>
        private static float Scale(RectTransform item, float stageHeight, Canvas stageCanvas)
        {
            var source = item.GetComponentInParent<Canvas>().rootCanvas;
            var cam = source.renderMode == RenderMode.ScreenSpaceOverlay ? null
                : source.worldCamera != null ? source.worldCamera : Camera.main;
            var r = item.rect;
            float top = RectTransformUtility.WorldToScreenPoint(cam, item.TransformPoint(new Vector3(r.center.x, r.yMax))).y;
            float bottom = RectTransformUtility.WorldToScreenPoint(cam, item.TransformPoint(new Vector3(r.center.x, r.yMin))).y;
            return Mathf.Abs(top - bottom) / Mathf.Max(1f, stageCanvas.scaleFactor) / stageHeight;
        }

        private static async UniTask Fly(RectTransform piece, Vector2 from, Vector2 to, float startScale, float seconds,
            CancellationToken token)
        {
            piece.anchoredPosition = from;
            piece.localScale = Vector3.one * startScale;
            await Tween(seconds, t =>
            {
                float e = 1f - (1f - t) * (1f - t) * (1f - t);
                piece.anchoredPosition = Vector2.Lerp(from, to, e) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 90f);
                piece.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, e);
                piece.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * -8f);
            }, piece, token);
        }

        private static UniTask Move(RectTransform piece, Vector2 to, float seconds, Ease ease, CancellationToken token)
        {
            var from = piece.anchoredPosition;
            return LMotion.Create(from, to, seconds).WithEase(ease).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(p => { if (piece != null) piece.anchoredPosition = p; }).ToUniTask(token);
        }

        private static UniTask Tween(float seconds, Action<float> step, Component owner, CancellationToken token = default) =>
            LMotion.Create(0f, 1f, seconds).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t => { if (owner != null) step(t); }).ToUniTask(token);

        private static UniTask Wait(float seconds, CancellationToken token) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: token);

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        private static Sprite Quill => quillSprite != null ? quillSprite : quillSprite = Load("Crafting/Quill");
        private static Sprite Label => labelSprite != null ? labelSprite : labelSprite = Load("Crafting/Label");
        private static Font Script => script != null ? script : script = Resources.Load<Font>("Fonts/DancingScript");

        private static Sprite Load(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite Dot
        {
            get
            {
                if (dotSprite != null) return dotSprite;
                const int size = 32;
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
