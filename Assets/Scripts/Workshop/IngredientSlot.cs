using System;
using System.Linq;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class IngredientSlot : MonoBehaviour
    {
        [SerializeField] private IngredientCatalog catalog;
        [SerializeField] private string variantId;
        public string VariantId => variantId;

        private void Start()
        {
            var variant = catalog == null ? null : catalog.Kinds.Where(k => k.Status == ContentStatus.Active)
                .SelectMany(k => k.Variants).FirstOrDefault(v => v.Id == variantId);
            if (variant?.Prefab == null)
            {
                Debug.LogError($"{name}: no prefab for {variantId}", this);
                return;
            }
            // Authored children are edit-time previews; the catalog is the runtime source.
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var instance = Instantiate(variant.Prefab, transform, false);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (!instance.TryGetComponent<WarehouseIngredient>(out var ingredient) || ingredient.VariantId != variantId)
            {
                instance.SetActive(false);
                Debug.LogError($"{variantId}: prefab WarehouseIngredient ID does not match.", this);
            }
        }
    }
}
