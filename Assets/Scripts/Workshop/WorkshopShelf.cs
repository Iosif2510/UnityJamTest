using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WorkshopShelf : MonoBehaviour
    {
        [SerializeField] private Transform stockRoot;

        public WarehouseIngredient FindStockedIngredient(Collider target)
        {
            var ingredient = target.GetComponentInParent<WarehouseIngredient>();
            return IsStocked(ingredient) ? ingredient : null;
        }

        public bool TryTake(WarehouseIngredient ingredient, Transform heldItemAnchor)
        {
            if (!IsStocked(ingredient))
                return false;

            ingredient.Hold(heldItemAnchor);
            return true;
        }

        private bool IsStocked(WarehouseIngredient ingredient) =>
            stockRoot != null && ingredient != null && ingredient.isActiveAndEnabled &&
            ingredient.transform.IsChildOf(stockRoot);
    }
}
