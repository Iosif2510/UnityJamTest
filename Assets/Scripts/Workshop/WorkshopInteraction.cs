using System.Collections.Generic;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PotionShop
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class WorkshopInteraction : MonoBehaviour
    {
        private const float RayOriginRadius = 0.001f;

        [SerializeField] private FirstPersonController player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform heldItemAnchor;
        [SerializeField] private RoundTuning tuning;
        [SerializeField] private Text prompt;
        [SerializeField] private Text pocketDisplay;
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private GameObject resultPanel;

        private readonly List<string> pocket = new();
        private readonly Collider[] originContacts = new Collider[16];
        private WarehouseIngredient held;
        private WorkshopCauldron activeCauldron;
        private bool complete;
        private bool wasLocked;
        private RoundDirector round;
        private PocketModelDisplay pocketModels;

        private void Start()
        {
            pocketModels = new PocketModelDisplay(pocketDisplay, playerCamera, tuning.PocketCapacity);
            var activeRound = RoundDirector.Instance;
            if (activeRound != null && activeRound.CurrentOrder != null)
            {
                round = activeRound;
                if (timerText == null)
                    timerText = CreateTimer();
            }
            if (timerText != null)
                timerText.gameObject.SetActive(round != null);
        }

        private void OnDestroy() => pocketModels?.Dispose();

        private void Update()
        {
            if (round != null)
            {
                timerText.text = RoundDirector.FormatClock(round.RemainingSeconds);
                if (round.Phase == RoundPhase.Result && !complete)
                {
                    complete = true;
                    crosshair.SetActive(false);
                    player.enabled = false;
                    prompt.text = "Time expired";
                    return;
                }
            }

            if (complete)
                return;
            if (activeCauldron != null)
            {
                UpdateBrewing(activeCauldron);
                return;
            }

            bool locked = player.IsCursorLocked;
            bool canInteract = locked && wasLocked && player.InputEnabled;
            wasLocked = locked;
            crosshair.SetActive(locked);
            if (!canInteract)
            {
                prompt.text = locked ? string.Empty : "Click the game view to resume";
                return;
            }

            if (held != null)
            {
                HandleHeldIngredient();
                return;
            }

            prompt.text = string.Empty;
            if (!TryGetInteractionHit(GetInteractionRay(), out var target, out _))
                return;

            var cauldron = target.GetComponentInParent<WorkshopCauldron>();
            if (cauldron != null)
            {
                bool hasIngredients = pocket.Count == tuning.PocketCapacity;
                prompt.text = hasIngredients ? "[Left click] Add ingredients to cauldron" :
                    $"Fill all {tuning.PocketCapacity} pocket slots first";
                if (hasIngredients && Mouse.current?.leftButton.wasPressedThisFrame == true)
                    StartBrewing(cauldron);
                return;
            }

            var shelf = target.GetComponentInParent<WorkshopShelf>();
            var ingredient = shelf != null ? shelf.FindStockedIngredient(target) : null;
            if (ingredient == null)
                return;

            bool hasSpace = pocket.Count < tuning.PocketCapacity;
            prompt.text = hasSpace ? "[Left click] Pick up" : "Pocket full. Bring ingredients to the cauldron.";
            if (hasSpace && Mouse.current?.leftButton.wasPressedThisFrame == true &&
                shelf.TryTake(ingredient, heldItemAnchor))
                held = ingredient;
        }

        private void HandleHeldIngredient()
        {
            prompt.text = "[F] Put in pocket   [Right click] Return to shelf";
            if (Mouse.current?.rightButton.wasPressedThisFrame == true)
            {
                held.ReturnToShelf();
                held = null;
            }
            else if (Keyboard.current?.fKey.wasPressedThisFrame == true && pocket.Count < tuning.PocketCapacity)
            {
                var stored = held;
                pocket.Add(stored.VariantId);
                stored.gameObject.SetActive(false);
                held = null;
                pocketModels.ShowIngredient(pocket.Count - 1, stored.transform);
            }
        }

        private void StartBrewing(WorkshopCauldron cauldron)
        {
            if (!cauldron.CanStartBrewing || (round != null && !round.TrySubmitIngredients(pocket)))
                return;
            if (!cauldron.TryStartBrewing())
                return;

            activeCauldron = cauldron;
            pocket.Clear();
            pocketModels.Clear();
            crosshair.SetActive(false);
            UpdateBrewing(cauldron);
        }

        private void UpdateBrewing(WorkshopCauldron cauldron)
        {
            double remaining = cauldron.RemainingSeconds;
            if (remaining > 0d)
            {
                string brewing = $"Brewing... {System.Math.Ceiling(remaining):0}s";
                prompt.text = round == null ? brewing : brewing + " | Switch to the prescription manual.";
                return;
            }

            if (round != null && !round.TryFinishBrewing())
                return;

            complete = true;
            prompt.text = string.Empty;
            player.enabled = false;
            if (round == null)
                resultPanel.SetActive(true);
        }

        private Ray GetInteractionRay()
        {
            var cameraTransform = playerCamera.transform;
            return new Ray(cameraTransform.position, cameraTransform.forward);
        }

        private bool TryGetInteractionHit(Ray ray, out Collider target, out Vector3 hitPoint)
        {
            target = null;
            hitPoint = ray.origin;
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0)
                return false;

            int mask = 1 << layer;
            // A ray starting inside a collider cannot return that collider.
            int count = Physics.OverlapSphereNonAlloc(ray.origin, RayOriginRadius, originContacts,
                mask, QueryTriggerInteraction.Ignore);
            float nearestCenter = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var contact = originContacts[i];
                originContacts[i] = null;
                float distance = (contact.bounds.center - ray.origin).sqrMagnitude;
                if (distance < nearestCenter)
                {
                    target = contact;
                    nearestCenter = distance;
                }
            }
            if (target != null)
                return true;

            if (!Physics.Raycast(ray, out var hit, tuning.PickupDistance, mask, QueryTriggerInteraction.Ignore))
                return false;

            target = hit.collider;
            hitPoint = hit.point;
            return true;
        }

        private Text CreateTimer()
        {
            var timer = new GameObject("RoundTimer", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)timer.transform;
            rect.SetParent(prompt.transform.parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.86f, 0.89f);
            rect.sizeDelta = new Vector2(160f, 56f);
            rect.anchoredPosition = Vector2.zero;

            var text = timer.GetComponent<Text>();
            text.font = prompt.font;
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

#if UNITY_EDITOR
        public bool TryGetRayGizmo(out Ray ray, out Vector3 end, out Collider target, out Vector3 hitPoint, out Color color)
        {
            ray = default;
            end = default;
            target = null;
            hitPoint = default;
            color = Color.cyan;
            if (playerCamera == null || tuning == null)
                return false;

            ray = GetInteractionRay();
            end = ray.GetPoint(tuning.PickupDistance);
            if (TryGetInteractionHit(ray, out target, out hitPoint))
            {
                if (target.GetComponentInParent<WorkshopCauldron>() != null)
                    color = Color.yellow;
                else
                    color = target.GetComponentInParent<WorkshopShelf>()?.FindStockedIngredient(target) != null
                        ? Color.green : Color.red;
            }

            return true;
        }
#endif
    }
}
