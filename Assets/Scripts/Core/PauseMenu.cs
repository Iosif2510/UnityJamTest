using Cysharp.Threading.Tasks;
using LitMotion;
using PotionShop.Online;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// Esc anywhere in the game (not on the title, which has its own menu) opens the options window — the same tabs as
    /// the title's — with 계속하기 and 타이틀로 underneath. The game keeps running behind it (a friend may be playing
    /// along). In the storeroom it frees the mouse; a click on the room locks it again as usual.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    internal sealed class PauseMenu : MonoBehaviour
    {
        private static PauseMenu instance;

        private GameObject screen;
        private CanvasGroup group;
        private OptionsWindow options;
        private bool leaving;

        /// <summary>True while the window is up, so other Esc handlers can stand aside.</summary>
        internal static bool IsOpen => instance != null && instance.screen != null && instance.screen.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            var go = new GameObject("PauseMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            Loc.Changed += Rebuild;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy()
        {
            Loc.Changed -= Rebuild;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (instance == this) instance = null;
        }

        private void OnSceneChanged(Scene from, Scene to)
        {
            leaving = false;
            Show(false);
        }

        private static bool OnTitle => SceneManager.GetActiveScene().name == OnlineCoopConfig.TitleScene;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (IsOpen)
            {
                Show(false);
                return;
            }
            // the title has its own menu; the memo book closes on Esc first
            if (OnTitle || leaving || SoloMemoBook.OpenNow) return;
            Show(true);
        }

        private void Show(bool show)
        {
            if (show && screen == null) Build();
            if (screen == null) return;
            if (show == screen.activeSelf) return;
            screen.SetActive(show);
            if (!show) return;
            options.Refresh();
            // free the mouse in the storeroom so the window can be used
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSfx.Play("Paper", 0.6f, 1.05f, 0.05f);
            group.alpha = 0f;
            LMotion.Create(0f, 1f, 0.2f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(a => { if (group != null) group.alpha = a; }).AddTo(screen);
        }

        /// <summary>A new language: the window is drawn again in it, still open if it was.</summary>
        private void Rebuild()
        {
            if (screen == null) return;
            bool wasOpen = screen.activeSelf;
            foreach (Transform child in transform) Destroy(child.gameObject);
            screen = null;
            if (wasOpen) Show(true);
        }

        private void Build()
        {
            var config = OnlineCoopConfig.Load();
            var font = config.BoldFont;
            // under the scene dimmer, so a floor change still fades over it
            var canvas = OnlineUi.Canvas("PauseMenuCanvas", SceneDimmer.SortingOrder - 2, transform);
            var root = (RectTransform)canvas.transform;
            screen = OnlineUi.Rect("Pause", root, Vector2.zero, Vector2.one).gameObject;
            var dim = OnlineUi.Fill((RectTransform)screen.transform, new Color(0.12f, 0.06f, 0.03f, 0.62f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => Show(false));

            var card = OnlineUi.Box("Window", screen.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 790f));
            card.pivot = new Vector2(0.5f, 0.5f);
            group = card.gameObject.AddComponent<CanvasGroup>();
            OnlineUiParts.Draw(card, "UI_Window", true);
            OnlineUiParts.Button(OnlineUi.Box("Close", card, new Vector2(1f, 1f), new Vector2(-44f, -40f),
                new Vector2(64f, 64f)), "UI_CloseButton", null, 0, null, () => Show(false));

            // the options take the top of the card, the two buttons the bottom
            var optionsArea = OnlineUi.Rect("Options", card, Vector2.zero, Vector2.one, new Vector2(0f, 150f), Vector2.zero);
            options = new OptionsWindow(optionsArea, font);

            var resume = OnlineUi.Box("Resume", card, new Vector2(0.5f, 0f), new Vector2(-170f, 56f), new Vector2(300f, 88f));
            resume.pivot = new Vector2(0.5f, 0f);
            OnlineUiParts.Button(resume, "UI_SmallButton", Loc.T("계속하기"), 32, font, () => Show(false));
            var title = OnlineUi.Box("Title", card, new Vector2(0.5f, 0f), new Vector2(170f, 56f), new Vector2(300f, 88f));
            title.pivot = new Vector2(0.5f, 0f);
            OnlineUiParts.Button(title, "UI_SmallButton", Loc.T("타이틀로"), 32, font, () => LeaveToTitle().Forget());
            screen.SetActive(false);
        }

        private async UniTaskVoid LeaveToTitle()
        {
            if (leaving) return;
            leaving = true;
            Show(false);
            leaving = await BackToTitleButton.LeaveToTitle();
        }
    }
}
