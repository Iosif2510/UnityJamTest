using System;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's desk in the loft. The narrator sits still; the memo sheets lie on the desk and are picked up
    /// with the mouse to be read, then put back where they were. Reading itself happens on <see cref="LoftView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftDesk : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Tooltip("Where a picked-up sheet is held, in front of the camera.")]
        private Transform holdAnchor;
        [SerializeField, Tooltip("Cover, six potion memos in POT order, then the counter manual.")]
        private Transform[] sheets = Array.Empty<Transform>();
        [SerializeField] private float hoverLift = 0.025f;
        [SerializeField] private float moveSeconds = 0.28f;
        [SerializeField, Tooltip("How far the view turns as the mouse moves across the screen, in degrees.")]
        private Vector2 lookRange = new(7f, 4f);

        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Quaternion cameraRest;
        private int hovered = -1;
        private bool moving;

        public int Held { get; private set; } = -1;
        public int Hovered => hovered;
        public int SheetCount => sheets.Length;

        /// <summary>Raised when a sheet has reached the narrator's hands and can be read.</summary>
        public event Action<int> PickedUp;
        /// <summary>Raised when the narrator lets go of the sheet, before it travels back to the desk.</summary>
        public event Action PutDown;

        private void Awake()
        {
            restPositions = new Vector3[sheets.Length];
            restRotations = new Quaternion[sheets.Length];
            for (int i = 0; i < sheets.Length; i++)
            {
                restPositions[i] = sheets[i].position;
                restRotations[i] = sheets[i].rotation;
            }
            cameraRest = viewCamera.transform.localRotation;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || moving)
                return;

            if (Held >= 0)
            {
                bool escape = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
                if (mouse.rightButton.wasPressedThisFrame || escape)
                    Release();
                return;
            }

            Look(mouse.position.ReadValue());
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            SetHovered(overUi ? -1 : SheetUnder(mouse.position.ReadValue()));
            if (hovered >= 0 && mouse.leftButton.wasPressedThisFrame)
                Pick(hovered);
        }

        /// <summary>Picks a sheet up without the mouse, e.g. from a shortcut or a guided step.</summary>
        public void Pick(int index)
        {
            if (moving || Held >= 0 || index < 0 || index >= sheets.Length)
                return;
            SetHovered(-1);
            Held = index;
            Move(sheets[index], holdAnchor.position, holdAnchor.rotation, () => PickedUp?.Invoke(index));
        }

        public void Release()
        {
            if (moving || Held < 0)
                return;
            int index = Held;
            PutDown?.Invoke();
            Move(sheets[index], restPositions[index], restRotations[index], () => Held = -1);
        }

        private void Look(Vector2 pointer)
        {
            // A small turn of the head toward the mouse keeps the still seat from feeling like a flat picture.
            float x = Mathf.Clamp01(pointer.x / Mathf.Max(1, Screen.width)) * 2f - 1f;
            float y = Mathf.Clamp01(pointer.y / Mathf.Max(1, Screen.height)) * 2f - 1f;
            var target = cameraRest * Quaternion.Euler(-y * lookRange.y, x * lookRange.x, 0f);
            viewCamera.transform.localRotation = Quaternion.Slerp(viewCamera.transform.localRotation, target,
                1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }

        private int SheetUnder(Vector2 pointer)
        {
            if (!Physics.Raycast(viewCamera.ScreenPointToRay(pointer), out var hit, 10f))
                return -1;
            return Array.IndexOf(sheets, hit.transform);
        }

        private void SetHovered(int index)
        {
            if (index == hovered)
                return;
            if (hovered >= 0)
                sheets[hovered].position = restPositions[hovered];
            hovered = index;
            if (hovered >= 0)
                sheets[hovered].position = restPositions[hovered] + Vector3.up * hoverLift;
        }

        private void Move(Transform sheet, Vector3 position, Quaternion rotation, Action arrived)
        {
            moving = true;
            Vector3 fromPosition = sheet.position;
            Quaternion fromRotation = sheet.rotation;
            LMotion.Create(0f, 1f, moveSeconds).WithEase(Ease.OutCubic)
                .WithOnComplete(() =>
                {
                    moving = false;
                    arrived?.Invoke();
                })
                .Bind(t =>
                {
                    // Lift in an arc so the sheet clears its neighbours on the desk.
                    var point = Vector3.Lerp(fromPosition, position, t);
                    point.y += Mathf.Sin(t * Mathf.PI) * 0.05f;
                    sheet.SetPositionAndRotation(point, Quaternion.Slerp(fromRotation, rotation, t));
                })
                .AddTo(gameObject);
        }
    }
}
