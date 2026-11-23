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
            ingredients.text = "재료";
            bottle.text = "물약병";
            ink.text = "잉크";
            total.text = "총점";
            ingredientPoints.text = bottlePoints.text = inkPoints.text = timeBonus.text = string.Empty;
            passableStamp.SetActive(false);
            fineBrewStamp.SetActive(false);
            otherStamp.gameObject.SetActive(false);
        }

        internal async UniTask Play(RoundResult result, Camera camera, CancellationToken token)
        {
            await BetweenRows(token);
            ingredients.text = $"재료   {result.CorrectIngredients} / {RecipeEntry.RequiredIngredientKinds}";
            await CountUp(ingredientPoints, result.IngredientPoints, "+ {0}", token);
            await BetweenRows(token);
            bottle.text = $"물약병   {MatchLabel(result.BottleCorrect, result.TimedOut)}";
            await CountUp(bottlePoints, result.BottlePoints, "+ {0}", token);
            await BetweenRows(token);
            ink.text = $"잉크   {MatchLabel(result.InkCorrect, result.TimedOut)}";
            await CountUp(inkPoints, result.InkPoints, "+ {0}", token);
            await BetweenRows(token);
            await CountUp(timeBonus, result.TimeBonus, "+ {0}", token);
            await BetweenRows(token);
            total.text = "총점   0점";
            await CountUp(total, result.Total, "총점   {0}점", token, 0.55f);
            await Pop(total.rectTransform, token);
            await BetweenRows(token);

            otherStamp.sprite = result.TimedOut ? timeoutStamp : failureStamp;
            GameObject stamp = result.Stamp switch
            {
                RoundResult.PassableStamp => passableStamp,
                RoundResult.FineBrewStamp => fineBrewStamp,
                _ => otherStamp.gameObject
            };
            await Stamp(stamp, camera, token);
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

        private static string MatchLabel(bool correct, bool timedOut) =>
            timedOut ? "미제출" : correct ? "일치" : "불일치";
    }
}
