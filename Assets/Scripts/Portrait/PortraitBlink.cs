using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PotionShop.Portraits
{
    public sealed class PortraitBlink : MonoBehaviour
    {
        [SerializeField] private PortraitLayers layers;
        [SerializeField] private Vector2 interval = new(3, 5);
        [SerializeField, Min(0.01f)] private float closedSeconds = 0.1f;
        private CancellationTokenSource cancellation;
        private readonly System.Random random = new();
        public void Play()
        {
            Stop();
            cancellation = new CancellationTokenSource();
            Run(cancellation.Token).Forget();
        }
        public void Stop()
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            SetClosed(false);
        }
        public void SetClosed(bool closed)
        { if (layers != null && layers.eyeClosed != null) layers.eyeClosed.enabled = closed && layers.eyeClosed.sprite != null; }
        public void BlinkOnce() { Stop(); cancellation = new CancellationTokenSource(); Once(cancellation.Token).Forget(); }
        private async UniTask Once(CancellationToken token)
        {
            try { SetClosed(true); await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.01f, closedSeconds)), true, cancellationToken: token); SetClosed(false); }
            catch (OperationCanceledException) { }
        }
        private async UniTask Run(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var min = Mathf.Max(0.01f, interval.x); var max = Mathf.Max(min, interval.y);
                    await UniTask.Delay(TimeSpan.FromSeconds(min + random.NextDouble() * (max - min)), true, cancellationToken: token);
                    await Once(token);
                }
            }
            catch (OperationCanceledException) { }
        }
        private void OnDisable() => Stop();
    }
}
