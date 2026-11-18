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

            TweenTo(anchor, Vector3.zero, Quaternion.identity, pickupDuration, restoreColliders: false);
        }

        public void ReturnToShelf() =>
            TweenTo(shelfSlot, shelfPosition, shelfRotation, returnDuration, restoreColliders: true);

        private void TweenTo(Transform destination, Vector3 localPosition, Quaternion localRotation,
            float duration, bool restoreColliders)
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
                    transform.localScale = Vector3.Lerp(startScale, shelfScale, progress);
                });
        }

        private void OnDisable() => transferMotion.TryComplete();

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
