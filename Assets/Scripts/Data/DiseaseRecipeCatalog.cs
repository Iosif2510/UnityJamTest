using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    public enum SymptomEffect { Inherit, None, ShadowLag, Echo, Drift, MirrorDoze, RainDrip, Frost }

    [Serializable]
    public sealed class DialogueSegment
    {
        [SerializeField, TextArea] private string text;
        [SerializeField] private bool isSymptom;
        [SerializeField] private SymptomEffect effect;

        public string Text => text;
        public bool IsSymptom => isSymptom;
        public SymptomEffect Effect => effect;

        public DialogueSegment() { }
        internal DialogueSegment(string text, bool isSymptom)
        {
            this.text = text;
            this.isSymptom = isSymptom;
        }
    }

    [Serializable]
    public sealed class DialogueOption
    {
        [SerializeField] private List<DialogueSegment> segments = new();
        [SerializeField] private SymptomEffect defaultSymptomEffect;

        public IReadOnlyList<DialogueSegment> Segments => segments;
        public SymptomEffect DefaultSymptomEffect => defaultSymptomEffect;

        public DialogueOption() { }
        internal DialogueOption(List<DialogueSegment> segments) => this.segments = segments;
    }

    [Serializable]
    public sealed class DiseaseEntry : CatalogEntry
    {
        [SerializeField] private string displayName;
        [SerializeField] private string recipeId;
        [SerializeField] private int order;
        [SerializeField] private SymptomEffect defaultSymptomEffect = SymptomEffect.None;
        [SerializeField] private string[] symptomMeanings = Array.Empty<string>();
        [SerializeField] private string[] docSymptoms = Array.Empty<string>();
        [SerializeField] private string[] lineExamples = Array.Empty<string>();
        [SerializeField] private string[] bannedWords = Array.Empty<string>();
        [SerializeField, TextArea] private string lore;
        [SerializeField] private Sprite docIllustration;
        [SerializeField] private List<DialogueOption> dialogue = new();

        public string DisplayName => displayName;
        public string RecipeId => recipeId;
        public int Order => order;
        public SymptomEffect DefaultSymptomEffect => defaultSymptomEffect;
        public IReadOnlyList<string> SymptomMeanings => symptomMeanings;
        public IReadOnlyList<string> DocSymptoms => docSymptoms;
        public IReadOnlyList<string> LineExamples => lineExamples;
        public IReadOnlyList<string> BannedWords => bannedWords;
        public string Lore => lore;
        public Sprite DocIllustration => docIllustration;
        public IReadOnlyList<DialogueOption> Dialogue => dialogue;
    }

    [Serializable]
    public sealed class RecipeEntry : CatalogEntry
    {
        public const int RequiredIngredientKinds = 3;

        [SerializeField] private string potionName;
        [SerializeField, TextArea] private string recipeText;
        [SerializeField] private List<string> ingredientVariantIds = new();

        public string PotionName => potionName;
        public string RecipeText => recipeText;
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
