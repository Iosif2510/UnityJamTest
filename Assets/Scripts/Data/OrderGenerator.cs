using System;
using System.Collections.Generic;
using System.Linq;

namespace PotionShop.Data
{
    public sealed class GeneratedOrder
    {
        internal GeneratedOrder(int seed, string previousDiseaseId, CustomerProfile customer,
            PortraitPart[] portrait, DiseaseEntry disease, DialogueOption dialogue, RecipeEntry recipe,
            BottleShape bottle, InkRule ink)
        {
            Seed = seed;
            PreviousDiseaseId = previousDiseaseId;
            Customer = customer;
            Portrait = Array.AsReadOnly(portrait);
            Disease = disease;
            Dialogue = dialogue;
            Recipe = recipe;
            Bottle = bottle;
            Ink = ink;
        }

        public int Seed { get; }
        public string PreviousDiseaseId { get; }
        public CustomerProfile Customer { get; }
        public IReadOnlyList<PortraitPart> Portrait { get; }
        public DiseaseEntry Disease { get; }
        public DialogueOption Dialogue { get; }
        public RecipeEntry Recipe { get; }
        public BottleShape Bottle { get; }
        public InkRule Ink { get; }
    }

    public static class OrderGenerator
    {
        public static GeneratedOrder Generate(LoadedCatalog catalog, int seed, string previousDiseaseId = null)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var random = new Random(seed);
            var profilesBySpecies = catalog.Customers.Values
                .GroupBy(customer => customer.SpeciesId, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToArray();
            var profiles = Choose(profilesBySpecies, random)
                .OrderBy(customer => customer.Id, StringComparer.Ordinal)
                .ToArray();
            var profile = Choose(profiles, random);
            var portrait = Enum.GetValues(typeof(PortraitSlot)).Cast<PortraitSlot>()
                .Select(slot => Choose(PartsFor(profile, slot), random))
                .ToArray();

            var diseases = catalog.Diseases.Values
                .Where(disease => catalog.Diseases.Count == 1 ||
                    !string.Equals(disease.Id, previousDiseaseId, StringComparison.Ordinal))
                .OrderBy(disease => disease.Id, StringComparer.Ordinal)
                .ToArray();
            var disease = Choose(diseases, random);
            if (disease.Dialogue.Count == 0)
                throw new InvalidOperationException($"{disease.Id}: at least one dialogue option is required.");

            var recipe = catalog.Recipes[disease.RecipeId];
            var bottle = ResolveBottle(catalog.PrescriptionRules, profile.Gender);
            var ink = ResolveInk(catalog.PrescriptionRules, portrait);
            return new GeneratedOrder(seed, previousDiseaseId, profile, portrait, disease,
                Choose(disease.Dialogue, random), recipe, bottle, ink);
        }

        public static PortraitPart[] PartsFor(CustomerProfile profile, PortraitSlot slot)
        {
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
            foreach (var rule in rules.Inks)
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
