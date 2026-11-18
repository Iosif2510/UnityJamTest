using System;
using System.Collections.Generic;
using System.Linq;
using PotionShop.Data;
using UnityEditor;
using UnityEngine;

namespace PotionShop.Editor
{
    internal static class CatalogValidationMenu
    {
        private const string DefaultPath = "Assets/GameData/PotionCatalog.asset";

        [MenuItem("Potion Shop/Data/Validate Catalog")]
        private static void ValidateCatalog()
        {
            var source = Selection.activeObject as PotionCatalog ??
                AssetDatabase.LoadAssetAtPath<PotionCatalog>(DefaultPath);
            if (source == null)
            {
                Debug.LogError("Select a PotionCatalog or create one at " + DefaultPath);
                return;
            }

            LoadedCatalog catalog;
            try
            {
                catalog = CatalogLoader.Load(source);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message, source);
                return;
            }

            int errors = 0;
            int warnings = 0;
            void Error(string text) { errors++; Debug.LogError(text, source); }
            void Warn(string text) { warnings++; Debug.LogWarning(text, source); }

            foreach (var disease in catalog.Diseases.Values)
            {
                var dialogue = catalog.GetDiseaseDialogue(disease);
                if (dialogue.Count == 0 || dialogue.Any(option =>
                    option == null || option.Segments.Count == 0 ||
                    option.Segments.Any(segment => segment == null || string.IsNullOrWhiteSpace(segment.Text)) ||
                    !option.Segments.Any(segment => segment.IsSymptom)))
                {
                    Error($"{disease.Id}: each disease needs dialogue with text and a symptom segment.");
                }
            }

            foreach (var variant in catalog.Variants.Values)
            {
                if (variant.Prefab == null)
                    Error($"{variant.Id}: assign a prefab for storage.");
                if (variant.BookletThumbnail == null)
                    Warn($"{variant.Id}: booklet thumbnail is not assigned.");
                if (variant.UsesPlaceholderArt)
                    Warn($"{variant.Id}: temporary model; replace before the MVP showcase.");
                if (variant.Prefab != null)
                {
                    var ingredient = variant.Prefab.GetComponent<WarehouseIngredient>();
                    if (ingredient == null || ingredient.VariantId != variant.Id) Error($"{variant.Id}: prefab WarehouseIngredient/ID mismatch.");
                    if (variant.Prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) Warn($"{variant.Id}: pocket preview does not support SkinnedMeshRenderer.");
                    if (variant.Prefab.GetComponentsInChildren<Collider>(true).Length == 0) Error($"{variant.Id}: no interaction colliders.");
                }
            }

            var tuning = catalog.Tuning;
            if (tuning.TimeLimitSeconds <= 0 || !(tuning.PickupDistance > 0f) ||
                !(tuning.BrewingSeconds >= 0f) || tuning.BonusIntervalSeconds <= 0 ||
                tuning.PointsPerIngredient < 0 || tuning.BottlePoints < 0 || tuning.InkPoints < 0 ||
                tuning.PointsPerBonusInterval < 0 || tuning.PassingStampThreshold < 0 ||
                tuning.PerfectStampThreshold < tuning.PassingStampThreshold)
            {
                Error("RoundTuning: invalid time, distance, points or stamp threshold.");
            }

            CheckPrescriptions(catalog, Error, Warn);
            if (source.FallbackDialogue != null)
                CheckDialogueLengths(source, Warn);
            Debug.Log($"Catalog validation: {errors} error(s), {warnings} warning(s).", source);
        }

        private static void CheckDialogueLengths(PotionCatalog source, Action<string> warn)
        {
            foreach (var line in source.FallbackDialogue.Lines)
            {
                var text = line.Text.Replace("<sym>", "").Replace("</sym>", "");
                int target = source.Tuning.LineLength;
                (int min, int max) = line.Kind switch
                {
                    FallbackKind.Symptom => (target * 8 / 10, target * 12 / 10),
                    FallbackKind.Receive => (30, 60),
                    _ => (40, 80)
                };
                if (text.Length < min || text.Length > max)
                    warn($"{line.Key}: {text.Length} characters including spaces. Target: {min}–{max}.");
            }
        }

        private static void CheckPrescriptions(LoadedCatalog catalog, Action<string> error, Action<string> warn)
        {
            var rules = catalog.PrescriptionRules;
            var bottleGenders = new HashSet<CustomerGender>();
            foreach (var mapping in rules.Bottles)
            {
                if (mapping == null || mapping.Gender == CustomerGender.Unspecified ||
                    mapping.Bottle == BottleShape.Unspecified || !bottleGenders.Add(mapping.Gender))
                    error("Bottle mappings must have unique genders and valid shapes.");
            }

            var inkIds = new HashSet<string>(StringComparer.Ordinal);
            bool invalidInk = false;
            var orderedInks = rules.OrderedInks.ToArray();
            var priorities = new HashSet<int>();
            bool usesExplicitPriority = orderedInks.Any(ink => ink?.Order > 0);
            for (int i = 0; i < orderedInks.Length; i++)
            {
                var ink = orderedInks[i];
                bool finalRule = i == orderedInks.Length - 1;
                if (ink == null || string.IsNullOrWhiteSpace(ink.Id) || !inkIds.Add(ink.Id) ||
                    (usesExplicitPriority && (ink.Order <= 0 || !priorities.Add(ink.Order))) ||
                    ink.InkNumber < 1 || ink.InkNumber > 6 || (ink.Conditions.Count == 0) != finalRule ||
                    (finalRule && ink.Exceptions.Count != 0) ||
                    ink.Conditions.Concat(ink.Exceptions).Any(condition => condition == null ||
                        condition.AnyOf.Count == 0 || condition.AnyOf.Contains(AppearanceValue.Unspecified)))
                {
                    invalidInk = true;
                    error($"Ink rule #{i + 1} has invalid data. Use one unconditional fallback at the end.");
                }
            }
            if (rules.Inks.Count == 0)
            {
                error("At least one ink rule and a fallback are required.");
                invalidInk = true;
            }

            var uses = new Dictionary<string, int>(StringComparer.Ordinal);
            var slots = (PortraitSlot[])Enum.GetValues(typeof(PortraitSlot));
            foreach (var profile in catalog.Customers.Values)
            {
                if (profile.BasePortrait == null)
                    warn($"{profile.Id}: base portrait is not assigned.");
                if (profile.FixedAppearance && profile.BasePortrait != null)
                {
                    if (profile.EyeClosed == null) warn($"{profile.Id}: closed-eye sprite missing.");
                    if (profile.Gender != CustomerGender.Genderless && profile.HairFront == null) warn($"{profile.Id}: front hair sprite missing.");
                    if (profile.BakedIris == null && (profile.IrisRegions.Count is < 1 or > 2 ||
                        profile.IrisRegions.Any(r => r.width <= 0 || r.height <= 0 || r.xMin < 0 ||
                            r.yMin < 0 || r.xMax > 1 || r.yMax > 1)))
                        error($"{profile.Id}: specify 1-2 iris rectangles within 0..1.");
                    foreach (var sprite in new[] {profile.BasePortrait, profile.EyeClosed, profile.HairFront, profile.HairBack}.Where(s => s != null))
                        if (sprite.rect.width != 1024 || sprite.rect.height != 1536) warn($"{profile.Id}: {sprite.name} must use 1024x1536 canvas.");
                }
                if (profile.Parts.Any(part => part == null ||
                    (part.Appearance != AppearanceValue.None && part.Sprite == null)))
                    warn($"{profile.Id}: one or more portrait parts lack a sprite.");

                try
                {
                    OrderGenerator.ResolveBottle(rules, profile.Gender);
                    var parts = slots.Select(slot => OrderGenerator.PartsFor(profile, slot)).ToArray();
                    if (!invalidInk)
                        CountInks(rules, parts, new PortraitPart[slots.Length], 0, uses);
                }
                catch (InvalidOperationException exception)
                {
                    error($"{profile.Id}: {exception.Message}");
                }
            }

            if (!invalidInk)
            {
                foreach (var rule in rules.Inks)
                {
                    uses.TryGetValue(rule.Id, out int count);
                    if (count == 0)
                        warn($"{rule.Id}: no active portrait receives this rule.");
                    else
                        Debug.Log($"{rule.Id}: {count} active portrait combination(s).");
                }
            }
        }

        private static void CountInks(PrescriptionRules rules, PortraitPart[][] parts, PortraitPart[] portrait,
            int slot, Dictionary<string, int> uses)
        {
            if (slot == parts.Length)
            {
                string id = OrderGenerator.ResolveInk(rules, portrait).Id;
                uses.TryGetValue(id, out int count);
                uses[id] = count + 1;
                return;
            }

            foreach (var part in parts[slot])
            {
                portrait[slot] = part;
                CountInks(rules, parts, portrait, slot + 1, uses);
            }
        }
    }
}
