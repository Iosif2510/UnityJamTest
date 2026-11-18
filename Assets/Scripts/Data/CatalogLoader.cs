using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace PotionShop.Data
{
    public sealed class LoadedCatalog
    {
        internal LoadedCatalog(
            Dictionary<string, CustomerProfile> customers,
            Dictionary<string, DiseaseEntry> diseases,
            Dictionary<string, RecipeEntry> recipes,
            Dictionary<string, IngredientKind> ingredientKinds,
            Dictionary<string, IngredientVariant> variants,
            Dictionary<string, IngredientKind> kindsByVariant,
            PrescriptionRules prescriptionRules,
            RoundTuning tuning)
        {
            Customers = new ReadOnlyDictionary<string, CustomerProfile>(customers);
            Diseases = new ReadOnlyDictionary<string, DiseaseEntry>(diseases);
            Recipes = new ReadOnlyDictionary<string, RecipeEntry>(recipes);
            IngredientKinds = new ReadOnlyDictionary<string, IngredientKind>(ingredientKinds);
            Variants = new ReadOnlyDictionary<string, IngredientVariant>(variants);
            KindsByVariant = new ReadOnlyDictionary<string, IngredientKind>(kindsByVariant);
            PrescriptionRules = prescriptionRules;
            Tuning = tuning;
        }

        public IReadOnlyDictionary<string, CustomerProfile> Customers { get; }
        public IReadOnlyDictionary<string, DiseaseEntry> Diseases { get; }
        public IReadOnlyDictionary<string, RecipeEntry> Recipes { get; }
        public IReadOnlyDictionary<string, IngredientKind> IngredientKinds { get; }
        public IReadOnlyDictionary<string, IngredientVariant> Variants { get; }
        public IReadOnlyDictionary<string, IngredientKind> KindsByVariant { get; }
        public PrescriptionRules PrescriptionRules { get; }
        public RoundTuning Tuning { get; }
    }

    public static class CatalogLoader
    {
        private static readonly Regex IdPattern = new(@"\A[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+\z");

        public static LoadedCatalog Load(PotionCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            if (catalog.Customers == null || catalog.DiseasesAndRecipes == null ||
                catalog.Ingredients == null || catalog.PrescriptionRules == null || catalog.Tuning == null)
            {
                throw new InvalidOperationException($"{catalog.name}: assign all five data assets.");
            }

            if (catalog.Tuning.TimeLimitSeconds <= 0 || catalog.Tuning.BonusIntervalSeconds <= 0)
                throw new InvalidOperationException($"{catalog.name}: time limit and bonus interval must be positive.");

            var customers = IndexActive(catalog.Customers.Profiles, "customer");
            var diseases = IndexActive(catalog.DiseasesAndRecipes.Diseases, "disease");
            var recipes = IndexActive(catalog.DiseasesAndRecipes.Recipes, "recipe");
            var kinds = IndexActive(catalog.Ingredients.Kinds, "kind");
            var variants = new Dictionary<string, IngredientVariant>(StringComparer.Ordinal);
            var kindsByVariant = new Dictionary<string, IngredientKind>(StringComparer.Ordinal);
            var variantIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kind in catalog.Ingredients.Kinds)
            {
                bool active = kind.Status == ContentStatus.Active;
                if (active && kind.Variants.Count != IngredientKind.RequiredVariants)
                {
                    throw new InvalidOperationException(
                        $"{kind.Id}: active ingredient kinds require exactly {IngredientKind.RequiredVariants} variants.");
                }

                foreach (var variant in kind.Variants)
                {
                    string id = variant?.Id;
                    RequireId(id, "ingredient", kind.Id);
                    if (!variantIds.Add(id))
                        throw new InvalidOperationException($"{kind.Id}: duplicate ingredient variant ID '{id}'.");

                    if (active)
                    {
                        variants.Add(id, variant);
                        kindsByVariant.Add(id, kind);
                    }
                }
            }

            foreach (var recipe in recipes.Values)
                CheckRecipe(recipe, kindsByVariant);

            foreach (var disease in diseases.Values)
            {
                if (string.IsNullOrEmpty(disease.RecipeId) || !recipes.ContainsKey(disease.RecipeId))
                    throw new InvalidOperationException($"{disease.Id}: missing active recipe '{disease.RecipeId}'.");
            }

            if (customers.Count == 0 || diseases.Count == 0)
                throw new InvalidOperationException($"{catalog.name}: at least one active customer and disease are required.");

            return new LoadedCatalog(customers, diseases, recipes, kinds, variants, kindsByVariant,
                catalog.PrescriptionRules, catalog.Tuning);
        }

        private static void CheckRecipe(RecipeEntry recipe, Dictionary<string, IngredientKind> kindsByVariant)
        {
            if (recipe.IngredientVariantIds.Count != RecipeEntry.RequiredIngredientKinds)
            {
                throw new InvalidOperationException(
                    $"{recipe.Id}: recipes require exactly {RecipeEntry.RequiredIngredientKinds} ingredients.");
            }

            var usedKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in recipe.IngredientVariantIds)
            {
                if (string.IsNullOrEmpty(id) || !kindsByVariant.TryGetValue(id, out var kind))
                    throw new InvalidOperationException($"{recipe.Id}: missing active ingredient variant '{id}'.");

                if (!usedKinds.Add(kind.Id))
                    throw new InvalidOperationException($"{recipe.Id}: ingredient kind '{kind.Id}' appears more than once.");
            }
        }

        private static Dictionary<string, T> IndexActive<T>(IReadOnlyList<T> entries, string prefix)
            where T : CatalogEntry
        {
            var allIds = new HashSet<string>(StringComparer.Ordinal);
            var active = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                string id = entry?.Id;
                RequireId(id, prefix, prefix);
                if (!allIds.Add(id))
                    throw new InvalidOperationException($"Duplicate {prefix} ID '{id}'.");

                if (entry.Status == ContentStatus.Active)
                    active.Add(id, entry);
            }

            return active;
        }

        private static void RequireId(string id, string prefix, string context)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix + ".", StringComparison.Ordinal) ||
                !IdPattern.IsMatch(id))
            {
                throw new InvalidOperationException($"{context}: invalid ID '{id}'; expected '{prefix}.<name>'.");
            }
        }
    }
}
