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
        private bool pouring;
        private bool brewingEffectsPlaying;
        private double completesAt;
        private float brewingVolume;

        public bool CanStartBrewing => isActiveAndEnabled && !hasBatch && !pouring && tuning != null && liquidMotion != null;

        public double RemainingSeconds => hasBatch ? Math.Max(0d, completesAt - Time.unscaledTimeAsDouble) : 0d;

        internal Vector3 LiquidImpactPoint(int previouslyLanded, int count) =>
            liquidMotion.SurfaceCenter(previouslyLanded / (float)count);

        private CauldronFeedback feedback;

        private void Start()
        {
            liquidMotion?.PrepareFill();
            feedback = GetComponent<CauldronFeedback>() ?? gameObject.AddComponent<CauldronFeedback>();
        }

        internal void BeginPour() => pouring = true;

        internal void AddIngredient(int landed, int count)
        {
            liquidMotion.AddIngredient(landed, count);
            if (ingredientDropAudio != null)
                ingredientDropAudio.Play();
            if (feedback != null)
                feedback.Splash(liquidMotion.SurfaceCenter(landed / (float)count));
        }

        internal void StartBrewing()
        {
            pouring = false;
            hasBatch = true;
            completesAt = Time.unscaledTimeAsDouble + tuning.BrewingSeconds;
            SetBrewingEffects(RemainingSeconds > 0d);
        }

        internal void CancelPour()
        {
            if (!pouring) return;
            pouring = false;
            if (liquidMotion != null) liquidMotion.ResetFill();
            if (ingredientDropAudio != null) ingredientDropAudio.Stop();
        }

        private void OnEnable() => SetBrewingEffects(RemainingSeconds > 0d);

        private void Update()
        {
            if (!brewingEffectsPlaying)
                return;

            double remaining = RemainingSeconds;
            if (remaining <= 0d)
            {
                SetBrewingEffects(false);
                if (feedback != null && liquidMotion != null)
                    feedback.BrewDone(liquidMotion.SurfaceCenter(1f));
            }
            else if (brewingAudio != null && remaining < BrewingFadeSeconds)
                brewingAudio.volume = brewingVolume * (float)(remaining / BrewingFadeSeconds);
        }

        private void OnDisable()
        {
            CancelPour();
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
                if (liquidMotion != null) liquidMotion.StopSloshing();
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
