using LitMotion;
using UnityEngine;

namespace PotionShop
{
    [RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public sealed class UiVerticalFloat : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float amplitude = 6f;
        [SerializeField, Min(0.1f)] private float period = 2.8f;

        private RectTransform rect;
        private float restingY;
        private MotionHandle motion;

        private void OnEnable()
        {
            rect = (RectTransform)transform;
            restingY = rect.anchoredPosition.y;
            motion = LMotion.Create(0f, 2f * Mathf.PI, period)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithLoops(-1)
                .Bind(phase => SetY(restingY + amplitude * Mathf.Sin(phase)));
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (rect != null) SetY(restingY);
        }

        private void SetY(float y)
        {
            var position = rect.anchoredPosition;
            position.y = y;
            rect.anchoredPosition = position;
        }
    }
}
