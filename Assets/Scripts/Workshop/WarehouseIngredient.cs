using System;
using LitMotion;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WarehouseIngredient : MonoBehaviour
    {
        [SerializeField] private string variantId;
        [SerializeField, Min(0f)] private float pickupDuration = 0.24f;
        [SerializeField, Min(0f)] private float returnDuration = 0.28f;

        private Transform shelfSlot;
        private Vector3 shelfPosition;
        private Quaternion shelfRotation;
        private Vector3 shelfScale;
        private Collider[] colliders;
        private bool[] enabledOnShelf;
        private Vector3 localRotationCenter;
        private MotionHandle transferMotion;

        public string VariantId => variantId;

        private void Awake()
        {
            shelfSlot = transform.parent;
            shelfPosition = transform.localPosition;
            shelfRotation = transform.localRotation;
            shelfScale = transform.localScale;
            colliders = GetComponentsInChildren<Collider>(true);
            if (TryGetComponent<BoxCollider>(out var box))
                localRotationCenter = box.center;
            enabledOnShelf = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
                enabledOnShelf[i] = colliders[i].enabled;
        }

        public void Hold(Transform anchor)
        {
            foreach (var shape in colliders)
                shape.enabled = false;

            TweenTo(anchor, Vector3.zero, Quaternion.identity, pickupDuration, restoreColliders: false, pop: true);
            WobbleShelf();
        }

        /// <summary>The shelf it came off gives a small rattle.</summary>
        private void WobbleShelf()
        {
            var shelf = shelfSlot != null ? shelfSlot.GetComponentInParent<WorkshopShelf>() : null;
            if (shelf == null) return;
            var t = shelf.transform;
            if (t.gameObject.isStatic) return; // batched statics would leave their colliders behind
            var rest = t.localRotation;
            LMotion.Create(0f, 1f, 0.35f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (t != null) t.localRotation = rest; })
                .Bind(p => { if (t != null) t.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(p * 25f) * (1f - p) * 0.7f); })
                .AddTo(t.gameObject);
        }

        public void ReturnToShelf(Action onArrived = null) =>
            TweenTo(shelfSlot, shelfPosition, shelfRotation, returnDuration, restoreColliders: true,
                onArrived: onArrived);

        private void TweenTo(Transform destination, Vector3 localPosition, Quaternion localRotation,
            float duration, bool restoreColliders, Action onArrived = null, bool pop = false)
        {
            transferMotion.TryCancel();
            transform.SetParent(destination, true);

            void Finish()
            {
                transform.SetLocalPositionAndRotation(localPosition, localRotation);
                transform.localScale = shelfScale;
                if (restoreColliders)
                {
                    for (int i = 0; i < colliders.Length; i++)
                        colliders[i].enabled = enabledOnShelf[i];
                }
                if (isActiveAndEnabled)
                    onArrived?.Invoke();
            }

            if (duration <= 0f || !isActiveAndEnabled)
            {
                Finish();
                return;
            }

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 startScale = transform.localScale;
            transferMotion = LMotion.Create(0f, 1f, duration)
                .WithEase(Ease.InOutQuad)
                .WithScheduler(MotionScheduler.PreLateUpdate)
                .WithOnComplete(Finish)
                .Bind(progress =>
                {
                    // Keep the departure pose fixed in world space while tracking the moving hand.
                    Vector3 targetPosition = destination != null ? destination.TransformPoint(localPosition) : localPosition;
                    Quaternion targetRotation = destination != null ? destination.rotation * localRotation : localRotation;
                    transform.SetPositionAndRotation(Vector3.Lerp(startPosition, targetPosition, progress),
                        Quaternion.Slerp(startRotation, targetRotation, progress));
                    // picked up: pressed down a little, then springs out past its size and settles
                    float squash = !pop ? 1f : progress < 0.3f ? Mathf.Lerp(1f, 0.82f, progress / 0.3f)
                        : 1f - 0.18f * (1f - (progress - 0.3f) / 0.7f) + Mathf.Sin((progress - 0.3f) / 0.7f * Mathf.PI) * 0.16f;
                    transform.localScale = Vector3.Lerp(startScale, shelfScale, progress) * squash;
                });
        }

        private void OnDisable() => transferMotion.TryComplete();

        internal void StopTransfer() => transferMotion.TryCancel();

        internal void RotateInHand(Transform view, Vector2 angles)
        {
            if (transferMotion.IsActive())
                return;

            Vector3 center = transform.TransformPoint(localRotationCenter);
            Quaternion rotation = Quaternion.AngleAxis(angles.x, view.up) *
                Quaternion.AngleAxis(-angles.y, view.right);
            transform.SetPositionAndRotation(center + rotation * (transform.position - center),
                rotation * transform.rotation);
        }
    }
}
