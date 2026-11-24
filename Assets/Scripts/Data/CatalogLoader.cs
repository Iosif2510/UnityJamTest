using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            RoundTuning tuning,
            HashSet<string> spawnCustomerIds)
        {
            Customers = new ReadOnlyDictionary<string, CustomerProfile>(customers);
            Diseases = new ReadOnlyDictionary<string, DiseaseEntry>(diseases);
            Recipes = new ReadOnlyDictionary<string, RecipeEntry>(recipes);
            IngredientKinds = new ReadOnlyDictionary<string, IngredientKind>(ingredientKinds);
            Variants = new ReadOnlyDictionary<string, IngredientVariant>(variants);
            KindsByVariant = new ReadOnlyDictionary<string, IngredientKind>(kindsByVariant);
            PrescriptionRules = prescriptionRules;
            Tuning = tuning;
            Appearances = new AppearanceBuckets(customers.Values, prescriptionRules, spawnCustomerIds);
        }

        public IReadOnlyDictionary<string, CustomerProfile> Customers { get; }
        public IReadOnlyDictionary<string, DiseaseEntry> Diseases { get; }
        public IReadOnlyDictionary<string, RecipeEntry> Recipes { get; }
        public IReadOnlyDictionary<string, IngredientKind> IngredientKinds { get; }
        public IReadOnlyDictionary<string, IngredientVariant> Variants { get; }
        public IReadOnlyDictionary<string, IngredientKind> KindsByVariant { get; }
        public PrescriptionRules PrescriptionRules { get; }
        public RoundTuning Tuning { get; }
        internal AppearanceBuckets Appearances { get; }
        public int AppearanceCountForInk(int inkNumber) => Appearances.Count(inkNumber);
        /// <summary>Every customer who can come in.</summary>
        public IReadOnlyList<string> SpawnCustomerIds => Appearances.SpawnCustomerIds;
        public IReadOnlyList<CustomerDialogueOverride> CustomerDialogue { get; internal set; }
        internal Dictionary<string, DialogueOption> SymptomFallbacks { get; } = new(StringComparer.Ordinal);
        internal IReadOnlyDictionary<string, string> ReactionLines { get; set; }
        internal Dictionary<string, CharacterVoice> Voices { get; } = new(StringComparer.Ordinal);
        internal DialogueTable DialogueTable { get; set; }

        public IReadOnlyList<DialogueOption> GetDiseaseDialogue(DiseaseEntry disease)
        {
            return SymptomFallbacks.TryGetValue(disease.Id, out var fallback)
                ? new[] { fallback }
                : disease.Dialogue;
        }
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

            if (catalog.Tuning.TimeLimitSeconds <= 0 ||
                !(catalog.Tuning.TimeBonusMultiplier >= 0d) || double.IsInfinity(catalog.Tuning.TimeBonusMultiplier) ||
                catalog.Tuning.TextSpeed <= 0 || catalog.Tuning.LineLength <= 0 ||
                !(catalog.Tuning.ReactionGap >= 0f) || float.IsInfinity(catalog.Tuning.ReactionGap))
                throw new InvalidOperationException($"{catalog.name}: check the round and dialogue timing values.");

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

            var dialogueKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dialogue in catalog.DiseasesAndRecipes.CustomerDialogue)
            {
                if (dialogue == null || !customers.ContainsKey(dialogue.customerId ?? "") || !diseases.ContainsKey(dialogue.diseaseId ?? "") ||
                    !dialogueKeys.Add(dialogue.customerId + "/" + dialogue.diseaseId) || dialogue.dialogue == null || dialogue.dialogue.Count == 0)
                    throw new InvalidOperationException("Invalid/duplicate customer dialogue reference.");
            }
            var spawnCustomerIds = ResolveSpawnCustomerIds(catalog.Customers, customers);
            var loaded = new LoadedCatalog(customers, diseases, recipes, kinds, variants, kindsByVariant,
                catalog.PrescriptionRules, catalog.Tuning, spawnCustomerIds)
            { CustomerDialogue = catalog.DiseasesAndRecipes.CustomerDialogue };
            if (catalog.CharacterVoices != null)
                foreach (var voice in catalog.CharacterVoices.Voices)
                    if (voice == null || string.IsNullOrWhiteSpace(voice.Code) ||
                        string.IsNullOrWhiteSpace(voice.DisplayName) || !loaded.Voices.TryAdd(voice.Code, voice))
                        throw new InvalidOperationException("Invalid or duplicate character voice.");
            if (catalog.FallbackDialogue != null)
            {
                var reactionLines = new Dictionary<string, string>(StringComparer.Ordinal);
                var diseaseIds = catalog.DiseasesAndRecipes.Diseases.Select(disease => disease.Id)
                    .ToHashSet(StringComparer.Ordinal);
                var fallbackIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var line in catalog.FallbackDialogue.Lines)
                {
                    if (line == null)
                        throw new InvalidOperationException("Missing fallback dialogue row.");
                    if (line.Kind != FallbackKind.Symptom)
                    {
                        if (!FallbackLine.ReactionKinds.TryGetValue(line.Key ?? "", out var kind) || line.Kind != kind ||
                            !FallbackLine.IsPlainText(line.Text) || !reactionLines.TryAdd(line.Key, line.Text))
                            throw new InvalidOperationException($"Invalid or duplicate reaction fallback: {line.Key}.");
                        continue;
                    }
                    if (!diseaseIds.Contains(line.DiseaseId) || line.Key != "sym_" + line.DiseaseId ||
                        !fallbackIds.Add(line.DiseaseId))
                        throw new InvalidOperationException($"Invalid/duplicate symptom fallback: {line.Key}.");
                    if (diseases.TryGetValue(line.DiseaseId, out var disease))
                    {
                        var dialogue = line.CreateSymptomDialogue();
                        if (!FallbackLine.IsValidSymptom(dialogue, disease))
                            throw new InvalidOperationException($"Invalid symptom fallback: {line.Key}.");
                        loaded.SymptomFallbacks.Add(line.DiseaseId, dialogue);
                    }
                }
                if (reactionLines.Count != FallbackLine.ReactionKinds.Count || loaded.SymptomFallbacks.Count != diseases.Count)
                    throw new InvalidOperationException("Assign all reaction fallbacks and a symptom fallback for each active disease.");
                loaded.ReactionLines = new ReadOnlyDictionary<string, string>(reactionLines);
            }
            if (diseases.Keys.Any(id => id.StartsWith("DIS_", StringComparison.Ordinal)) &&
                (loaded.ReactionLines == null || customers.Keys.Any(id => !loaded.Voices.ContainsKey(id))))
                throw new InvalidOperationException("MVP requires reaction fallbacks and a voice for every active customer.");
            loaded.DialogueTable = DialogueTable.Load(catalog.DialogueTable, loaded.Customers, loaded.Diseases);
            return loaded;
        }

        private static HashSet<string> ResolveSpawnCustomerIds(CustomerCatalog catalog,
            Dictionary<string, CustomerProfile> activeCustomers)
        {
            if (catalog.AllowAllCustomers)
                return null;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in catalog.SpawnPool)
            {
                if (string.IsNullOrWhiteSpace(id) || !activeCustomers.ContainsKey(id))
                    throw new InvalidOperationException($"{catalog.name}: Spawn Pool entry '{id}' must be an active customer ID.");
                ids.Add(id);
            }

            if (ids.Count == 0)
                throw new InvalidOperationException($"{catalog.name}: add at least one customer to Spawn Pool or enable Allow All Customers.");

            return ids;
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
                if (prefix == "customer" && entry is CustomerProfile profile && profile.FixedAppearance)
                {
                    if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, @"\A(?:(?:HUM|ELF|BST)_[MF][1-3]|GOL_G[1-4])\z"))
                        throw new InvalidOperationException($"Invalid customer code: {id}");
                    if (profile.Status == ContentStatus.Active && profile.EyeColors.Count == 0)
                        throw new InvalidOperationException($"{id}: eye colors are required.");
                }
                else RequireId(id, prefix, prefix);
                if (!allIds.Add(id))
                    throw new InvalidOperationException($"Duplicate {prefix} ID '{id}'.");

                if (entry.Status == ContentStatus.Active)
                    active.Add(id, entry);
            }

            return active;
        }

        private static void RequireId(string id, string prefix, string context)
        {
            string mvpPattern = prefix switch
            {
                "kind" => @"\A[A-F]\z",
                "ingredient" => @"\A[A-F][1-3]\z",
                "disease" => @"\ADIS_0[1-6]\z",
                "recipe" => @"\APOT_0[1-6]\z",
                _ => null
            };
            if (id != null && mvpPattern != null && Regex.IsMatch(id, mvpPattern))
                return;
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix + ".", StringComparison.Ordinal) ||
                !IdPattern.IsMatch(id))
            {
                throw new InvalidOperationException($"{context}: invalid {prefix} ID '{id}'.");
            }
        }
    }
}
