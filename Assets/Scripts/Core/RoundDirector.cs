using System;
using System.Collections.Generic;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionShop
{
    public enum RoundPhase { Waiting, Consult, Storage, Prescribe, Result }

    [DisallowMultipleComponent]
    public sealed class RoundDirector : MonoBehaviour
    {
        private const string CounterScene = "01_Order";
        private const string WorkshopScene = "02_Workshop";

        [SerializeField] private PotionCatalog catalog;

        private readonly System.Random seedSource = new();
        private LoadedCatalog loadedCatalog;
        private AsyncOperation sceneLoad;
        private double deadline;

        public static RoundDirector Instance { get; private set; }
        public RoundPhase Phase { get; private set; }
        public GeneratedOrder CurrentOrder { get; private set; }
        public IReadOnlyList<string> SubmittedIngredients { get; private set; }
        public RoundResult Result { get; private set; }
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
        }

        private void Update()
        {
            ExpireIfNeeded();
            if (Phase == RoundPhase.Result)
                LoadSceneIfNeeded(CounterScene);
        }

        public bool BeginOrder()
        {
            if (Phase is not (RoundPhase.Waiting or RoundPhase.Result))
                return false;

            loadedCatalog ??= CatalogLoader.Load(catalog);
            string previous = CurrentOrder?.Disease.Id;
#if UNITY_EDITOR
            CurrentOrder = EditorOrderFactory != null ? EditorOrderFactory(loadedCatalog, previous) :
                OrderGenerator.Generate(loadedCatalog, seedSource.Next(), previous);
#else
            CurrentOrder = OrderGenerator.Generate(loadedCatalog, seedSource.Next(), previous);
#endif
            SubmittedIngredients = null;
            Result = null;
            deadline = Time.unscaledTimeAsDouble + loadedCatalog.Tuning.TimeLimitSeconds;
            Phase = RoundPhase.Consult;
            return true;
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
            Phase = RoundPhase.Result;
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
            Phase = RoundPhase.Result;
            return true;
        }

        private bool LoadSceneIfNeeded(string sceneName)
        {
            if (sceneLoad != null && !sceneLoad.isDone)
                return false;
            if (SceneManager.GetActiveScene().name == sceneName)
                return true;

            sceneLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            return sceneLoad != null;
        }
    }
}
