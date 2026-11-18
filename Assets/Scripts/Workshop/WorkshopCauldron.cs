using System;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.VFX;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WorkshopCauldron : MonoBehaviour
    {
        [SerializeField] private RoundTuning tuning;
        [SerializeField] private LiquidMotion liquidMotion;
        [SerializeField] private VisualEffect brewingSteam;

        private bool hasBatch;
        private bool brewingEffectsPlaying;
        private double completesAt;

        public bool CanStartBrewing => isActiveAndEnabled && !hasBatch && tuning != null && liquidMotion != null;

        public double RemainingSeconds => hasBatch ? Math.Max(0d, completesAt - Time.unscaledTimeAsDouble) : 0d;

        public bool TryStartBrewing()
        {
            if (!CanStartBrewing)
                return false;

            hasBatch = true;
            completesAt = Time.unscaledTimeAsDouble + tuning.BrewingSeconds;
            SetBrewingEffects(RemainingSeconds > 0d);
            return true;
        }

        private void OnEnable() => SetBrewingEffects(RemainingSeconds > 0d);

        private void Update()
        {
            if (brewingEffectsPlaying && RemainingSeconds <= 0d)
                SetBrewingEffects(false);
        }

        private void OnDisable() => SetBrewingEffects(false);

        private void SetBrewingEffects(bool playing)
        {
            if (brewingEffectsPlaying == playing)
                return;

            brewingEffectsPlaying = playing;
            if (playing)
            {
                liquidMotion.StartSloshing();
                brewingSteam?.Play();
            }
            else
            {
                liquidMotion.StopSloshing();
                brewingSteam?.Stop();
            }
        }
    }
}
