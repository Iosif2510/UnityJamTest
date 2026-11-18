using LitMotion;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PotionShop
{
    [RequireComponent(typeof(Light2D)), DisallowMultipleComponent]
    public sealed class Light2DFlicker : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float minimumInterval = 2.5f;
        [SerializeField, Min(0.1f)] private float maximumInterval = 6f;
        [SerializeField, Min(0.01f)] private float flickerDuration = 0.28f;
        [SerializeField, Range(0f, 1f)] private float dimming = 0.38f;

        private static readonly AnimationCurve FlickerProfile = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.45f, 0.12f),
            new Keyframe(0.65f, 0.55f),
            new Keyframe(1f, 0f));

        private Light2D targetLight;
        private float baseIntensity;
        private MotionHandle motion;

        private void OnEnable()
        {
            targetLight = GetComponent<Light2D>();
            baseIntensity = targetLight.intensity;
            ScheduleFlicker();
        }

        private void ScheduleFlicker()
        {
            float delay = Random.Range(minimumInterval, Mathf.Max(minimumInterval, maximumInterval));
            motion = LMotion.Create(0f, 1f, flickerDuration)
                .WithDelay(delay)
                .WithOnComplete(ScheduleFlicker)
                .Bind(progress => targetLight.intensity = baseIntensity * (1f - dimming * FlickerProfile.Evaluate(progress)));
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (targetLight != null)
                targetLight.intensity = baseIntensity;
        }
    }
}
