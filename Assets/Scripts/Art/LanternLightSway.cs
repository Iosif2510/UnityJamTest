using LitMotion;
using UnityEngine;

namespace PotionShop
{
    [RequireComponent(typeof(Light)), DisallowMultipleComponent]
    public sealed class LanternLightSway : MonoBehaviour
    {
        private const float FullCycle = 2f * Mathf.PI;

        [SerializeField, Range(0f, 1f)] private float amplitude = 0.22f;
        [SerializeField, Min(0.1f)] private float period = 2.1f;

        private Light targetLight;
        private float baseIntensity;
        private MotionHandle motion;

        private void OnEnable()
        {
            targetLight = GetComponent<Light>();
            baseIntensity = targetLight.intensity;
            float startPhase = Random.Range(0f, FullCycle);
            float cycleDuration = period * Random.Range(0.8f, 1.2f);
            motion = LMotion.Create(startPhase, startPhase + FullCycle, cycleDuration)
                .WithLoops(-1)
                .Bind(ApplyIntensity);
        }

        private void ApplyIntensity(float phase)
        {
            float sway = 0.7f * Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 3f + 1f);
            targetLight.intensity = baseIntensity * (1f + amplitude * sway);
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (targetLight != null)
                targetLight.intensity = baseIntensity;
        }
    }
}
