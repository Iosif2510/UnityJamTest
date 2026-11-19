using LitMotion;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PotionShop
{
    [RequireComponent(typeof(Light2D)), DisallowMultipleComponent]
    public sealed class Light2DIntensitySway : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float amplitude = 0.08f;
        [SerializeField, Min(0.1f)] private float period = 6f;

        private Light2D targetLight;
        private float baseIntensity;
        private MotionHandle motion;

        private void OnEnable()
        {
            targetLight = GetComponent<Light2D>();
            baseIntensity = targetLight.intensity;
            float variation = targetLight.lightType == Light2D.LightType.Global ? Mathf.Min(amplitude, .10f) : amplitude;
            motion = LMotion.Create(0f, 2f * Mathf.PI, period)
                .WithLoops(-1)
                .Bind(phase => targetLight.intensity = baseIntensity * (1f + variation * Mathf.Sin(phase)));
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (targetLight != null)
                targetLight.intensity = baseIntensity;
        }
    }
}
