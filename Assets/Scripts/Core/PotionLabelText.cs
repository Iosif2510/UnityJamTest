using System.Collections.Generic;
using System.Linq;
using System.Text;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// What the quill writes on the label, in English: the potion that was brewed (its English name from
    /// Resources/Crafting/potions_en.json, or "Curious Brew" when the ingredients match no known recipe) and who it
    /// is for, the customer's English name (Resources/Crafting/customers_en.json, romanized if missing).
    /// </summary>
    internal static class PotionLabelText
    {
        private const string Unknown = "Curious Brew";
        private static Dictionary<string, string> names, people;

        public static (string potion, string forWhom) For(GeneratedOrder order, IReadOnlyList<string> submitted)
        {
            string potion = Unknown;
            if (order?.Recipe != null && submitted != null &&
                submitted.OrderBy(s => s).SequenceEqual(order.Recipe.IngredientVariantIds.OrderBy(s => s)))
                potion = EnglishName(order.Recipe.Id) ?? Unknown;
            string name = order?.Voice?.DisplayName;
            if (string.IsNullOrEmpty(name)) return (potion, "");
            people ??= Table("Crafting/customers_en");
            return (potion, "for " + (people.TryGetValue(name, out var english) ? english : Romanize(name)));
        }

        private static string EnglishName(string recipeId)
        {
            names ??= Table("Crafting/potions_en");
            return recipeId != null && names.TryGetValue(recipeId, out var n) ? n : null;
        }

        /// <summary>A flat {"key": "value"} JSON file, one pair per line.</summary>
        private static Dictionary<string, string> Table(string resource)
        {
            var table = new Dictionary<string, string>();
            var json = Resources.Load<TextAsset>(resource);
            if (json == null) return table;
            foreach (var line in json.text.Split('\n'))
            {
                var parts = line.Split('"');
                if (parts.Length >= 5) table[parts[1]] = parts[3];
            }
            return table;
        }

        private static readonly string[] Initials =
            { "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" };
        private static readonly string[] Medials =
        {
            "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu",
            "eu", "ui", "i"
        };
        private static readonly string[] Finals =
        {
            "", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "l", "l", "l", "p", "l", "m", "p", "p", "t", "t",
            "ng", "t", "t", "k", "t", "p", "t"
        };

        /// <summary>Revised Romanization, syllable by syllable (no sound changes between syllables), capitalized.</summary>
        internal static string Romanize(string korean)
        {
            var sb = new StringBuilder();
            foreach (char c in korean)
            {
                if (c < '가' || c > '힣') { sb.Append(c); continue; }
                int code = c - 0xAC00;
                sb.Append(Initials[code / 588]).Append(Medials[code % 588 / 28]).Append(Finals[code % 28]);
            }
            var s = sb.ToString();
            return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
