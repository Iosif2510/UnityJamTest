using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    [Serializable]
    public sealed class DialogueSegment
    {
        [SerializeField, TextArea] private string text;
        [SerializeField] private bool isSymptom;

        public string Text => text;
        public bool IsSymptom => isSymptom;
    }

    [Serializable]
    public sealed class DialogueOption
    {
        [SerializeField] private List<DialogueSegment> segments = new();

        public IReadOnlyList<DialogueSegment> Segments => segments;
    }

    [Serializable]
    public sealed class DiseaseEntry : CatalogEntry
    {
        [SerializeField] private string displayName;
        [SerializeField] private string recipeId;
        [SerializeField] private List<DialogueOption> dialogue = new();

        public string DisplayName => displayName;
        public string RecipeId => recipeId;
        public IReadOnlyList<DialogueOption> Dialogue => dialogue;
    }

    [Serializable]
    public sealed class RecipeEntry : CatalogEntry
    {
        public const int RequiredIngredientKinds = 3;

        [SerializeField] private string potionName;
        [SerializeField] private List<string> ingredientVariantIds = new();

        public string PotionName => potionName;
        public IReadOnlyList<string> IngredientVariantIds => ingredientVariantIds;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Diseases and Recipes")]
    public sealed class DiseaseRecipeCatalog : ScriptableObject
    {
        [SerializeField] private List<DiseaseEntry> diseases = new();
        [SerializeField] private List<RecipeEntry> recipes = new();
        [SerializeField] private List<CustomerDialogueOverride> customerDialogue = new();

        public IReadOnlyList<DiseaseEntry> Diseases => diseases;
        public IReadOnlyList<RecipeEntry> Recipes => recipes;
        public IReadOnlyList<CustomerDialogueOverride> CustomerDialogue => customerDialogue;
    }

    [Serializable]
    public sealed class CustomerDialogueOverride
    {
        public string customerId;
        public string diseaseId;
        public List<DialogueOption> dialogue = new();
    }
}
