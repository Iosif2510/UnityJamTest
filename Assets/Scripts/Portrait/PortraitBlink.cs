using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PotionShop.Portraits
{
    public sealed class PortraitBlink : MonoBehaviour
    {
        [SerializeField] private PortraitLayers layers;
        [SerializeField, Tooltip("Seconds between the starts of consecutive automatic blinks.")]
        private Vector2 interval = new(2f, 2.5f);
        [SerializeField, Min(0.01f)] private float closedSeconds = 0.1f;
        private CancellationTokenSource cancellation;
        private readonly System.Random random = new();
        private bool autoBlink;

        public void Play()
        {
            Stop();
            autoBlink = true;
            if (isActiveAndEnabled) StartAutoBlink();
        }

        private void StartAutoBlink()
        {
            cancellation = new CancellationTokenSource();
            Run(cancellation.Token).Forget();
        }

        public void Stop()
        {
            autoBlink = false;
            CancelBlink();
        }

        private void CancelBlink()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            SetClosed(false);
        }

        public void SetClosed(bool closed)
        { if (layers != null && layers.eyeClosed != null) layers.eyeClosed.enabled = closed && layers.eyeClosed.sprite != null; }

        public void BlinkOnce()
        {
            Stop();
            if (!isActiveAndEnabled) return;
            cancellation = new CancellationTokenSource();
            Once(cancellation.Token).Forget();
        }

        private async UniTask Once(CancellationToken token)
        {
            SetClosed(true);
            if (!await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.01f, closedSeconds)),
                true, cancellationToken: token).SuppressCancellationThrow())
                SetClosed(false);
        }

        private async UniTask Run(CancellationToken token)
        {
            double lastBlinkStart = Time.unscaledTimeAsDouble;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    float min = Mathf.Max(0.01f, interval.x);
                    float max = Mathf.Max(min, interval.y);
                    double nextBlink = lastBlinkStart + min + random.NextDouble() * (max - min);
                    double delay = Math.Max(0d, nextBlink - Time.unscaledTimeAsDouble);
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), true, cancellationToken: token);
                    lastBlinkStart = Time.unscaledTimeAsDouble;
                    await Once(token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void OnEnable()
        {
            if (autoBlink) StartAutoBlink();
        }

        private void OnDisable() => CancelBlink();
    }
}
