using System;
using System.Linq;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class OrderSceneView : MonoBehaviour
    {
        [SerializeField] private Text dialogueText;
        [SerializeField] private Button enterWorkshopButton;
        [SerializeField] private Text phaseLabel;
        [SerializeField] private Text timerText;
        [SerializeField] private Text customerClues;
        [SerializeField] private GameObject prescriptionPanel;
        [SerializeField] private Button squareBottle;
        [SerializeField] private Button heartBottle;
        [SerializeField] private Button roundBottle;
        [SerializeField] private Button[] inkButtons;
        [SerializeField] private Text choiceSummary;
        [SerializeField] private Button serveButton;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultSummary;
        [SerializeField] private Button nextOrderButton;

        private RoundDirector round;
        private RoundPhase? shownPhase;
        private Font temporaryFont;
        private BottleShape chosenBottle;
        private int chosenInk;

        private void Start()
        {
            round = RoundDirector.Instance;
            enterWorkshopButton.onClick.AddListener(EnterWorkshop);
            squareBottle.onClick.AddListener(() => SelectBottle(BottleShape.Square));
            heartBottle.onClick.AddListener(() => SelectBottle(BottleShape.Heart));
            roundBottle.onClick.AddListener(() => SelectBottle(BottleShape.Round));
            for (int i = 0; i < inkButtons.Length; i++)
            {
                int ink = i + 1;
                inkButtons[i].onClick.AddListener(() => SelectInk(ink));
            }
            serveButton.onClick.AddListener(Serve);
            nextOrderButton.onClick.AddListener(StartNewOrder);

            if (Font.GetOSInstalledFontNames().Contains("Malgun Gothic"))
            {
                temporaryFont = Font.CreateDynamicFontFromOSFont("Malgun Gothic", dialogueText.fontSize);
                dialogueText.font = temporaryFont;
            }

            if (round == null || round.Phase == RoundPhase.Waiting)
                StartNewOrder();
            else
                RefreshPhase();
        }

        private void Update()
        {
            if (round == null)
                return;

            if (shownPhase != round.Phase)
                RefreshPhase();

            if (timerText.gameObject.activeSelf)
                timerText.text = RoundDirector.FormatClock(round.RemainingSeconds);
        }

        private void OnDestroy()
        {
            if (temporaryFont != null)
                Destroy(temporaryFont);
        }

        private void StartNewOrder()
        {
            try
            {
                if (round == null)
                    throw new InvalidOperationException("No RoundDirector is present in the order scene.");

                if (round.BeginOrder())
                    RefreshPhase();
            }
            catch (Exception exception)
            {
                dialogueText.text = "The order could not be loaded.";
                enterWorkshopButton.interactable = false;
                Debug.LogException(exception, this);
            }
        }

        private void EnterWorkshop()
        {
            if (round.TryEnterStorage())
                enterWorkshopButton.interactable = false;
        }

        private void Serve()
        {
            if (round.TryServe(chosenBottle, chosenInk))
                RefreshPhase();
        }

        private void RefreshPhase()
        {
            RoundPhase phase = round.Phase;
            shownPhase = phase;
            bool consult = phase == RoundPhase.Consult;
            bool prescribe = phase == RoundPhase.Prescribe;
            bool result = phase == RoundPhase.Result;

            dialogueText.transform.parent.gameObject.SetActive(consult);
            enterWorkshopButton.gameObject.SetActive(consult);
            prescriptionPanel.SetActive(prescribe);
            resultPanel.SetActive(result);
            customerClues.transform.parent.gameObject.SetActive(consult || prescribe);
            timerText.gameObject.SetActive(consult || prescribe);
            timerText.text = RoundDirector.FormatClock(round.RemainingSeconds);
            phaseLabel.text = result ? "Result" : prescribe ? "Prescription" : "Orders";

            if (consult || prescribe)
                customerClues.text = DescribeCustomer(round.CurrentOrder);

            if (consult)
            {
                dialogueText.text = string.Concat(round.CurrentOrder.Dialogue.Segments.Select(segment => segment.Text));
                enterWorkshopButton.interactable = true;
            }
            else if (prescribe)
            {
                chosenBottle = BottleShape.Unspecified;
                chosenInk = 0;
                RefreshChoices();
            }
            else if (result)
            {
                RoundResult score = round.Result;
                resultSummary.text = score.TimedOut
                    ? "Time expired\nThe customer left without a potion.\n\nTotal: 0"
                    : $"Ingredients: {score.CorrectIngredients}/{RecipeEntry.RequiredIngredientKinds} (+{score.IngredientPoints})\n" +
                      $"Bottle: {(score.BottleCorrect ? "Correct" : "Wrong")} (+{score.BottlePoints})\n" +
                      $"Ink: {(score.InkCorrect ? "Correct" : "Wrong")} (+{score.InkPoints})\n" +
                      $"Time bonus: +{score.TimeBonus}\n\nTotal: {score.Total}   |   {score.Stamp}";
            }
        }

        private void SelectBottle(BottleShape bottle)
        {
            chosenBottle = bottle;
            RefreshChoices();
        }

        private void SelectInk(int ink)
        {
            chosenInk = ink;
            RefreshChoices();
        }

        private void RefreshChoices()
        {
            string bottle = chosenBottle == BottleShape.Unspecified ? "-" : chosenBottle.ToString();
            string ink = chosenInk == 0 ? "-" : chosenInk.ToString();
            choiceSummary.text = $"Bottle: {bottle}   |   Ink: {ink}";
            serveButton.interactable = chosenBottle != BottleShape.Unspecified && chosenInk != 0;
        }

        private static string DescribeCustomer(GeneratedOrder order)
        {
            string speciesId = order.Customer.SpeciesId;
            string species = speciesId[(speciesId.LastIndexOf('.') + 1)..].Replace('_', ' ');
            string Part(PortraitSlot slot) => order.Portrait.First(part => part.Slot == slot).Appearance switch
            {
                AppearanceValue.PointedEars => "Pointed",
                AppearanceValue.RoundEars => "Round",
                AppearanceValue.AnimalEars => "Animal",
                AppearanceValue.RockCrack => "Rock crack",
                var value => value.ToString()
            };

            return $"{species.ToUpperInvariant()}  |  {order.Customer.Gender}\n" +
                   $"Hair: {Part(PortraitSlot.Hair)}   |   Eyes: {Part(PortraitSlot.Eyes)}\n" +
                   $"Mouth: {Part(PortraitSlot.Mouth)}   |   Ears: {Part(PortraitSlot.Ears)}";
        }
    }
}
