using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PotionShop.Data
{
    public enum IngredientFeature { Color, Pattern, Shape }

    [Serializable]
    public sealed class IngredientVariant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private IngredientFeature[] features = Array.Empty<IngredientFeature>();
        [SerializeField] private string distinctPoint;
        [SerializeField, FormerlySerializedAs("icon")] private Sprite bookletThumbnail;
        [SerializeField, Tooltip("기획 표와 다른 임시 모델. 시연용 아트 검사에서 교체 필요.")]
        private bool usesPlaceholderArt;

        public string Id => id;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public IReadOnlyList<IngredientFeature> Features => features;
        public string DistinctPoint => distinctPoint;
        public Sprite BookletThumbnail => bookletThumbnail;
        public bool UsesPlaceholderArt => usesPlaceholderArt;
    }

    [Serializable]
    public sealed class IngredientKind : CatalogEntry
    {
        public const int RequiredVariants = 3;

        [SerializeField] private string displayName;
        [SerializeField] private string baseAppearance;
        [SerializeField] private string featureA;
        [SerializeField] private string featureB;
        [SerializeField] private string featureC;
        [SerializeField] private List<IngredientVariant> variants = new();

        public string DisplayName => displayName;
        public string BaseAppearance => baseAppearance;
        public string FeatureA => featureA;
        public string FeatureB => featureB;
        public string FeatureC => featureC;
        public IReadOnlyList<IngredientVariant> Variants => variants;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Ingredients")]
    public sealed class IngredientCatalog : ScriptableObject
    {
        [SerializeField] private List<IngredientKind> kinds = new();

        public IReadOnlyList<IngredientKind> Kinds => kinds;
    }
}
