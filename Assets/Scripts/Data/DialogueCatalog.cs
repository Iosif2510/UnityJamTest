using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PotionShop.Data
{
    public enum FallbackKind { Symptom, Receive, AfterDrinking, Timeout }

    [Serializable]
    public sealed class FallbackLine
    {
        private static readonly Regex SymptomTags = new("<sym>([^<>]+)</sym>");

        [SerializeField] private string key;
        [SerializeField] private FallbackKind kind;
        [SerializeField] private string diseaseId;
        [SerializeField] private string conditionText;
        [SerializeField] private string targetLength;
        [SerializeField, TextArea(3, 8)] private string text;

        public string Key => key;
        public FallbackKind Kind => kind;
        public string DiseaseId => diseaseId;
        public string ConditionText => conditionText;
        public string TargetLength => targetLength;
        public string Text => text;

        // Keeps the existing order scene usable while reaction dialogue is migrated separately.
        internal DialogueOption CreateSymptomDialogue()
        {
            var segments = new List<DialogueSegment>();
            int start = 0;
            var matches = SymptomTags.Matches(text ?? "");
            if (matches.Count != 3 || SymptomTags.Replace(text, "").IndexOfAny(new[] { '<', '>' }) >= 0)
                throw new InvalidOperationException($"{key}: expected exactly three valid symptom spans.");

            foreach (Match match in matches)
            {
                if (match.Index > start)
                    segments.Add(new DialogueSegment(text.Substring(start, match.Index - start), false));
                segments.Add(new DialogueSegment(match.Groups[1].Value, true));
                start = match.Index + match.Length;
            }
            if (start < text.Length)
                segments.Add(new DialogueSegment(text.Substring(start), false));

            return new DialogueOption(segments);
        }
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Fallback Dialogue")]
    public sealed class DialogueCatalog : ScriptableObject
    {
        [SerializeField] private List<FallbackLine> lines = new();

        public IReadOnlyList<FallbackLine> Lines => lines;
    }
}
