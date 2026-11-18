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
        [SerializeField] private TMP_Text speaker;
        [SerializeField] private TMP_Text body;
        [SerializeField] private ScrollRect scroll;
        [SerializeField, Min(0)] private float fadeSeconds = .16f;
        [SerializeField] private DialogueRevealMode revealMode;

        private DialogueTimeline activeLine;
        private DialogueGlyphLayer mainLayer;
        private DialogueGlyphLayer firstCopy;
        private DialogueGlyphLayer secondCopy;
        private TMP_Text firstCopyText;
        private TMP_Text secondCopyText;
        private DialogueRainGraphic rain;
        private double startedAt;
        private double skippedAt;
        private int lastScrolledIndex;
        private float lastWidth;
        private bool revealing;
        private bool finalFrameDrawn;

        public void SetSpeaker(string name) => speaker.text = string.IsNullOrWhiteSpace(name) ? "손님" : name;

        public void ShowMessage(string message)
        {
            ClearLine();
            body.text = message;
            body.maxVisibleCharacters = int.MaxValue;
            FitContent();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        public UniTask PlaySymptoms(DialogueOption line, SymptomEffect defaultEffect, int charactersPerSecond,
            CancellationToken token) =>
            Reveal(new DialogueTimeline(line, defaultEffect, charactersPerSecond, revealMode), token);

        public UniTask PlayReaction(string line, int charactersPerSecond, CancellationToken token) =>
            Reveal(new DialogueTimeline(line, charactersPerSecond, revealMode), token);

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
            startedAt = Time.unscaledTimeAsDouble;
            skippedAt = double.PositiveInfinity;
            lastScrolledIndex = -1;
            finalFrameDrawn = false;
            revealing = true;

            mainLayer ??= new DialogueGlyphLayer(body);
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

        private void LateUpdate()
        {
            if (activeLine == null) return;
            if (scroll != null && !Mathf.Approximately(lastWidth, scroll.viewport.rect.width))
                FitContent();
            double elapsed = Time.unscaledTimeAsDouble - startedAt;
            if (!finalFrameDrawn || activeLine.HasEffects)
                Draw(elapsed);
            if (!revealing && elapsed >= Math.Min(activeLine.Duration + fadeSeconds, skippedAt))
                finalFrameDrawn = true;
        }

        private void Draw(double elapsed)
        {
            mainLayer.Render(elapsed, skippedAt);
            if (activeLine.HasGlyphCopies) firstCopy.Render(elapsed, skippedAt);
            if (activeLine.HasDoubleEcho) secondCopy.Render(elapsed, skippedAt);
            if (activeLine.HasDrops) rain.Draw(body, activeLine, elapsed, skippedAt);
            ScrollToLatestGlyph(elapsed);
        }

        private void FitContent()
        {
            if (scroll == null) return;
            lastWidth = scroll.viewport.rect.width;
            float height = Mathf.Max(scroll.viewport.rect.height,
                body.GetPreferredValues(body.text, lastWidth, 0).y + 16);
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            body.ForceMeshUpdate();
            if (revealing) lastScrolledIndex = -1;
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
                float target = Mathf.Clamp(-character.descender + 12 - scroll.viewport.rect.height, 0, overflow);
                float current = (1 - Mathf.Clamp01(scroll.verticalNormalizedPosition)) * overflow;
                if (target > current) scroll.verticalNormalizedPosition = 1 - target / overflow;
                return;
            }
        }

        private TextMeshProUGUI CreateCopy(string layerName)
        {
            var copy = NewLayer(layerName).gameObject.AddComponent<TextMeshProUGUI>();
            copy.font = body.font;
            copy.fontSharedMaterial = body.fontSharedMaterial;
            copy.fontSize = body.fontSize;
            copy.fontStyle = body.fontStyle;
            copy.alignment = body.alignment;
            copy.color = body.color;
            copy.lineSpacing = body.lineSpacing;
            copy.characterSpacing = body.characterSpacing;
            copy.margin = body.margin;
            copy.textWrappingMode = body.textWrappingMode;
            copy.overflowMode = body.overflowMode;
            copy.raycastTarget = false;
            return copy;
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
