namespace PotionShop
{
    /// <summary>
    /// A murmured voice for revealed dialogue: each Hangul syllable plays a short synthesized syllable (Sfx
    /// Voice_&lt;vowel&gt;_&lt;onset&gt;) matching its vowel and the kind of its first consonant, with a gentle rise
    /// and fall in pitch. Spaces and punctuation are silent, so phrases breathe.
    /// </summary>
    internal sealed class DialogueVoice
    {
        private int syllable;
        private float drift;

        /// <summary>Starts a new line: the melody begins again.</summary>
        public void Begin()
        {
            syllable = 0;
            drift = 0f;
        }

        /// <summary>Voices one revealed character; <paramref name="questionEnd"/> lifts the tail of a question.</summary>
        public void Speak(char c, float pitch, bool questionEnd)
        {
            if (!Voiced(c, out char vowel, out char onset)) return;
            syllable++;
            // a slow wave over the phrase plus a small wander, so neighbouring syllables don't repeat exactly
            drift = UnityEngine.Mathf.Clamp(drift + UnityEngine.Random.Range(-0.025f, 0.025f), -0.06f, 0.06f);
            float melody = 1f + 0.05f * UnityEngine.Mathf.Sin(syllable * 0.9f) + drift + (questionEnd ? 0.12f : 0f);
            GameSfx.Play($"Voice_{vowel}_{onset}", 0.5f, pitch * melody, 0.02f);
        }

        /// <summary>The vowel (a e i o u) and onset (p n f 0) of a character, or false for a silent one.</summary>
        internal static bool Voiced(char c, out char vowel, out char onset)
        {
            vowel = 'a';
            onset = '0';
            if (c >= '가' && c <= '힣')
            {
                int code = c - 0xAC00;
                int initial = code / 588, medial = code % 588 / 28;
                vowel = medial switch
                {
                    0 or 2 or 9 => 'a',                                  // ㅏ ㅑ ㅘ
                    1 or 3 or 5 or 7 or 10 or 11 or 15 => 'e',           // ㅐ ㅒ ㅔ ㅖ ㅙ ㅚ ㅞ
                    4 or 6 or 8 or 12 or 14 => 'o',                      // ㅓ ㅕ ㅗ ㅛ ㅝ
                    13 or 17 or 18 => 'u',                               // ㅜ ㅠ ㅡ
                    _ => 'i',                                            // ㅟ ㅢ ㅣ
                };
                onset = initial switch
                {
                    2 or 5 or 6 => 'n',                                  // ㄴ ㄹ ㅁ
                    9 or 10 or 18 => 'f',                                // ㅅ ㅆ ㅎ
                    11 => '0',                                           // ㅇ
                    _ => 'p',                                            // ㄱ ㄷ ㅂ ㅈ ㅊ ㅋ ㅌ ㅍ and their doubles
                };
                return true;
            }
            char lower = char.ToLowerInvariant(c);
            if (lower >= 'a' && lower <= 'z')
            {
                vowel = "aeiou".IndexOf(lower) >= 0 ? lower : "aeiou"[lower % 5];
                onset = "aeiou".IndexOf(lower) >= 0 ? '0' : "mnlr".IndexOf(lower) >= 0 ? 'n' : "sfhzvx".IndexOf(lower) >= 0 ? 'f' : 'p';
                return true;
            }
            // kana and Chinese characters: each one a syllable, its sound picked from the character so a word
            // keeps the same murmur every time it is said
            bool kana = c >= 'ぁ' && c <= 'ヺ' && c != '・';
            bool han = c >= '一' && c <= '鿿';
            if (kana || han)
            {
                vowel = "aeiou"[c % 5];
                onset = "pnf0"[(c >> 3) % 4];
                return true;
            }
            if (char.IsDigit(c))
            {
                vowel = "aeiou"[c % 5];
                onset = 'p';
                return true;
            }
            return false;
        }
    }
}
