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
        private float flicker;
        private float flareStrength;
        private float flareSince = -10f;
        private float flareSeconds = 1f;

        /// <summary>The fire flares up by <paramref name="strength"/> (0.5 = half again as bright) and dies back.</summary>
        public void Flare(float strength, float seconds = 0.8f)
        {
            flareStrength = Mathf.Max(strength, CurrentFlare);
            flareSince = Time.unscaledTime;
            flareSeconds = Mathf.Max(0.05f, seconds);
        }

        private float CurrentFlare
        {
            get
            {
                float age = Time.unscaledTime - flareSince;
                if (age < 0f || age > flareSeconds) return 0f;
                // a quick swell, then a slow fall
                float rise = Mathf.Clamp01(age / 0.08f);
                return flareStrength * rise * (1f - Mathf.SmoothStep(0f, 1f, age / flareSeconds));
            }
        }

        private void Update()
        {
            if (targetLight != null) targetLight.intensity = flicker * (1f + CurrentFlare);
        }

        private void OnEnable()
        {
            targetLight = GetComponent<Light>();
            baseIntensity = targetLight.intensity;
            flicker = baseIntensity;
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
                .Bind(this, static (intensity, self) => self.flicker = intensity);
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (targetLight != null)
                targetLight.intensity = baseIntensity;
        }
    }
}
