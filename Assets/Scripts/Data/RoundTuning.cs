using UnityEngine;

namespace PotionShop.Data
{
    [CreateAssetMenu(menuName = "Potion Shop/Data/Round Tuning")]
    public sealed class RoundTuning : ScriptableObject
    {
        [SerializeField, Min(1)] private int timeLimitSeconds = 180;
        [SerializeField, Min(0.01f)] private float pickupDistance = 2.5f;
        [SerializeField, Min(0f)] private float brewingSeconds = 4f;
        [SerializeField, Min(0)] private int pointsPerIngredient = 20;
        [SerializeField, Min(0)] private int bottlePoints = 20;
        [SerializeField, Min(0)] private int inkPoints = 20;
        [SerializeField, Min(1)] private int bonusIntervalSeconds = 10;
        [SerializeField, Min(0)] private int pointsPerBonusInterval = 5;
        [SerializeField, Min(0)] private int passingStampThreshold = 60;
        [SerializeField, Min(0)] private int perfectStampThreshold = 100;

        public int TimeLimitSeconds => timeLimitSeconds;
        public int PocketCapacity => RecipeEntry.RequiredIngredientKinds;
        public float PickupDistance => pickupDistance;
        public float BrewingSeconds => brewingSeconds;
        public int PointsPerIngredient => pointsPerIngredient;
        public int BottlePoints => bottlePoints;
        public int InkPoints => inkPoints;
        public int BonusIntervalSeconds => bonusIntervalSeconds;
        public int PointsPerBonusInterval => pointsPerBonusInterval;
        public int PassingStampThreshold => passingStampThreshold;
        public int PerfectStampThreshold => perfectStampThreshold;
    }
}
