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
        private enum Stage { Collecting, Brewing, Complete }

        [SerializeField] private FirstPersonController player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform heldItemAnchor;
        [SerializeField] private Transform cauldron;
        [SerializeField] private RoundTuning tuning;
        [SerializeField] private Text prompt;
        [SerializeField] private Text pocketDisplay;
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private GameObject potionSurface;
        [SerializeField] private GameObject resultPanel;

        private readonly List<string> pocket = new();
        private WarehouseIngredient held;
        private Stage stage;
        private bool wasLocked;
        private double brewEndsAt;
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
                if (round.Phase == RoundPhase.Result && stage != Stage.Complete)
                {
                    stage = Stage.Complete;
                    crosshair.SetActive(false);
                    player.enabled = false;
                    prompt.text = "Time expired";
                    return;
                }
            }

            if (stage == Stage.Brewing)
            {
                UpdateBrewing();
                return;
            }
            if (stage != Stage.Collecting)
                return;

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
            if (!TryGetInteractionHit(GetInteractionRay(), out var hit))
                return;

            if (hit.transform.IsChildOf(cauldron))
            {
                bool ready = pocket.Count == tuning.PocketCapacity;
                prompt.text = ready ? "[Left click] Add ingredients to cauldron" :
                    $"Fill all {tuning.PocketCapacity} pocket slots first";
                if (ready && Mouse.current?.leftButton.wasPressedThisFrame == true)
                    StartBrewing();
                return;
            }

            var ingredient = hit.collider.GetComponentInParent<WarehouseIngredient>();
            if (ingredient == null)
                return;

            bool hasSpace = pocket.Count < tuning.PocketCapacity;
            prompt.text = hasSpace ? "[Left click] Pick up" : "Pocket full. Bring ingredients to the cauldron.";
            if (hasSpace && Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                held = ingredient;
                held.Hold(heldItemAnchor);
            }
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

        private void StartBrewing()
        {
            if (round != null && !round.TrySubmitIngredients(pocket))
                return;

            stage = Stage.Brewing;
            brewEndsAt = Time.unscaledTimeAsDouble + tuning.BrewingSeconds;
            pocket.Clear();
            pocketModels.Clear();
            crosshair.SetActive(false);
            potionSurface.SetActive(true);
            UpdateBrewing();
        }

        private void UpdateBrewing()
        {
            double remaining = brewEndsAt - Time.unscaledTimeAsDouble;
            if (remaining > 0d)
            {
                string brewing = $"Brewing... {System.Math.Ceiling(remaining):0}s";
                prompt.text = round == null ? brewing : brewing + " | Switch to the prescription manual.";
                return;
            }

            if (round != null && !round.TryFinishBrewing())
                return;

            stage = Stage.Complete;
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

        private bool TryGetInteractionHit(Ray ray, out RaycastHit hit) =>
            Physics.Raycast(ray, out hit, tuning.PickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

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
        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null || tuning == null)
                return;

            Ray ray = GetInteractionRay();
            Vector3 end = ray.GetPoint(tuning.PickupDistance);
            Color previousColor = Gizmos.color;
            if (TryGetInteractionHit(ray, out var hit))
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(hit.point, end);

                if (cauldron != null && hit.transform.IsChildOf(cauldron))
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = hit.collider.GetComponentInParent<WarehouseIngredient>() != null
                        ? Color.green : Color.red;

                Gizmos.DrawLine(ray.origin, hit.point);
                Gizmos.DrawWireSphere(hit.point, 0.045f);
            }
            else
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(ray.origin, end);
            }

            Gizmos.color = previousColor;
        }
#endif
    }
}
