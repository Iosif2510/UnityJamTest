using System;
using System.Collections.Generic;
using System.Linq;

namespace PotionShop.Data
{
    internal sealed class AppearanceBuckets
    {
        internal const int InkCount = 6;
        private readonly List<Candidate>[] buckets = Enumerable.Range(0, InkCount)
            .Select(_ => new List<Candidate>()).ToArray();
        private readonly List<Candidate>[] spawnBuckets;
        private readonly List<Candidate>[] availableSpawnBuckets;

        internal sealed class Candidate
        {
            internal CustomerProfile Customer { get; }
            internal PortraitPart[] Portrait { get; }
            internal BottleShape Bottle { get; }
            internal InkRule Ink { get; }

            internal Candidate(CustomerProfile customer, PortraitPart[] portrait, BottleShape bottle, InkRule ink)
            {
                Customer = customer;
                Portrait = portrait;
                Bottle = bottle;
                Ink = ink;
            }
        }

        internal AppearanceBuckets(IEnumerable<CustomerProfile> customers, PrescriptionRules rules,
            HashSet<string> spawnCustomerIds)
        {
            var slots = (PortraitSlot[])Enum.GetValues(typeof(PortraitSlot));
            foreach (var customer in customers.OrderBy(profile => profile.Id, StringComparer.Ordinal))
            {
                var bottle = OrderGenerator.ResolveBottle(rules, customer.Gender);
                var parts = slots.Select(slot => OrderGenerator.PartsFor(customer, slot)).ToArray();
                var portrait = new PortraitPart[slots.Length];
                AddCombinations(0);

                void AddCombinations(int index)
                {
                    if (index == parts.Length)
                    {
                        var ink = OrderGenerator.ResolveInk(rules, portrait);
                        buckets[ink.InkNumber - 1].Add(new Candidate(customer, (PortraitPart[])portrait.Clone(), bottle, ink));
                        return;
                    }

                    foreach (var part in parts[index])
                    {
                        portrait[index] = part;
                        AddCombinations(index + 1);
                    }
                }
            }

            for (int index = 0; index < buckets.Length; index++)
                if (buckets[index].Count == 0)
                    throw new InvalidOperationException($"Ink {index + 1} has no valid active customer appearance.");

            spawnBuckets = spawnCustomerIds == null ? buckets : buckets.Select(bucket =>
                bucket.Where(candidate => spawnCustomerIds.Contains(candidate.Customer.Id)).ToList()).ToArray();
            availableSpawnBuckets = spawnBuckets.Where(bucket => bucket.Count > 0).ToArray();
        }

        internal int Count(int inkNumber) => spawnBuckets[inkNumber - 1].Count;

        /// <summary>Every customer who can come in, in a stable order.</summary>
        internal IReadOnlyList<string> SpawnCustomerIds => availableSpawnBuckets.SelectMany(bucket => bucket)
            .Select(candidate => candidate.Customer.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();

        /// <summary>One of <paramref name="customerId"/>'s looks, any of them equally likely.</summary>
        internal Candidate ChooseFor(Random random, string customerId)
        {
            var looks = availableSpawnBuckets.SelectMany(bucket => bucket)
                .Where(candidate => candidate.Customer.Id == customerId).ToArray();
            if (looks.Length == 0) throw new InvalidOperationException($"{customerId} cannot come in.");
            return looks[random.Next(looks.Length)];
        }

        internal Candidate Choose(Random random)
        {
            var bucket = availableSpawnBuckets[random.Next(availableSpawnBuckets.Length)];
            return bucket[random.Next(bucket.Count)];
        }

#if UNITY_EDITOR
        internal Candidate ChooseFixed(Random random, string customerId, AppearanceValue? hair, AppearanceValue? eyes)
        {
            var matching = buckets.SelectMany(bucket => bucket).Where(candidate =>
                (customerId == null || candidate.Customer.Id == customerId) &&
                (!hair.HasValue || candidate.Portrait.Any(part => part.Slot == PortraitSlot.Hair && part.Appearance == hair.Value)) &&
                (!eyes.HasValue || candidate.Portrait.Any(part => part.Slot == PortraitSlot.Eyes && part.Appearance == eyes.Value)))
                .ToArray();
            if (matching.Length == 0)
                throw new InvalidOperationException($"No appearance matches the fixed test settings ({customerId}, {hair}, {eyes}).");
            return matching[random.Next(matching.Length)];
        }
#endif
    }
}
