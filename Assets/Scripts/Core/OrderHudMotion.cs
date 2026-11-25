using LitMotion;
using UnityEngine;

namespace PotionShop
{
    internal sealed class OrderHudMotion
    {
        private readonly UnityEngine.UI.Text timer;
        private readonly UnityEngine.UI.Text label;
        private readonly UnityEngine.UI.Graphic background;
        private readonly Color labelColor;
        private readonly Color backgroundColor;
        private MotionHandle timerMotion;
        private MotionHandle buttonMotion;

        internal OrderHudMotion(UnityEngine.UI.Text timer, UnityEngine.UI.Button button)
        {
            this.timer = timer;
            label = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            background = button.targetGraphic;
            labelColor = label.color;
            backgroundColor = background.color;
            var rect = timer.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -43);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x * 2, Mathf.Max(63, rect.sizeDelta.y));
            timer.fontSize *= 2;
            timer.alignment = TextAnchor.MiddleCenter;
        }

        internal void SetTimer(bool visible)
        {
            timerMotion.TryCancel();
            if (timer == null) return;
            var rect = timer.rectTransform;
            if (rect == null) return;
            rect.localRotation = Quaternion.identity;
            timer.gameObject.SetActive(visible);
            if (!visible) return;
            timerMotion = SwayTimer(rect);
        }

        private static readonly System.Collections.Generic.Dictionary<Transform, (Vector3 rest, MotionHandle motion)> bounces = new();

        /// <summary>A quick squash-and-settle on a pressed choice; repeated presses restart from its rest size.</summary>
        internal static void Bounce(Transform target, float peak = 1.12f)
        {
            if (target == null) return;
            Vector3 rest = target.localScale;
            if (bounces.TryGetValue(target, out var running))
            {
                running.motion.TryCancel();
                rest = running.rest;
            }
            var handle = LMotion.Create(peak, 1f, 0.3f).WithEase(Ease.OutElastic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => bounces.Remove(target))
                .Bind(s => { if (target != null) target.localScale = rest * s; });
            bounces[target] = (rest, handle);
        }

        internal static MotionHandle SwayTimer(RectTransform rect) => LMotion.Create(0f, 2 * Mathf.PI, 2f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).WithLoops(-1)
            .Bind(phase =>
            {
                if (rect != null) rect.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * 2);
            });

        internal void HighlightWorkshop(bool highlighted)
        {
            buttonMotion.TryCancel();
            if (label != null) label.color = highlighted ? new Color(.20f, .105f, .055f, labelColor.a) : labelColor;
            if (background == null) return;
            background.color = backgroundColor;
            if (!highlighted) return;
            // Existing art may already have a white tint, so pulse between warm and full brightness.
            var low = backgroundColor * new Color(.79f, .69f, .53f, 1);
            buttonMotion = LMotion.Create(0f, 2 * Mathf.PI, 1.65f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).WithLoops(-1)
                .Bind(phase =>
                {
                    if (background != null)
                        background.color = Color.Lerp(low, backgroundColor, .5f + .5f * Mathf.Cos(phase));
                });
        }

        /// <summary>Greyed out but still pressable: the button only explains why it cannot go yet.</summary>
        internal void LockWorkshop()
        {
            buttonMotion.TryCancel();
            if (label != null) label.color = new Color(labelColor.r, labelColor.g, labelColor.b, labelColor.a * 0.6f);
            if (background != null) background.color = backgroundColor * new Color(.62f, .6f, .58f, 1f);
        }

        internal void Stop()
        {
            SetTimer(false);
            HighlightWorkshop(false);
        }
    }
}
