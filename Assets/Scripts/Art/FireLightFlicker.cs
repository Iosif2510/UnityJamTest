using LitMotion;
using UnityEngine;

namespace PotionShop
{
    [RequireComponent(typeof(Light)), DisallowMultipleComponent]
    public sealed class FireLightFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float amplitude = 0.28f;
        [SerializeField, Min(0.02f)] private float minimumPause = 0.06f;
        [SerializeField, Min(0.02f)] private float maximumPause = 0.24f;

        private Light targetLight;
        private float baseIntensity;
        private MotionHandle motion;

        private void OnEnable()
        {
            targetLight = GetComponent<Light>();
            baseIntensity = targetLight.intensity;
            ScheduleFlicker();
        }

        private void ScheduleFlicker()
        {
            float pause = Random.Range(minimumPause, Mathf.Max(minimumPause, maximumPause));
            float variation = Random.value;
            float change = amplitude * Mathf.Lerp(0.25f, 1f, variation * variation);
            if (Random.value < 0.4f)
                change = -change;

            float pulseIntensity = baseIntensity * (1f + change);
            float settleIntensity = baseIntensity * (1f + change * 0.2f);
            float settleDuration = Random.Range(0.06f, 0.16f);

            motion = LMotion.Create(pulseIntensity, settleIntensity, settleDuration)
                .WithDelay(pause)
                .WithImmediateBind(false)
                .WithEase(Ease.OutQuad)
                .WithOnComplete(ScheduleFlicker)
                .Bind(targetLight, static (intensity, light) => light.intensity = intensity);
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (targetLight != null)
                targetLight.intensity = baseIntensity;
        }
    }
}
