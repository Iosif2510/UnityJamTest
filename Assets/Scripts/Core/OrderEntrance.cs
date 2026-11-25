using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using UnityEngine;

namespace PotionShop
{
    internal sealed class OrderEntrance
    {
        private readonly RectTransform screen;
        private readonly TextMeshProUGUI banner;
        private static readonly Color BannerColor = new(1, .91f, .67f);

        internal OrderEntrance(Canvas canvas, TMP_FontAsset font, Art.UiTypography typography)
        {
            screen = (RectTransform)canvas.transform;
            var container = new GameObject("Customer Arrival", typeof(RectTransform));
            container.transform.SetParent(screen, false);
            var layer = container.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = SceneDimmer.SortingOrder + 1;
            banner = container.AddComponent<TextMeshProUGUI>();
            banner.font = font;
            banner.fontSize = 85;
            banner.fontStyle = FontStyles.Bold;
            banner.alignment = TextAlignmentOptions.Center;
            banner.color = BannerColor;
            banner.raycastTarget = false;
            banner.rectTransform.sizeDelta = new Vector2(620, 140);
            typography.Apply(banner);
            container.SetActive(false);
        }

        internal void ShowLoading()
        {
            banner.text = Loc.T("대사 준비 중…");
            banner.fontSize = 32;
            banner.rectTransform.anchoredPosition = Vector2.zero;
            banner.rectTransform.localRotation = Quaternion.identity;
            banner.gameObject.SetActive(true);
        }

        internal void Hide()
        {
            if (banner != null) banner.gameObject.SetActive(false);
        }

        internal async UniTask Play(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            banner.text = Loc.T("손님입장");
            banner.fontSize = 85;
            Pose(0);
            banner.gameObject.SetActive(true);
            try
            {
                await LMotion.Create(0f, .5f, .48f).WithEase(Ease.OutQuad)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).Bind(Pose).ToUniTask(token);
                await LMotion.Create(.5f, 1f, .46f).WithEase(Ease.InQuad)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).Bind(Pose).ToUniTask(token);
                // the door let the draught in: the flames gutter as the lights come back
                Light2DFlicker.Gust();
                GameSfx.Play("Whoosh", .22f, .6f, .05f);
            }
            finally { Hide(); }
        }

        private void Pose(float progress)
        {
            if (banner == null || screen == null) return;
            float time = progress * 2 - 1;
            var rect = banner.rectTransform;
            rect.anchoredPosition = new Vector2(screen.rect.width * .24f * time,
                -(screen.rect.height * .5f + 170) * time * time);
            rect.localRotation = Quaternion.Euler(0, 0, time * 68);
        }
    }
}
