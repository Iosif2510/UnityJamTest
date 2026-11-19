using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionShop
{
    public enum RoundPhase { Waiting, Consult, Storage, Prescribe, Result, Ending, Preparing }

    [DisallowMultipleComponent]
    public sealed class RoundDirector : MonoBehaviour
    {
        private const string CounterScene = "01_Order";
        private const string WorkshopScene = "02_Workshop";

        [SerializeField] private PotionCatalog catalog;
        [SerializeField, Tooltip("Generate dialogue through the local OpenAI relay starting with the next order.")]
        private bool useDialogueApi;

        private readonly System.Random seedSource = new();
        private LoadedCatalog loadedCatalog;
        private string previousDiseaseId;
        private double deadline;
        private CancellationTokenSource dialoguePreparation;
        private OpenAiDialogueProvider onlineDialogue;

        public static RoundDirector Instance { get; private set; }
        public RoundPhase Phase { get; private set; }
        public GeneratedOrder CurrentOrder { get; private set; }
        public IReadOnlyList<string> SubmittedIngredients { get; private set; }
        public RoundResult Result { get; private set; }
        public RoundTuning Tuning => loadedCatalog?.Tuning;
        public Sprite SlimePortrait => catalog.Customers.SlimePortrait;
#if UNITY_EDITOR
        public static Func<LoadedCatalog, string, GeneratedOrder> EditorOrderFactory;
#endif

        public double RemainingSeconds => HasDeadline ? Math.Max(0d, deadline - Time.unscaledTimeAsDouble) : 0d;

        private bool HasDeadline => Phase is RoundPhase.Consult or RoundPhase.Storage or RoundPhase.Prescribe;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            Instance = null;
#if UNITY_EDITOR
            EditorOrderFactory = null;
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            dialoguePreparation?.Cancel();
            onlineDialogue?.Dispose();
        }

        private void Update()
        {
            ExpireIfNeeded();
            if (Phase is RoundPhase.Ending or RoundPhase.Result)
                LoadSceneIfNeeded(CounterScene);
        }

        public bool BeginOrder()
        {
            if (Phase != RoundPhase.Waiting)
                return false;

            loadedCatalog ??= CatalogLoader.Load(catalog);
#if UNITY_EDITOR
            CurrentOrder = EditorOrderFactory != null ? EditorOrderFactory(loadedCatalog, previousDiseaseId) :
                OrderGenerator.Generate(loadedCatalog, seedSource.Next(), previousDiseaseId);
#else
            CurrentOrder = OrderGenerator.Generate(loadedCatalog, seedSource.Next(), previousDiseaseId);
#endif
            previousDiseaseId = CurrentOrder.Disease.Id;
            if (useDialogueApi && CurrentOrder.Voice != null)
            {
                Phase = RoundPhase.Preparing;
                dialoguePreparation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                onlineDialogue ??= new OpenAiDialogueProvider();
                PrepareDialogue(CurrentOrder, dialoguePreparation, onlineDialogue).Forget();
            }
            else
                StartConsultation();
            return true;
        }

        private async UniTask PrepareDialogue(GeneratedOrder order, CancellationTokenSource preparation,
            IDialogueProvider provider)
        {
            using (preparation)
            {
                var token = preparation.Token;
                var started = System.Diagnostics.Stopwatch.StartNew();
                using var timeout = preparation.CancelAfterSlim(OpenAiDialogueProvider.PreparationLimit, DelayType.Realtime);
                try
                {
                    var bundle = await provider.GenerateAsync(order, Tuning.LineLength, token);
                    if (IsPreparing(order) && !token.IsCancellationRequested &&
                        started.Elapsed < OpenAiDialogueProvider.PreparationLimit)
                        order.SetGeneratedDialogue(bundle.Symptoms, bundle.Reactions);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    if (IsPreparing(order))
                        Debug.LogWarning("[Order dialogue] Fallback: preparation_timeout.", this);
                }
                catch (DialogueProviderException exception)
                {
                    if (IsPreparing(order))
                        Debug.LogWarning($"[Order dialogue] Fallback: {exception.Reason}.", this);
                }
                finally
                {
                    if (dialoguePreparation == preparation)
                        dialoguePreparation = null;
                    if (IsPreparing(order))
                        StartConsultation();
                }
            }
        }

        private bool IsPreparing(GeneratedOrder order) =>
            Instance == this && this != null && Phase == RoundPhase.Preparing && CurrentOrder == order;

        private void StartConsultation()
        {
            deadline = Time.unscaledTimeAsDouble + loadedCatalog.Tuning.TimeLimitSeconds;
            Phase = RoundPhase.Consult;
        }

        public bool TryEnterStorage()
        {
            if (ExpireIfNeeded() || Phase != RoundPhase.Consult || !LoadSceneIfNeeded(WorkshopScene))
                return false;

            Phase = RoundPhase.Storage;
            return true;
        }

        public bool TrySubmitIngredients(IReadOnlyList<string> ingredientVariantIds)
        {
            if (ExpireIfNeeded() || Phase != RoundPhase.Storage || SubmittedIngredients != null ||
                ingredientVariantIds == null || ingredientVariantIds.Count != RecipeEntry.RequiredIngredientKinds)
                return false;

            var submission = new string[ingredientVariantIds.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < submission.Length; i++)
            {
                string id = ingredientVariantIds[i];
                if (string.IsNullOrEmpty(id) || !loadedCatalog.Variants.ContainsKey(id) || !seen.Add(id))
                    return false;
                submission[i] = id;
            }
            SubmittedIngredients = Array.AsReadOnly(submission);
            return true;
        }

        public bool TryFinishBrewing()
        {
            if (ExpireIfNeeded() || Phase != RoundPhase.Storage || SubmittedIngredients == null ||
                !LoadSceneIfNeeded(CounterScene))
                return false;

            Phase = RoundPhase.Prescribe;
            return true;
        }

        public bool TryServe(BottleShape bottle, int inkNumber)
        {
            if (ExpireIfNeeded() || Phase != RoundPhase.Prescribe || SubmittedIngredients == null ||
                bottle is not (BottleShape.Square or BottleShape.Heart or BottleShape.Round) ||
                inkNumber is < 1 or > 6)
                return false;

            Result = RoundJudge.Score(CurrentOrder, SubmittedIngredients, bottle, inkNumber, RemainingSeconds,
                loadedCatalog.Tuning);
            Phase = RoundPhase.Ending;
            return true;
        }

        internal void CompleteEnding(RoundResult result)
        {
            if (Phase == RoundPhase.Ending && Result == result)
                Phase = RoundPhase.Result;
        }

        internal bool DismissResult()
        {
            if (Phase != RoundPhase.Result)
                return false;

            CurrentOrder = null;
            SubmittedIngredients = null;
            Result = null;
            Phase = RoundPhase.Waiting;
            return true;
        }

        public static string FormatClock(double seconds)
        {
            int wholeSeconds = (int)Math.Ceiling(Math.Max(0d, seconds));
            return $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
        }

        private bool ExpireIfNeeded()
        {
            if (!HasDeadline || Time.unscaledTimeAsDouble < deadline)
                return false;

            Result = RoundResult.TimeExpired();
            Phase = RoundPhase.Ending;
            return true;
        }

        private bool LoadSceneIfNeeded(string sceneName)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
                return true;

            return SceneDimmer.Instance.TryLoad(sceneName, () =>
            {
                ExpireIfNeeded();
                return Phase is RoundPhase.Ending or RoundPhase.Result ? CounterScene : sceneName;
            });
        }
    }
}
