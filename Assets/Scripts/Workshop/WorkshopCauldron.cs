using System;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WorkshopCauldron : MonoBehaviour
    {
        [SerializeField] private RoundTuning tuning;
        [SerializeField] private LiquidMotion liquidMotion;

        private bool hasBatch;
        private double completesAt;

        public bool CanStartBrewing => isActiveAndEnabled && !hasBatch && tuning != null && liquidMotion != null;

        public double RemainingSeconds => hasBatch ? Math.Max(0d, completesAt - Time.unscaledTimeAsDouble) : 0d;

        public bool TryStartBrewing()
        {
            if (!CanStartBrewing)
                return false;

            hasBatch = true;
            completesAt = Time.unscaledTimeAsDouble + tuning.BrewingSeconds;
            liquidMotion.PlaySlosh();
            return true;
        }
    }
}
