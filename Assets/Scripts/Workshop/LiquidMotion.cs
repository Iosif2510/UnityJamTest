using LitMotion;
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
        private static readonly int Amount = Shader.PropertyToID("_Amount");
        private Renderer liquidRenderer;
        private MaterialPropertyBlock properties;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector3 previousVelocity;
        private Vector2 tilt;
        private Vector2 tiltVelocity;
        private MotionHandle sloshMotion;
        private MotionHandle fillMotion;
        private MotionHandle impactMotion;
        private float sloshTilt;
        private float impactTilt;
        private float initialFill;
        private float fullFill;
        private float fill;
        private bool controlsFill;
        private double previousTime;

        internal Vector3 SurfaceCenter(float filledFraction)
        {
            var bounds = liquidRenderer.bounds;
            float level = Mathf.Lerp(initialFill, fullFill, filledFraction);
            float surface = controlsFill ? transform.position.y + (level - .5f) * HeightScale : bounds.center.y;
            return new Vector3(bounds.center.x, Mathf.Clamp(surface, bounds.min.y, bounds.max.y), bounds.center.z);
        }

        internal Renderer LiquidRenderer => liquidRenderer != null ? liquidRenderer : liquidRenderer = GetComponent<Renderer>();

        private float HeightScale => Mathf.Max(.0001f, transform.TransformVector(Vector3.up).magnitude);

        internal void PrepareFill()
        {
            if (liquidRenderer == null || liquidRenderer.sharedMaterial == null ||
                !liquidRenderer.sharedMaterial.HasProperty(Amount)) return;
            var bounds = liquidRenderer.bounds;
            float bottom = (bounds.min.y - transform.position.y) / HeightScale + .5f;
            float top = (bounds.max.y - transform.position.y) / HeightScale + .5f;
            fullFill = Mathf.Clamp(liquidRenderer.sharedMaterial.GetFloat(Amount),
                Mathf.Lerp(bottom, top, .55f), Mathf.Lerp(bottom, top, .86f));
            initialFill = Mathf.Max(Mathf.Lerp(bottom, top, .12f), fullFill - (top - bottom) * .48f);
            controlsFill = true;
            ResetFill();
        }

        internal void AddIngredient(int landed, int count)
        {
            fillMotion.TryCancel();
            impactMotion.TryCancel();
            if (controlsFill)
            {
                float target = Mathf.Lerp(initialFill, fullFill, landed / (float)count);
                fillMotion = LMotion.Create(fill, target, .15f).WithEase(Ease.OutQuad)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).Bind(value =>
                    {
                        fill = value;
                        ApplyTilt();
                    });
            }
            impactMotion = LMotion.Create(0f, 1f, .24f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t => impactTilt = Mathf.Sin(t * 2f * Mathf.PI) * (1f - t) * maximumTilt * .7f);
        }

        internal void ResetFill()
        {
            fillMotion.TryCancel();
            impactMotion.TryCancel();
            impactTilt = 0;
            fill = initialFill;
            ApplyTilt();
        }

        public void StartSloshing()
        {
            if (!isActiveAndEnabled)
                return;

            sloshMotion.TryCancel();
            sloshMotion = LMotion.Create(0f, 1f, 0.8f)
                .WithLoops(-1)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(UpdateSlosh);
        }

        public void StopSloshing()
        {
            sloshMotion.TryCancel();
            sloshTilt = 0f;
            ApplyTilt();
        }

        private void UpdateSlosh(float progress)
        {
            sloshTilt = Mathf.Sin(progress * 2f * Mathf.PI) * (1f - progress) * maximumTilt;
            ApplyTilt();
        }

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
            Vector2 combinedTilt = Vector2.ClampMagnitude(tilt + new Vector2(sloshTilt, impactTilt), maximumTilt);
            properties.SetFloat(WobbleX, combinedTilt.x);
            properties.SetFloat(WobbleZ, combinedTilt.y);
            if (controlsFill) properties.SetFloat(Amount, fill);
            liquidRenderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            tilt = Vector2.zero;
            ResetFill();
            StopSloshing();
        }
    }
}
