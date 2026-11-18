using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PotionShop
{
    /// <summary>
    /// First-person movement and look controller.
    /// Yaw rotates the player body, pitch rotates the camera pivot.
    /// A CinemachineCamera hard-locks position and rotation to the camera pivot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [Tooltip("Unlocks the cursor (e.g. Escape).")]
        [SerializeField] private InputActionReference unlockCursorAction;
        [Tooltip("Re-locks the cursor when clicking in the game view (ignored while over UI).")]
        [SerializeField] private InputActionReference lockCursorAction;

        [Header("References")]
        [Tooltip("Pivot followed by the CinemachineCamera. Pitch is applied here.")]
        [SerializeField] private Transform cameraTarget;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 6f;
        [Tooltip("How quickly horizontal velocity reaches the target speed (units/s²).")]
        [SerializeField, Min(0f)] private float acceleration = 30f;
        [Tooltip("Fraction of acceleration available while airborne.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.3f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -20f;
        [Tooltip("Small downward velocity that keeps the controller snapped to the ground.")]
        [SerializeField] private float groundedStickVelocity = -2f;
        [SerializeField, Min(0f)] private float terminalFallSpeed = 50f;

        [Header("Look")]
        [Tooltip("Degrees per pixel of mouse/pointer delta.")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full gamepad stick deflection.")]
        [SerializeField, Min(0f)] private float gamepadSensitivity = 180f;
        [SerializeField] private bool invertY;
        [SerializeField, Range(0f, 90f)] private float maxPitch = 85f;

        [Header("Arm Jiggle")]
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [Tooltip("Local side, vertical and forward movement in metres at full movement speed.")]
        [SerializeField] private Vector3 armPositionAmplitude = new Vector3(0.008f, 0.012f, 0.018f);
        [Tooltip("Local pitch, yaw and roll wobble in degrees.")]
        [SerializeField] private Vector3 armRotationAmplitude = new Vector3(2f, 1f, 2f);
        [Tooltip("Full arm-swing cycles per second at full movement speed.")]
        [SerializeField, Min(0f)] private float armJiggleFrequency = 1.8f;
        [Tooltip("How quickly the arms blend into motion and return to their rest pose.")]
        [SerializeField, Min(0f)] private float armJiggleSmoothing = 12f;

        [Header("Held Item Wiggle")]
        [SerializeField] private Transform heldItemAnchor;
        [Tooltip("Maximum vertical offset in metres at full movement speed.")]
        [SerializeField, Min(0f)] private float heldItemWiggleAmplitude = 0.015f;
        [Tooltip("Vertical bob cycles per second at full movement speed, independent of arm timing.")]
        [SerializeField, Min(0f)] private float heldItemWiggleFrequency = 2.5f;
        [SerializeField, Min(0f)] private float heldItemWiggleSmoothing = 12f;

        private Vector3 heldItemRestPosition;
        private float heldItemWigglePhase;
        private float heldItemWiggleWeight;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnEnable = true;

        private Vector3 leftArmRestPosition;
        private Vector3 rightArmRestPosition;
        private Quaternion leftArmRestRotation;
        private Quaternion rightArmRestRotation;
        private float armJigglePhase;
        private float armJiggleWeight;

        private CharacterController characterController;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float pitch;

        public bool IsGrounded => characterController.isGrounded;
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>When false, movement and look input are ignored (e.g. while a UI panel is open).</summary>
        public bool InputEnabled { get; set; } = true;
        internal Func<bool> TryHandleLookInput { get; set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (cameraTarget == null)
                Debug.LogError("FirstPersonController: Camera Target is not assigned.", this);
            else
                pitch = NormalizeAngle(cameraTarget.localEulerAngles.x);

            if (leftArm != null)
            {
                leftArmRestPosition = leftArm.localPosition;
                leftArmRestRotation = leftArm.localRotation;
            }
            if (rightArm != null)
            {
                rightArmRestPosition = rightArm.localPosition;
                rightArmRestRotation = rightArm.localRotation;
            }
            if (heldItemAnchor != null)
                heldItemRestPosition = heldItemAnchor.localPosition;
        }

        private void OnEnable()
        {
            EnableAction(moveAction);
            EnableAction(lookAction);
            EnableAction(unlockCursorAction);
            EnableAction(lockCursorAction);

            if (lockCursorOnEnable)
                SetCursorLocked(true);
        }

        private void OnDisable()
        {
            ResetArmJiggle();
            ResetHeldItemWiggle();
            SetCursorLocked(false);
        }

        private void Update()
        {
            HandleCursor();
            HandleLook();
            HandleMovement();
        }

        private void LateUpdate()
        {
            if (leftArm == null && rightArm == null && heldItemAnchor == null)
                return;

            // Use actual displacement so pushing into a wall does not keep the arms swinging.
            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            float speedRatio = InputEnabled && characterController.isGrounded
                ? Mathf.Clamp01(velocity.magnitude / Mathf.Max(walkSpeed, 0.001f)) : 0f;
            UpdateHeldItemWiggle(speedRatio);
            float blend = 1f - Mathf.Exp(-armJiggleSmoothing * Time.deltaTime);
            armJiggleWeight = Mathf.Lerp(armJiggleWeight, speedRatio, blend);
            armJigglePhase = Mathf.Repeat(armJigglePhase + speedRatio * armJiggleFrequency
                * Mathf.PI * 2f * Time.deltaTime, Mathf.PI * 2f);

            if (speedRatio == 0f && armJiggleWeight < 0.001f)
            {
                ResetArmJiggle();
                return;
            }

            float swing = Mathf.Sin(armJigglePhase);
            float bounce = Mathf.Sin(armJigglePhase * 2f);
            float jiggle = Mathf.Sin(armJigglePhase * 4f) * 0.15f;
            ApplyArmJiggle(leftArm, leftArmRestPosition, leftArmRestRotation, swing, bounce, jiggle);
            ApplyArmJiggle(rightArm, rightArmRestPosition, rightArmRestRotation, -swing, bounce, -jiggle);
        }

        private void UpdateHeldItemWiggle(float speedRatio)
        {
            if (heldItemAnchor == null)
                return;

            float blend = 1f - Mathf.Exp(-heldItemWiggleSmoothing * Time.deltaTime);
            heldItemWiggleWeight = Mathf.Lerp(heldItemWiggleWeight, speedRatio, blend);
            heldItemWigglePhase = Mathf.Repeat(heldItemWigglePhase + speedRatio
                * heldItemWiggleFrequency * Mathf.PI * 2f * Time.deltaTime, Mathf.PI * 2f);

            if (speedRatio == 0f && heldItemWiggleWeight < 0.001f)
            {
                ResetHeldItemWiggle();
                return;
            }

            float offset = Mathf.Sin(heldItemWigglePhase) * heldItemWiggleAmplitude * heldItemWiggleWeight;
            heldItemAnchor.localPosition = heldItemRestPosition + Vector3.up * offset;
        }

        private void ResetHeldItemWiggle()
        {
            heldItemWigglePhase = 0f;
            heldItemWiggleWeight = 0f;
            if (heldItemAnchor != null)
                heldItemAnchor.localPosition = heldItemRestPosition;
        }

        private void ApplyArmJiggle(Transform arm, Vector3 restPosition, Quaternion restRotation,
            float swing, float bounce, float jiggle)
        {
            if (arm == null)
                return;

            Vector3 offset = Vector3.Scale(armPositionAmplitude,
                new Vector3(swing, bounce + jiggle, swing)) * armJiggleWeight;
            Vector3 rotation = Vector3.Scale(armRotationAmplitude,
                new Vector3(swing + jiggle, swing, swing)) * armJiggleWeight;
            arm.localPosition = restPosition + offset;
            arm.localRotation = restRotation * Quaternion.Euler(rotation);
        }

        private void ResetArmJiggle()
        {
            armJiggleWeight = 0f;
            armJigglePhase = 0f;
            if (leftArm != null)
            {
                leftArm.localPosition = leftArmRestPosition;
                leftArm.localRotation = leftArmRestRotation;
            }
            if (rightArm != null)
            {
                rightArm.localPosition = rightArmRestPosition;
                rightArm.localRotation = rightArmRestRotation;
            }
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleCursor()
        {
            if (IsCursorLocked)
            {
                if (WasPressed(unlockCursorAction))
                    SetCursorLocked(false);
            }
            else if (WasPressed(lockCursorAction) && !IsPointerOverUI())
            {
                SetCursorLocked(true);
            }
        }

        private void HandleLook()
        {
            if (!InputEnabled || !IsCursorLocked)
                return;
            if (TryHandleLookInput?.Invoke() == true || lookAction == null || cameraTarget == null)
                return;

            Vector2 look = lookAction.action.ReadValue<Vector2>();
            if (look.sqrMagnitude < 0.000001f)
                return;

            // Pointer delta is already per-frame; stick values are rates and need deltaTime.
            bool isPointer = lookAction.action.activeControl?.device is Pointer;
            float scale = isPointer ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

            float yawDelta = look.x * scale;
            float pitchDelta = look.y * scale * (invertY ? 1f : -1f);

            pitch = Mathf.Clamp(pitch + pitchDelta, -maxPitch, maxPitch);
            cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.Rotate(0f, yawDelta, 0f, Space.Self);
        }

        private void HandleMovement()
        {
            bool grounded = characterController.isGrounded;

            Vector2 input = InputEnabled && moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 wishDirection = transform.right * input.x + transform.forward * input.y;
            Vector3 targetVelocity = wishDirection * walkSpeed;

            float accel = grounded ? acceleration : acceleration * airControl;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * Time.deltaTime);

            if (grounded && verticalVelocity < 0f)
                verticalVelocity = groundedStickVelocity;

            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -terminalFallSpeed);

            Vector3 motion = (horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
            CollisionFlags flags = characterController.Move(motion);

            // Stop upward velocity when hitting a ceiling.
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;
        }

        private static void EnableAction(InputActionReference reference)
        {
            if (reference != null && reference.action != null && !reference.action.enabled)
                reference.action.Enable();
        }

        private static bool WasPressed(InputActionReference reference)
        {
            return reference != null && reference.action != null && reference.action.WasPressedThisFrame();
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
