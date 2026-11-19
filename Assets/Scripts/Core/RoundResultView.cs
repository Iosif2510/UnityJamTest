using PotionShop.Data;
using TMPro;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class RoundResultView : MonoBehaviour
    {
        [SerializeField] private TMP_Text ingredients;
        [SerializeField] private TMP_Text bottle;
        [SerializeField] private TMP_Text ink;
        [SerializeField] private TMP_Text ingredientPoints;
        [SerializeField] private TMP_Text bottlePoints;
        [SerializeField] private TMP_Text inkPoints;
        [SerializeField] private TMP_Text timeBonus;
        [SerializeField] private TMP_Text total;
        [SerializeField] private GameObject passableStamp;
        [SerializeField] private GameObject fineBrewStamp;
        [SerializeField] private TMP_Text otherOutcome;

        public void Show(RoundResult result)
        {
            ingredients.text = $"재료   {result.CorrectIngredients} / {RecipeEntry.RequiredIngredientKinds}";
            bottle.text = $"물약병   {MatchLabel(result.BottleCorrect, result.TimedOut)}";
            ink.text = $"잉크   {MatchLabel(result.InkCorrect, result.TimedOut)}";
            ingredientPoints.text = $"+ {result.IngredientPoints}";
            bottlePoints.text = $"+ {result.BottlePoints}";
            inkPoints.text = $"+ {result.InkPoints}";
            timeBonus.text = $"+ {result.TimeBonus}";
            total.text = $"총점   {result.Total}점";

            bool passable = result.Stamp == RoundResult.PassableStamp;
            bool fine = result.Stamp == RoundResult.FineBrewStamp;
            passableStamp.SetActive(passable);
            fineBrewStamp.SetActive(fine);
            otherOutcome.gameObject.SetActive(!passable && !fine);
            otherOutcome.text = result.TimedOut ? "시간 초과" : "조제 실패";
        }

        private static string MatchLabel(bool correct, bool timedOut) =>
            timedOut ? "미제출" : correct ? "일치" : "불일치";
    }
}
