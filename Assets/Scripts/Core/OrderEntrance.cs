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

        internal OrderEntrance(Canvas canvas, TMP_FontAsset font, Art.UiTypography typography)
        {
            screen = (RectTransform)canvas.transform;
            var container = new GameObject("Customer Arrival", typeof(RectTransform));
            container.transform.SetParent(screen, false);
            banner = container.AddComponent<TextMeshProUGUI>();
            banner.font = font;
            banner.text = "손님입장";
            banner.fontSize = 85;
            banner.fontStyle = FontStyles.Bold;
            banner.alignment = TextAlignmentOptions.Center;
            banner.color = new Color(1, .91f, .67f);
            banner.raycastTarget = false;
            banner.rectTransform.sizeDelta = new Vector2(620, 140);
            typography.Apply(banner);
            container.SetActive(false);
        }

        internal async UniTask Play(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            banner.transform.SetAsLastSibling();
            Pose(0);
            banner.gameObject.SetActive(true);
            try
            {
                await LMotion.Create(0f, 1f, .94f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(Pose).ToUniTask(token);
            }
            finally { if (banner != null) banner.gameObject.SetActive(false); }
        }

        private void Pose(float progress)
        {
            float time = progress * 2 - 1;
            float travel = .23f * time + .77f * time * time * time;
            var rect = banner.rectTransform;
            rect.anchoredPosition = new Vector2(screen.rect.width * .24f * travel,
                -(screen.rect.height * .5f + 170) * time * time);
            rect.localRotation = Quaternion.Euler(0, 0, travel * 68);
        }
    }
}
