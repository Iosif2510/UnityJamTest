using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The game's language, for everything on screen. Text is written in Korean in the code and the scenes; the
    /// Korean is the key into Resources/Localization/strings.json, which holds each line in English, Japanese and
    /// both Chinese scripts. A line with no entry stays Korean. The choice is saved, and the memos follow it too.
    /// </summary>
    public static class Loc
    {
        public const string Korean = "ko";
        private const string LanguageKey = "game.language";
        private const string OldMemoKey = "memo.language";

        public static readonly (string Code, string Name)[] Languages =
        {
            (Korean, "한국어"), ("en", "English"), ("ja", "日本語"), ("zh-Hans", "简体中文"), ("zh-Hant", "繁體中文"),
        };

        /// <summary>Raised after the language changes.</summary>
        public static event Action Changed;

        private static Dictionary<string, Dictionary<string, string>> table;
        private static string language;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            table = null;
            language = null;
            Changed = null;
        }

        /// <summary>The language code: a saved choice, else the system language, else Korean.</summary>
        public static string Language
        {
            get
            {
                if (language != null) return language;
                string saved = PlayerPrefs.GetString(LanguageKey, PlayerPrefs.GetString(OldMemoKey, string.Empty));
                if (saved.Length > 0) return language = saved;
                return language = Application.systemLanguage switch
                {
                    SystemLanguage.English => "en",
                    SystemLanguage.Japanese => "ja",
                    SystemLanguage.ChineseSimplified or SystemLanguage.Chinese => "zh-Hans",
                    SystemLanguage.ChineseTraditional => "zh-Hant",
                    _ => Korean,
                };
            }
            set
            {
                if (value == Language) return;
                language = value;
                PlayerPrefs.SetString(LanguageKey, value);
                PlayerPrefs.SetString(OldMemoKey, value);
                LocFonts.Apply();
                Changed?.Invoke();
            }
        }

        public static bool IsKorean => Language == Korean;

        /// <summary><paramref name="korean"/> in the current language (itself when there is no entry).</summary>
        public static string T(string korean)
        {
            if (string.IsNullOrEmpty(korean) || IsKorean) return korean;
            Load();
            if (table.TryGetValue(korean, out var lines) && lines.TryGetValue(Language, out var line) &&
                !string.IsNullOrEmpty(line))
                return line;
#if UNITY_EDITOR
            Missing(korean);
#endif
            return korean;
        }

        /// <summary><see cref="T"/> of a format string ({0}, {1}...), then filled in.</summary>
        public static string F(string koreanFormat, params object[] args) => string.Format(T(koreanFormat), args);

        private static readonly Dictionary<string, string> StampKeys = new(StringComparer.Ordinal)
        {
            ["훌륭한"] = "great", ["그럭저럭"] = "okay", ["stamp-communication"] = "talk", ["stamp-slow"] = "slow",
        };
        private static readonly Dictionary<string, Sprite> stamps = new(StringComparer.Ordinal);

        /// <summary>
        /// A result stamp drawn with Korean wording, in the current language (Resources/Localization/Stamps);
        /// the stamp itself in Korean or when there is no drawing for it.
        /// </summary>
        public static Sprite Stamp(Sprite korean)
        {
            if (korean == null || IsKorean || !StampKeys.TryGetValue(korean.name, out string key)) return korean;
            string path = $"Localization/Stamps/{key}_{Language}";
            if (stamps.TryGetValue(path, out var known) && known != null) return known;
            var art = Resources.Load<Texture2D>(path);
            if (art == null) return korean;
            return stamps[path] = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(0.5f, 0.5f),
                korean.pixelsPerUnit);
        }

        /// <summary>Whether the table knows <paramref name="korean"/> (for texts found in the scenes).</summary>
        public static bool Has(string korean)
        {
            Load();
            return !string.IsNullOrEmpty(korean) && table.ContainsKey(korean);
        }

        private static void Load()
        {
            if (table != null) return;
            table = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("Localization/strings");
            if (asset == null) return;
            try
            {
                var root = JObject.Parse(asset.text);
                foreach (var entry in (JObject)root["entries"])
                {
                    var lines = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var line in (JObject)entry.Value) lines[line.Key] = (string)line.Value;
                    table[entry.Key] = lines;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Loc] strings.json could not be read: " + e.Message);
            }
        }

#if UNITY_EDITOR
        private static readonly HashSet<string> reported = new();

        private static void Missing(string korean)
        {
            foreach (char c in korean)
                if (c >= '가' && c <= '힣')
                {
                    if (reported.Add(korean)) Debug.LogWarning($"[Loc] no {Language} line for \"{korean}\"");
                    return;
                }
        }
#endif

        // ---- texts placed in the scenes ------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            LocFonts.Apply();
            SceneManager.sceneLoaded += (scene, _) => LocalizeScene(scene);
            LocalizeScene(SceneManager.GetActiveScene());
        }

        /// <summary>Every fixed text in the scene whose Korean is in the table follows the language.</summary>
        private static void LocalizeScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    if (Has(text.text) && !text.TryGetComponent(out LocalizedText _))
                        text.gameObject.AddComponent<LocalizedText>();
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                    if (Has(text.text) && !text.TryGetComponent(out LocalizedText _))
                        text.gameObject.AddComponent<LocalizedText>();
            }
        }
    }

    /// <summary>A text placed in a scene, kept in the current language (its Korean is remembered as the key).</summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        private string korean;
        private TMP_Text tmp;
        private Text legacy;

        private void Awake()
        {
            TryGetComponent(out tmp);
            TryGetComponent(out legacy);
            korean = tmp != null ? tmp.text : legacy != null ? legacy.text : string.Empty;
            Apply();
            Loc.Changed += Apply;
        }

        private void OnDestroy() => Loc.Changed -= Apply;

        private void Apply()
        {
            string line = Loc.T(korean);
            if (tmp != null) tmp.text = line;
            if (legacy != null) legacy.text = line;
        }
    }

    /// <summary>
    /// Japanese and Chinese letters are mostly missing from the game's Korean fonts. While the game runs, CJK fonts
    /// go behind every TextMeshPro font as fallbacks: the computer's own CJK font for the chosen script first
    /// (Simplified and Traditional Chinese and Japanese draw some characters differently), then the others. Where the
    /// computer lends none (a browser), the game's own Resources/Fonts/WebCJK is used. They are taken out again when
    /// the game stops, so no asset changes.
    /// </summary>
    internal static class LocFonts
    {
        private static readonly string[] Simplified = { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC" };
        private static readonly string[] Traditional = { "Microsoft JhengHei UI", "Microsoft JhengHei", "PingFang TC", "Noto Sans CJK TC", "Noto Sans TC" };
        private static readonly string[] Japanese = { "Yu Gothic UI", "Yu Gothic", "Meiryo", "Hiragino Sans", "Noto Sans CJK JP", "Noto Sans JP" };

        private static readonly List<TMP_FontAsset> ours = new();
        private static readonly Dictionary<string, TMP_FontAsset> made = new();
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // font assets made in an earlier play are gone (no domain reload between plays)
            ours.Clear();
            made.Clear();
            var fallbacks = TMP_Settings.fallbackFontAssets;
            fallbacks?.RemoveAll(font => font == null || font.name.EndsWith("(runtime fallback)"));
        }

        internal static void Apply()
        {
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null) return;
            Remove();
            string language = Loc.Language;
            var families = language switch
            {
                "zh-Hant" => new[] { Traditional, Simplified, Japanese },
                "ja" => new[] { Japanese, Traditional, Simplified },
                _ => new[] { Simplified, Traditional, Japanese },
            };
            foreach (var list in families)
            {
                var font = First(list);
                if (font != null) ours.Add(font);
            }
            // no system fonts to borrow (a browser): the game's own cut of the characters it shows
            if (ours.Count == 0 && Bundled() is { } bundled) ours.Add(bundled);
            fallbacks.AddRange(ours);
            if (hooked) return;
            hooked = true;
            Application.quitting += Remove;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Remove();
            };
#endif
        }

        /// <summary>A font asset from the first of these families the computer has, or null.</summary>
        private static TMP_FontAsset First(string[] families)
        {
            foreach (var family in families)
            {
                if (made.TryGetValue(family, out var known) && known != null) return known;
                TMP_FontAsset asset = null;
                try { asset = TMP_FontAsset.CreateFontAsset(family, "Bold", 90) ?? TMP_FontAsset.CreateFontAsset(family, "Regular", 90); }
                catch (Exception) { /* not installed */ }
                if (asset == null) continue;
                asset.name = family + " (runtime fallback)";
                made[family] = asset;
                return asset;
            }
            return null;
        }

        /// <summary>Resources/Fonts/WebCJK (Tools/WebFont/subset_web_cjk.py) as a font asset, or null.</summary>
        private static TMP_FontAsset Bundled()
        {
            const string key = "WebCJK";
            if (made.TryGetValue(key, out var known) && known != null) return known;
            var source = Resources.Load<Font>("Fonts/WebCJK");
            if (source == null) return null;
            var asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
            if (asset == null) return null;
            asset.name = key + " (runtime fallback)";
            return made[key] = asset;
        }

        private static void Remove()
        {
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks != null) foreach (var font in ours) fallbacks.Remove(font);
            ours.Clear();
        }
    }
}
