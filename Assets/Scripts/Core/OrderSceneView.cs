using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PotionShop.Data;
using PotionShop.Portraits;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class OrderSceneView : MonoBehaviour
    {
        private static readonly Color OddPotionTint = new(0.62f, 0.95f, 0.69f);

        [SerializeField] private OrderDialogueView dialogueView;
        [SerializeField] private Button enterWorkshopButton;
        [SerializeField] private Text phaseLabel;
        [SerializeField] private Text timerText;
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
        [SerializeField] private PortraitView portraitView;
        [SerializeField] private PortraitColorCatalog portraitColors;

        private RoundDirector round;
        private PortraitView resultPortrait;
        private RoundPhase? shownPhase;
        private CancellationTokenSource dialoguePlayback;
        private BottleShape chosenBottle;
        private int chosenInk;
        private bool endingReady;

        private void Start()
        {
            round = RoundDirector.Instance;
            resultPortrait = resultPanel.GetComponentInChildren<PortraitView>(true);
            enterWorkshopButton.onClick.AddListener(AdvanceDialogue);
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

        private void OnEnable() => shownPhase = null;

        private void OnDisable()
        {
            StopDialogue();
            if (dialogueView != null) dialogueView.StopPresentation();
        }

        private void StopDialogue()
        {
            if (dialoguePlayback == null) return;
            dialoguePlayback.Cancel();
            dialoguePlayback.Dispose();
            dialoguePlayback = null;
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
                dialogueView.ShowMessage("대사를 불러오지 못했습니다.");
                enterWorkshopButton.interactable = false;
                Debug.LogException(exception, this);
            }
        }

        private void AdvanceDialogue()
        {
            if (endingReady && round.Phase == RoundPhase.Ending)
            {
                endingReady = false;
                round.CompleteEnding(round.Result);
                RefreshPhase();
            }
            else if (enterWorkshopButton.interactable && round.TryEnterStorage())
                enterWorkshopButton.interactable = false;
        }

        private void Serve()
        {
            if (round.TryServe(chosenBottle, chosenInk))
                RefreshPhase();
        }

        private void RefreshPhase()
        {
            StopDialogue();
            RoundPhase phase = round.Phase;
            shownPhase = phase;
            bool consult = phase == RoundPhase.Consult;
            bool preparing = phase == RoundPhase.Preparing;
            bool prescribe = phase == RoundPhase.Prescribe;
            bool ending = phase == RoundPhase.Ending;
            bool result = phase == RoundPhase.Result;
            endingReady = false;

            dialogueView.gameObject.SetActive(preparing || consult || ending);
            enterWorkshopButton.gameObject.SetActive(consult);
            enterWorkshopButton.interactable = false;
            prescriptionPanel.SetActive(prescribe);
            resultPanel.SetActive(result);
            timerText.gameObject.SetActive(consult || prescribe);
            timerText.text = RoundDirector.FormatClock(round.RemainingSeconds);
            phaseLabel.gameObject.SetActive(prescribe || result);
            phaseLabel.text = result ? "조제 결과" : "처방전";

            if (portraitView != null)
            {
                bool showPortrait = (consult || prescribe || ending) && portraitColors != null &&
                    round.CurrentOrder.Customer.FixedAppearance && round.CurrentOrder.Customer.BasePortrait != null;
                portraitView.gameObject.SetActive(showPortrait);
                if (showPortrait) portraitView.Show(round.CurrentOrder, portraitColors);
            }

            if (consult || ending)
            {
                dialogueView.SetSpeaker(round.CurrentOrder.Voice?.DisplayName ?? string.Empty);
                dialoguePlayback = new CancellationTokenSource();
            }

            if (preparing)
            {
                dialogueView.SetSpeaker(string.Empty);
                dialogueView.ShowMessage("손님이 오고 있어요.");
            }
            else if (consult)
            {
                SetAdvanceLabel("창고로");
                PlayConsult(round.CurrentOrder, dialoguePlayback.Token).Forget();
            }
            else if (prescribe)
            {
                chosenBottle = BottleShape.Unspecified;
                chosenInk = 0;
                RefreshChoices();
            }
            else if (ending)
                PlayEnding(round.CurrentOrder, round.Result, dialoguePlayback.Token).Forget();
            else if (result)
            {
                RoundResult score = round.Result;
                ShowResultPortrait(score);
                resultSummary.text = score.TimedOut
                    ? "Time expired\nThe customer left without a potion.\n\nTotal: 0"
                    : $"Ingredients: {score.CorrectIngredients}/{RecipeEntry.RequiredIngredientKinds} (+{score.IngredientPoints})\n" +
                      $"Bottle: {(score.BottleCorrect ? "Correct" : "Wrong")} (+{score.BottlePoints})\n" +
                      $"Ink: {(score.InkCorrect ? "Correct" : "Wrong")} (+{score.InkPoints})\n" +
                      $"Time bonus: +{score.TimeBonus}\n\nTotal: {score.Total}   |   {score.Stamp}";
            }
        }

        private async UniTask PlayConsult(GeneratedOrder order, CancellationToken token)
        {
            await dialogueView.PlaySymptoms(order.Dialogue, order.Disease.DefaultSymptomEffect, round.Tuning.TextSpeed, token);
            if (round.Phase == RoundPhase.Consult && round.CurrentOrder == order)
                enterWorkshopButton.interactable = true;
        }

        private async UniTask PlayEnding(GeneratedOrder order, RoundResult result, CancellationToken token)
        {
            var gap = TimeSpan.FromSeconds(round.Tuning.ReactionGap);
            int speed = round.Tuning.TextSpeed;
            if (result.TimedOut)
            {
                await dialogueView.PlayReaction(order.GetReactionLine("timeout"), speed, token);
                await UniTask.Delay(gap, ignoreTimeScale: true, cancellationToken: token);
                round.CompleteEnding(result);
                return;
            }
            else
            {
                await dialogueView.PlayReaction(order.GetReactionLine(result.ReceiveDialogueKey), speed, token);
                await UniTask.Delay(gap, ignoreTimeScale: true, cancellationToken: token);
                ApplyEffect(result.Effect);
                await dialogueView.PlayReaction(order.GetReactionLine(result.DrinkDialogueKey), speed, token);
            }
            if (round.Phase != RoundPhase.Ending || round.Result != result) return;
            endingReady = true;
            SetAdvanceLabel("결과 보기");
            enterWorkshopButton.gameObject.SetActive(true);
            enterWorkshopButton.interactable = true;
        }

        private void SetAdvanceLabel(string title) => enterWorkshopButton.GetComponentInChildren<Text>(true).text = title;

        private void ShowResultPortrait(RoundResult result)
        {
            if (resultPortrait == null) return;

            if (result.Effect == CustomerEffect.Slime && round.SlimePortrait != null)
            {
                resultPortrait.gameObject.SetActive(true);
                resultPortrait.ShowSlime(round.SlimePortrait);
                return;
            }

            GeneratedOrder order = round.CurrentOrder;
            CustomerProfile customer = order.Customer;
            bool visible = portraitColors != null && customer.FixedAppearance && customer.BasePortrait != null;
            resultPortrait.gameObject.SetActive(visible);
            if (!visible) return;

            Sprite expression = result.TimedOut ? customer.AngryExpression : result.DrinkDialogueKey switch
            {
                "aft_cured" => customer.HappyExpression,
                "aft_odd" => customer.AngryExpression,
                _ => null
            };
            resultPortrait.Show(order, portraitColors, expression);
            if (result.Effect == CustomerEffect.OddTint)
                resultPortrait.SetTint(OddPotionTint);
        }

        private void ApplyEffect(CustomerEffect effect)
        {
            if (portraitView == null) return;
            if (effect == CustomerEffect.Slime && round.SlimePortrait != null)
            {
                portraitView.gameObject.SetActive(true);
                portraitView.ShowSlime(round.SlimePortrait);
            }
            else if (effect == CustomerEffect.OddTint && portraitView.isActiveAndEnabled)
                portraitView.Tint(OddPotionTint);
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

    }
}
