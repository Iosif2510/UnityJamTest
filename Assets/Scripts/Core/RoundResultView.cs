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
            ingredientPoints.text = $"+ {result.IngredientPoints}";
            await BetweenRows(token);
            bottle.text = $"물약병   {MatchLabel(result.BottleCorrect, result.TimedOut)}";
            bottlePoints.text = $"+ {result.BottlePoints}";
            await BetweenRows(token);
            ink.text = $"잉크   {MatchLabel(result.InkCorrect, result.TimedOut)}";
            inkPoints.text = $"+ {result.InkPoints}";
            await BetweenRows(token);
            timeBonus.text = $"+ {result.TimeBonus}";
            await BetweenRows(token);
            total.text = $"총점   {result.Total}점";
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
                await LMotion.Create(0f, 1f, .22f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        float remaining = (1 - t) * (1 - t);
                        var offset = new Vector2(Mathf.Cos(28 * t) * 14, Mathf.Sin(39 * t + .4f) * 11) * remaining;
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
