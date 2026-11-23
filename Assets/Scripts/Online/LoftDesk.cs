using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's desk in the loft (handoff 조립_시안 01–03): three tabs on the left, and the wooden desk of
    /// <c>BG_Loft</c> as the table. Picking a tab deals its sheets onto the desk; clicking one unfolds it. While unfolded
    /// the narrator flips to the neighbouring sheets with the arrows, and on the potion memos presses "찾았어!" to tell
    /// the maker downstairs that it is time for the storage room. The sheet stays open so the ingredients can be read out.
    /// The sheets are landscape drawings with the writing laid over them in the chosen language (<see cref="MemoSheetView"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftDesk : MonoBehaviour
    {
        private static readonly Vector2 CardSize = new(450f, 253f);
        private static readonly Vector2 PaperPos = new(490f, 150f), PaperSize = new(1256f, 706f);
        private const float ToastSeconds = 2.6f;
        private static readonly Color PaperColor = new(0.96f, 0.92f, 0.82f);

        private sealed class Tab
        {
            public string Name;
            public string Count;
            public int[] Sheets;
            public bool CanSignal;
            public Button Button;
            public Image Image;
        }

        private OnlineCoopConfig config;
        private OnlineSession session;
        private Tab[] tabs;
        private int tabIndex = -1;
        private int page = -1;          // index into the current tab's sheets while unfolded, -1 on the table
        private readonly HashSet<int> seen = new();
        private bool signalSent;
        private int blinkTab = -1;
        private bool easy;

        private RectTransform table;
        private readonly List<RectTransform> cards = new();
        private GameObject unfolded;
        private CanvasGroup unfoldedGroup;
        private RectTransform paper;
        private MemoSheetView sheet;
        private readonly List<MemoSheetView> cardViews = new();
        private Button previous, next, found, toTable;
        private Text foundLabel, pageLabel;
        private RectTransform dots;
        private readonly List<Image> dotImages = new();
        private RectTransform toast;
        private float toastSince = float.NegativeInfinity;

        public void Build(RectTransform root, OnlineCoopConfig coopConfig, OnlineSession onlineSession)
        {
            config = coopConfig;
            session = onlineSession;
            var memos = new int[config.MemoSheets.Length - 2];
            for (int i = 0; i < memos.Length; i++) memos[i] = i + 1;
            tabs = new[]
            {
                new Tab { Name = "질병과 재료", Count = $"메모 {memos.Length}장", Sheets = memos, CanSignal = true },
                new Tab { Name = "포장", Count = "1장", Sheets = new[] { config.CounterManualIndex } },
                new Tab { Name = "안내서", Sheets = new[] { 0 } },
            };

            BuildTabs(root);
            table = OnlineUi.Rect("Table", root, Vector2.zero, Vector2.one);
            BuildUnfolded(root);
        }

        /// <summary>The guide is where the narrator starts while waiting for the first customer.</summary>
        public void OpenStart() => Select(2);

        private static RectTransform At(Transform parent, string name, Vector2 topLeft, Vector2 size)
        {
            var rect = OnlineUi.Box(name, parent, new Vector2(0f, 1f), new Vector2(topLeft.x, -topLeft.y), size);
            return rect;
        }

        // ---- tabs ------------------------------------------------------------------------------------------------
        private void BuildTabs(RectTransform root)
        {
            float[] tops = { 116f, 436f, 636f };
            float[] heights = { 300f, 180f, 112f };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var tab = tabs[i];
                var rect = At(root, "Tab " + tab.Name, new Vector2(30f, tops[i]), new Vector2(280f, heights[i]));
                tab.Button = OnlineUiParts.Button(rect, "LOFT_Tab", null, 0, config.BoldFont, () => Select(index));
                tab.Image = (Image)tab.Button.targetGraphic;
                bool counted = tab.Count != null;
                OnlineUi.Label(OnlineUi.Rect("Name", rect, new Vector2(0f, counted ? 0.32f : 0f), Vector2.one,
                    new Vector2(8f, 0f), new Vector2(-14f, 0f)), tab.Name, 36, config.BoldFont, OnlineUiParts.Ink);
                if (counted)
                    OnlineUi.Label(OnlineUi.Rect("Count", rect, Vector2.zero, new Vector2(1f, 0.32f), new Vector2(8f, 10f),
                        new Vector2(-14f, 0f)), tab.Count, 22, config.BoldFont, OnlineUiParts.InkSoft);
            }
        }

        private void Select(int index)
        {
            if (index == tabIndex && page < 0 && tabs[index].Sheets.Length > 1)
                return;
            tabIndex = index;
            RefreshTabs();
            // A tab with a single sheet opens it straight away; the potion memos are dealt onto the desk first.
            if (tabs[index].Sheets.Length == 1)
                Unfold(0);
            else
                DealCards();
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                bool selected = i == tabIndex;
                bool blink = i == blinkTab && !selected && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
                var sprite = OnlineUiParts.Get(selected ? "LOFT_Tab_Selected" : blink ? "LOFT_Tab_Blink" : "LOFT_Tab_Normal");
                if (tabs[i].Image.sprite == sprite)
                    continue;
                tabs[i].Image.sprite = sprite;
                var state = tabs[i].Button.spriteState;
                state.highlightedSprite = state.pressedSprite = selected ? sprite : OnlineUiParts.Get("LOFT_Tab_Hover");
                state.selectedSprite = sprite;
                tabs[i].Button.spriteState = state;
            }
        }

        // ---- table -----------------------------------------------------------------------------------------------
        private void DealCards()
        {
            page = -1;
            SetUnfolded(false);
            foreach (Transform child in table) Destroy(child.gameObject);
            cards.Clear();
            cardViews.Clear();

            // A soft dark pad quiets the props on the desk behind the cards.
            OnlineUiParts.Shade(At(table, "Shade", new Vector2(350f, 180f), new Vector2(1510f, 660f)), 0.55f);
            var hint = OnlineUi.Label(At(table, "Hint", new Vector2(390f, 852f), new Vector2(1440f, 48f)),
                "종이를 누르면 크게 펼쳐져요", 28, config.BoldFont, OnlineUiParts.Cream);
            OnlineUiParts.Outlined(hint);

            var sheets = tabs[tabIndex].Sheets;
            const int columns = 3;
            for (int i = 0; i < sheets.Length; i++)
            {
                int slot = i;
                var target = new Vector2(390f + (i % columns) * (CardSize.x + 40f) + CardSize.x * 0.5f,
                    -(214f + (i / columns) * (CardSize.y + 64f) + CardSize.y * 0.5f));
                var card = OnlineUi.Box("Card " + i, table, new Vector2(0f, 1f), target, CardSize);
                card.pivot = new Vector2(0.5f, 0.5f);
                card.anchoredPosition = target;
                var shadow = OnlineUi.Rect("Shadow", card, Vector2.zero, Vector2.one, new Vector2(-12f, -16f), new Vector2(12f, 8f));
                OnlineUiParts.Draw(shadow, "LOFT_PaperShadow");
                var face = OnlineUi.Fill(OnlineUi.Rect("Paper", card, Vector2.zero, Vector2.one), PaperColor);
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                var colors = button.colors;
                colors.highlightedColor = new Color(1f, 0.98f, 0.9f);
                colors.pressedColor = new Color(0.9f, 0.86f, 0.78f);
                button.colors = colors;
                button.onClick.AddListener(() => Unfold(slot));
                face.gameObject.AddComponent<CardHover>();
                var view = new MemoSheetView((RectTransform)face.transform);
                view.Show(sheets[i], false);
                cardViews.Add(view);
                if (seen.Contains(sheets[i]))
                {
                    var badge = OnlineUi.Box("Seen", card, new Vector2(1f, 1f), new Vector2(-10f, -8f), new Vector2(96f, 40f));
                    OnlineUiParts.Draw(badge, "LOFT_SeenBadge");
                    OnlineUi.Label(OnlineUi.Rect("Text", badge, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero),
                        "본 장", 22, config.BoldFont, OnlineUiParts.StampRed);
                }

                // Sheets fly in from the left one after another and settle slightly askew, like paper.
                float tilt = (i * 37 % 7 - 3) * 0.4f;
                var from = target + new Vector2(-1500f, 60f);
                card.anchoredPosition = from;
                card.localRotation = Quaternion.Euler(0f, 0f, 14f);
                LMotion.Create(0f, 1f, 0.42f).WithDelay(i * 0.06f).WithEase(Ease.OutCubic)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        card.anchoredPosition = Vector2.LerpUnclamped(from, target, t);
                        card.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(14f, tilt, t));
                    }).AddTo(card.gameObject);
                cards.Add(card);
            }
        }

        // ---- unfolded sheet --------------------------------------------------------------------------------------
        private void BuildUnfolded(RectTransform root)
        {
            unfolded = OnlineUi.Rect("Unfolded", root, Vector2.zero, Vector2.one).gameObject;
            var u = (RectTransform)unfolded.transform;
            unfoldedGroup = unfolded.AddComponent<CanvasGroup>();

            paper = At(u, "Paper", PaperPos, PaperSize);
            OnlineUiParts.Draw(OnlineUi.Rect("Shadow", paper, Vector2.zero, Vector2.one, new Vector2(-12f, -16f),
                new Vector2(12f, 8f)), "LOFT_PaperShadow");
            sheet = new MemoSheetView(paper);

            previous = Arrow(u, "Previous", 380f, false, () => Flip(-1));
            next = Arrow(u, "Next", 1772f, true, () => Flip(1));

            toTable = OnlineUiParts.Button(At(u, "ToTable", new Vector2(490f, 916f), new Vector2(186f, 72f)),
                "UI_SmallButton", "탁자로", 30, config.BoldFont, ShowTable);

            dots = At(u, "Dots", new Vector2(960f, 932f), new Vector2(240f, 40f));
            var layout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            pageLabel = OnlineUiParts.Outlined(OnlineUi.Label(At(u, "Page", new Vector2(1208f, 928f), new Vector2(120f, 48f)),
                string.Empty, 30, config.BoldFont, OnlineUiParts.Cream, TextAnchor.MiddleLeft));

            var foundRect = At(u, "Found", new Vector2(1406f, 906f), new Vector2(340f, 96f));
            found = OnlineUiParts.Button(foundRect, "UI_BigButton", "찾았어!", 42, config.BoldFont, SendFound);
            foundLabel = foundRect.GetComponentInChildren<Text>();

            toast = At(u, "Toast", new Vector2(838f, 118f), new Vector2(560f, 64f));
            toast.pivot = new Vector2(0.5f, 0.5f);
            toast.anchoredPosition += new Vector2(280f, -32f);
            OnlineUiParts.Draw(toast, "LOFT_Toast");
            OnlineUi.Label(OnlineUi.Rect("Text", toast, Vector2.zero, Vector2.one, new Vector2(30f, 2f), new Vector2(-30f, 0f)),
                "아래층에 신호를 보냈어요", 28, config.BoldFont, OnlineUiParts.Cream);
            toast.gameObject.SetActive(false);
            unfolded.SetActive(false);
        }

        private Button Arrow(RectTransform parent, string name, float x, bool right, UnityEngine.Events.UnityAction onClick)
        {
            var rect = At(parent, name, new Vector2(x, 403f), new Vector2(84f, 200f));
            var button = OnlineUiParts.Button(rect, "LOFT_Arrow", null, 0, config.BoldFont, onClick);
            if (right)
            {
                // The part points left; flip it about its own centre.
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition += new Vector2(42f, -100f);
                rect.localScale = new Vector3(-1f, 1f, 1f);
            }
            return button;
        }

        private void Unfold(int slot)
        {
            var sheets = tabs[tabIndex].Sheets;
            page = Mathf.Clamp(slot, 0, sheets.Length - 1);
            int index = sheets[page];
            seen.Add(index);
            sheet.Show(index, easy);
            sheet.Refresh();
            SetUnfolded(true);
            RefreshControls();

            unfoldedGroup.alpha = 0f;
            LMotion.Create(0f, 1f, 0.22f).WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    unfoldedGroup.alpha = t;
                    paper.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, t);
                }).AddTo(unfolded);
        }

        private void Flip(int step)
        {
            int target = page + step;
            if (target < 0 || target >= tabs[tabIndex].Sheets.Length) return;
            Unfold(target);
        }

        private void ShowTable()
        {
            if (tabs[tabIndex].Sheets.Length > 1)
                DealCards();
            else
                SetUnfolded(false);
        }

        private void SetUnfolded(bool open)
        {
            unfolded.SetActive(open);
            table.gameObject.SetActive(!open);
        }

        private void RefreshControls()
        {
            var tab = tabs[tabIndex];
            int count = tab.Sheets.Length;
            bool several = count > 1;
            previous.gameObject.SetActive(several);
            next.gameObject.SetActive(several);
            previous.interactable = page > 0;
            next.interactable = page < count - 1;
            toTable.gameObject.SetActive(several);
            pageLabel.text = several ? $"{page + 1} / {count}" : string.Empty;

            while (dotImages.Count < count)
            {
                var dot = OnlineUi.Rect("Dot", dots, Vector2.zero, Vector2.zero);
                dot.sizeDelta = new Vector2(28f, 28f);
                dotImages.Add(OnlineUiParts.Draw(dot, "LOFT_Dot_Unseen"));
            }
            for (int i = 0; i < dotImages.Count; i++)
            {
                bool shown = i < count && several;
                dotImages[i].gameObject.SetActive(shown);
                if (!shown) continue;
                dotImages[i].sprite = OnlineUiParts.Get(i == page ? "LOFT_Dot_Current"
                    : seen.Contains(tab.Sheets[i]) ? "LOFT_Dot_Seen" : "LOFT_Dot_Unseen");
            }

            found.gameObject.SetActive(tab.CanSignal);
            found.interactable = !signalSent;
            foundLabel.text = signalSent ? "보냈어" : "찾았어!";
            foundLabel.color = signalSent ? OnlineUiParts.InkSoft : OnlineUiParts.Ink;
        }

        private void SendFound()
        {
            if (signalSent || !session.Signal.Send())
                return;
            signalSent = true;
            toastSince = Time.unscaledTime;
            toast.gameObject.SetActive(true);
            LMotion.Create(0.7f, 1f, 0.28f).WithEase(Ease.OutBack).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(s => toast.localScale = new Vector3(s, s, 1f)).AddTo(toast.gameObject);
            RefreshControls();
        }

        // ---- per frame -------------------------------------------------------------------------------------------
        /// <summary>Called every frame by the loft with the round phase downstairs (null when not connected).</summary>
        public void Tick(RoundPhase? phase, bool easyMode, bool newCustomer)
        {
            if (newCustomer)
            {
                // A new customer: the signal can be sent again and every sheet counts as unread.
                signalSent = false;
                seen.Clear();
                if (page >= 0) RefreshControls();
                else if (tabIndex >= 0 && tabs[tabIndex].Sheets.Length > 1) DealCards();
            }
            if (easyMode != easy)
            {
                easy = easyMode;
                if (page >= 0) sheet.Show(tabs[tabIndex].Sheets[page], easy);
            }

            // Point at the tab the narrator needs next.
            blinkTab = phase switch
            {
                RoundPhase.Consult or RoundPhase.Storage => 0,
                RoundPhase.Prescribe => 1,
                _ => -1
            };
            RefreshTabs();

            // Sheets lay out a frame after they are built; resize their writing once they have a size.
            sheet.Refresh();
            foreach (var view in cardViews) view.Refresh();

            if (toast.gameObject.activeSelf && Time.unscaledTime - toastSince > ToastSeconds)
                toast.gameObject.SetActive(false);
        }

        /// <summary>Lifts a card a little under the mouse.</summary>
        private sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public void OnPointerEnter(PointerEventData eventData) => transform.parent.localScale = Vector3.one * 1.04f;
            public void OnPointerExit(PointerEventData eventData) => transform.parent.localScale = Vector3.one;
        }
    }
}
