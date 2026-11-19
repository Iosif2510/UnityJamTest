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
        [SerializeField, Range(0f, .5f)] private float flameVariation = .12f;
        [SerializeField, Min(.1f)] private float flameSpeed = 6f;

        private static readonly AnimationCurve FlickerProfile = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.45f, 0.12f),
            new Keyframe(0.65f, 0.55f),
            new Keyframe(1f, 0f));

        private Light2D targetLight;
        private float baseIntensity;
        private float noiseSeed;
        private float dip;
        private MotionHandle motion;

        private void OnEnable()
        {
            targetLight = GetComponent<Light2D>();
            baseIntensity = targetLight.intensity;
            noiseSeed = Random.Range(1f, 1000f);
            dip = 0;
            ScheduleFlicker();
        }

        private void Update()
        {
            float time = (float)Time.unscaledTimeAsDouble * flameSpeed;
            float slow = Mathf.PerlinNoise(noiseSeed, time * .34f) * 2 - 1;
            float fast = Mathf.PerlinNoise(noiseSeed + 21f, time) * 2 - 1;
            targetLight.intensity = baseIntensity * Mathf.Max(0, 1 + flameVariation * (slow * .62f + fast * .38f) - dip);
        }

        private void ScheduleFlicker()
        {
            float delay = Random.Range(minimumInterval, Mathf.Max(minimumInterval, maximumInterval));
            motion = LMotion.Create(0f, 1f, flickerDuration)
                .WithDelay(delay)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(ScheduleFlicker)
                .Bind(progress => dip = dimming * FlickerProfile.Evaluate(progress));
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (targetLight != null)
                targetLight.intensity = baseIntensity;
        }
    }
}
