using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using PotionShop.Data;
using PotionShop.Portraits;
using TMPro;
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
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject prescriptionPanel;
        [SerializeField] private Button squareBottle;
        [SerializeField] private Button heartBottle;
        [SerializeField] private Button roundBottle;
        [SerializeField] private Button[] inkButtons;
        [SerializeField] private Button serveButton;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button nextOrderButton;
        [SerializeField] private PortraitView portraitView;
        [SerializeField] private PortraitColorCatalog portraitColors;
        [SerializeField] private CounterCurtain curtain;
        [SerializeField] private Button receiveCustomerButton;
        [SerializeField] private TMP_Text curtainWaitingLabel;
        [SerializeField, Min(0f)] private float customerEntryDelay = 0.8f;
        [SerializeField, Min(0f)] private float customerFadeDuration = 0.45f;

        private RoundDirector round;
        private CanvasGroup dialogueOpacity;
        private CanvasGroup portraitOpacity;
        private RoundResultView resultView;
        private PortraitView resultPortrait;
        private RoundPhase? shownPhase;
        private CancellationTokenSource phasePlayback;
        private BottleShape chosenBottle;
        private int chosenInk;
        private bool endingReady;

        private void Start()
        {
            round = RoundDirector.Instance;
            dialogueOpacity = GetOrAddCanvasGroup(dialogueView);
            if (portraitView != null)
                portraitOpacity = GetOrAddCanvasGroup(portraitView);
            resultView = resultPanel.GetComponent<RoundResultView>();
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
            nextOrderButton.onClick.AddListener(DismissResult);
            receiveCustomerButton.onClick.AddListener(ReceiveCustomer);

            if (round == null)
            {
                Debug.LogError("No RoundDirector is present in the order scene.", this);
                receiveCustomerButton.interactable = false;
                return;
            }

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
            StopPhasePlayback();
            if (dialogueView != null) dialogueView.StopPresentation();
        }

        private void StopPhasePlayback()
        {
            if (phasePlayback == null) return;
            phasePlayback.Cancel();
            phasePlayback.Dispose();
            phasePlayback = null;
        }

        private CancellationToken StartPhasePlayback()
        {
            phasePlayback = new CancellationTokenSource();
            return phasePlayback.Token;
        }

        private void ReceiveCustomer()
        {
            if (round.Phase != RoundPhase.Waiting || !receiveCustomerButton.gameObject.activeInHierarchy)
                return;

            receiveCustomerButton.gameObject.SetActive(false);
            dialogueView.gameObject.SetActive(false);
            BeginCustomerEntry(StartPhasePlayback()).Forget();
        }

        private async UniTask BeginCustomerEntry(CancellationToken token)
        {
            try
            {
                if (!round.UsesDialogueApi)
                {
                    await curtain.FadeTo(false, token);
                    if (customerEntryDelay > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(customerEntryDelay), ignoreTimeScale: true,
                            cancellationToken: token);
                }

                token.ThrowIfCancellationRequested();
                if (round.BeginOrder())
                    RefreshPhase();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                RefreshPhase();
                dialogueView.gameObject.SetActive(true);
                dialogueView.SetSpeaker(string.Empty);
                dialogueView.ShowMessage("대사를 불러오지 못했습니다.");
                Debug.LogException(exception, this);
            }
        }

        private void DismissResult()
        {
            if (resultPanel.activeInHierarchy && round.DismissResult())
                RefreshPhase();
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
            StopPhasePlayback();
            RoundPhase phase = round.Phase;
            bool waiting = phase == RoundPhase.Waiting;
            bool preparing = phase == RoundPhase.Preparing;
            bool consult = phase == RoundPhase.Consult;
            bool customerEntering = consult && shownPhase is RoundPhase.Waiting or RoundPhase.Preparing;
            bool fadeIn = customerEntering && customerFadeDuration > 0f;
            bool prescribe = phase == RoundPhase.Prescribe;
            bool ending = phase == RoundPhase.Ending;
            bool result = phase == RoundPhase.Result;
            shownPhase = phase;
            endingReady = false;

            if (!result && !customerEntering)
                curtain.SetClosed(waiting || preparing);
            receiveCustomerButton.gameObject.SetActive(waiting);
            if (curtainWaitingLabel != null)
                curtainWaitingLabel.gameObject.SetActive(preparing);
            SetPresentationOpacity(fadeIn ? 0f : 1f);
            dialogueView.gameObject.SetActive(consult || ending);
            enterWorkshopButton.gameObject.SetActive(consult);
            enterWorkshopButton.interactable = false;
            prescriptionPanel.SetActive(prescribe);
            resultPanel.SetActive(false);
            timerText.gameObject.SetActive(consult || prescribe);
            timerText.text = RoundDirector.FormatClock(round.RemainingSeconds);

            if (portraitView != null)
            {
                bool showPortrait = (consult || prescribe || ending) && portraitColors != null &&
                    round.CurrentOrder.Customer.FixedAppearance && round.CurrentOrder.Customer.BasePortrait != null;
                portraitView.gameObject.SetActive(showPortrait);
                if (showPortrait) portraitView.Show(round.CurrentOrder, portraitColors);
            }

            if (consult || ending)
                dialogueView.SetSpeaker(round.CurrentOrder.Voice?.DisplayName ?? string.Empty);

            if (consult)
            {
                SetAdvanceLabel("창고로");
                PlayConsult(round.CurrentOrder, customerEntering, fadeIn, StartPhasePlayback()).Forget();
            }
            else if (prescribe)
            {
                chosenBottle = BottleShape.Unspecified;
                chosenInk = 0;
                RefreshChoices();
            }
            else if (ending)
                PlayEnding(round.CurrentOrder, round.Result, StartPhasePlayback()).Forget();
            else if (result)
                CloseForResult(round.Result, StartPhasePlayback()).Forget();
        }

        private static CanvasGroup GetOrAddCanvasGroup(Component view) =>
            view.TryGetComponent<CanvasGroup>(out var group) ? group : view.gameObject.AddComponent<CanvasGroup>();

        private void SetPresentationOpacity(float opacity)
        {
            dialogueOpacity.alpha = opacity;
            if (portraitOpacity != null)
                portraitOpacity.alpha = opacity;
        }

        private async UniTask CloseForResult(RoundResult result, CancellationToken token)
        {
            await curtain.FadeTo(true, token);
            token.ThrowIfCancellationRequested();
            resultPanel.SetActive(true);
            resultView.Show(result);
            ShowResultPortrait(result);
        }

        private async UniTask PlayConsult(GeneratedOrder order, bool customerEntering, bool fadeIn, CancellationToken token)
        {
            var dialogue = dialogueView.PlaySymptoms(order.Dialogue, order.Disease.DefaultSymptomEffect, round.Tuning.TextSpeed, token);
            if (customerEntering)
                await UniTask.WhenAll(dialogue, curtain.FadeTo(false, token), FadeCustomerIn(fadeIn, token));
            else
                await dialogue;

            if (round.Phase == RoundPhase.Consult && round.CurrentOrder == order)
                enterWorkshopButton.interactable = true;
        }

        private UniTask FadeCustomerIn(bool fadeIn, CancellationToken token) => fadeIn
            ? LMotion.Create(0f, 1f, customerFadeDuration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithEase(Ease.OutSine)
                .Bind(SetPresentationOpacity)
                .ToUniTask(token)
            : UniTask.CompletedTask;

        private async UniTask PlayEnding(GeneratedOrder order, RoundResult result, CancellationToken token)
        {
            var gap = TimeSpan.FromSeconds(round.Tuning.ReactionGap);
            int speed = round.Tuning.TextSpeed;
            if (result.TimedOut)
            {
                await dialogueView.PlayReaction(order.GetReactionLine("timeout"), speed, token, "timeout");
                await UniTask.Delay(gap, ignoreTimeScale: true, cancellationToken: token);
                round.CompleteEnding(result);
                return;
            }
            else
            {
                await dialogueView.PlayReaction(order.GetReactionLine(result.ReceiveDialogueKey), speed, token,
                    result.ReceiveDialogueKey);
                await UniTask.Delay(gap, ignoreTimeScale: true, cancellationToken: token);
                await ApplyEffectAsync(result.Effect, token);
                await dialogueView.PlayReaction(order.GetReactionLine(result.DrinkDialogueKey), speed, token,
                    result.DrinkDialogueKey);
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

        private async UniTask ApplyEffectAsync(CustomerEffect effect, CancellationToken token)
        {
            if (portraitView == null) return;
            if (effect == CustomerEffect.Slime && round.SlimePortrait != null)
                await portraitView.TransformIntoSlime(round.SlimePortrait, token);
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
            SetChoiceHighlight(squareBottle, chosenBottle == BottleShape.Square);
            SetChoiceHighlight(heartBottle, chosenBottle == BottleShape.Heart);
            SetChoiceHighlight(roundBottle, chosenBottle == BottleShape.Round);
            for (int i = 0; i < inkButtons.Length; i++)
                SetChoiceHighlight(inkButtons[i], chosenInk == i + 1);

            serveButton.interactable = chosenBottle != BottleShape.Unspecified && chosenInk != 0;
        }

        private static void SetChoiceHighlight(Button button, bool selected) =>
            button.GetComponent<UnityEngine.UI.Outline>().enabled = selected;
    }
}
