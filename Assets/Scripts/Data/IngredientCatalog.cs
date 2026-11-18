using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    [Serializable]
    public sealed class IngredientVariant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public Sprite Icon => icon;
    }

    [Serializable]
    public sealed class IngredientKind : CatalogEntry
    {
        public const int RequiredVariants = 3;

        [SerializeField] private string displayName;
        [SerializeField] private List<IngredientVariant> variants = new();

        public string DisplayName => displayName;
        public IReadOnlyList<IngredientVariant> Variants => variants;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Ingredients")]
    public sealed class IngredientCatalog : ScriptableObject
    {
        [SerializeField] private List<IngredientKind> kinds = new();

        public IReadOnlyList<IngredientKind> Kinds => kinds;
    }
}
