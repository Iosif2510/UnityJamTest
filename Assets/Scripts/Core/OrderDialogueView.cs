using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PotionShop.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class OrderDialogueView : MonoBehaviour, IPointerClickHandler
    {
        private const float BottomPadding = 22f;

        [SerializeField] private TMP_Text speaker;
        [SerializeField] private TMP_Text body;
        [SerializeField] private ScrollRect scroll;
        [SerializeField, Min(0)] private float fadeSeconds = .16f;
        [SerializeField] private DialogueRevealMode revealMode;

        private DialogueTimeline activeLine;
        private string message;
        private DialogueGlyphLayer mainLayer;
        private DialogueGlyphLayer firstCopy;
        private DialogueGlyphLayer secondCopy;
        private TMP_Text firstCopyText;
        private TMP_Text secondCopyText;
        private DialogueRainGraphic rain;
        private double startedAt;
        private double skippedAt;
        private int lastScrolledIndex;
        private Vector2 lastViewportSize;
        private float lastWrapWidth;
        private TMP_FontAsset lastFont;
        private bool revealing;
        private bool finalFrameDrawn;
        private int speechBeat;
        private bool skipBeatPlayed;

        internal event Action<float> SpeechBeat;

        /// <summary>Pitch of the speaking blips; each customer gets their own (set before a line plays).</summary>
        internal float VoicePitch { get; set; } = 1f;

        private const float MinVoiceGap = 0.065f;
        private readonly DialogueVoice voice = new();
        private int voicedChars;
        private double lastVoiced;
        internal TMP_FontAsset SpeakerFont => speaker.font;

        public void SetSpeaker(string name) => speaker.text = string.IsNullOrWhiteSpace(name) ? "손님" : name;

        public void ShowMessage(string message)
        {
            ClearLine();
            this.message = message?.Replace('<', '‹').Replace('>', '›') ?? string.Empty;
            body.maxVisibleCharacters = int.MaxValue;
            body.text = Layout(this.message).Text;
            FitContent();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        public UniTask PlaySymptoms(DialogueOption line, SymptomEffect defaultEffect, int charactersPerSecond,
            CancellationToken token) =>
            Reveal(new DialogueTimeline(line, defaultEffect, charactersPerSecond, revealMode), token);

        public UniTask PlayReaction(string line, int charactersPerSecond, CancellationToken token, string reactionKey = "") =>
            Reveal(new DialogueTimeline(line, reactionKey, charactersPerSecond, revealMode), token);

        private void Awake()
        {
            body.textWrappingMode = TextWrappingModes.NoWrap;
            var margin = body.margin;
            // Keep the strongest motion inside the clipped viewport.
            body.margin = new Vector4(Mathf.Max(20f, margin.x), Mathf.Max(12f, margin.y),
                Mathf.Max(36f, margin.z), margin.w);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (revealing && double.IsPositiveInfinity(skippedAt) && !eventData.dragging &&
                eventData.button == PointerEventData.InputButton.Left)
                skippedAt = Time.unscaledTimeAsDouble - startedAt;
        }

        private async UniTask Reveal(DialogueTimeline line, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ClearLine();
            activeLine = line;
            skippedAt = double.PositiveInfinity;
            lastScrolledIndex = -1;
            finalFrameDrawn = false;
            revealing = true;

            mainLayer ??= new DialogueGlyphLayer(body);
            line.Layout = Layout(line.Text);
            float fade = revealMode == DialogueRevealMode.Character ? 0f : fadeSeconds;
            mainLayer.SetLine(line, fade);
            FitContent();
            if (scroll != null)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1;
            }
            if (line.HasGlyphCopies)
            {
                if (firstCopy == null)
                    firstCopy = new DialogueGlyphLayer(firstCopyText = CreateCopy("Symptom Afterimage"), 1);
                firstCopyText.gameObject.SetActive(true);
                firstCopy.SetLine(line, fade);
            }
            if (line.HasDoubleEcho)
            {
                if (secondCopy == null)
                    secondCopy = new DialogueGlyphLayer(secondCopyText = CreateCopy("Second Echo"), 2);
                secondCopyText.gameObject.SetActive(true);
                secondCopy.SetLine(line, fade);
            }
            if (line.HasDrops)
            {
                if (rain == null)
                {
                    rain = NewLayer("Raindrops").gameObject.AddComponent<DialogueRainGraphic>();
                    rain.raycastTarget = false;
                }
                rain.gameObject.SetActive(true);
            }
            startedAt = Time.unscaledTimeAsDouble;
            voicedChars = 0;
            lastVoiced = double.NegativeInfinity;
            voice.Begin();
            speechBeat = 0;
            skipBeatPlayed = false;
            SpeechBeat?.Invoke(.68f);

            try
            {
                while (activeLine == line && Time.unscaledTimeAsDouble - startedAt < Math.Min(line.Duration, skippedAt))
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (activeLine != line) throw new OperationCanceledException();
                revealing = false;
                Draw(Time.unscaledTimeAsDouble - startedAt);
            }
            catch (OperationCanceledException)
            {
                if (activeLine == line) ClearLine();
                throw;
            }
        }

        private void Voice(double elapsed)
        {
            var text = activeLine.Text;
            var starts = activeLine.CharStarts;
            int newest = -1;
            while (voicedChars < text.Length && starts[voicedChars] <= elapsed) newest = voicedChars++;
            // several letters in one frame (or very fast text): voice only the latest, and not too often
            if (newest < 0 || Time.unscaledTimeAsDouble - lastVoiced < MinVoiceGap) return;
            if (!DialogueVoice.Voiced(text[newest], out _, out _)) return;
            lastVoiced = Time.unscaledTimeAsDouble;
            int end = text.Length - 1;
            while (end > 0 && !DialogueVoice.Voiced(text[end], out _, out _)) end--;
            bool question = newest >= end - 1 && text.TrimEnd().EndsWith("?");
            voice.Speak(text[newest], VoicePitch, question);
        }

        private void LateUpdate()
        {
            if (activeLine == null && message == null) return;
            bool reflow = lastWrapWidth != WrapWidth || lastFont != body.font;
            bool resized = scroll != null && lastViewportSize != scroll.viewport.rect.size;
            if (activeLine == null)
            {
                if (reflow) body.text = Layout(message).Text;
                if (reflow || resized) FitContent();
                return;
            }

            double elapsed = Time.unscaledTimeAsDouble - startedAt;
            // A murmured voice, one syllable per letter as it appears; quiet once the line is skipped or done.
            if (revealing && elapsed < skippedAt && elapsed < activeLine.Duration) Voice(elapsed);
            if (reflow)
            {
                activeLine.Layout = Layout(activeLine.Text);
                mainLayer.Reflow(elapsed, skippedAt);
                if (activeLine.HasGlyphCopies) ReflowCopy(firstCopy, firstCopyText, elapsed);
                if (activeLine.HasDoubleEcho) ReflowCopy(secondCopy, secondCopyText, elapsed);
            }
            if (reflow || resized) FitContent();
            if (reflow || resized || !finalFrameDrawn || activeLine.HasEffects)
                Draw(elapsed);
            double visualEnd = double.IsPositiveInfinity(skippedAt)
                ? Math.Max(activeLine.Duration + fadeSeconds, activeLine.LastAccentEnd)
                : skippedAt + DialogueEmphasis.PulseSeconds;
            if (!revealing && elapsed >= visualEnd)
                finalFrameDrawn = true;
        }

        private float WrapWidth => Mathf.Max(1f, body.rectTransform.rect.width - body.margin.x - body.margin.z);

        private DialogueLineLayout Layout(string source)
        {
            lastWrapWidth = WrapWidth;
            lastFont = body.font;
            return DialogueLineLayout.Create(source, body, lastWrapWidth);
        }

        private void ReflowCopy(DialogueGlyphLayer layer, TMP_Text copy, double elapsed)
        {
            MatchBodyLayout(copy);
            layer.Reflow(elapsed, skippedAt);
        }

        private void Draw(double elapsed)
        {
            var beats = activeLine.AccentTimes;
            if (elapsed >= skippedAt && !skipBeatPlayed)
            {
                skipBeatPlayed = true;
                SpeechBeat?.Invoke(1f);
            }
            else if (elapsed < skippedAt && speechBeat < beats.Count && elapsed >= beats[speechBeat])
            {
                if (beats[speechBeat] >= .12f) SpeechBeat?.Invoke(1f);
                speechBeat++;
            }
            mainLayer.Render(elapsed, skippedAt);
            if (activeLine.HasGlyphCopies) firstCopy.Render(elapsed, skippedAt);
            if (activeLine.HasDoubleEcho) secondCopy.Render(elapsed, skippedAt);
            if (activeLine.HasDrops) rain.Draw(body, activeLine, elapsed, skippedAt);
            ScrollToLatestGlyph(elapsed);
        }

        private void FitContent()
        {
            if (scroll == null) return;
            lastViewportSize = scroll.viewport.rect.size;
            float height = Mathf.Max(lastViewportSize.y,
                body.GetPreferredValues(body.text, WrapWidth, 0).y + BottomPadding);
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            body.ForceMeshUpdate();
            lastScrolledIndex = -1;
        }

        private void ScrollToLatestGlyph(double elapsed)
        {
            if (scroll == null) return;
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0) return;
            var info = body.textInfo;
            for (int i = info.characterCount - 1; i > lastScrolledIndex; i--)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible || !activeLine.TryGetCue(character.index, out var cue) ||
                    elapsed < Math.Min(cue.Start, skippedAt))
                    continue;
                lastScrolledIndex = i;
                float descender = info.lineInfo[character.lineNumber].descender;
                Vector3 worldBottom = body.rectTransform.TransformPoint(new Vector3(0f, descender, 0f));
                float lineBottom = scroll.viewport.InverseTransformPoint(worldBottom).y;
                float missingHeight = scroll.viewport.rect.yMin + BottomPadding - lineBottom;
                if (missingHeight > 0f)
                {
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition =
                        Mathf.Clamp01(scroll.verticalNormalizedPosition - missingHeight / overflow);
                }
                return;
            }
        }

        private TextMeshProUGUI CreateCopy(string layerName)
        {
            var copy = NewLayer(layerName).gameObject.AddComponent<TextMeshProUGUI>();
            MatchBodyLayout(copy);
            copy.raycastTarget = false;
            return copy;
        }

        private void MatchBodyLayout(TMP_Text copy)
        {
            copy.font = body.font;
            // The moving afterimages already act as shadows.
            copy.fontSharedMaterial = body.font.material;
            copy.fontSize = body.fontSize;
            copy.fontStyle = body.fontStyle;
            copy.fontWeight = body.fontWeight;
            copy.fontFeatures = body.fontFeatures;
            copy.styleSheet = body.styleSheet;
            copy.textStyle = body.textStyle;
            copy.alignment = body.alignment;
            copy.color = body.color;
            copy.lineSpacing = body.lineSpacing;
            copy.characterSpacing = body.characterSpacing;
            copy.characterHorizontalScale = body.characterHorizontalScale;
            copy.wordSpacing = body.wordSpacing;
            copy.paragraphSpacing = body.paragraphSpacing;
            copy.margin = body.margin;
            copy.textWrappingMode = body.textWrappingMode;
            copy.overflowMode = body.overflowMode;
        }

        private RectTransform NewLayer(string layerName)
        {
            var rect = (RectTransform)new GameObject(layerName, typeof(RectTransform)).transform;
            rect.SetParent(body.transform.parent, false);
            rect.pivot = body.rectTransform.pivot;
            rect.anchorMin = body.rectTransform.anchorMin;
            rect.anchorMax = body.rectTransform.anchorMax;
            rect.offsetMin = body.rectTransform.offsetMin;
            rect.offsetMax = body.rectTransform.offsetMax;
            rect.SetAsFirstSibling();
            return rect;
        }

        private void ClearLine()
        {
            activeLine = null;
            message = null;
            revealing = false;
            mainLayer?.Clear();
            firstCopy?.Clear();
            secondCopy?.Clear();
            if (firstCopyText != null) firstCopyText.gameObject.SetActive(false);
            if (secondCopyText != null) secondCopyText.gameObject.SetActive(false);
            if (rain != null) rain.gameObject.SetActive(false);
        }

        private void OnDisable() => ClearLine();

        internal void StopPresentation() => ClearLine();

        private void OnDestroy()
        {
            mainLayer?.Dispose();
            firstCopy?.Dispose();
            secondCopy?.Dispose();
        }
    }
}
