using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PotionShop.Data;

namespace PotionShop
{
    public enum DialogueRevealMode { Word, Sentence, Character }

    internal sealed class DialogueTimeline
    {
        internal readonly struct Cue
        {
            internal readonly double Start;
            internal readonly SymptomEffect Effect;
            internal readonly int PhaseIndex;
            internal readonly bool EchoTail;
            internal readonly DialogueAccent Accent;
            internal readonly bool LastAccent;

            internal Cue(double start, SymptomEffect effect, int phaseIndex, bool echoTail,
                DialogueAccent accent = DialogueAccent.None, bool lastAccent = false)
            {
                Start = start;
                Effect = effect;
                PhaseIndex = phaseIndex;
                EchoTail = echoTail;
                Accent = accent;
                LastAccent = lastAccent;
            }
        }

        private static readonly Regex EchoLetters = new("[^가-힣A-Za-z0-9]");
        private readonly Dictionary<int, Cue> glyphs = new();
        internal DialogueLineLayout Layout { private get; set; }
        internal string Text { get; }
        internal string DisplayText => Layout?.Text ?? Text;
        internal double Duration { get; }
        internal bool HasGlyphCopies { get; }
        internal bool HasDoubleEcho { get; }
        internal bool HasDrops { get; }
        internal bool HasEffects { get; }
        internal double LastAccentEnd { get; }
        internal IReadOnlyList<double> AccentTimes { get; }

        internal DialogueTimeline(DialogueOption line, SymptomEffect inheritedEffect, int speed, DialogueRevealMode mode)
            : this(line.Segments, line.DefaultSymptomEffect == SymptomEffect.Inherit ? inheritedEffect :
                line.DefaultSymptomEffect, speed, mode) { }

        internal DialogueTimeline(string line, string reactionKey, int speed, DialogueRevealMode mode)
            : this(new[] { new DialogueSegment(line, false) }, SymptomEffect.None, speed, mode, reactionKey) { }

        private DialogueTimeline(IReadOnlyList<DialogueSegment> segments, SymptomEffect inheritedEffect,
            int speed, DialogueRevealMode mode, string reactionKey = null)
        {
            var text = string.Concat(segments.Select(s => s.Text));
            var flags = segments.SelectMany(s => Enumerable.Repeat(s.IsSymptom, s.Text.Length)).ToArray();
            var starts = new double[text.Length];
            Duration = new StringInfo(text).LengthInTextElements / (double)Math.Max(1, speed);
            AssignTimes(text, flags, starts, Duration, mode);
            var accented = new bool[text.Length];
            var (accent, lastAccent) = DialogueEmphasis.Select(text, flags, starts, reactionKey, accented);
            var beats = new List<double>();
            for (int i = 0; i < accented.Length; i++)
                if (accented[i] && (i == 0 || !accented[i - 1])) beats.Add(starts[i]);
            AccentTimes = beats;
            int lastAccentEnd = lastAccent;
            while (lastAccentEnd >= 0 && lastAccentEnd < text.Length && accented[lastAccentEnd]) lastAccentEnd++;
            LastAccentEnd = lastAccent < 0 ? 0 : starts[lastAccentEnd - 1] + DialogueEmphasis.PulseSeconds;
            var rich = new StringBuilder();
            int source = 0;
            foreach (var segment in segments)
            {
                var effect = !segment.IsSymptom ? SymptomEffect.None :
                    segment.Effect == SymptomEffect.Inherit ? inheritedEffect : segment.Effect;
                HasGlyphCopies |= effect is SymptomEffect.ShadowLag or SymptomEffect.Echo or
                    SymptomEffect.MirrorDoze or SymptomEffect.Frost;
                HasDoubleEcho |= effect == SymptomEffect.Echo;
                HasDrops |= effect == SymptomEffect.RainDrip;
                HasEffects |= effect is not (SymptomEffect.None or SymptomEffect.Inherit);

                int phase = 0;
                if (segment.IsSymptom) rich.Append("<b>");
                foreach (char ch in segment.Text)
                {
                    glyphs.Add(rich.Length, new Cue(starts[source], effect, phase++, false,
                        accented[source] ? accent : DialogueAccent.None, source >= lastAccent && source < lastAccentEnd));
                    source++;
                    rich.Append(ch == '<' ? '‹' : ch == '>' ? '›' : ch);
                }
                if (segment.IsSymptom) rich.Append("</b>");
                if (effect == SymptomEffect.Echo && segment.Text.Length > 0)
                {
                    var words = segment.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    var tail = words.Length == 0 ? "" : EchoLetters.Replace(words[^1], "");
                    if (tail.Length > 0)
                    {
                        rich.Append("<nobr><size=55%>");
                        foreach (char ch in "…" + tail.Substring(Math.Max(0, tail.Length - 2)))
                        {
                            glyphs.Add(rich.Length, new Cue(starts[source - 1], SymptomEffect.Echo, 0, true));
                            rich.Append(ch);
                        }
                        rich.Append("</size></nobr>");
                    }
                }
            }
            Text = rich.ToString();
        }

        internal bool TryGetCue(int displayIndex, out Cue cue) =>
            glyphs.TryGetValue(Layout?.SourceIndex(displayIndex) ?? displayIndex, out cue);

        private static void AssignTimes(string text, bool[] flags, double[] starts, double duration, DialogueRevealMode mode)
        {
            var words = new List<(int Start, int End, double Read, double Pause)>();
            for (int i = 0; i < text.Length;)
            {
                int start = i;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                int letters = 0;
                bool symptom = false;
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    symptom |= flags[i];
                    if (char.IsLetterOrDigit(text[i])) letters++;
                    i++;
                }
                string word = text[start..i].Trim().TrimEnd('"', '\'', '”', '’', ')');
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                double pause = word.EndsWith("...") || word.EndsWith("…") ? 3.2 :
                    word.EndsWith(".") || word.EndsWith("?") || word.EndsWith("!") ? 2.4 :
                    word.EndsWith(",") ? 1.3 : 0;
                words.Add((start, i, (0.9 + letters) * (symptom ? 1.25 : 1), pause));
            }
            double total = words.Sum(w => w.Read + w.Pause);
            if (total <= 0) return;
            double time = 0;
            double sentence = 0;
            foreach (var word in words)
            {
                for (int index = word.Start; index < word.End; index++)
                    starts[index] = duration / total * (mode switch
                    {
                        DialogueRevealMode.Sentence => sentence,
                        DialogueRevealMode.Character => time + word.Read * (index - word.Start) / (word.End - word.Start),
                        _ => time
                    });
                time += word.Read + word.Pause;
                if (word.Pause >= 2.4) sentence = time;
            }
        }
    }
}
