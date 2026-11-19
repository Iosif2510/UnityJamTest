using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;

namespace PotionShop
{
    internal sealed class DialogueLineLayout
    {
        private readonly int[] sourceIndices;
        internal string Text { get; }

        private DialogueLineLayout(string source, List<(int Index, int Length)> breaks)
        {
            if (breaks.Count == 0)
            {
                Text = source;
                return;
            }

            var text = new StringBuilder(source.Length + breaks.Count);
            var indices = new List<int>(source.Length + breaks.Count);
            int nextBreak = 0;
            for (int index = 0; index < source.Length;)
            {
                if (nextBreak < breaks.Count && breaks[nextBreak].Index == index)
                {
                    text.Append('\n');
                    indices.Add(-1);
                    index += breaks[nextBreak++].Length;
                    continue;
                }

                text.Append(source[index]);
                indices.Add(index++);
            }

            Text = text.ToString();
            sourceIndices = indices.ToArray();
        }

        internal int SourceIndex(int displayIndex) => sourceIndices == null ? displayIndex :
            (uint)displayIndex < (uint)sourceIndices.Length ? sourceIndices[displayIndex] : -1;

        internal static DialogueLineLayout Create(string source, TMP_Text body, float width)
        {
            // Measure the styled glyphs in one pass. Keep TMP wrapping disabled for the final layout too.
            var info = body.GetTextInfo(source);
            var glyphs = info.characterInfo;
            var protectedGlyphs = ProtectNoBreakSpans(source, info);
            var breaks = new List<(int Index, int Length)>();
            int paragraphStart = 0;
            for (int end = 0; end <= info.characterCount; end++)
            {
                if (end < info.characterCount && !IsNewline(glyphs[end].character))
                    continue;

                WrapParagraph(paragraphStart, end);
                paragraphStart = end + 1;
            }

            return new DialogueLineLayout(source, breaks);

            void WrapParagraph(int start, int end)
            {
                var words = new List<(int Start, int End)>();
                for (int cursor = start; cursor < end;)
                {
                    if (char.IsWhiteSpace(glyphs[cursor].character))
                    {
                        cursor++;
                        continue;
                    }

                    int wordEnd = FindWordEnd(glyphs, protectedGlyphs, cursor, end);
                    while (Width(cursor, wordEnd) > width)
                    {
                        int split = FindLongWordSplit(cursor, wordEnd);
                        if (split == wordEnd)
                            break;

                        words.Add((cursor, split));
                        cursor = split;
                    }

                    words.Add((cursor, wordEnd));
                    cursor = wordEnd;
                }

                if (words.Count < 2)
                    return;

                var lines = new List<int> { 0 };
                for (int i = 1; i < words.Count; i++)
                {
                    if (Width(words[lines[^1]].Start, words[i].End) > width)
                        lines.Add(i);
                }

                if (lines.Count > 1)
                {
                    int last = lines[^1];
                    var finalWord = words[last];
                    if (last == words.Count - 1 && last - lines[^2] > 1 &&
                        IsSingleLetter(glyphs, finalWord.Start, finalWord.End) &&
                        Width(words[last - 1].Start, finalWord.End) <= width)
                        lines[^1]--;
                }

                for (int row = 1; row < lines.Count; row++)
                {
                    int next = words[lines[row]].Start;
                    int previousEnd = words[lines[row] - 1].End;
                    int index = glyphs[next].index;
                    int length = 0;
                    if (previousEnd < next)
                    {
                        index = glyphs[previousEnd].index;
                        while (index + length < source.Length && char.IsWhiteSpace(source[index + length]) &&
                               !IsNewline(source[index + length]))
                            length++;
                    }
                    breaks.Add((index, length));
                }
            }

            float Width(int start, int end) => glyphs[end - 1].xAdvance - glyphs[start].origin;

            int FindLongWordSplit(int start, int end)
            {
                int best = end;
                for (int i = start + 1; i < end; i++)
                {
                    if (protectedGlyphs[i] || !CanSplit(glyphs, i))
                        continue;
                    if (Width(start, i) > width)
                        return best == end ? i : best;
                    best = i;
                }
                return best;
            }
        }

        private static bool[] ProtectNoBreakSpans(string source, TMP_TextInfo info)
        {
            const string openTag = "<nobr>";
            const string closeTag = "</nobr>";
            var result = new bool[info.characterCount];
            int start = source.IndexOf(openTag, StringComparison.Ordinal);
            int glyph = 0;
            while (start >= 0)
            {
                int end = source.IndexOf(closeTag, start + openTag.Length, StringComparison.Ordinal);
                if (end < 0) end = source.Length;
                for (; glyph < info.characterCount && info.characterInfo[glyph].index < end; glyph++)
                    result[glyph] = info.characterInfo[glyph].index > start;
                start = source.IndexOf(openTag, end, StringComparison.Ordinal);
            }
            return result;
        }

        private static int FindWordEnd(TMP_CharacterInfo[] glyphs, bool[] protectedGlyphs, int start, int end)
        {
            for (int i = start + 1; i < end; i++)
            {
                if (protectedGlyphs[i]) continue;
                char current = glyphs[i].character;
                char previous = glyphs[i - 1].character;
                if (char.IsWhiteSpace(current))
                {
                    int afterSpace = i + 1;
                    while (afterSpace < end && char.IsWhiteSpace(glyphs[afterSpace].character))
                        afterSpace++;
                    if (afterSpace == end || (!IsClosing(glyphs[afterSpace].character) && !IsOpening(previous)))
                        return i;
                    i = afterSpace;
                    continue;
                }

                bool decimalPoint = previous == '.' && i > start + 1 &&
                    char.IsDigit(glyphs[i - 2].character) && char.IsDigit(current);
                if (!decimalPoint && !IsClosing(current) &&
                    (IsSentenceEnd(previous) || (IsClosing(previous) && EndsSentence(glyphs, start, i - 1))))
                    return i;
            }

            return end;
        }

        private static bool EndsSentence(TMP_CharacterInfo[] glyphs, int start, int end)
        {
            for (int i = end; i >= start && IsClosing(glyphs[i].character); i--)
            {
                if (IsSentenceEnd(glyphs[i].character))
                    return true;
            }
            return false;
        }

        private static bool IsSingleLetter(TMP_CharacterInfo[] glyphs, int start, int end)
        {
            int letters = 0;
            for (int i = start; i < end; i++)
            {
                if (char.IsLetterOrDigit(glyphs[i].character) && ++letters > 1)
                    return false;
            }
            return letters == 1;
        }

        private static bool CanSplit(TMP_CharacterInfo[] glyphs, int index)
        {
            char previous = glyphs[index - 1].character;
            char current = glyphs[index].character;
            return !IsOpening(previous) && !IsClosing(current) && previous != '\u200d' && current != '\u200d' &&
                   char.GetUnicodeCategory(current) is not (UnicodeCategory.NonSpacingMark or
                       UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark);
        }

        private static bool IsNewline(char value) => value is '\r' or '\n' or '\u2028' or '\u2029';
        private static bool IsSentenceEnd(char value) => value is '.' or '!' or '?' or '…' or '。' or '！' or '？';
        private static bool IsClosing(char value) => IsSentenceEnd(value) || value is ',' or ':' or ';' or
            ')' or ']' or '}' or '”' or '’' or '"' or '\'' or '」' or '』' or '】';
        private static bool IsOpening(char value) => value is '(' or '[' or '{' or '“' or '‘' or '「' or '『' or '【';
    }
}
