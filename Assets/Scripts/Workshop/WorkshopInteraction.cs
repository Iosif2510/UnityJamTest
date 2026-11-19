using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private WorkshopAudio workshopAudio;

        private readonly List<WarehouseIngredient> pocket = new();
        private readonly Collider[] originContacts = new Collider[16];
        private WarehouseIngredient held;
        private WorkshopCauldron activeCauldron;
        private bool complete;
        private bool wasLocked;
        private RoundDirector round;
        private PocketModelDisplay pocketModels;
        private CancellationTokenSource transfer;

        private void OnEnable() => player.TryHandleLookInput = TryRotateHeldIngredient;

        private void OnDisable()
        {
            transfer?.Cancel();
            if (player != null && player.TryHandleLookInput == TryRotateHeldIngredient)
                player.TryHandleLookInput = null;
            if (workshopAudio != null)
                workshopAudio.UpdateCountdown(0d);
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
                double remainingSeconds = round.RemainingSeconds;
                hud.UpdateTimer(remainingSeconds, round.Tuning.TimeLimitSeconds);
                workshopAudio?.UpdateCountdown(remainingSeconds);
                if (round.Phase is (RoundPhase.Ending or RoundPhase.Result) && !complete)
                {
                    complete = true;
                    transfer?.Cancel();
                    if (round.Result?.TimedOut == true)
                        workshopAudio?.PlayTimeExpired();
                    hud.SetCrosshairVisible(false);
                    player.enabled = false;
                    hud.ShowPrompt(action: "시간 초과");
                    return;
                }
            }

            if (complete)
                return;
            if (transfer != null)
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
                if (hasIngredients) hud.ShowPrompt("가마솥", "재료 넣기", "좌클릭");
                else hud.ShowPrompt("가마솥", $"재료 {tuning.PocketCapacity}개를 먼저 모으세요");
                if (hasIngredients && Mouse.current?.leftButton.wasPressedThisFrame == true)
                    BeginPour(cauldron);
                return;
            }

            var shelf = target.GetComponentInParent<WorkshopShelf>();
            var ingredient = shelf != null ? shelf.FindStockedIngredient(target) : null;
            if (ingredient == null)
            {
                hud.ShowPrompt();
                return;
            }

            foreach (var stored in pocket)
            {
                if (stored.VariantId != ingredient.VariantId) continue;
                hud.ShowIngredient(ingredient.VariantId, "이미 주머니에 넣었습니다");
                return;
            }

            bool hasSpace = pocket.Count < tuning.PocketCapacity;
            hud.ShowIngredient(ingredient.VariantId,
                hasSpace ? "집기" : "주머니가 가득 찼습니다. 가마솥으로 이동하세요",
                hasSpace ? "E" : null);
            Vector3 shelfPosition = ingredient.transform.position;
            if (hasSpace && (Keyboard.current?.eKey.wasPressedThisFrame == true ||
                Mouse.current?.leftButton.wasPressedThisFrame == true) &&
                shelf.TryTake(ingredient, heldItemAnchor))
            {
                SetHeldIngredient(ingredient);
                workshopAudio?.PlayPickup(ingredient.VariantId, shelfPosition);
            }
        }

        private void HandleHeldIngredient()
        {
            hud.ShowHeldIngredient(held.VariantId);
            if (Mouse.current?.rightButton.wasPressedThisFrame == true)
            {
                var returning = held;
                returning.ReturnToShelf(() =>
                {
                    if (workshopAudio != null && workshopAudio.isActiveAndEnabled)
                        workshopAudio.PlayReturn(returning.VariantId, returning.transform.position);
                });
                SetHeldIngredient(null);
            }
            else if (Keyboard.current?.fKey.wasPressedThisFrame == true && pocket.Count < tuning.PocketCapacity)
            {
                transfer = new CancellationTokenSource();
                hud.ShowPrompt(action: "주머니에 넣는 중");
                StoreIngredient(held, transfer).Forget();
            }
        }

        private async UniTask StoreIngredient(WarehouseIngredient ingredient, CancellationTokenSource operation)
        {
            try
            {
                await WorkshopIngredientMotion.Store(ingredient, playerCamera, operation.Token);
                ingredient.transform.SetParent(null, true);
                pocket.Add(ingredient);
                SetHeldIngredient(null);
                pocketModels.ShowIngredient(pocket.Count - 1, ingredient.transform);
                if (workshopAudio != null) workshopAudio.PlayPocket();
            }
            finally { FinishTransfer(operation); }
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
            if (!wasLocked || complete || transfer != null || held == null || mouse == null ||
                Keyboard.current?.rKey.isPressed != true ||
                (round != null && round.Phase is (RoundPhase.Ending or RoundPhase.Result)))
                return false;

            Vector2 angles = mouse.delta.ReadValue() * heldRotationSensitivity;
            if (angles != Vector2.zero)
                held.RotateInHand(playerCamera.transform, angles);
            return true;
        }

        private void BeginPour(WorkshopCauldron cauldron)
        {
            if (!cauldron.CanStartBrewing) return;
            cauldron.BeginPour();
            transfer = new CancellationTokenSource();
            hud.SetCrosshairVisible(false);
            hud.ShowPrompt("가마솥", "재료 넣는 중");
            PourIngredients(cauldron, transfer).Forget();
        }

        private async UniTask PourIngredients(WorkshopCauldron cauldron, CancellationTokenSource operation)
        {
            bool brewing = false;
            try
            {
                int count = pocket.Count;
                var pours = new UniTask[count];
                var ingredientIds = new string[count];
                for (int i = 0; i < count; i++)
                {
                    ingredientIds[i] = pocket[i].VariantId;
                    pours[i] = ThrowIngredient(i, pocket[i], count, cauldron, operation.Token);
                }

                await UniTask.WhenAll(pours);
                operation.Token.ThrowIfCancellationRequested();
                if (cauldron == null || !cauldron.isActiveAndEnabled ||
                    (round != null && !round.TrySubmitIngredients(ingredientIds))) return;

                cauldron.StartBrewing();
                brewing = true;
                activeCauldron = cauldron;
                pocket.Clear();
                pocketModels.Clear();
                UpdateBrewing(cauldron);
            }
            finally
            {
                if (!brewing)
                {
                    operation.Cancel();
                    if (cauldron != null) cauldron.CancelPour();
                    for (int i = 0; i < pocket.Count; i++) pocketModels.SetSlotVisible(i, true);
                }
                FinishTransfer(operation);
            }
        }

        private async UniTask ThrowIngredient(int slot, WarehouseIngredient ingredient, int count,
            WorkshopCauldron cauldron, CancellationToken token)
        {
            if (slot > 0)
                await UniTask.Delay(System.TimeSpan.FromSeconds(WorkshopIngredientMotion.ThrowInterval * slot),
                    ignoreTimeScale: true, cancellationToken: token);
            if (cauldron == null || !cauldron.isActiveAndEnabled) throw new System.OperationCanceledException();
            await WorkshopIngredientMotion.Throw(ingredient, playerCamera, cauldron.LiquidImpactPoint(slot, count), token);
            token.ThrowIfCancellationRequested();
            if (cauldron == null || !cauldron.isActiveAndEnabled) throw new System.OperationCanceledException();
            pocketModels.SetSlotVisible(slot, false);
            cauldron.AddIngredient(slot + 1, count);
        }

        private void FinishTransfer(CancellationTokenSource operation)
        {
            if (transfer == operation) transfer = null;
            operation.Dispose();
        }

        private void UpdateBrewing(WorkshopCauldron cauldron)
        {
            double remaining = cauldron.RemainingSeconds;
            if (remaining > 0d)
            {
                hud.ShowPrompt($"제조 중 · {System.Math.Ceiling(remaining):0}초");
                return;
            }

            if (round != null && !round.TryFinishBrewing())
                return;

            complete = true;
            hud.ShowPrompt();
            player.enabled = false;
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
