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

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnEnable = true;

        private CharacterController characterController;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float pitch;

        public bool IsGrounded => characterController.isGrounded;
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>When false, movement and look input are ignored (e.g. while a UI panel is open).</summary>
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (cameraTarget == null)
                Debug.LogError("FirstPersonController: Camera Target is not assigned.", this);
            else
                pitch = NormalizeAngle(cameraTarget.localEulerAngles.x);
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
            SetCursorLocked(false);
        }

        private void Update()
        {
            HandleCursor();
            HandleLook();
            HandleMovement();
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
            if (!InputEnabled || !IsCursorLocked || lookAction == null || cameraTarget == null)
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
