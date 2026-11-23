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
        [SerializeField] private GameObject waitingPanel;
        [SerializeField] private Button receiveCustomerButton;
        [SerializeField] private TMP_Text curtainWaitingLabel;
        private RoundDirector round;
        private Art.UiTypography typography;
        private OrderHudMotion hudMotion;
        private OrderEntrance entrance;
        private Material disabledServeMaterial;
        private CanvasGroup portraitOpacity;
        private RoundResultView resultView;
        private PortraitView resultPortrait;
        private RoundPhase? shownPhase;
        private CancellationTokenSource phasePlayback;
        private CancellationTokenSource customerPlayback;
        private BottleShape chosenBottle;
        private int chosenInk;
        private bool endingReady;

        private void Start()
        {
            round = RoundDirector.Instance;
            typography = new Art.UiTypography();
            var orderCanvas = dialogueView.GetComponentInParent<Canvas>(true);
            if (orderCanvas != null)
            {
                typography.Apply(orderCanvas.transform);
                entrance = new OrderEntrance(orderCanvas, dialogueView.SpeakerFont, typography);
            }
            hudMotion = new OrderHudMotion(timerText, enterWorkshopButton);
            dialogueView.SpeechBeat += OnSpeechBeat;
            if (curtainWaitingLabel != null &&
                (orderCanvas == null || !curtainWaitingLabel.transform.IsChildOf(orderCanvas.transform)))
                typography.Apply(curtainWaitingLabel);
            disabledServeMaterial = serveButton.image.material;
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
            var selected = prescriptionPanel.transform.Find("SelectedOptions")?.GetComponent<Text>();
            prescriptionFeel = prescriptionPanel.GetComponent<PrescriptionFeel>() ??
                               prescriptionPanel.AddComponent<PrescriptionFeel>();
            prescriptionFeel.Build(squareBottle, heartBottle, roundBottle, inkButtons, serveButton, selected,
                selected != null ? selected.font : timerText.font);
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
            {
                timerText.text = RoundDirector.FormatClock(round.RemainingSeconds);
                timerUrgency ??= new TimerUrgency(timerText);
                timerUrgency.Show(round.RemainingSeconds, round.Phase is RoundPhase.Consult or RoundPhase.Prescribe);
            }
        }

        private TimerUrgency timerUrgency;
        private PrescriptionFeel prescriptionFeel;

        private void OnEnable() => shownPhase = null;

        private void OnDestroy()
        {
            if (dialogueView != null) dialogueView.SpeechBeat -= OnSpeechBeat;
            typography?.Dispose();
        }

        private void OnDisable()
        {
            customerPlayback?.Cancel();
            StopPhasePlayback();
            hudMotion?.Stop();
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
            if (customerPlayback != null || round == null || round.Phase != RoundPhase.Waiting ||
                !receiveCustomerButton.gameObject.activeInHierarchy)
                return;

            receiveCustomerButton.interactable = false;
            customerPlayback = new CancellationTokenSource();
            ReceiveAndPresentCustomer(customerPlayback).Forget();
        }

        private async UniTask ReceiveAndPresentCustomer(CancellationTokenSource playback)
        {
            var token = playback.Token;
            GeneratedOrder order = null;
            try
            {
                await SceneDimmer.Instance.DuringEntrance(async () =>
                {
                    if (!round.BeginOrder()) return;
                    RefreshPhase();
                    if (round.Phase == RoundPhase.Preparing)
                    {
                        entrance?.ShowLoading();
                        await UniTask.WaitWhile(() => round != null && round.Phase == RoundPhase.Preparing,
                            cancellationToken: token);
                    }

                    token.ThrowIfCancellationRequested();
                    if (round == null || round.Phase != RoundPhase.Consult) return;
                    if (shownPhase != round.Phase) RefreshPhase();
                    order = round.CurrentOrder;
                    GameSfx.Play("DoorBell", 0.7f, 1f, 0.03f);
                    if (entrance != null) await entrance.Play(token);
                    curtain.SetClosed(false);
                }, token);

                if (order == null || round == null || round.Phase != RoundPhase.Consult) return;
                if (portraitView != null && portraitView.isActiveAndEnabled)
                    await portraitView.SlideIn(portraitOpacity, token);
                await UniTask.Delay(TimeSpan.FromSeconds(.22), ignoreTimeScale: true, cancellationToken: token);
                await PlayConsult(order, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                customerPlayback = null;
                if (round == null || !isActiveAndEnabled) return;
                RefreshPhase();
                if (round.Phase == RoundPhase.Consult) return;
                dialogueView.gameObject.SetActive(true);
                dialogueView.SetSpeaker(string.Empty);
                dialogueView.ShowMessage("대사를 불러오지 못했습니다.");
            }
            finally
            {
                entrance?.Hide();
                customerPlayback = null;
                playback.Dispose();
                if (receiveCustomerButton != null && round != null && round.Phase == RoundPhase.Waiting)
                    receiveCustomerButton.interactable = true;
            }
        }

        private bool leaving;

        private void DismissResult()
        {
            if (leaving || !resultPanel.activeInHierarchy || !nextOrderButton.interactable) return;
            CustomerLeaves().Forget();
        }

        /// <summary>The customer waves off: turns to the door, slides out and fades, the door bell rings once.</summary>
        private async UniTaskVoid CustomerLeaves()
        {
            leaving = true;
            nextOrderButton.interactable = false;
            try
            {
                if (resultPortrait != null && resultPortrait.isActiveAndEnabled)
                {
                    var rect = (RectTransform)resultPortrait.transform;
                    var group = GetOrAddCanvasGroup(resultPortrait);
                    var rest = rect.anchoredPosition;
                    var scale = rect.localScale;
                    GameSfx.Play("DoorBell", 0.45f, 1.05f);
                    await LMotion.Create(0f, 1f, 0.45f).WithEase(Ease.InCubic)
                        .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                        .Bind(t =>
                        {
                            if (rect == null) return;
                            rect.anchoredPosition = rest + new Vector2(-260f * t, 14f * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t));
                            rect.localScale = new Vector3(-Mathf.Abs(scale.x), scale.y, scale.z); // facing the door
                            group.alpha = 1f - t;
                        }).ToUniTask(this.GetCancellationTokenOnDestroy());
                    rect.anchoredPosition = rest;
                    rect.localScale = scale;
                    group.alpha = 1f;
                }
            }
            finally
            {
                leaving = false;
                nextOrderButton.interactable = true;
            }
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
            {
                enterWorkshopButton.interactable = false;
                hudMotion.HighlightWorkshop(false);
            }
        }

        private void Serve()
        {
            if (chosenBottle == BottleShape.Unspecified || chosenInk == 0) return;
            prescriptionFeel.Serve(portraitView != null ? (RectTransform)portraitView.transform : null);
            if (!round.TryServe(chosenBottle, chosenInk)) return;
            GameSfx.Play("Serve", 0.8f);
            RefreshPhase();
        }

        private void RefreshPhase()
        {
            StopPhasePlayback();
            RoundPhase phase = round.Phase;
            bool waiting = phase == RoundPhase.Waiting;
            bool preparing = phase == RoundPhase.Preparing;
            bool consult = phase == RoundPhase.Consult;
            if (!waiting && !preparing && !consult) customerPlayback?.Cancel();
            bool customerEntering = consult && customerPlayback is { IsCancellationRequested: false };
            bool prescribe = phase == RoundPhase.Prescribe;
            bool ending = phase == RoundPhase.Ending;
            bool result = phase == RoundPhase.Result;
            shownPhase = phase;
            endingReady = false;
            if (waitingPanel != null) waitingPanel.SetActive(waiting);
            else receiveCustomerButton.gameObject.SetActive(waiting);
            if (waiting) receiveCustomerButton.interactable = customerPlayback == null;
            hudMotion.HighlightWorkshop(false);
            if (phase == RoundPhase.Storage) return;

            if (result)
            {
                hudMotion.SetTimer(false);
                enterWorkshopButton.interactable = false;
                CloseForResult(round.Result, StartPhasePlayback()).Forget();
                return;
            }

            if (!customerEntering)
                curtain.SetClosed(waiting || preparing);
            if (curtainWaitingLabel != null)
                curtainWaitingLabel.gameObject.SetActive(preparing && (entrance == null || customerPlayback == null));
            if (portraitOpacity != null) portraitOpacity.alpha = customerEntering ? 0f : 1f;
            dialogueView.gameObject.SetActive((consult && !customerEntering) || ending);
            enterWorkshopButton.gameObject.SetActive(consult && !customerEntering);
            enterWorkshopButton.interactable = false;
            prescriptionPanel.SetActive(prescribe);
            resultPanel.SetActive(false);
            hudMotion.SetTimer(consult || prescribe);
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
                if (!customerEntering) PlayConsult(round.CurrentOrder, StartPhasePlayback()).Forget();
            }
            else if (prescribe)
            {
                chosenBottle = BottleShape.Unspecified;
                chosenInk = 0;
                prescriptionFeel.Open();
                RefreshChoices();
            }
            else if (ending)
                PlayEnding(round.CurrentOrder, round.Result, StartPhasePlayback()).Forget();
        }

        private static CanvasGroup GetOrAddCanvasGroup(Component view) =>
            view.TryGetComponent<CanvasGroup>(out var group) ? group : view.gameObject.AddComponent<CanvasGroup>();

        private async UniTask CloseForResult(RoundResult result, CancellationToken token)
        {
            nextOrderButton.interactable = false;
            await curtain.FadeTo(true, token, () =>
            {
                dialogueView.gameObject.SetActive(false);
                enterWorkshopButton.gameObject.SetActive(false);
                if (portraitView != null) portraitView.gameObject.SetActive(false);
                resultPanel.SetActive(true);
                resultView.Prepare();
                ShowResultPortrait(result);
            });
            var portraitCanvas = portraitView != null ? portraitView.GetComponentInParent<Canvas>(true) : null;
            var order = round.CurrentOrder;
            string bottleAnswer = order == null ? null : order.Bottle switch
            {
                BottleShape.Square => "네모 병",
                BottleShape.Heart => "하트 병",
                BottleShape.Round => "동그란 병",
                _ => null
            };
            int inkIndex = order != null ? order.Ink.InkNumber - 1 : -1;
            string inkAnswer = inkIndex >= 0 && inkIndex < inkButtons.Length
                ? inkButtons[inkIndex].transform.Find("Label")?.GetComponent<Text>()?.text : null;
            await resultView.Play(result, portraitCanvas != null ? portraitCanvas.worldCamera : Camera.main, token,
                bottleAnswer, inkAnswer);
            nextOrderButton.interactable = true;
        }

        private async UniTask PlayConsult(GeneratedOrder order, CancellationToken token)
        {
            SetVoice(order);
            token.ThrowIfCancellationRequested();
            dialogueView.gameObject.SetActive(true);
            enterWorkshopButton.gameObject.SetActive(true);
            await dialogueView.PlaySymptoms(order.Dialogue, order.Disease.DefaultSymptomEffect, round.Tuning.TextSpeed, token);

            if (round.Phase == RoundPhase.Consult && round.CurrentOrder == order)
            {
                enterWorkshopButton.interactable = true;
                hudMotion.HighlightWorkshop(true);
            }
        }

        private void OnSpeechBeat(float strength)
        {
            if (portraitView != null && portraitView.isActiveAndEnabled) portraitView.Speak(strength);
        }

        /// <summary>Each customer keeps one voice pitch, from their id, between 0.8 and 1.25.</summary>
        private void SetVoice(GeneratedOrder order)
        {
            string id = order?.Customer != null ? order.Customer.Id : string.Empty;
            int hash = 17;
            foreach (char c in id ?? string.Empty) hash = hash * 31 + c;
            dialogueView.VoicePitch = 0.8f + Mathf.Abs(hash % 1000) / 1000f * 0.45f;
        }

        private async UniTask PlayEnding(GeneratedOrder order, RoundResult result, CancellationToken token)
        {
            SetVoice(order);
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
            var button = bottle switch
            {
                BottleShape.Square => squareBottle,
                BottleShape.Heart => heartBottle,
                _ => roundBottle
            };
            OrderHudMotion.Bounce(button.transform);
            GameSfx.Play("Select", 0.7f, 0.95f, 0.04f);
        }

        private void SelectInk(int ink)
        {
            chosenInk = ink;
            RefreshChoices();
            if (ink >= 1 && ink <= inkButtons.Length) OrderHudMotion.Bounce(inkButtons[ink - 1].transform);
            GameSfx.Play("Paper", 0.6f, 1f, 0.08f);
        }

        private void RefreshChoices()
        {
            SetChoiceHighlight(squareBottle, chosenBottle == BottleShape.Square);
            SetChoiceHighlight(heartBottle, chosenBottle == BottleShape.Heart);
            SetChoiceHighlight(roundBottle, chosenBottle == BottleShape.Round);
            for (int i = 0; i < inkButtons.Length; i++)
                SetChoiceHighlight(inkButtons[i], chosenInk == i + 1);

            prescriptionFeel.Refresh(chosenBottle switch
            {
                BottleShape.Square => 0,
                BottleShape.Heart => 1,
                BottleShape.Round => 2,
                _ => -1
            }, chosenInk - 1);
            bool readyToServe = chosenBottle != BottleShape.Unspecified && chosenInk != 0;
            // The serve button pops the moment both choices are made.
            if (readyToServe && !serveButton.interactable) OrderHudMotion.Bounce(serveButton.transform, 1.15f);
            serveButton.interactable = readyToServe;
            serveButton.image.material = readyToServe ? null : disabledServeMaterial;
        }

        private static void SetChoiceHighlight(Button button, bool selected)
        {
            button.GetComponent<UnityEngine.UI.Outline>().enabled = selected;
            var glow = button.transform.Find("SelectionGlow");
            if (glow != null)
                glow.gameObject.SetActive(selected);
        }
    }
}
