using System;
using System.Collections.Generic;
using PotionShop.Data;

namespace PotionShop
{
    public enum CustomerEffect { None, Recovered, OddTint, Slime }

    public sealed class RoundResult
    {
        internal RoundResult(bool timedOut, int correctIngredients, bool bottleCorrect, bool inkCorrect,
            int ingredientPoints, int bottlePoints, int inkPoints, int timeBonus, string stamp)
        {
            TimedOut = timedOut;
            CorrectIngredients = correctIngredients;
            BottleCorrect = bottleCorrect;
            InkCorrect = inkCorrect;
            IngredientPoints = ingredientPoints;
            BottlePoints = bottlePoints;
            InkPoints = inkPoints;
            TimeBonus = timeBonus;
            Stamp = stamp;
        }

        public bool TimedOut { get; }
        public int CorrectIngredients { get; }
        public bool BottleCorrect { get; }
        public bool InkCorrect { get; }
        public int IngredientPoints { get; }
        public int BottlePoints { get; }
        public int InkPoints { get; }
        public int TimeBonus { get; }
        public int Total => IngredientPoints + BottlePoints + InkPoints + TimeBonus;
        public string Stamp { get; }

        public string ReceiveDialogueKey => TimedOut ? null : (BottleCorrect, InkCorrect) switch
        {
            (true, true) => "rcv_ok",
            (false, true) => "rcv_bottle",
            (true, false) => "rcv_ink",
            _ => "rcv_both"
        };

        public CustomerEffect Effect => TimedOut ? CustomerEffect.None : CorrectIngredients switch
        {
            RecipeEntry.RequiredIngredientKinds => CustomerEffect.Recovered,
            RecipeEntry.RequiredIngredientKinds - 1 => CustomerEffect.OddTint,
            _ => CustomerEffect.Slime
        };

        public string DrinkDialogueKey => Effect switch
        {
            CustomerEffect.Recovered => InkCorrect ? "aft_cured" : "aft_cured_ink",
            CustomerEffect.OddTint => "aft_odd",
            CustomerEffect.Slime => "aft_slime",
            _ => null
        };

        internal static RoundResult TimeExpired() => new(true, 0, false, false, 0, 0, 0, 0, "Slime");
    }

    public static class RoundJudge
    {
        public static RoundResult Score(GeneratedOrder order, IReadOnlyList<string> ingredients, BottleShape bottle,
            int inkNumber, double remainingSeconds, RoundTuning tuning)
        {
            var unmatched = new HashSet<string>(order.Recipe.IngredientVariantIds, StringComparer.Ordinal);
            int correctIngredients = 0;
            foreach (string ingredient in ingredients)
                if (unmatched.Remove(ingredient))
                    correctIngredients++;

            bool bottleCorrect = bottle == order.Bottle;
            bool inkCorrect = inkNumber == order.Ink.InkNumber;
            int ingredientPoints = correctIngredients * tuning.PointsPerIngredient;
            int bottlePoints = bottleCorrect ? tuning.BottlePoints : 0;
            int inkPoints = inkCorrect ? tuning.InkPoints : 0;
            int timeBonus = (int)Math.Floor(Math.Max(0d, remainingSeconds) / tuning.BonusIntervalSeconds) *
                tuning.PointsPerBonusInterval;
            int total = ingredientPoints + bottlePoints + inkPoints + timeBonus;
            string stamp = total >= tuning.PerfectStampThreshold ? "Fine Brew" :
                total >= tuning.PassingStampThreshold ? "Passable" : "Slime";

            return new RoundResult(false, correctIngredients, bottleCorrect, inkCorrect,
                ingredientPoints, bottlePoints, inkPoints, timeBonus, stamp);
        }
    }
}
