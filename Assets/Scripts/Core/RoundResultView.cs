using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using PotionShop.Data;
using TMPro;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class RoundResultView : MonoBehaviour
    {
        [SerializeField] private TMP_Text ingredients;
        [SerializeField] private TMP_Text bottle;
        [SerializeField] private TMP_Text ink;
        [SerializeField] private TMP_Text ingredientPoints;
        [SerializeField] private TMP_Text bottlePoints;
        [SerializeField] private TMP_Text inkPoints;
        [SerializeField] private TMP_Text timeBonus;
        [SerializeField] private TMP_Text total;
        [SerializeField] private GameObject passableStamp;
        [SerializeField] private GameObject fineBrewStamp;
        [SerializeField] private UnityEngine.UI.Image otherStamp;
        [SerializeField] private Sprite failureStamp;
        [SerializeField] private Sprite timeoutStamp;

        internal void Prepare()
        {
            ingredients.text = Loc.T("재료");
            bottle.text = Loc.T("물약병");
            ink.text = Loc.T("잉크");
            total.text = Loc.T("총점");
            ingredientPoints.text = bottlePoints.text = inkPoints.text = timeBonus.text = string.Empty;
            passableStamp.SetActive(false);
            fineBrewStamp.SetActive(false);
            otherStamp.gameObject.SetActive(false);
            // the stamps' wording in the game's language
            foreach (var stampImage in new[] { passableStamp, fineBrewStamp })
                foreach (var picture in stampImage.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    picture.sprite = Loc.Stamp(picture.sprite);
            foreach (var row in new[] { ingredients, bottle, ink })
            {
                row.textWrappingMode = TextWrappingModes.NoWrap; // an answer note may run past the label column
                row.richText = true;
            }
            // The customer stands larger beside the card, cut off by the bottom edge rather than standing on the desk.
            var portrait = transform.Find("ResultPortrait") as RectTransform;
            if (portrait != null)
            {
                portrait.sizeDelta = new Vector2(320f, 480f);
                portrait.anchoredPosition = new Vector2(-440f, -150f);
            }
            var next = transform.Find("ResultCard/NextOrderButton/Label")?.GetComponent<UnityEngine.UI.Text>();
            if (next != null) next.text = Loc.T("다음 주문");
        }

        /// <param name="bottleAnswer">Name of the right bottle, shown when the choice was wrong (so the next one is better).</param>
        internal async UniTask Play(RoundResult result, Camera camera, CancellationToken token,
            string bottleAnswer = null, string inkAnswer = null)
        {
            await DropIn(token);
            await BetweenRows(token);
            ingredients.text = Loc.F("재료   {0} / {1}", result.CorrectIngredients, RecipeEntry.RequiredIngredientKinds);
            await CountUp(ingredientPoints, result.IngredientPoints, "+ {0}", token);
            await BetweenRows(token);
            bottle.text = Loc.F("물약병  {0}", MatchLabel(result.BottleCorrect, result.TimedOut)) + Answer(result.BottleCorrect, bottleAnswer);
            await CountUp(bottlePoints, result.BottlePoints, "+ {0}", token);
            await BetweenRows(token);
            ink.text = Loc.F("잉크  {0}", MatchLabel(result.InkCorrect, result.TimedOut)) + Answer(result.InkCorrect, inkAnswer);
            await CountUp(inkPoints, result.InkPoints, "+ {0}", token);
            await BetweenRows(token);
            await CountUp(timeBonus, result.TimeBonus, "+ {0}", token);
            await BetweenRows(token);
            total.text = Loc.T("총점   0점");
            await CountUp(total, result.Total, Loc.T("총점   {0}점"), token, 0.55f);
            await Pop(total.rectTransform, token);
            await BetweenRows(token);

            otherStamp.sprite = Loc.Stamp(result.TimedOut ? timeoutStamp : failureStamp);
            GameObject stamp = result.Stamp switch
            {
                RoundResult.PassableStamp => passableStamp,
                RoundResult.FineBrewStamp => fineBrewStamp,
                _ => otherStamp.gameObject
            };
            await Stamp(stamp, camera, token);
            if (result.Stamp == RoundResult.FineBrewStamp && !result.TimedOut)
                await Celebrate((RectTransform)stamp.transform, token);
            else if (result.Stamp != RoundResult.PassableStamp || result.TimedOut)
                await Sag(token);
        }

        /// <summary>Rolls a number up with a tick per step, so each row is felt rather than just shown.</summary>
        private static async UniTask CountUp(TMP_Text label, int value, string format, CancellationToken token,
            float duration = 0.28f)
        {
            if (value <= 0)
            {
                label.text = string.Format(format, value);
                GameSfx.Play("Tick", 0.5f, 0.8f);
                return;
            }
            int shown = -1;
            await LMotion.Create(0f, value, duration).WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(v =>
                {
                    int n = Mathf.RoundToInt(v);
                    if (n == shown) return;
                    shown = n;
                    label.text = string.Format(format, n);
                    GameSfx.Play("Tick", 0.55f, 0.9f + 0.5f * n / value, 0.03f);
                }).ToUniTask(token);
            label.text = string.Format(format, value);
        }

        private static UniTask Pop(RectTransform rect, CancellationToken token)
        {
            var scale = rect.localScale;
            return LMotion.Create(1.18f, 1f, 0.22f).WithEase(Ease.OutBack)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(s => rect.localScale = scale * s).ToUniTask(token);
        }

        private static UniTask BetweenRows(CancellationToken token) =>
            UniTask.Delay(TimeSpan.FromSeconds(.16), ignoreTimeScale: true, cancellationToken: token);

        private async UniTask Stamp(GameObject stamp, Camera camera, CancellationToken token)
        {
            var rect = (RectTransform)stamp.transform;
            var group = stamp.TryGetComponent<CanvasGroup>(out var current) ? current : stamp.AddComponent<CanvasGroup>();
            var scale = rect.localScale;
            var position = rect.localPosition;
            var center = rect.localRotation * Vector3.Scale((Vector3)rect.rect.center, scale);
            var panel = (RectTransform)transform;
            var panelPosition = panel.anchoredPosition;
            var cameraPosition = camera != null ? camera.transform.localPosition : Vector3.zero;
            SetStampPose(0);
            stamp.SetActive(true);
            try
            {
                await LMotion.Create(0f, 1f, .25f).WithEase(Ease.OutCubic)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).Bind(SetStampPose).ToUniTask(token);
                SetStampPose(1);
                // The stamp lands: thud, then a held beat before the shake, so the impact reads.
                GameSfx.Play("StampThud", 1f, 1f, 0.04f);
                await UniTask.Delay(TimeSpan.FromSeconds(0.07), ignoreTimeScale: true, cancellationToken: token);
                await LMotion.Create(0f, 1f, .22f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        float remaining = (1 - t) * (1 - t);
                        var offset = new Vector2(Mathf.Cos(28 * t) * 18, Mathf.Sin(39 * t + .4f) * 14) * remaining;
                        panel.anchoredPosition = panelPosition + offset;
                        if (camera != null)
                            camera.transform.localPosition = cameraPosition + (Vector3)offset * .015f;
                    }).ToUniTask(token);
            }
            finally
            {
                if (rect != null) SetStampPose(1);
                if (panel != null) panel.anchoredPosition = panelPosition;
                if (camera != null) camera.transform.localPosition = cameraPosition;
            }

            void SetStampPose(float t)
            {
                float factor = Mathf.Lerp(1.8f, 1f, t);
                rect.localScale = scale * factor;
                rect.localPosition = position + (1 - factor) * center;
                group.alpha = t;
            }
        }

        private static readonly Color[] ConfettiColors =
        {
            new(0.98f, 0.78f, 0.30f), new(0.93f, 0.45f, 0.32f), new(0.50f, 0.78f, 0.52f), new(0.45f, 0.62f, 0.92f),
            new(0.95f, 0.92f, 0.82f),
        };

        /// <summary>A fine brew: a bright arpeggio and a burst of paper confetti from the stamp.</summary>
        private async UniTask Celebrate(RectTransform stamp, CancellationToken token)
        {
            GameSfx.Play("Fanfare", 0.85f);
            var card = (RectTransform)ingredients.transform.parent;
            var origin = card.InverseTransformPoint(stamp.TransformPoint(stamp.rect.center));
            var pieces = new RectTransform[28];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = new GameObject("Confetti", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
                piece.SetParent(card, false);
                piece.sizeDelta = new Vector2(UnityEngine.Random.Range(8f, 14f), UnityEngine.Random.Range(12f, 20f));
                piece.localPosition = origin;
                var image = piece.GetComponent<UnityEngine.UI.Image>();
                image.color = ConfettiColors[i % ConfettiColors.Length];
                image.raycastTarget = false;
                pieces[i] = piece;
                float angle = UnityEngine.Random.Range(35f, 145f) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(260f, 520f);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                float spin = UnityEngine.Random.Range(-720f, 720f);
                var start = (Vector2)origin;
                _ = LMotion.Create(0f, 1.1f, 1.1f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        if (piece == null) return;
                        piece.localPosition = start + velocity * t + new Vector2(0f, -620f) * t * t;
                        piece.localRotation = Quaternion.Euler(0f, 0f, spin * t);
                        var c = image.color;
                        image.color = new Color(c.r, c.g, c.b, Mathf.Clamp01((1.1f - t) / 0.35f));
                    }).ToUniTask(token);
            }
            try { await UniTask.Delay(TimeSpan.FromSeconds(1.15), ignoreTimeScale: true, cancellationToken: token); }
            finally { foreach (var p in pieces) if (p != null) Destroy(p.gameObject); }
        }

        /// <summary>A failed brew: a deflating sound and the card sags a little, then settles.</summary>
        private UniTask Sag(CancellationToken token)
        {
            GameSfx.Play("Flop", 0.8f);
            var card = (RectTransform)ingredients.transform.parent;
            var rest = card.anchoredPosition;
            return LMotion.Create(0f, 1f, 0.6f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    if (card == null) return;
                    float droop = Mathf.Sin(Mathf.Min(t, 0.5f) / 0.5f * Mathf.PI * 0.5f) * (1f - Mathf.Max(0f, t - 0.5f) * 1.2f);
                    card.anchoredPosition = rest + new Vector2(0f, -12f * droop);
                    card.localRotation = Quaternion.Euler(0f, 0f, 2.5f * droop);
                }).ToUniTask(token);
        }

        /// <summary>The right answer, small and muted, after a wrong one.</summary>
        private static string Answer(bool correct, string answer) =>
            correct || string.IsNullOrEmpty(answer) ? string.Empty : $" <size=88%><color=#9a4a2c>→ {answer}</color></size>";

        /// <summary>The card lands on the counter: drops in from above, tilting straight, with a papery sound.</summary>
        private UniTask DropIn(CancellationToken token)
        {
            var card = (RectTransform)ingredients.transform.parent;
            var rest = card.anchoredPosition;
            GameSfx.Play("Card", 0.8f, 1f, 0.05f);
            return LMotion.Create(1f, 0f, 0.38f).WithEase(Ease.OutBack).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    if (card == null) return;
                    card.anchoredPosition = rest + new Vector2(0f, 90f * t);
                    card.localRotation = Quaternion.Euler(0f, 0f, -4f * t);
                }).ToUniTask(token);
        }

        private static string MatchLabel(bool correct, bool timedOut) =>
            timedOut ? Loc.T("미제출") : correct ? Loc.T("일치") : Loc.T("불일치");
        // (labels kept short so a right-answer note fits on the same line)
    }
}
