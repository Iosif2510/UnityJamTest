using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PotionShop
{
    internal enum DialogueAccent { None, Symptom, Relief, Concern, Alarm }

    internal static class DialogueEmphasis
    {
        internal const float PulseSeconds = .62f;
        private static readonly Regex Words = new(@"[^\s.!?…！？。]+[.!?…！？。]*");

        internal static (DialogueAccent Accent, int Last) Select(string text, bool[] symptoms, double[] starts,
            string reactionKey, bool[] selected)
        {
            var (accent, keywords) = ReactionStyle(reactionKey);
            var candidates = new List<(int Start, int Length, int Score)>();
            foreach (Match match in Words.Matches(text))
            {
                string word = match.Value;
                int letters = 0;
                bool symptom = false;
                for (int i = 0; i < word.Length; i++)
                {
                    if (char.IsLetterOrDigit(word[i])) letters++;
                    symptom |= symptoms[match.Index + i];
                }
                if (letters == 0) continue;

                int score = Math.Min(letters, 7) + (symptom ? 12 : 0);
                if (word.Contains('!') || word.Contains('?')) score += 4;
                foreach (string keyword in keywords)
                {
                    if (!word.Contains(keyword, StringComparison.Ordinal)) continue;
                    score += 20;
                    break;
                }
                candidates.Add((match.Index, match.Length, score));
            }

            candidates.Sort((left, right) => left.Score == right.Score ? left.Start.CompareTo(right.Start) :
                right.Score.CompareTo(left.Score));
            if (candidates.Count == 0) return (accent, -1);

            var first = candidates[0];
            Array.Fill(selected, true, first.Start, first.Length);
            int last = first.Start;
            if (reactionKey == null && text.Length >= 10)
            {
                for (int i = 1; i < candidates.Count; i++)
                {
                    var next = candidates[i];
                    if (Math.Abs(starts[first.Start] - starts[next.Start]) < .35) continue;
                    Array.Fill(selected, true, next.Start, next.Length);
                    last = Math.Max(last, next.Start);
                    break;
                }
            }

            return (accent, last);
        }

        private static (DialogueAccent Accent, string[] Keywords) ReactionStyle(string key) => key switch
        {
            "rcv_ok" => (DialogueAccent.Relief, new[] { "예쁘", "고마", "멋지", "맞", "좋" }),
            "aft_cured" => (DialogueAccent.Relief, new[] { "나았", "낫", "회복", "고마", "살겠" }),
            "aft_cured_ink" => (DialogueAccent.Concern, new[] { "잉크", "라벨", "글씨", "읽", "나았" }),
            "rcv_bottle" or "rcv_ink" or "rcv_both" => (DialogueAccent.Concern,
                new[] { "이상", "글자", "글씨", "읽", "취향", "틀", "다르", "못" }),
            "aft_odd" => (DialogueAccent.Alarm, new[] { "이상", "색", "손이", "왜" }),
            "aft_slime" => (DialogueAccent.Alarm, new[] { "슬라임", "물컹", "물렁", "돌려" }),
            "timeout" => (DialogueAccent.Alarm, new[] { "기다리", "시간", "늦", "다른", "못" }),
            null => (DialogueAccent.Symptom, Array.Empty<string>()),
            _ => (DialogueAccent.Concern, Array.Empty<string>())
        };
    }
}
