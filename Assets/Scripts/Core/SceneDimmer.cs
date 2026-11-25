using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionShop
{
    internal sealed class SceneDimmer : MonoBehaviour
    {
        internal const int SortingOrder = 32760;
        private static SceneDimmer instance;
        private UnityEngine.UI.Image overlay;
        private bool busy;

        internal static SceneDimmer Instance
        {
            get
            {
                if (instance != null) return instance;
                var root = new GameObject("Scene Dimmer", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                DontDestroyOnLoad(root);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortingOrder;
                instance = root.AddComponent<SceneDimmer>();
                var image = new GameObject("Dim", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                instance.overlay = image.GetComponent<UnityEngine.UI.Image>();
                var rect = instance.overlay.rectTransform;
                rect.SetParent(root.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                instance.SetOpacity(0);
                return instance;
            }
        }

        internal bool TryLoad(string destination, Func<string> resolveDestination = null)
        {
            if (busy || !Application.CanStreamedLevelBeLoaded(destination)) return false;
            busy = true;
            Load(resolveDestination ?? (() => destination), this.GetCancellationTokenOnDestroy()).Forget();
            return true;
        }

        internal UniTask Cut(Action change, CancellationToken token) => WhileDimmed(.87f, () =>
        {
            change();
            return UniTask.CompletedTask;
        }, token);

        internal UniTask DuringEntrance(Func<UniTask> play, CancellationToken token) => WhileDimmed(.96f, play, token);

        private async UniTask WhileDimmed(float opacity, Func<UniTask> play, CancellationToken token)
        {
            while (busy) await UniTask.Yield(PlayerLoopTiming.Update, token);
            token.ThrowIfCancellationRequested();
            busy = true;
            try
            {
                await Fade(0, opacity, .12f, token);
                token.ThrowIfCancellationRequested();
                await play();
                token.ThrowIfCancellationRequested();
                await Fade(opacity, 0, .18f, token);
            }
            finally { Release(); }
        }

        private async UniTask Load(Func<string> destination, CancellationToken token)
        {
            try
            {
                Footsteps(token).Forget();
                await Fade(0, 1, .13f, token);
                while (true)
                {
                    string next = destination();
                    if (SceneManager.GetActiveScene().name != next)
                    {
                        await SceneManager.LoadSceneAsync(next, LoadSceneMode.Single).ToUniTask(cancellationToken: token);
                        await UniTask.NextFrame(token);
                        continue;
                    }

                    await LMotion.Create(1f, 0f, .20f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                        .WithEase(Ease.InOutSine).Bind(alpha => SetOpacity(destination() == next ? alpha : 1f))
                        .ToUniTask(token);
                    if (destination() == next) break;
                }
            }
            finally { Release(); }
        }

        /// <summary>A few steps on the stairs while the screen is dark, so changing rooms sounds like walking there.</summary>
        private static async UniTaskVoid Footsteps(CancellationToken token)
        {
            int first = UnityEngine.Random.Range(1, 5);
            for (int i = 0; i < 4; i++)
            {
                GameSfx.Play("Step" + ((first + i) % 4 + 1), 0.55f - i * 0.08f, 1f, 0.06f);
                if (await UniTask.Delay(TimeSpan.FromSeconds(0.17), ignoreTimeScale: true, cancellationToken: token)
                        .SuppressCancellationThrow())
                    return;
            }
        }

        private UniTask Fade(float start, float end, float duration, CancellationToken token) =>
            LMotion.Create(start, end, duration).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithEase(Ease.InOutSine).Bind(SetOpacity).ToUniTask(token);

        private void SetOpacity(float value)
        {
            overlay.enabled = value > 0;
            overlay.color = new Color(.04f, .018f, .01f, value);
        }

        private void Release()
        {
            if (overlay != null) SetOpacity(0);
            busy = false;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
