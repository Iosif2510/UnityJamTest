using LitMotion;
using PotionShop.Data;
using PotionShop.Online;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// Playing alone there is no friend upstairs to read the memos out, so Tab opens the loft's desk
    /// (<see cref="LoftDesk"/>, without the 찾았어! signal) over the counter or the storage room. The clock keeps
    /// running while it is open. In the storage room the view stops turning and the cursor is freed while reading.
    /// Playing together it never opens: the memos belong upstairs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloMemoBook : MonoBehaviour
    {
        private const string Counter = "01_Order", Workshop = "02_Workshop";

        private Canvas canvas;
        private CanvasGroup group;
        private LoftDesk desk;
        private RectTransform hint;
        private bool open;
        private GeneratedOrder shownOrder;
        private MotionHandle fade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (OnlineCoopConfig.Load() == null) return;
            var go = new GameObject("SoloMemoBook");
            DontDestroyOnLoad(go);
            go.AddComponent<SoloMemoBook>();
        }

        private void Start()
        {
            var config = OnlineCoopConfig.Load();
            canvas = OnlineUi.Canvas("SoloMemoCanvas", 80, transform);
            var root = (RectTransform)canvas.transform;

            // the hint, shown wherever the book can be opened
            hint = OnlineUi.Box("Hint", root, Vector2.zero, new Vector2(28f, 28f), new Vector2(210f, 56f));
            OnlineUi.Fill(hint, new Color(0.12f, 0.06f, 0.03f, 0.6f), false);
            var key = OnlineUi.Box("Key", hint, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(64f, 38f));
            key.pivot = new Vector2(0f, 0.5f);
            OnlineUi.Fill(key, OnlineUiParts.Cream, false);
            OnlineUi.Label(OnlineUi.Rect("Text", key, Vector2.zero, Vector2.one), "Tab", 22, config.BoldFont, OnlineUiParts.Ink);
            // built once for the whole game: the Korean stays the key, so the words follow a later language change
            OnlineUiParts.Outlined(OnlineUi.Label(OnlineUi.Rect("Label", hint, Vector2.zero, Vector2.one, new Vector2(86f, 0f),
                Vector2.zero), "메모책", 26, config.BoldFont, OnlineUiParts.Cream, TextAnchor.MiddleLeft))
                .gameObject.AddComponent<LocalizedText>();

            // the book: the loft's desk under the same tabs and sheets
            var book = OnlineUi.Rect("Book", root, Vector2.zero, Vector2.one);
            group = book.gameObject.AddComponent<CanvasGroup>();
            OnlineUiParts.Backdrop(book, "BG_Loft");
            desk = book.gameObject.AddComponent<LoftDesk>();
            desk.Build(book, config, null);
            var title = OnlineUi.Box("Title", book, new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(900f, 64f));
            OnlineUiParts.Outlined(OnlineUi.Label(title, "메모책  ·  Tab으로 닫기", 34, config.BoldFont, OnlineUiParts.Cream,
                TextAnchor.MiddleLeft)).gameObject.AddComponent<LocalizedText>();
            OnlineUiParts.Button(OnlineUi.Box("Close", book, new Vector2(1f, 1f), new Vector2(-36f, -28f),
                new Vector2(72f, 72f)), "UI_CloseButton", null, 0, config.BoldFont, () => Show(false));
            book.gameObject.SetActive(false);

            SceneManager.activeSceneChanged += (_, _) => { if (open) Show(false, instant: true); };
        }

        private static bool Allowed
        {
            get
            {
                if (OnlineSession.CurrentRole != OnlineRole.Offline) return false;
                string scene = SceneManager.GetActiveScene().name;
                return scene == Counter || scene == Workshop;
            }
        }

        private void Update()
        {
            if (canvas == null) return;
            bool allowed = Allowed;
            if (hint.gameObject.activeSelf != (allowed && !open))
                hint.gameObject.SetActive(allowed && !open);
            if (open && !allowed)
            {
                Show(false, instant: true);
                return;
            }

            var keyboard = Keyboard.current;
            if (allowed && keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame) Show(!open);
                else if (open && keyboard.escapeKey.wasPressedThisFrame) Show(false);
            }
            if (!open) return;

            // a new customer: every sheet is unread again
            var order = RoundDirector.Instance != null ? RoundDirector.Instance.CurrentOrder : null;
            bool newCustomer = order != null && order != shownOrder;
            if (newCustomer) shownOrder = order;
            desk.Tick(RoundDirector.Instance != null ? RoundDirector.Instance.Phase : (RoundPhase?)null, false, newCustomer);
        }

        private void Show(bool show, bool instant = false)
        {
            if (open == show) return;
            open = show;
            var book = group.gameObject;
            fade.TryCancel();
            if (show)
            {
                book.SetActive(true);
                desk.OpenMemos();
                GameSfx.Play("PageTurn", 0.75f, 0.95f, 0.05f);
            }
            else if (!instant)
                GameSfx.Play("PageTurn", 0.6f, 1.1f, 0.05f);
            Hold(show);
            if (instant)
            {
                group.alpha = show ? 1f : 0f;
                book.SetActive(show);
                return;
            }
            float from = group.alpha, to = show ? 1f : 0f;
            fade = LMotion.Create(from, to, 0.18f).WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (!open) book.SetActive(false); })
                .Bind(a => group.alpha = a).AddTo(gameObject);
        }

        /// <summary>In the storage room: stop turning and free the cursor while reading, and take it back after.</summary>
        private static void Hold(bool reading)
        {
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player == null || !player.isActiveAndEnabled) return;
            player.InputEnabled = !reading;
            player.SetCursorLocked(!reading);
        }
    }
}
