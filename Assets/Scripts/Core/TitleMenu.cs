using PotionShop.Online;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The title screen's menu under the logo: 혼자하기 walks to the counter, 함께하기 opens the online co-op window,
    /// 옵션 opens the settings on tabs with the credits (<see cref="OptionsWindow"/>) and 게임끄기 asks once before quitting.
    /// The four buttons are copies of <see cref="template"/>, the counter's own wooden button, centred under the
    /// logo on a soft dark pad (게임끄기 a little apart and in red); each shows clearly when it is pointed at (<see cref="TitleMenuItem"/>) and the arrow keys walk them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleMenu : MonoBehaviour
    {
        [SerializeField] private Button template;
        [SerializeField, Min(0f)] private float spacing = 76f;
        [SerializeField, Min(0f)] private float quitGap = 14f;
        [SerializeField, Min(0f)] private float appearDelay = 0.9f;

        private GameObject options;
        private GameObject quitConfirm;
        private OptionsWindow optionsWindow;
        private bool leaving;
        private readonly System.Collections.Generic.List<TitleMenuItem> items = new();

        private void Start()
        {
            var parent = (RectTransform)template.transform.parent;
            var templateRect = (RectTransform)template.transform;
            var first = templateRect.anchoredPosition;
            var entries = new (string label, UnityEngine.Events.UnityAction action)[]
            {
                (Loc.T("혼자하기"), PlaySolo),
                (Loc.T("함께하기"), PlayTogether),
                (Loc.T("옵션"), () => Show(options, true)),
                (Loc.T("게임끄기"), () => Show(quitConfirm, true)),
            };

            // a soft dark pad behind the column, so the buttons read against the busy counter
            float height = spacing * (entries.Length - 1) + quitGap + templateRect.sizeDelta.y;
            var pad = OnlineUi.Rect("Menu Pad", parent, templateRect.anchorMin, templateRect.anchorMax);
            pad.pivot = new Vector2(0.5f, 1f);
            pad.anchoredPosition = first + new Vector2(0f, templateRect.sizeDelta.y * 0.5f + 60f);
            pad.sizeDelta = new Vector2(templateRect.sizeDelta.x + 260f, height + 120f);
            OnlineUiParts.Shade(pad, 0.5f);
            pad.SetSiblingIndex(templateRect.GetSiblingIndex());

            for (int i = 0; i < entries.Length; i++)
            {
                var button = Instantiate(template, parent);
                button.name = "Menu " + entries[i].label;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(entries[i].action);
                SetLabel(button, entries[i].label);
                var rect = (RectTransform)button.transform;
                bool quit = i == entries.Length - 1;
                rect.anchoredPosition = first + Vector2.down * (spacing * i + (quit ? quitGap : 0f));
                // leaving is set apart by colour: a dusty red board with cream lettering
                if (quit) Recolour(button, QuitBoard, QuitInk);
                var item = button.gameObject.AddComponent<TitleMenuItem>();
                item.Menu = this;
                item.Settle();
                item.Appear(appearDelay + i * 0.08f);
                items.Add(item);
            }
            template.gameObject.SetActive(false);
            // arrow keys walk the list top to bottom and wrap
            for (int i = 0; i < items.Count; i++)
            {
                var button = items[i].GetComponent<Button>();
                button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i + items.Count - 1) % items.Count].GetComponent<Button>(),
                    selectOnDown = items[(i + 1) % items.Count].GetComponent<Button>(),
                };
            }

            var config = OnlineCoopConfig.Load();
            var font = config != null ? config.BoldFont : null;
            var canvas = OnlineUi.Canvas("TitleWindows", 45, transform);
            options = BuildOptions((RectTransform)canvas.transform, font);
            quitConfirm = BuildQuit((RectTransform)canvas.transform, font);

            // a new language: build the title again in it, with the options sheet still open
            Loc.Changed += Rebuild;
            if (reopenOptions)
            {
                reopenOptions = false;
                options.SetActive(true);
            }
        }

        private static bool reopenOptions;

        private void Rebuild()
        {
            reopenOptions = options != null && options.activeSelf;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }

        private void OnDestroy() => Loc.Changed -= Rebuild;

        /// <summary>One entry pointed at shows it; the others step back.</summary>
        internal void Refresh()
        {
            TitleMenuItem pointed = null;
            foreach (var item in items) if (item.Pointed) pointed = item;
            foreach (var item in items) item.SetState(item == pointed, pointed != null);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (options.activeSelf) Show(options, false);
                else if (quitConfirm.activeSelf) Show(quitConfirm, false);
                return;
            }
            // the first arrow press picks up the menu where nothing is selected yet
            var events = UnityEngine.EventSystems.EventSystem.current;
            bool windowOpen = options.activeSelf || quitConfirm.activeSelf;
            if (!windowOpen && events != null && items.Count > 0 &&
                (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) &&
                (events.currentSelectedGameObject == null || !events.currentSelectedGameObject.activeInHierarchy))
                events.SetSelectedGameObject(items[keyboard.upArrowKey.wasPressedThisFrame ? items.Count - 1 : 0].gameObject);
        }

        private void PlaySolo()
        {
            if (leaving) return;
            leaving = SceneDimmer.Instance.TryLoad(OnlineCoopConfig.CounterScene);
        }

        private static void PlayTogether() => TitleOnlinePanel.Open();

        private static readonly Color QuitBoard = new(0.72f, 0.38f, 0.3f), QuitInk = new(1f, 0.95f, 0.86f);

        private static void Recolour(Button button, Color board, Color ink)
        {
            if (button.targetGraphic != null) button.targetGraphic.color = board;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.color = ink;
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.color = ink;
        }

        private static void SetLabel(Button button, string text)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = text;
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = text;
        }

        private void Show(GameObject window, bool show)
        {
            if (show) optionsWindow?.Refresh();
            window.SetActive(show);
            GameSfx.Play("Paper", 0.6f, show ? 1.05f : 0.95f, 0.05f);
            if (show) OrderHudMotion.Bounce(window.transform.GetChild(0), 1.04f);
        }

        /// <summary>A dimmed screen with a parchment window and a close button, hidden.</summary>
        private RectTransform Window(RectTransform root, string name, Vector2 size, GameObject[] holder)
        {
            var screen = OnlineUi.Rect(name, root, Vector2.zero, Vector2.one);
            var dim = OnlineUi.Fill(screen, new Color(0.12f, 0.06f, 0.03f, 0.6f));
            var card = OnlineUi.Box("Window", screen, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            card.pivot = new Vector2(0.5f, 0.5f);
            OnlineUiParts.Draw(card, "UI_Window", true);
            var go = screen.gameObject;
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => Show(go, false));
            OnlineUiParts.Button(OnlineUi.Box("Close", card, new Vector2(1f, 1f), new Vector2(-44f, -40f),
                new Vector2(64f, 64f)), "UI_CloseButton", null, 0, null, () => Show(go, false));
            holder[0] = go;
            go.SetActive(false);
            return card;
        }

        private GameObject BuildOptions(RectTransform root, Font font)
        {
            var holder = new GameObject[1];
            var card = Window(root, "Options", new Vector2(1040f, 640f), holder);
            optionsWindow = new OptionsWindow(card, font);
            return holder[0];
        }

        private GameObject BuildQuit(RectTransform root, Font font)
        {
            var holder = new GameObject[1];
            var card = Window(root, "Quit", new Vector2(640f, 360f), holder);
            OnlineUi.Label(Row(card, 80f, 70f), Loc.T("게임을 끌까요?"), 40, font, OnlineUiParts.Ink);
            var go = holder[0];
            OnlineUiParts.Button(Cell(card, 200f, 84f, 0.05f, 0.48f), "UI_SmallButton", Loc.T("그만할래요"), 30, font, Quit);
            OnlineUiParts.Button(Cell(card, 200f, 84f, 0.52f, 0.95f), "UI_SmallButton", Loc.T("더 할래요"), 30, font,
                () => Show(go, false));
            return go;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static RectTransform Row(RectTransform card, float top, float height) =>
            OnlineUi.Rect("Row", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(64f, -top - height),
                new Vector2(-64f, -top));

        private static RectTransform Cell(RectTransform card, float top, float height, float from, float to)
        {
            var rect = OnlineUi.Rect("Cell", card, new Vector2(from, 1f), new Vector2(to, 1f), new Vector2(0f, -top - height),
                new Vector2(0f, -top));
            rect.offsetMin += new Vector2(48f, 0f);
            rect.offsetMax -= new Vector2(48f, 0f);
            return rect;
        }
    }
}
