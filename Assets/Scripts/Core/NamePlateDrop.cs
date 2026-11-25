using LitMotion;
using TMPro;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The speaker's name tag on the dialogue paper: whenever it shows a new name (a new customer at the counter)
    /// it drops in from above, lands with a soft slap and swings to rest from its top corner, like a tag hung on a nail.
    /// The same name coming back (the reaction lines) leaves it be.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NamePlateDrop : MonoBehaviour
    {
        private RectTransform rect;
        private TMP_Text label;
        private Vector2 rest;
        private string shownName;
        private MotionHandle motion;

        private void Awake()
        {
            rect = (RectTransform)transform;
            rest = rect.anchoredPosition;
            label = GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>Called as the paper comes up: drops in if the name is new since the last time.</summary>
        internal void Present()
        {
            if (label == null || label.text == shownName) return;
            shownName = label.text;
            Drop();
        }

        private void Drop()
        {
            motion.TryCancel();
            const float height = 70f;
            motion = LMotion.Create(0f, 1f, 0.75f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(Settle)
                .Bind(t =>
                {
                    // falls for the first quarter, then swings on its corner and settles
                    float fall = Mathf.Clamp01(t / 0.24f);
                    rect.anchoredPosition = rest + Vector2.up * height * (1f - fall * fall);
                    float swing = t < 0.24f ? -10f * (1f - fall)
                        : 7f * Mathf.Sin((t - 0.24f) / 0.76f * Mathf.PI * 2.5f) * Mathf.Pow(Mathf.Clamp01(1f - (t - 0.24f) / 0.76f), 1.6f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, swing);
                }).AddTo(gameObject);
            LMotion.Create(0f, 1f, 0.18f).WithDelay(0.18f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => GameSfx.Play("CardDeal", 0.5f, 1.1f, 0.05f)).Bind(_ => { }).AddTo(gameObject);
        }

        private void Settle()
        {
            rect.anchoredPosition = rest;
            rect.localRotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (rect != null) Settle();
        }
    }
}
