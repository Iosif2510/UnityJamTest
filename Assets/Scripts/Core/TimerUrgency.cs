using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// Makes a countdown feel urgent near the end: under <see cref="WarnSeconds"/> the digits warm towards red, and
    /// under <see cref="PulseSeconds"/> they thump once a second with a soft heartbeat. Call <see cref="Show"/> every
    /// frame with the time left; works for any UI Graphic (TextMeshPro or legacy Text).
    /// </summary>
    internal sealed class TimerUrgency
    {
        public const float WarnSeconds = 30f;
        public const float PulseSeconds = 10f;
        private static readonly Color Hot = new(0.92f, 0.28f, 0.18f, 1f);

        private readonly Graphic digits;
        private readonly Color calm;
        private readonly Vector3 scale;
        private readonly bool heartbeat;
        private int lastPulse = -1;

        /// <param name="playHeartbeat">False where another sound already counts down (the storage room's tick).</param>
        public TimerUrgency(Graphic digits, bool playHeartbeat = true)
        {
            this.digits = digits;
            calm = digits.color;
            scale = digits.rectTransform.localScale;
            heartbeat = playHeartbeat;
        }

        public void Show(double remainingSeconds, bool running = true)
        {
            if (digits == null) return;
            float left = (float)remainingSeconds;
            if (!running || left > WarnSeconds || left <= 0f)
            {
                digits.color = calm;
                lastPulse = -1;
                return;
            }

            float heat = 1f - Mathf.Clamp01((left - PulseSeconds) / (WarnSeconds - PulseSeconds));
            digits.color = Color.Lerp(calm, Hot, Mathf.Lerp(0.35f, 1f, heat));

            int second = Mathf.CeilToInt(left);
            if (left > PulseSeconds || second == lastPulse) return;
            lastPulse = second;
            var rect = digits.rectTransform;
            var baseScale = scale;
            LMotion.Create(1.25f, 1f, 0.35f).WithEase(Ease.OutBack).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(s => { if (rect != null) rect.localScale = baseScale * s; }).AddTo(digits.gameObject);
            if (heartbeat)
                GameSfx.Play("Heartbeat", Mathf.Lerp(0.35f, 0.7f, 1f - left / PulseSeconds));
        }
    }
}
