using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The player's settings beyond the master volume (<see cref="GameSfx.MasterVolume"/>) and the language
    /// (<see cref="Loc"/>), saved between sessions: separate volumes for music, sound effects and the customers'
    /// murmured voices, how fast the mouse turns the view in the storage room (and whether up is down), and a calm
    /// camera with no head bob or bumps for players who get queasy.
    /// </summary>
    public static class GameSettings
    {
        public static float Music
        {
            get => PlayerPrefs.GetFloat("settings.music", 1f);
            set => PlayerPrefs.SetFloat("settings.music", Mathf.Clamp01(value));
        }

        public static float Effects
        {
            get => PlayerPrefs.GetFloat("settings.effects", 1f);
            set => PlayerPrefs.SetFloat("settings.effects", Mathf.Clamp01(value));
        }

        public static float Voices
        {
            get => PlayerPrefs.GetFloat("settings.voices", 1f);
            set => PlayerPrefs.SetFloat("settings.voices", Mathf.Clamp01(value));
        }

        /// <summary>A multiplier on the view's turning speed, 0.25 to 2.5 (1 as designed).</summary>
        public static float MouseSpeed
        {
            get => PlayerPrefs.GetFloat("settings.mouse", 1f);
            set => PlayerPrefs.SetFloat("settings.mouse", Mathf.Clamp(value, 0.25f, 2.5f));
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt("settings.invertY", 0) == 1;
            set => PlayerPrefs.SetInt("settings.invertY", value ? 1 : 0);
        }

        /// <summary>No head bob while walking and no camera bumps.</summary>
        public static bool CalmCamera
        {
            get => PlayerPrefs.GetInt("settings.calmCamera", 0) == 1;
            set => PlayerPrefs.SetInt("settings.calmCamera", value ? 1 : 0);
        }
    }
}
