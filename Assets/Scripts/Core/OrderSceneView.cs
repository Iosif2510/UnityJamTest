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

        // online: the storeroom waits for the friend's "찾았어!", and the result card opens on both screens together
        private GeneratedOrder lockOrder;
        private float lockSince = float.NegativeInfinity;
        private bool memoLocked;
        private bool waitingFriend;
        private bool sharedEnding;

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
            // the name tag drops in for each new customer
            var plate = dialogueView.transform.Find("SpeakerNameplate");
            if (plate != null && !plate.TryGetComponent(out namePlate)) namePlate = plate.gameObject.AddComponent<NamePlateDrop>();
            if (curtainWaitingLabel != null &&
                (orderCanvas == null || !curtainWaitingLabel.transform.IsChildOf(orderCanvas.transform)))
                typography.Apply(curtainWaitingLabel);
            disabledServeMaterial = serveButton.image.material;
            if (portraitView != null)
                portraitOpacity = GetOrAddCanvasGroup(portraitView);
            resultView = resultPanel.GetComponent<RoundResultView>();
            resultPortrait = resultPanel.GetComponentInChildren<PortraitView>(true);
            enterWorkshopButton.onClick.AddListener(AdvanceDialogue);
            // watching a friend's hand-over, the picks are theirs: clicks do nothing
            squareBottle.onClick.AddListener(() => { if (!Watching) SelectBottle(BottleShape.Square); });
            heartBottle.onClick.AddListener(() => { if (!Watching) SelectBottle(BottleShape.Heart); });
            roundBottle.onClick.AddListener(() => { if (!Watching) SelectBottle(BottleShape.Round); });
            for (int i = 0; i < inkButtons.Length; i++)
            {
                int ink = i + 1;
                inkButtons[i].onClick.AddListener(() => { if (!Watching) SelectInk(ink); });
            }
            serveButton.onClick.AddListener(() => { if (!Watching) Serve(); });
            var selected = prescriptionPanel.transform.Find("SelectedOptions")?.GetComponent<Text>();
            prescriptionFeel = (prescriptionPanel.TryGetComponent(out PrescriptionFeel foundPrescriptionFeel) ? foundPrescriptionFeel : prescriptionPanel.AddComponent<PrescriptionFeel>());
            prescriptionFeel.Pointed += LookAtChoice;
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

            // a friend downstairs just handed a potion over: watch it with them
            if (Online.SharedEnding.TakePending(out var handover)) Watch(handover).Forget();
        }

        private bool Watching => round != null && round.Spectating;

        /// <summary>
        /// Replays the friend's hand-over here: the same customer at the counter, their bottle and ink picked one
        /// after the other, then the potion made and handed over, and the customer's reaction and result as usual.
        /// </summary>
        private async UniTaskVoid Watch(Online.SharedEnding.Handover handover)
        {
            var token = this.GetCancellationTokenOnDestroy();
            await UniTask.Yield(token);
            CauldronColor.ShowBrew(handover.Brew);
            if (!round.BeginSpectating(handover.Seed, handover.PreviousDisease, handover.CustomerId, handover.Ingredients,
                    handover.Bottle, handover.Ink, handover.RemainingSeconds))
            {
                Online.SharedEnding.ReturnUpstairs();
                return;
            }
            RefreshPhase();
            await UniTask.Delay(TimeSpan.FromSeconds(1.2), ignoreTimeScale: true, cancellationToken: token);
            SelectBottle(handover.Bottle);
            await UniTask.Delay(TimeSpan.FromSeconds(0.8), ignoreTimeScale: true, cancellationToken: token);
            SelectInk(handover.Ink);
            await UniTask.Delay(TimeSpan.FromSeconds(1.0), ignoreTimeScale: true, cancellationToken: token);
            if (round.Phase != RoundPhase.Prescribe || chosenBottle == BottleShape.Unspecified || chosenInk == 0) return;
            servedBottle = prescriptionFeel.ChosenBottleSprite;
            servedInk = prescriptionFeel.ChosenInkSprite;
            var (potion, forWhom) = PotionLabelText.For(round.CurrentOrder, round.SubmittedIngredients);
            var craft = new PotionCrafting.Order(prescriptionFeel.ChosenBottleRect, servedBottle,
                prescriptionFeel.ChosenInkRect, servedInk, prescriptionFeel.ChosenInkColour, potion, forWhom,
                portraitView != null ? (RectTransform)portraitView.transform : null);
            round.SpectateServe();
            CraftAndServe(craft, Online.CounterTogether.FriendShares(true)).Forget();
        }

        private void Update()
        {
            if (round == null)
                return;

            if (shownPhase != round.Phase && !crafting)
                RefreshPhase();
            RefreshStorageLock();

            if (timerText.gameObject.activeSelf && !crafting) // the clock stopped when the potion was handed over
            {
                timerText.text = RoundDirector.FormatClock(round.RemainingSeconds);
                timerUrgency ??= new TimerUrgency(timerText);
                timerUrgency.Show(round.RemainingSeconds, round.Phase is RoundPhase.Consult or RoundPhase.Prescribe);
            }
            impatience ??= new CustomerImpatience(portraitView, portraitColors, dialogueView.SpeakerFont);
            impatience.Show((float)round.RemainingSeconds,
                round.Phase is RoundPhase.Consult or RoundPhase.Prescribe && round.CurrentOrder != null, round.CurrentOrder);
        }

        private TimerUrgency timerUrgency;
        private CustomerImpatience impatience;
        private Sprite servedBottle, servedInk;
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
                }, token);

                if (order == null || round == null || round.Phase != RoundPhase.Consult) return;
                // the lights come back on the closed curtain, which is drawn open before the customer steps up
                await curtain.PullOpen(token);
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
                dialogueView.ShowMessage(Loc.T("대사를 불러오지 못했습니다."));
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
            if (round.Spectating)
            {
                // the friend's customer has gone: back upstairs (unless the roles swapped and this is home now)
                round.EndSpectating();
                Online.SharedEnding.ReturnUpstairs();
            }
        }

        private void AdvanceDialogue()
        {
            if (endingReady && round.Phase == RoundPhase.Ending)
            {
                if (waitingFriend) return;
                if (sharedEnding && Online.CounterTogether.FriendShares(Watching))
                {
                    ShowResultTogether().Forget();
                    return;
                }
                endingReady = false;
                round.CompleteEnding(round.Result);
                RefreshPhase();
            }
            else if (enterWorkshopButton.interactable && memoLocked)
            {
                // the friend upstairs is still looking for the memo
                FloatingNote.Show((RectTransform)enterWorkshopButton.transform, Loc.T("아직 찾고 있어요"), 1.6f);
                GameSfx.Play("Blip", 0.7f, 0.8f);
                OrderHudMotion.Bounce(enterWorkshopButton.transform, 0.92f);
            }
            else if (enterWorkshopButton.interactable && round.TryEnterStorage())
            {
                enterWorkshopButton.interactable = false;
                hudMotion.HighlightWorkshop(false);
            }
        }

        /// <summary>"결과 보기" online: grey "친구 기다리는 중…" until the friend presses it too, then both cards open.</summary>
        private async UniTaskVoid ShowResultTogether()
        {
            waitingFriend = true;
            var result = round.Result;
            hudMotion.LockWorkshop();
            SetAdvanceLabel(Loc.T("친구 기다리는 중…"));
            GameSfx.Play("StampThud", 0.6f);
            try
            {
                await Online.CounterTogether.Agree(Online.PairTopic.Result, 0f, this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) { return; }
            finally { waitingFriend = false; }
            if (round.Phase != RoundPhase.Ending || round.Result != result || !endingReady) return;
            endingReady = false;
            hudMotion.HighlightWorkshop(false);
            round.CompleteEnding(result);
            RefreshPhase();
        }

        /// <summary>
        /// Online, the maker's "창고로" stays grey until the friend upstairs pressed "찾았어!" for this customer, then
        /// lights up with a bounce.
        /// </summary>
        private void RefreshStorageLock()
        {
            var order = round.CurrentOrder;
            if (order != lockOrder)
            {
                lockOrder = order;
                lockSince = Time.unscaledTime;
            }
            bool locked = round.Phase == RoundPhase.Consult && !endingReady && !Watching &&
                Online.CounterTogether.MemoStillHidden(lockSince);
            if (locked == memoLocked) return;
            memoLocked = locked;
            if (!enterWorkshopButton.interactable || round.Phase != RoundPhase.Consult) return;
            if (locked)
                hudMotion.LockWorkshop();
            else
            {
                hudMotion.HighlightWorkshop(true);
                OrderHudMotion.Bounce(enterWorkshopButton.transform, 1.2f);
                GameSfx.Play("FoundBell", 0.7f);
            }
        }

        private void Serve()
        {
            if (crafting || chosenBottle == BottleShape.Unspecified || chosenInk == 0) return;
            servedBottle = prescriptionFeel.ChosenBottleSprite;
            servedInk = prescriptionFeel.ChosenInkSprite;
            var (potion, forWhom) = PotionLabelText.For(round.CurrentOrder, round.SubmittedIngredients);
            var craft = new PotionCrafting.Order(prescriptionFeel.ChosenBottleRect, servedBottle,
                prescriptionFeel.ChosenInkRect, servedInk, prescriptionFeel.ChosenInkColour, potion, forWhom,
                portraitView != null ? (RectTransform)portraitView.transform : null);
            // the score and the clock are settled now; the crafting scene plays before the customer reacts
            if (!round.TryServe(chosenBottle, chosenInk)) return;
            CraftAndServe(craft, Online.CounterTogether.FriendShares(false)).Forget();
        }

        private bool crafting;
        private NamePlateDrop namePlate;

        private async UniTaskVoid CraftAndServe(PotionCrafting.Order craft, bool together = false)
        {
            crafting = true;
            serveButton.interactable = false;
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                // online, the potion is made on both screens at the same moment: the maker waits here while the
                // friend walks down and watches the bottle and ink being picked
                sharedEnding = false;
                if (together)
                {
                    var serveRect = (RectTransform)serveButton.transform;
                    if (!Watching) FloatingNote.Show(serveRect, Loc.T("친구가 내려오는 중…"), 0f);
                    try { sharedEnding = await Online.CounterTogether.Agree(Online.PairTopic.Ending, Watching ? 10f : 20f, token); }
                    finally { FloatingNote.Hide(serveRect); }
                }
                await PotionCrafting.Play(craft, token);
            }
            catch (OperationCanceledException) { return; }
            finally
            {
                crafting = false;
            }
            GameSfx.Play("Serve", 0.8f);
            RefreshPhase();
        }

        /// <summary>The customer leans a little towards the bottle or ink the player is pointing at.</summary>
        private void LookAtChoice(RectTransform choice)
        {
            if (portraitView == null || !portraitView.isActiveAndEnabled) return;
            if (choice == null || round.Phase != RoundPhase.Prescribe)
            {
                portraitView.LookToward(0f);
                return;
            }
            float side = (ScreenX(choice) - ScreenX((RectTransform)portraitView.transform)) / (Screen.width * 0.35f);
            portraitView.LookToward(side);
        }

        private static float ScreenX(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return rect.position.x;
            var cam = canvas.rootCanvas.worldCamera != null ? canvas.rootCanvas.worldCamera : Camera.main;
            return cam != null ? cam.WorldToScreenPoint(rect.position).x : rect.position.x;
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
                impatience?.Reset();
                if (showPortrait) portraitView.LookToward(0f);
            }

            if (consult || ending)
                dialogueView.SetSpeaker(Loc.T(round.CurrentOrder.Voice?.DisplayName ?? string.Empty));

            if (consult)
            {
                SetAdvanceLabel(Loc.T("창고로"));
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
                BottleShape.Square => Loc.T("네모 병"),
                BottleShape.Heart => Loc.T("하트 병"),
                BottleShape.Round => Loc.T("동그란 병"),
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
            namePlate?.Present();
            enterWorkshopButton.gameObject.SetActive(true);
            await dialogueView.PlaySymptoms(order.Dialogue, order.Disease.DefaultSymptomEffect, round.Tuning.TextSpeed, token);

            if (round.Phase == RoundPhase.Consult && round.CurrentOrder == order)
            {
                enterWorkshopButton.interactable = true;
                if (memoLocked) hudMotion.LockWorkshop();
                else hudMotion.HighlightWorkshop(true);
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
                await TimeoutMoment.Play(impatience, order, token);
                await dialogueView.PlayReaction(Loc.T(order.GetReactionLine("timeout")), speed, token, "timeout");
                await UniTask.Delay(gap, ignoreTimeScale: true, cancellationToken: token);
                round.CompleteEnding(result);
                return;
            }
            else
            {
                await dialogueView.PlayReaction(order.GetReactionLine(result.ReceiveDialogueKey), speed, token,
                    result.ReceiveDialogueKey);
                await UniTask.Delay(gap, ignoreTimeScale: true, cancellationToken: token);
                await CustomerDrink.Play(portraitView, servedBottle, servedInk, token);
                await ApplyEffectAsync(result.Effect, token);
                await dialogueView.PlayReaction(order.GetReactionLine(result.DrinkDialogueKey), speed, token,
                    result.DrinkDialogueKey);
            }
            if (round.Phase != RoundPhase.Ending || round.Result != result) return;
            endingReady = true;
            SetAdvanceLabel(Loc.T("결과 보기"));
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
            if (effect == CustomerEffect.Recovered && portraitView.isActiveAndEnabled)
            {
                // cured: the smile comes the moment the bottle is set down, before they say a word
                var happy = round.CurrentOrder?.Customer.HappyExpression;
                if (happy != null && portraitColors != null) portraitView.Show(round.CurrentOrder, portraitColors, happy);
                portraitView.Speak(1f);
                GameSfx.Play("FoundBell", 0.45f, 1.15f, 0.04f);
                await UniTask.Delay(TimeSpan.FromSeconds(0.35), ignoreTimeScale: true, cancellationToken: token);
            }
            else if (effect == CustomerEffect.Slime && round.SlimePortrait != null)
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
