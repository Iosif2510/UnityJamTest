using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The moment the customer's time runs out, before their parting line: a heavy thud, the screen's edges flush
    /// red once like a held breath let go, and the customer huffs "흥!" with a cross face and a deflating "bwomp".
    /// </summary>
    internal static class TimeoutMoment
    {
        private const float Seconds = 1.1f;
        private static Sprite edgeSprite;

        public static async UniTask Play(CustomerImpatience customer, Data.GeneratedOrder order,
            CancellationToken token)
        {
            GameSfx.Play("StampThud", 0.7f, 0.7f);
            GameSfx.Play("Heartbeat", 0.8f, 0.8f);
            customer?.Huff(order);

            // its own full-screen overlay, so it frames the whole screen whatever canvas the scene uses
            var flash = new GameObject("TimeoutFlash", typeof(RectTransform), typeof(Canvas));
            var overlay = flash.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 60;
            {
                var rect = (RectTransform)new GameObject("Edges", typeof(RectTransform), typeof(Image)).transform;
                rect.SetParent(flash.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = rect.GetComponent<Image>();
                image.sprite = EdgeSprite;
                image.raycastTarget = false;
                image.color = Color.clear;
                _ = LMotion.Create(0f, 1f, Seconds).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        // in fast, out slow
                        float a = t < 0.12f ? t / 0.12f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.12f) / 0.88f);
                        image.color = new Color(0.75f, 0.08f, 0.05f, 0.55f * a);
                    }).AddTo(flash);
            }
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.35f), ignoreTimeScale: true, cancellationToken: token);
                GameSfx.Play("Flop", 0.6f);
                await UniTask.Delay(TimeSpan.FromSeconds(Seconds - 0.35f), ignoreTimeScale: true, cancellationToken: token);
            }
            finally
            {
                if (flash != null) UnityEngine.Object.Destroy(flash);
            }
        }

        /// <summary>Clear in the middle, solid towards the screen's edges.</summary>
        private static Sprite EdgeSprite
        {
            get
            {
                if (edgeSprite != null) return edgeSprite;
                const int w = 128, h = 72;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                        float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d));
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return edgeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
