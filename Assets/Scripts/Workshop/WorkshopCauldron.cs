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

        /// <summary>0 when brewing starts, 1 when the potion is done (0 with no batch).</summary>
        internal float BrewProgress => hasBatch && tuning != null && tuning.BrewingSeconds > 0f
            ? 1f - (float)(RemainingSeconds / tuning.BrewingSeconds) : 0f;

        private CauldronFeedback feedback;
        private CauldronColor colour;

        private void Start()
        {
            liquidMotion?.PrepareFill();
            feedback = GetComponent<CauldronFeedback>() ?? gameObject.AddComponent<CauldronFeedback>();
            if (liquidMotion == null) return;
            colour = GetComponent<CauldronColor>() ?? gameObject.AddComponent<CauldronColor>();
            colour.Bind(liquidMotion.LiquidRenderer);
            var ring = GetComponent<BrewProgressRing>() ?? gameObject.AddComponent<BrewProgressRing>();
            ring.Bind(this, colour);
        }

        internal void BeginPour() => pouring = true;

        internal void AddIngredient(int landed, int count, WarehouseIngredient ingredient = null)
        {
            liquidMotion.AddIngredient(landed, count);
            if (colour != null) colour.Add(ingredient, landed, count);
            if (ingredientDropAudio != null)
                ingredientDropAudio.Play();
            if (feedback != null)
            {
                feedback.Splash(liquidMotion.SurfaceCenter(landed / (float)count), colour != null ? colour.LastTint : null);
                feedback.FireFlare(landed >= count ? 0.9f : 0.45f);
            }
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
            if (colour != null) colour.ResetColor();
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
