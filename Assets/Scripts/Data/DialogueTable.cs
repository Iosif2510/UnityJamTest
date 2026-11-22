using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PotionShop.Data
{
    /// <summary>
    /// Customer dialogue written ahead of time, one entry per customer and disease
    /// (Tools/DialogueTable). An order that has an entry speaks those lines; any other order keeps the
    /// shared fallback lines, so a missing or rejected entry never blocks a round.
    /// </summary>
    internal sealed class DialogueTable
    {
        private const int SchemaVersion = 1;
        private static readonly string[] ReactionKeys = FallbackLine.ReactionKinds.Keys
            .Where(key => key != "timeout").ToArray();

        private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

        internal int Count => entries.Count;

        /// <summary>Parses the table, keeping only entries that pass the same checks as the fallback lines.</summary>
        internal static DialogueTable Load(TextAsset asset, IReadOnlyDictionary<string, CustomerProfile> customers,
            IReadOnlyDictionary<string, DiseaseEntry> diseases)
        {
            var table = new DialogueTable();
            if (asset == null)
                return table;

            JObject root;
            try { root = JObject.Parse(asset.text); }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Dialogue table] {asset.name} is not valid JSON; using fallback lines. {exception.Message}");
                return table;
            }
            if ((int?)root["schemaVersion"] != SchemaVersion || root["entries"] is not JObject rows)
            {
                Debug.LogWarning($"[Dialogue table] {asset.name}: unsupported schema; using fallback lines.");
                return table;
            }

            int rejected = 0;
            foreach (var row in rows)
            {
                string[] id = row.Key.Split('|');
                if (id.Length != 2 || !customers.ContainsKey(id[0]) || !diseases.TryGetValue(id[1], out var disease) ||
                    row.Value is not JObject lines || !TryRead(lines, disease, out var entry))
                {
                    rejected++;
                    continue;
                }
                table.entries[row.Key] = entry;
            }
            if (rejected > 0)
                Debug.LogWarning($"[Dialogue table] {asset.name}: {rejected} entr{(rejected == 1 ? "y" : "ies")} rejected.");
            return table;
        }

        /// <summary>Gives the order its own lines when the table has them. Returns whether it did.</summary>
        internal bool Apply(GeneratedOrder order)
        {
            if (order == null || !entries.TryGetValue(order.Customer.Id + "|" + order.Disease.Id, out var entry))
                return false;
            order.SetGeneratedDialogue(entry.Symptoms, entry.Reactions);
            return true;
        }

        private static bool TryRead(JObject lines, DiseaseEntry disease, out Entry entry)
        {
            entry = default;
            if (!TryLine(lines, "symptom", out string symptom) ||
                !FallbackLine.TryParseSymptoms(symptom, out var dialogue) || !FallbackLine.IsValidSymptom(dialogue, disease))
                return false;

            var reactions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in ReactionKeys)
            {
                if (!TryLine(lines, key, out string line) || !FallbackLine.IsPlainText(line))
                    return false;
                reactions.Add(key, line);
            }
            entry = new Entry(dialogue, new ReadOnlyDictionary<string, string>(reactions));
            return true;
        }

        private static bool TryLine(JObject lines, string key, out string line)
        {
            line = lines[key]?.Type == JTokenType.String ? ((string)lines[key]).Trim() : null;
            return !string.IsNullOrEmpty(line) && !line.Any(char.IsControl);
        }

        private readonly struct Entry
        {
            internal Entry(DialogueOption symptoms, IReadOnlyDictionary<string, string> reactions)
            {
                Symptoms = symptoms;
                Reactions = reactions;
            }

            internal DialogueOption Symptoms { get; }
            internal IReadOnlyDictionary<string, string> Reactions { get; }
        }
    }
}
