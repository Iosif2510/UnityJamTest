using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class CounterCurtain : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer openCurtain;
        [SerializeField] private SpriteRenderer closedCurtain;
        [SerializeField, Min(0f)] private float openDuration = 1f;
        [SerializeField, Min(0f)] private float closeDuration = 1f;

        private MotionHandle fade;
        private float closedAmount = 1f;

        public void SetClosed(bool closed)
        {
            fade.TryCancel();
            ApplyBlend(closed ? 1f : 0f);
        }

        public UniTask FadeTo(bool closed, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            fade.TryCancel();
            float target = closed ? 1f : 0f;
            float duration = closed ? closeDuration : openDuration;
            if (closedAmount == target || duration <= 0f)
            {
                ApplyBlend(target);
                return UniTask.CompletedTask;
            }

            fade = LMotion.Create(closedAmount, target, duration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithEase(Ease.InOutSine)
                .Bind(ApplyBlend);
            return fade.ToUniTask(token);
        }

        private void ApplyBlend(float amount)
        {
            closedAmount = amount;
            SetOpacity(openCurtain, 1f - amount);
            SetOpacity(closedCurtain, amount);
        }

        private static void SetOpacity(SpriteRenderer curtain, float opacity)
        {
            var color = curtain.color;
            color.a = opacity;
            curtain.color = color;
            curtain.enabled = opacity > 0f;
        }

        private void OnDisable() => fade.TryCancel();
    }
}
