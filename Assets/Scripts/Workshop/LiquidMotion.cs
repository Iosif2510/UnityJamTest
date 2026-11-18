using UnityEngine;

namespace PotionShop
{
    /// <summary>Drives each renderer's liquid surface without instantiating its material.</summary>
    [ExecuteAlways, RequireComponent(typeof(Renderer)), DisallowMultipleComponent]
    public sealed class LiquidMotion : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementResponse = 0.65f;
        [SerializeField, Min(0f)] private float rotationResponse = 3f;
        [SerializeField, Range(0f, 0.5f)] private float maximumTilt = 0.18f;
        [SerializeField, Min(0.1f)] private float frequency = 2.2f;
        [SerializeField, Min(0f)] private float damping = 3.5f;
        [Tooltip("Movement beyond this distance in one frame resets the liquid instead of shaking it.")]
        [SerializeField, Min(0.01f)] private float teleportDistance = 2f;

        private static readonly int WobbleX = Shader.PropertyToID("_WobbleX");
        private static readonly int WobbleZ = Shader.PropertyToID("_WobbleZ");
        private Renderer liquidRenderer;
        private MaterialPropertyBlock properties;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector3 previousVelocity;
        private Vector2 tilt;
        private Vector2 tiltVelocity;
        private double previousTime;

        private void OnEnable()
        {
            liquidRenderer = GetComponent<Renderer>();
            properties ??= new MaterialPropertyBlock();
            ResetMotion();
        }

        private void ResetMotion()
        {
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            previousVelocity = Vector3.zero;
            tilt = tiltVelocity = Vector2.zero;
            previousTime = Time.realtimeSinceStartupAsDouble;
            ApplyTilt();
        }

        private void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = Application.isPlaying ? Time.deltaTime : (float)(now - previousTime);
            previousTime = now;
            if (dt <= 0f)
                return;

            Vector3 displacement = transform.position - previousPosition;
            if (dt > 0.2f || displacement.sqrMagnitude > teleportDistance * teleportDistance)
            {
                ResetMotion();
                return;
            }

            Vector3 velocity = displacement / dt;
            Vector3 velocityChange = velocity - previousVelocity;
            Quaternion rotationDelta = transform.rotation * Quaternion.Inverse(previousRotation);
            rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 rotationImpulse = angle == 0f ? Vector3.zero : axis * (angle * Mathf.Deg2Rad);
            tiltVelocity += new Vector2(-velocityChange.x, -velocityChange.z) * movementResponse
                + new Vector2(-rotationImpulse.z, rotationImpulse.x) * rotationResponse;

            // Small integration steps keep the damped spring stable at low frame rates.
            int steps = Mathf.CeilToInt(dt / (1f / 120f));
            float step = dt / steps;
            float omega = 2f * Mathf.PI * frequency;
            for (int i = 0; i < steps; i++)
            {
                tiltVelocity += (-omega * omega * tilt - 2f * damping * tiltVelocity) * step;
                tilt += tiltVelocity * step;
                if (tilt.sqrMagnitude > maximumTilt * maximumTilt)
                {
                    tilt = Vector2.ClampMagnitude(tilt, maximumTilt);
                    float outwardSpeed = Vector2.Dot(tiltVelocity, tilt.normalized);
                    if (outwardSpeed > 0f) tiltVelocity -= tilt.normalized * outwardSpeed;
                }
            }

            previousPosition = transform.position;
            previousRotation = transform.rotation;
            previousVelocity = velocity;
            ApplyTilt();
        }

        private void ApplyTilt()
        {
            if (liquidRenderer == null) return;
            liquidRenderer.GetPropertyBlock(properties);
            properties.SetFloat(WobbleX, tilt.x);
            properties.SetFloat(WobbleZ, tilt.y);
            liquidRenderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            tilt = Vector2.zero;
            ApplyTilt();
        }
    }
}
