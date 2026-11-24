using System;
using System.Collections.Generic;
using System.Linq;

namespace PotionShop.Data
{
    public sealed class GeneratedOrder
    {
        private readonly DialogueOption fallbackDialogue;
        private readonly IReadOnlyDictionary<string, string> fallbackReactions;
        private DialogueOption generatedDialogue;
        private IReadOnlyDictionary<string, string> generatedReactions;

        internal GeneratedOrder(int seed, string previousDiseaseId, CustomerProfile customer,
            PortraitPart[] portrait, DiseaseEntry disease, DialogueOption dialogue, RecipeEntry recipe,
            BottleShape bottle, InkRule ink, CharacterVoice voice, IReadOnlyDictionary<string, string> reactionLines)
        {
            Seed = seed;
            PreviousDiseaseId = previousDiseaseId;
            Customer = customer;
            Portrait = Array.AsReadOnly(portrait);
            Disease = disease;
            fallbackDialogue = dialogue;
            Recipe = recipe;
            Bottle = bottle;
            Ink = ink;
            Voice = voice;
            fallbackReactions = reactionLines;
        }

        public int Seed { get; }
        public string PreviousDiseaseId { get; }
        public CustomerProfile Customer { get; }
        public IReadOnlyList<PortraitPart> Portrait { get; }
        public DiseaseEntry Disease { get; }
        public DialogueOption Dialogue => generatedDialogue ?? fallbackDialogue;
        public RecipeEntry Recipe { get; }
        public BottleShape Bottle { get; }
        public InkRule Ink { get; }
        public CharacterVoice Voice { get; }
        public string GetReactionLine(string key)
        {
            if (generatedReactions != null && generatedReactions.TryGetValue(key, out var generated))
                return generated;
            return fallbackReactions != null && fallbackReactions.TryGetValue(key, out var fallback)
                ? fallback : string.Empty;
        }

        internal void SetGeneratedDialogue(DialogueOption dialogue, IReadOnlyDictionary<string, string> reactions)
        {
            generatedDialogue = dialogue;
            generatedReactions = reactions;
        }
    }

    public static class OrderGenerator
    {
        public static GeneratedOrder Generate(LoadedCatalog catalog, int seed, string previousDiseaseId = null)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var random = new Random(seed);
            return CreateOrder(catalog, seed, previousDiseaseId, random, catalog.Appearances.Choose(random));
        }

        /// <summary>An order from <paramref name="customerId"/>, in one of their looks.</summary>
        public static GeneratedOrder Generate(LoadedCatalog catalog, int seed, string previousDiseaseId, string customerId)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var random = new Random(seed);
            return CreateOrder(catalog, seed, previousDiseaseId, random, catalog.Appearances.ChooseFor(random, customerId));
        }

#if UNITY_EDITOR
        public static GeneratedOrder GenerateFixed(LoadedCatalog catalog, int seed, string previousDiseaseId,
            string customerId, AppearanceValue? hairColor, AppearanceValue? eyeColor)
        {
            var random = new Random(seed);
            return CreateOrder(catalog, seed, previousDiseaseId, random,
                catalog.Appearances.ChooseFixed(random, customerId, hairColor, eyeColor));
        }
#endif

        private static GeneratedOrder CreateOrder(LoadedCatalog catalog, int seed, string previousDiseaseId,
            Random random, AppearanceBuckets.Candidate candidate)
        {
            var profile = candidate.Customer;
            var diseases = catalog.Diseases.Values
                .Where(disease => catalog.Diseases.Count == 1 ||
                    !string.Equals(disease.Id, previousDiseaseId, StringComparison.Ordinal))
                .OrderBy(disease => disease.Id, StringComparer.Ordinal)
                .ToArray();
            var disease = Choose(diseases, random);
            var diseaseDialogue = catalog.GetDiseaseDialogue(disease);
            if (diseaseDialogue.Count == 0)
                throw new InvalidOperationException($"{disease.Id}: at least one dialogue option is required.");

            var recipe = catalog.Recipes[disease.RecipeId];
            var dialogueOverride = catalog.CustomerDialogue?.FirstOrDefault(d => d.customerId == profile.Id && d.diseaseId == disease.Id);
            var overrides = dialogueOverride?.dialogue.Where(d => catalog.ReactionLines == null ||
                FallbackLine.IsValidSymptom(d, disease)).ToArray();
            IReadOnlyList<DialogueOption> dialogues = overrides is { Length: > 0 } ? overrides : diseaseDialogue;
            catalog.Voices.TryGetValue(profile.Id, out var voice);
            var order = new GeneratedOrder(seed, previousDiseaseId, profile, candidate.Portrait, disease,
                Choose(dialogues, random), recipe, candidate.Bottle, candidate.Ink, voice, catalog.ReactionLines);
            // Lines written for this customer and disease replace the shared fallback ones when the table has them.
            catalog.DialogueTable?.Apply(order);
            return order;
        }

        public static PortraitPart[] PartsFor(CustomerProfile profile, PortraitSlot slot)
        {
            if (profile.FixedAppearance)
            {
                var values = slot switch
                {
                    PortraitSlot.Hair => profile.HairColors.Count == 0 ? new[] { AppearanceValue.None } : profile.HairColors.ToArray(),
                    PortraitSlot.Eyes => profile.EyeColors.ToArray(),
                    PortraitSlot.Mouth => new[] { profile.HasFang ? AppearanceValue.Fangs : AppearanceValue.Normal },
                    _ => new[] { profile.EarType }
                };
                if (values.Length == 0) throw new InvalidOperationException($"{profile.Id}: no values for {slot}");
                if (values.Distinct().Count() != values.Length)
                    throw new InvalidOperationException($"{profile.Id}: duplicate values for {slot}.");
                return values.Select(v => new PortraitPart(slot, v)).OrderBy(part => part.Id, StringComparer.Ordinal).ToArray();
            }
            var options = profile.Parts
                .Where(part => part != null && part.Slot == slot && part.Appearance != AppearanceValue.Unspecified)
                .OrderBy(part => part.Id, StringComparer.Ordinal)
                .ToArray();
            if (options.Length == 0)
                throw new InvalidOperationException($"{profile.Id}: no valid portrait options for {slot}.");
            return options;
        }

        public static BottleShape ResolveBottle(PrescriptionRules rules, CustomerGender gender)
        {
            if (gender != CustomerGender.Unspecified)
            {
                foreach (var rule in rules.Bottles)
                    if (rule != null && rule.Gender == gender && rule.Bottle != BottleShape.Unspecified)
                        return rule.Bottle;
            }
            throw new InvalidOperationException($"No bottle mapping for {gender}.");
        }

        public static InkRule ResolveInk(PrescriptionRules rules, IReadOnlyList<PortraitPart> portrait)
        {
            foreach (var rule in rules.OrderedInks)
            {
                if (rule != null && MatchesAll(rule.Conditions, portrait) &&
                    (rule.Exceptions.Count == 0 || !MatchesAll(rule.Exceptions, portrait)))
                {
                    if (rule.InkNumber < 1 || rule.InkNumber > 6)
                        throw new InvalidOperationException($"{rule.Id}: ink must be numbered 1 to 6.");
                    return rule;
                }
            }
            throw new InvalidOperationException("No ink rule matched the generated portrait.");
        }

        private static bool MatchesAll(IReadOnlyList<AppearanceCondition> conditions, IReadOnlyList<PortraitPart> portrait)
        {
            return conditions.All(condition => condition != null &&
                portrait.Any(part => part.Slot == condition.Slot && condition.AnyOf.Contains(part.Appearance)));
        }

        private static T Choose<T>(IReadOnlyList<T> choices, Random random) => choices[random.Next(choices.Count)];
    }
}
