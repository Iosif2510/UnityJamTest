using System;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.VFX;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WorkshopCauldron : MonoBehaviour
    {
        private const float BrewingFadeSeconds = 0.18f;

        [SerializeField] private RoundTuning tuning;
        [SerializeField] private LiquidMotion liquidMotion;
        [SerializeField] private VisualEffect brewingSteam;
        [SerializeField] private AudioSource ingredientDropAudio;
        [SerializeField] private AudioSource brewingAudio;

        private bool hasBatch;
        private bool brewingEffectsPlaying;
        private double completesAt;
        private float brewingVolume;

        public bool CanStartBrewing => isActiveAndEnabled && !hasBatch && tuning != null && liquidMotion != null;

        public double RemainingSeconds => hasBatch ? Math.Max(0d, completesAt - Time.unscaledTimeAsDouble) : 0d;

        public bool TryStartBrewing()
        {
            if (!CanStartBrewing)
                return false;

            hasBatch = true;
            completesAt = Time.unscaledTimeAsDouble + tuning.BrewingSeconds;
            if (ingredientDropAudio != null)
                ingredientDropAudio.Play();
            SetBrewingEffects(RemainingSeconds > 0d);
            return true;
        }

        private void OnEnable() => SetBrewingEffects(RemainingSeconds > 0d);

        private void Update()
        {
            if (!brewingEffectsPlaying)
                return;

            double remaining = RemainingSeconds;
            if (remaining <= 0d)
                SetBrewingEffects(false);
            else if (brewingAudio != null && remaining < BrewingFadeSeconds)
                brewingAudio.volume = brewingVolume * (float)(remaining / BrewingFadeSeconds);
        }

        private void OnDisable()
        {
            SetBrewingEffects(false);
            if (ingredientDropAudio != null)
                ingredientDropAudio.Stop();
        }

        private void SetBrewingEffects(bool playing)
        {
            if (brewingEffectsPlaying == playing)
                return;

            brewingEffectsPlaying = playing;
            if (playing)
            {
                liquidMotion.StartSloshing();
                brewingSteam?.Play();
                if (brewingAudio != null)
                {
                    brewingVolume = brewingAudio.volume;
                    brewingAudio.Play();
                }
            }
            else
            {
                liquidMotion.StopSloshing();
                brewingSteam?.Stop();
                if (brewingAudio != null)
                {
                    brewingAudio.Stop();
                    brewingAudio.volume = brewingVolume;
                }
            }
        }
    }
}
