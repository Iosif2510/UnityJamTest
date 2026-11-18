using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WarehouseIngredient : MonoBehaviour
    {
        [SerializeField] private string variantId;

        private Transform shelfSlot;
        private Vector3 shelfPosition;
        private Quaternion shelfRotation;
        private Collider[] colliders;
        private bool[] enabledOnShelf;
        private Vector3 localRotationCenter;

        public string VariantId => variantId;

        private void Awake()
        {
            shelfSlot = transform.parent;
            shelfPosition = transform.localPosition;
            shelfRotation = transform.localRotation;
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

            transform.SetParent(anchor, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void ReturnToShelf()
        {
            transform.SetParent(shelfSlot, false);
            transform.SetLocalPositionAndRotation(shelfPosition, shelfRotation);

            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = enabledOnShelf[i];
        }

        internal void RotateInHand(Transform view, Vector2 angles)
        {
            Vector3 center = transform.TransformPoint(localRotationCenter);
            Quaternion rotation = Quaternion.AngleAxis(angles.x, view.up) *
                Quaternion.AngleAxis(-angles.y, view.right);
            transform.SetPositionAndRotation(center + rotation * (transform.position - center),
                rotation * transform.rotation);
        }
    }
}
