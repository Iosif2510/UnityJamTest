using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's memos as data. Korean shows the handwritten originals (Resources/MemoText/Sheets_ko); other
    /// languages use drawing-only sheets (Resources/MemoText/Sheets) plus where each handwritten phrase
    /// sits on them (Resources/MemoText/layout.json, built by Tools/MemoText). The words themselves come from a text
    /// table per language (Resources/MemoText/text_&lt;lang&gt;.json, phrase key to text); Korean is the layout's own
    /// text and the fallback for any phrase a table lacks. Sheet indices follow OnlineCoopConfig.MemoSheets.
    /// </summary>
    internal static class MemoText
    {
        public const string Korean = "ko";
        private const string LanguageKey = "memo.language";
        private static readonly string[] SheetNames =
            { "Cover", "POT_01", "POT_02", "POT_03", "POT_04", "POT_05", "POT_06", "CounterManual" };

        [Serializable] private sealed class PhraseData
        {
            public string key;
            public string ko;
            public float[] box;
            public int rows;
            public float rowHeight;
            public string mark;
            public float[] written;
        }

        [Serializable] private sealed class LayoutData
        {
            public float margin;
            public SheetData[] list;
        }

        [Serializable] private sealed class SheetData
        {
            public string name;
            public PhraseData[] phrases;
        }

        public readonly struct Phrase
        {
            public readonly string Key;
            /// <summary>Box on the sheet image, 0–1, origin bottom-left (Unity UI anchors).</summary>
            public readonly Rect Box;
            /// <summary>Written row height as a fraction of the sheet height.</summary>
            public readonly float RowHeight;
            /// <summary>"symptom", "ingredient" or empty: what easy mode marks.</summary>
            public readonly string Mark;
            /// <summary>Where the Korean original has it written, same space as <see cref="Box"/>.</summary>
            public readonly Rect Written;

            public Phrase(string key, Rect box, float rowHeight, string mark, Rect written)
            {
                Written = written;
                Key = key;
                Box = box;
                RowHeight = rowHeight;
                Mark = mark;
            }
        }

        private static Dictionary<string, Phrase[]> layout;
        private static Dictionary<string, string> korean;
        private static Dictionary<string, string> table;
        private static string tableLanguage;
        private static readonly Dictionary<int, Sprite> sheets = new();
        private static readonly Dictionary<int, Sprite> originals = new();
        private static Font font;

        /// <summary>The memo language: a saved choice, else the system language when a table exists, else Korean.</summary>
        public static string Language
        {
            get
            {
                string saved = PlayerPrefs.GetString(LanguageKey, string.Empty);
                if (saved.Length > 0) return saved;
                string system = Application.systemLanguage switch
                {
                    SystemLanguage.Korean => Korean,
                    SystemLanguage.English => "en",
                    SystemLanguage.Japanese => "ja",
                    SystemLanguage.ChineseSimplified or SystemLanguage.Chinese => "zh-Hans",
                    SystemLanguage.ChineseTraditional => "zh-Hant",
                    _ => Korean
                };
                return system == Korean || Resources.Load<TextAsset>("MemoText/text_" + system) != null ? system : Korean;
            }
            set => PlayerPrefs.SetString(LanguageKey, value);
        }

        /// <summary>
        /// Handwriting for the memo text: Xiaolai (OFL) for Chinese and Japanese, Nanum Pen Script (OFL) for the rest.
        /// Xiaolai is cut down to the characters of the ja / zh tables (Tools/MemoText/subset_font.py).
        /// </summary>
        public static Font Font
        {
            get
            {
                string language = Language;
                bool cjk = language == "ja" || language.StartsWith("zh");
                if (font == null || fontIsCjk != cjk)
                {
                    font = Resources.Load<Font>(cjk ? "MemoText/Xiaolai-Regular" : "MemoText/NanumPenScript-Regular");
                    fontIsCjk = cjk;
                }
                return font;
            }
        }

        private static bool fontIsCjk;

        /// <summary>Korean shows the handwritten original; every other language writes on the drawing-only sheet.</summary>
        public static bool ShowsOriginal(int index) => Language == Korean && Original(index) != null;

        /// <summary>The sheet to show in the current language.</summary>
        public static Sprite Sheet(int index) => ShowsOriginal(index) ? Original(index) : Drawing(index);

        private static Sprite Drawing(int index) => Cached(sheets, index, "MemoText/Sheets/");
        private static Sprite Original(int index) => Cached(originals, index, "MemoText/Sheets_ko/");

        private static Sprite Cached(Dictionary<int, Sprite> cache, int index, string folder)
        {
            if (cache.TryGetValue(index, out var sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>(folder + SheetNames[index]);
            if (sprite != null) cache[index] = sprite; // a miss is not remembered (assets may import later)
            return sprite;
        }

        public static IReadOnlyList<Phrase> Phrases(int index)
        {
            Load();
            return layout.TryGetValue(SheetNames[index], out var list) ? list : Array.Empty<Phrase>();
        }

        public static string Text(int index, string key)
        {
            Load();
            string id = SheetNames[index] + "." + key;
            string language = Language;
            if (language != tableLanguage)
            {
                tableLanguage = language;
                table = language == Korean ? null : ReadTable(Resources.Load<TextAsset>("MemoText/text_" + language));
            }
            if (table != null && table.TryGetValue(id, out var text) && !string.IsNullOrEmpty(text)) return text;
            return korean.TryGetValue(id, out var ko) ? ko : string.Empty;
        }

        private static void Load()
        {
            if (layout != null) return;
            layout = new Dictionary<string, Phrase[]>();
            korean = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("MemoText/layout");
            if (asset == null)
            {
                Debug.LogWarning("[Memo] Resources/MemoText/layout.json is missing.");
                return;
            }
            // JsonUtility cannot read a dictionary, so the "sheets" object is rewritten as a list first.
            var data = JsonUtility.FromJson<LayoutData>(AsList(asset.text));
            float m = data.margin, scale = 1f - 2f * m;
            foreach (var sheet in data.list)
            {
                var phrases = new Phrase[sheet.phrases.Length];
                for (int i = 0; i < phrases.Length; i++)
                {
                    var p = sheet.phrases[i];
                    // Layout boxes are on the drawing, y down; the sheet image adds the paper margin around it.
                    float x0 = m + p.box[0] * scale, x1 = m + p.box[2] * scale;
                    float yTop = m + p.box[1] * scale, yBottom = m + p.box[3] * scale;
                    var w = p.written ?? p.box;
                    var written = Rect.MinMaxRect(m + w[0] * scale, 1f - (m + w[3] * scale), m + w[2] * scale,
                        1f - (m + w[1] * scale));
                    phrases[i] = new Phrase(p.key, Rect.MinMaxRect(x0, 1f - yBottom, x1, 1f - yTop), p.rowHeight * scale,
                        p.mark ?? string.Empty, written);
                    korean[sheet.name + "." + p.key] = p.ko;
                }
                layout[sheet.name] = phrases;
            }
        }

        /// <summary>{"sheets": {"A": [...], ...}} to {"list": [{"name": "A", "phrases": [...]}, ...]}.</summary>
        private static string AsList(string json)
        {
            int start = json.IndexOf("\"sheets\"", StringComparison.Ordinal);
            int open = json.IndexOf('{', start);
            var sb = new System.Text.StringBuilder(json.Length + 256);
            sb.Append(json, 0, start).Append("\"list\": [");
            int i = open + 1, depth = 0, entries = 0;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"' && depth == 0)
                {
                    int end = json.IndexOf('"', i + 1);
                    string name = json.Substring(i + 1, end - i - 1);
                    int colon = json.IndexOf(':', end);
                    int arrayStart = json.IndexOf('[', colon);
                    int arrayEnd = MatchingBracket(json, arrayStart);
                    if (entries++ > 0) sb.Append(',');
                    sb.Append("{\"name\":\"").Append(name).Append("\",\"phrases\":")
                        .Append(json, arrayStart, arrayEnd - arrayStart + 1).Append('}');
                    i = arrayEnd + 1;
                    continue;
                }
                if (c == '}' && depth == 0) { i++; break; }
                i++;
            }
            sb.Append(']').Append(json, i, json.Length - i);
            return sb.ToString();
        }

        private static int MatchingBracket(string s, int open)
        {
            int depth = 0;
            bool quoted = false;
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && s[i - 1] != '\\') quoted = !quoted;
                if (quoted) continue;
                if (c == '[' || c == '{') depth++;
                else if ((c == ']' || c == '}') && --depth == 0) return i;
            }
            return s.Length - 1;
        }

        /// <summary>A flat {"POT_01.s1": "...", ...} table.</summary>
        private static Dictionary<string, string> ReadTable(TextAsset asset)
        {
            var result = new Dictionary<string, string>();
            if (asset == null) return result;
            var matches = System.Text.RegularExpressions.Regex.Matches(asset.text,
                "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            foreach (System.Text.RegularExpressions.Match match in matches)
                result[match.Groups[1].Value] = System.Text.RegularExpressions.Regex.Unescape(match.Groups[2].Value);
            return result;
        }
    }
}
