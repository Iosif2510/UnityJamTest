using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PotionShop
{
    /// <summary>
    /// Workshop controls: left-click an ingredient to pick it up. While holding it, drag with the left button to
    /// turn it (it also turns slowly on its own), right-click to put it back on the shelf, or carry it to the
    /// cauldron and left-click to put it in. Brewing starts once the cauldron holds the recipe's three ingredients.
    /// The slot row at the bottom shows what is already in the cauldron and appears with the first ingredient.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class WorkshopInteraction : MonoBehaviour
    {
        private const float RayOriginRadius = 0.001f;
        private static readonly int IsHolding = Animator.StringToHash("IsHolding");

        [SerializeField] private FirstPersonController player;
        [SerializeField] private Camera playerCamera;
        [SerializeField, Tooltip("Distance in front of the camera at which a held ingredient is shown, centred.")]
        private float heldDistance = 0.55f;
        [SerializeField, Tooltip("Largest dimension a held ingredient is scaled to, in metres (about 45 % of the view).")]
        private float heldSize = 0.3f;
        [SerializeField] private Animator armsAnimator;
        [SerializeField, Min(0f)] private float heldRotationSensitivity = 0.35f;
        [SerializeField, Tooltip("How fast a held ingredient turns on its own, in degrees per second.")]
        private float heldSpinDegreesPerSecond = 28f;
        [SerializeField] private RoundTuning tuning;
        [SerializeField] private WorkshopHud hud;
        [SerializeField] private WorkshopAudio workshopAudio;

        private readonly List<string> inCauldron = new();
        private readonly Collider[] originContacts = new Collider[16];
        private WarehouseIngredient held;
        private WorkshopCauldron activeCauldron;
        private bool complete;
        private bool wasLocked;
        private RoundDirector round;
        private PocketModelDisplay pocketModels;
        private CancellationTokenSource transfer;
        private Transform holdAnchor;
        private AimHighlight aimHighlight;
        // Created on first use: scene objects kept across a script reload skip field initializers.
        private AimHighlight aim => aimHighlight ??= new AimHighlight();
        private HeldItemOverlay heldOverlay;

        private static int Required => RecipeEntry.RequiredIngredientKinds;
        private int Gathered => inCauldron.Count + (held != null ? 1 : 0);

        private void OnEnable() => player.TryHandleLookInput = TryRotateHeldIngredient;

        private void OnDisable()
        {
            aim.Release();
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
            holdAnchor = new GameObject("CenterHoldAnchor").transform;
            holdAnchor.SetParent(playerCamera.transform, false);
            heldOverlay = new HeldItemOverlay(playerCamera);
            hud.ShowPrompt();
            var activeRound = RoundDirector.Instance;
            if (activeRound != null && activeRound.CurrentOrder != null)
                round = activeRound;
            hud.SetTimerVisible(round != null);
        }

        private void OnDestroy() => pocketModels?.Dispose();

        private void Update()
        {
            heldOverlay?.SyncLens();
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
            if (held != null && transfer == null && !IsRotatingHeldIngredient(Mouse.current))
                held.RotateInHand(playerCamera.transform, new Vector2(heldSpinDegreesPerSecond * Time.deltaTime, 0f));
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
                aim.Release();
                hud.SetCrosshairFocus(false);
                hud.ShowPrompt(action: locked ? "" : "게임 화면을 클릭해 계속하기");
                return;
            }

            var mouse = Mouse.current;
            bool click = mouse?.leftButton.wasPressedThisFrame == true;
            TryGetInteractionHit(GetInteractionRay(), out var target, out _);
            var cauldron = target != null ? target.GetComponentInParent<WorkshopCauldron>() : null;

            // What a click would act on: a free ingredient on a shelf, or the cauldron while holding one.
            var aimedShelf = held == null && cauldron == null && target != null ? target.GetComponentInParent<WorkshopShelf>() : null;
            var aimed = aimedShelf != null ? aimedShelf.FindStockedIngredient(target) : null;
            if (aimed != null && (AlreadyGathered(aimed.VariantId) || Gathered >= Required)) aimed = null;
            aim.Focus(aimed != null ? aimed.transform : null);
            hud.SetCrosshairFocus(aimed != null || (held != null && cauldron != null));

            if (held != null)
            {
                HandleHeldIngredient(cauldron, click, mouse?.rightButton.wasPressedThisFrame == true);
                return;
            }

            if (cauldron != null)
            {
                hud.ShowPrompt("가마솥", $"재료를 들고 오세요 ({inCauldron.Count}/{Required})");
                return;
            }

            var shelf = target != null ? target.GetComponentInParent<WorkshopShelf>() : null;
            var ingredient = shelf != null ? shelf.FindStockedIngredient(target) : null;
            if (ingredient == null)
            {
                hud.ShowPrompt();
                return;
            }

            if (AlreadyGathered(ingredient.VariantId))
            {
                hud.ShowIngredient(ingredient.VariantId, "이미 챙긴 재료입니다");
                return;
            }

            bool hasSpace = Gathered < Required;
            hud.ShowIngredient(ingredient.VariantId,
                hasSpace ? "집기" : "재료가 다 모였습니다. 가마솥으로 가세요",
                hasSpace ? "좌클릭" : null);
            Vector3 shelfPosition = ingredient.transform.position;
            if (hasSpace && click)
                aim.Release(); // back to its own size and colour before it flies to the hand
            if (hasSpace && click && shelf.TryTake(ingredient, PrepareHoldAnchor(ingredient)))
            {
                SetHeldIngredient(ingredient);
                workshopAudio?.PlayPickup(ingredient.VariantId, shelfPosition);
            }
        }

        private void HandleHeldIngredient(WorkshopCauldron cauldron, bool click, bool putBack)
        {
            if (cauldron != null) hud.ShowHeldIngredient("가마솥에 넣기", "좌클릭");
            else hud.ShowHeldIngredient("돌려 보기", "좌클릭 드래그");

            if (putBack)
            {
                var returning = held;
                returning.ReturnToShelf(() =>
                {
                    if (workshopAudio != null && workshopAudio.isActiveAndEnabled)
                        workshopAudio.PlayReturn(returning.VariantId, returning.transform.position);
                });
                SetHeldIngredient(null);
            }
            else if (click && cauldron != null)
                BeginPour(cauldron);
        }

        /// <summary>
        /// Places the hold anchor so the ingredient's visual centre sits in the middle of the view at
        /// <see cref="heldDistance"/>, scaled so its largest side is <see cref="heldSize"/>. The ingredient keeps
        /// its shelf scale relative to the anchor, so putting it back restores its shelf size.
        /// </summary>
        private Transform PrepareHoldAnchor(WarehouseIngredient ingredient)
        {
            var renderers = ingredient.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return holdAnchor;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var item = ingredient.transform;
            float slotScale = item.parent != null ? item.parent.lossyScale.x : 1f;
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = heldSize / Mathf.Max(0.01f, largest) * slotScale;
            Vector3 centre = Vector3.Scale(item.localScale, item.InverseTransformPoint(bounds.center));
            holdAnchor.localRotation = Quaternion.identity;
            holdAnchor.localScale = Vector3.one * scale;
            holdAnchor.localPosition = new Vector3(0f, 0f, heldDistance) - centre * scale;
            return holdAnchor;
        }

        private bool AlreadyGathered(string variantId) => inCauldron.Contains(variantId);

        private bool TryRotateHeldIngredient()
        {
            var mouse = Mouse.current;
            if (!IsRotatingHeldIngredient(mouse))
                return false;

            Vector2 angles = mouse.delta.ReadValue() * heldRotationSensitivity;
            if (angles != Vector2.zero)
                held.RotateInHand(playerCamera.transform, angles);
            return true;
        }

        // Dragging with the left button turns the held ingredient instead of the view.
        private bool IsRotatingHeldIngredient(Mouse mouse) =>
            wasLocked && !complete && transfer == null && held != null && mouse != null &&
            mouse.leftButton.isPressed && !mouse.leftButton.wasPressedThisFrame &&
            (round == null || round.Phase is not (RoundPhase.Ending or RoundPhase.Result));

        private void SetHeldIngredient(WarehouseIngredient ingredient)
        {
            held = ingredient;
            if (held != null) heldOverlay?.Show(held);
            else heldOverlay?.Clear();
            if (armsAnimator != null)
                armsAnimator.SetBool(IsHolding, held != null);
        }

        /// <summary>Throws the held ingredient into the cauldron and adds it to the slot row.</summary>
        private void BeginPour(WorkshopCauldron cauldron)
        {
            if (inCauldron.Count == 0)
            {
                if (!cauldron.CanStartBrewing) return;
                cauldron.BeginPour();
            }
            transfer = new CancellationTokenSource();
            hud.SetCrosshairVisible(false);
            hud.ShowPrompt("가마솥", "재료 넣는 중");
            PourIngredients(cauldron, transfer).Forget();
        }

        private async UniTask PourIngredients(WorkshopCauldron cauldron, CancellationTokenSource operation)
        {
            try
            {
                var ingredient = held;
                int slot = inCauldron.Count;
                heldOverlay?.Clear(); // the throw flies into the cauldron, so it must be hidden by the rim again
                await ThrowIngredient(ingredient, slot, cauldron, operation.Token);
                operation.Token.ThrowIfCancellationRequested();

                inCauldron.Add(ingredient.VariantId);
                SetHeldIngredient(null);
                pocketModels.ShowIngredient(slot, ingredient.transform);
                hud.SetPocketVisible(true);

                if (inCauldron.Count < Required || cauldron == null || !cauldron.isActiveAndEnabled ||
                    (round != null && !round.TrySubmitIngredients(inCauldron)))
                    return;

                cauldron.StartBrewing();
                activeCauldron = cauldron;
                UpdateBrewing(cauldron);
            }
            finally
            {
                FinishTransfer(operation);
            }
        }

        private async UniTask ThrowIngredient(WarehouseIngredient ingredient, int landed, WorkshopCauldron cauldron,
            CancellationToken token)
        {
            if (cauldron == null || !cauldron.isActiveAndEnabled) throw new System.OperationCanceledException();
            await WorkshopIngredientMotion.Throw(ingredient, playerCamera, cauldron.LiquidImpactPoint(landed, Required), token);
            token.ThrowIfCancellationRequested();
            if (cauldron == null || !cauldron.isActiveAndEnabled) throw new System.OperationCanceledException();
            cauldron.AddIngredient(landed + 1, Required);
            // A small kick forward and down as it lands, stronger for the last one.
            bool last = landed + 1 >= Required;
            player.Kick(new Vector3(0f, -1f, 0.5f), last ? 0.035f : 0.02f);
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
