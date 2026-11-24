using PotionShop.Online;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The title screen's menu under the logo: 혼자하기 walks to the counter, 함께하기 opens the online co-op window,
    /// 옵션 opens a small settings sheet (volume, full screen, memo language) and 게임끄기 asks once before quitting.
    /// The four buttons are copies of <see cref="template"/>, the counter's own wooden button, in a column on a soft
    /// dark pad; each shows clearly when it is pointed at (<see cref="TitleMenuItem"/>) and the arrow keys walk them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleMenu : MonoBehaviour
    {
        [SerializeField] private Button template;
        [SerializeField, Min(0f)] private float spacing = 116f;
        [SerializeField, Min(0f)] private float quitGap = 30f;
        [SerializeField, Min(0f)] private float appearDelay = 0.9f;

        private GameObject options;
        private GameObject quitConfirm;
        private MemoLanguagePicker languages;
        private Toggle fullScreen;
        private bool leaving;
        private readonly System.Collections.Generic.List<TitleMenuItem> items = new();

        private void Start()
        {
            var parent = (RectTransform)template.transform.parent;
            var templateRect = (RectTransform)template.transform;
            var first = templateRect.anchoredPosition;
            var entries = new (string label, UnityEngine.Events.UnityAction action)[]
            {
                ("혼자하기", PlaySolo),
                ("함께하기", PlayTogether),
                ("옵션", () => Show(options, true)),
                ("게임끄기", () => Show(quitConfirm, true)),
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
                // leaving is the quiet one: a little smaller
                if (quit) rect.localScale = Vector3.one * 0.86f;
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
        }

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

        private static void SetLabel(Button button, string text)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = text;
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = text;
        }

        private void Show(GameObject window, bool show)
        {
            if (show)
            {
                languages?.Refresh();
                if (fullScreen != null) fullScreen.SetIsOnWithoutNotify(Screen.fullScreen);
            }
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
            var card = Window(root, "Options", new Vector2(860f, 500f), holder);
            OnlineUi.Label(Row(card, 56f, 60f), "옵션", 42, font, OnlineUiParts.Ink, TextAnchor.MiddleLeft);

            VolumeSlider.Build(Row(card, 150f, 56f), font, OnlineUiParts.Ink);

            // full screen on or off, as a ticked box
            var row = Row(card, 240f, 56f);
            OnlineUi.Label(OnlineUi.Rect("Label", row, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(240f, 0f)),
                "전체 화면", 26, font, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            // an ink rim drawn behind the cream box (a child would draw over it)
            var rim = OnlineUi.Box("Rim", row, new Vector2(0f, 0.5f), new Vector2(247f, 0f), new Vector2(54f, 54f));
            rim.pivot = new Vector2(0f, 0.5f);
            OnlineUi.Fill(rim, OnlineUiParts.Ink, false);
            var box = OnlineUi.Box("Box", row, new Vector2(0f, 0.5f), new Vector2(250f, 0f), new Vector2(48f, 48f));
            box.pivot = new Vector2(0f, 0.5f);
            var boxImage = OnlineUi.Fill(box, new Color(0.99f, 0.95f, 0.87f));
            var tick = OnlineUi.Rect("Tick", box, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            var tickImage = OnlineUi.Fill(tick, OnlineUiParts.StampRed, false);
            fullScreen = box.gameObject.AddComponent<Toggle>();
            fullScreen.targetGraphic = boxImage;
            fullScreen.graphic = tickImage;
            fullScreen.SetIsOnWithoutNotify(Screen.fullScreen);
            fullScreen.onValueChanged.AddListener(on =>
            {
                Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                GameSfx.Play("Tick", 0.8f);
            });

            OnlineUi.Label(Row(card, 330f, 40f), "메모 언어 (함께하기에서 위층 친구가 읽는 메모)", 24, font, OnlineUiParts.Ink,
                TextAnchor.MiddleLeft);
            languages = new MemoLanguagePicker(Row(card, 380f, 64f), font);
            return holder[0];
        }

        private GameObject BuildQuit(RectTransform root, Font font)
        {
            var holder = new GameObject[1];
            var card = Window(root, "Quit", new Vector2(640f, 360f), holder);
            OnlineUi.Label(Row(card, 80f, 70f), "게임을 끌까요?", 40, font, OnlineUiParts.Ink);
            var go = holder[0];
            OnlineUiParts.Button(Cell(card, 200f, 84f, 0.05f, 0.48f), "UI_SmallButton", "그만할래요", 30, font, Quit);
            OnlineUiParts.Button(Cell(card, 200f, 84f, 0.52f, 0.95f), "UI_SmallButton", "더 할래요", 30, font,
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
