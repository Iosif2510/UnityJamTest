using System.Collections.Generic;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PotionShop
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class WorkshopInteraction : MonoBehaviour
    {
        private const float RayOriginRadius = 0.001f;
        private static readonly int IsHolding = Animator.StringToHash("IsHolding");

        [SerializeField] private FirstPersonController player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform heldItemAnchor;
        [SerializeField] private Animator armsAnimator;
        [SerializeField, Min(0f)] private float heldRotationSensitivity = 0.35f;
        [SerializeField] private RoundTuning tuning;
        [SerializeField] private WorkshopHud hud;
        [SerializeField] private GameObject resultPanel;

        private readonly List<string> pocket = new();
        private readonly Collider[] originContacts = new Collider[16];
        private WarehouseIngredient held;
        private WorkshopCauldron activeCauldron;
        private bool complete;
        private bool wasLocked;
        private RoundDirector round;
        private PocketModelDisplay pocketModels;

        private void OnEnable() => player.TryHandleLookInput = TryRotateHeldIngredient;

        private void OnDisable()
        {
            if (player != null && player.TryHandleLookInput == TryRotateHeldIngredient)
                player.TryHandleLookInput = null;
            wasLocked = false;
        }

        private void Start()
        {
            // Free is a release clip. Start at its open-hand pose without playing a release on spawn.
            if (armsAnimator != null)
                armsAnimator.Play("Base Layer.Free", 0, 1f);
            pocketModels = hud.CreatePocketDisplay(playerCamera);
            hud.ShowPrompt();
            var activeRound = RoundDirector.Instance;
            if (activeRound != null && activeRound.CurrentOrder != null)
                round = activeRound;
            hud.SetTimerVisible(round != null);
        }

        private void OnDestroy() => pocketModels?.Dispose();

        private void Update()
        {
            if (round != null)
            {
                hud.UpdateTimer(round.RemainingSeconds, round.Tuning.TimeLimitSeconds);
                if (round.Phase is (RoundPhase.Ending or RoundPhase.Result) && !complete)
                {
                    complete = true;
                    hud.SetCrosshairVisible(false);
                    player.enabled = false;
                    hud.ShowPrompt(action: "시간 초과");
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
            hud.SetCrosshairVisible(locked);
            if (!canInteract)
            {
                hud.ShowPrompt(action: locked ? "" : "게임 화면을 클릭해 계속하기");
                return;
            }

            if (held != null)
            {
                HandleHeldIngredient();
                return;
            }

            if (!TryGetInteractionHit(GetInteractionRay(), out var target, out _))
            {
                hud.ShowPrompt();
                return;
            }

            var cauldron = target.GetComponentInParent<WorkshopCauldron>();
            if (cauldron != null)
            {
                bool hasIngredients = pocket.Count == tuning.PocketCapacity;
                hud.ShowPrompt("가마솥", hasIngredients ? "좌클릭으로 재료 넣기" :
                    $"재료 {tuning.PocketCapacity}개를 먼저 모으세요");
                if (hasIngredients && Mouse.current?.leftButton.wasPressedThisFrame == true)
                    StartBrewing(cauldron);
                return;
            }

            var shelf = target.GetComponentInParent<WorkshopShelf>();
            var ingredient = shelf != null ? shelf.FindStockedIngredient(target) : null;
            if (ingredient == null)
            {
                hud.ShowPrompt();
                return;
            }

            bool hasSpace = pocket.Count < tuning.PocketCapacity;
            hud.ShowIngredient(ingredient.VariantId,
                hasSpace ? "집기" : "주머니가 가득 찼습니다. 가마솥으로 이동하세요",
                hasSpace ? "E" : null);
            if (hasSpace && (Keyboard.current?.eKey.wasPressedThisFrame == true ||
                Mouse.current?.leftButton.wasPressedThisFrame == true) &&
                shelf.TryTake(ingredient, heldItemAnchor))
                SetHeldIngredient(ingredient);
        }

        private void HandleHeldIngredient()
        {
            hud.ShowIngredient(held.VariantId, "주머니에 넣기  ·  R + 마우스로 돌리기  ·  우클릭으로 돌려놓기", "F");
            if (Mouse.current?.rightButton.wasPressedThisFrame == true)
            {
                held.ReturnToShelf();
                SetHeldIngredient(null);
            }
            else if (Keyboard.current?.fKey.wasPressedThisFrame == true && pocket.Count < tuning.PocketCapacity)
            {
                var stored = held;
                pocket.Add(stored.VariantId);
                stored.gameObject.SetActive(false);
                SetHeldIngredient(null);
                pocketModels.ShowIngredient(pocket.Count - 1, stored.transform);
            }
        }

        private void SetHeldIngredient(WarehouseIngredient ingredient)
        {
            held = ingredient;
            if (armsAnimator != null)
                armsAnimator.SetBool(IsHolding, held != null);
        }

        private bool TryRotateHeldIngredient()
        {
            var mouse = Mouse.current;
            if (!wasLocked || complete || held == null || mouse == null ||
                Keyboard.current?.rKey.isPressed != true ||
                (round != null && round.Phase is (RoundPhase.Ending or RoundPhase.Result)))
                return false;

            Vector2 angles = mouse.delta.ReadValue() * heldRotationSensitivity;
            if (angles != Vector2.zero)
                held.RotateInHand(playerCamera.transform, angles);
            return true;
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
            hud.SetCrosshairVisible(false);
            UpdateBrewing(cauldron);
        }

        private void UpdateBrewing(WorkshopCauldron cauldron)
        {
            double remaining = cauldron.RemainingSeconds;
            if (remaining > 0d)
            {
                string brewing = $"제조 중 · {System.Math.Ceiling(remaining):0}초";
                hud.ShowPrompt(brewing, round == null ? "" : "처방전 매뉴얼로 이동하세요");
                return;
            }

            if (round != null && !round.TryFinishBrewing())
                return;

            complete = true;
            hud.ShowPrompt();
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
