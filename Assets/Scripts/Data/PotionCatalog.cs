using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    public enum ContentStatus { Draft, Active, Retired }

    [Serializable]
    public abstract class CatalogEntry
    {
        [SerializeField] private string id;
        [SerializeField] private ContentStatus status;

        public string Id => id;
        public ContentStatus Status => status;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Potion Catalog")]
    public sealed class PotionCatalog : ScriptableObject
    {
        [SerializeField] private CustomerCatalog customers;
        [SerializeField] private DiseaseRecipeCatalog diseasesAndRecipes;
        [SerializeField] private IngredientCatalog ingredients;
        [SerializeField] private PrescriptionRules prescriptionRules;
        [SerializeField] private RoundTuning tuning;

        public CustomerCatalog Customers => customers;
        public DiseaseRecipeCatalog DiseasesAndRecipes => diseasesAndRecipes;
        public IngredientCatalog Ingredients => ingredients;
        public PrescriptionRules PrescriptionRules => prescriptionRules;
        public RoundTuning Tuning => tuning;
    }
}
