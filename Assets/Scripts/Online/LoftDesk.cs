using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's desk in the loft (B-style mockups 01–03): three tabs on the left and a felt table. Picking a tab
    /// deals its sheets onto the table; clicking one unfolds it large. While unfolded the narrator flips to the
    /// neighbouring sheets with the arrows, and on the potion memos presses "찾았어!" to tell the maker downstairs
    /// that it is time for the storage room. The sheet stays open so the ingredients can be read out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftDesk : MonoBehaviour
    {
        private const float FeltLeft = 336f, FeltTop = 108f, FeltRight = 24f, FeltBottom = 30f;
        private static readonly Vector2 CardSize = new(420f, 300f);
        private const float ToastSeconds = 2.6f;

        private sealed class Tab
        {
            public string Name;
            public int[] Sheets;
            public bool CanSignal;
            public RectTransform Rect;
            public Button Button;
            public Image Image;
            public Text Label;
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
        private readonly Dictionary<int, Sprite> cardFaces = new();
        private GameObject unfolded;
        private CanvasGroup unfoldedGroup;
        private Image sheet;
        private SheetPanZoom panZoom;
        private SheetMarks marks;
        private Button previous, next, found;
        private Text foundLabel;
        private RectTransform dots;
        private readonly List<Image> dotImages = new();
        private RectTransform toast;
        private float toastSince = float.NegativeInfinity;

        public void Build(RectTransform root, OnlineCoopConfig coopConfig, OnlineSession onlineSession)
        {
            config = coopConfig;
            session = onlineSession;
            int memoCount = config.MemoSheets.Length;
            var memos = new int[memoCount - 2];
            for (int i = 0; i < memos.Length; i++) memos[i] = i + 1;
            tabs = new[]
            {
                new Tab { Name = "질병과 재료", Sheets = memos, CanSignal = true },
                new Tab { Name = "포장", Sheets = new[] { config.CounterManualIndex } },
                new Tab { Name = "안내서", Sheets = new[] { 0 } },
            };

            BuildTabs(root);
            // Fixed left and top (tabs and band), stretching to the right and bottom edges for other aspects.
            var felt = OnlineUi.Rect("Table", root, Vector2.zero, Vector2.one, new Vector2(FeltLeft, FeltBottom),
                new Vector2(-FeltRight, -FeltTop));
            OnlineUiParts.Draw(felt, "LOFT_Felt");
            table = OnlineUi.Rect("Cards", felt, Vector2.zero, Vector2.one);
            BuildUnfolded(felt);
        }

        /// <summary>The guide is where the narrator starts while waiting for the first customer.</summary>
        public void OpenStart() => Select(2);

        // ---- tabs ------------------------------------------------------------------------------------------------
        private void BuildTabs(RectTransform root)
        {
            float[] heights = { 300f, 180f, 112f };
            int[] sizes = { 40, 40, 34 };
            float top = 112f;
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var tab = tabs[i];
                tab.Rect = OnlineUi.Box("Tab " + tab.Name, root, new Vector2(0f, 1f), new Vector2(24f, -top),
                    new Vector2(296f, heights[i]));
                tab.Button = OnlineUiParts.Button(tab.Rect, "LOFT_Tab", tab.Name, sizes[i], config.BoldFont,
                    () => Select(index), OnlineUiParts.Ink);
                tab.Image = (Image)tab.Button.targetGraphic;
                tab.Label = tab.Rect.GetComponentInChildren<Text>();
                top += heights[i] + 16f;
            }
        }

        private void Select(int index)
        {
            if (index == tabIndex && page < 0 && tabs[index].Sheets.Length > 1)
                return;
            tabIndex = index;
            RefreshTabs();
            // A tab with a single sheet opens it straight away; the potion memos are dealt onto the table first.
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
                string normal = selected ? "LOFT_Tab_Selected" : blink ? "LOFT_Tab_Blink" : "LOFT_Tab_Normal";
                var sprite = OnlineUiParts.Get(normal);
                if (tabs[i].Image.sprite != sprite)
                {
                    tabs[i].Image.sprite = sprite;
                    var state = tabs[i].Button.spriteState;
                    state.highlightedSprite = state.pressedSprite = selected ? sprite : OnlineUiParts.Get("LOFT_Tab_Hover");
                    state.selectedSprite = sprite;
                    tabs[i].Button.spriteState = state;
                }
            }
        }

        // ---- table -----------------------------------------------------------------------------------------------
        private void DealCards()
        {
            page = -1;
            SetUnfolded(false);
            foreach (var card in cards) Destroy(card.gameObject);
            cards.Clear();

            var sheets = tabs[tabIndex].Sheets;
            const int columns = 3;
            int rows = Mathf.CeilToInt(sheets.Length / (float)columns);
            const float gapX = 40f, gapY = 44f;
            float gridW = columns * CardSize.x + (columns - 1) * gapX;
            float gridH = rows * CardSize.y + (rows - 1) * gapY;
            for (int i = 0; i < sheets.Length; i++)
            {
                int slot = i;
                int col = i % columns, row = i / columns;
                var target = new Vector2(-gridW * 0.5f + col * (CardSize.x + gapX) + CardSize.x * 0.5f,
                    gridH * 0.5f - row * (CardSize.y + gapY) - CardSize.y * 0.5f);
                var card = OnlineUi.Box("Card " + i, table, new Vector2(0.5f, 0.5f), target, CardSize);
                card.pivot = new Vector2(0.5f, 0.5f);
                var shadow = OnlineUi.Rect("Shadow", card, Vector2.zero, Vector2.one, new Vector2(8f, -12f), new Vector2(8f, -12f));
                OnlineUi.Fill(shadow, new Color(0.08f, 0.02f, 0.12f, 0.45f), false);
                var face = OnlineUi.Rect("Face", card, Vector2.zero, Vector2.one);
                var image = OnlineUi.Fill(face, Color.white);
                image.sprite = CardFace(sheets[i]);
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                var colors = button.colors;
                colors.highlightedColor = new Color(1f, 0.98f, 0.92f);
                colors.pressedColor = new Color(0.88f, 0.84f, 0.78f);
                button.colors = colors;
                button.onClick.AddListener(() => Unfold(slot));
                face.gameObject.AddComponent<CardHover>();
                if (seen.Contains(sheets[i]))
                {
                    var mark = OnlineUi.Box("Seen", face, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(26f, 26f));
                    OnlineUiParts.Draw(mark, "LOFT_Dot_Seen");
                }

                // Sheets fly in from the left one after another and settle slightly askew, like paper.
                float tilt = (i * 37 % 7 - 3) * 0.5f;
                card.anchoredPosition = target + new Vector2(-1500f, 60f);
                card.localRotation = Quaternion.Euler(0f, 0f, 14f);
                var from = card.anchoredPosition;
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

        /// <summary>The top of a sheet, where its title and symptoms are, cut to the card's shape.</summary>
        private Sprite CardFace(int sheetIndex)
        {
            if (cardFaces.TryGetValue(sheetIndex, out var face))
                return face;
            var source = config.MemoSheets[sheetIndex];
            var r = source.textureRect;
            float h = Mathf.Min(r.height, r.width * CardSize.y / CardSize.x);
            face = Sprite.Create(source.texture, new Rect(r.x, r.y + r.height - h, r.width, h), new Vector2(0.5f, 0.5f));
            face.name = source.name + " (card)";
            cardFaces[sheetIndex] = face;
            return face;
        }

        // ---- unfolded sheet --------------------------------------------------------------------------------------
        private void BuildUnfolded(RectTransform felt)
        {
            unfolded = OnlineUi.Rect("Unfolded", felt, Vector2.zero, Vector2.one).gameObject;
            var u = (RectTransform)unfolded.transform;
            unfoldedGroup = unfolded.AddComponent<CanvasGroup>();

            // The sheet: whole by default; the wheel zooms in and dragging moves it, for the small handwriting.
            var viewport = OnlineUi.Rect("Viewport", u, Vector2.zero, Vector2.one, new Vector2(130f, 150f),
                new Vector2(-130f, -40f));
            OnlineUi.Fill(viewport, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = OnlineUi.Rect("Page", viewport, Vector2.zero, Vector2.zero);
            var shadow = OnlineUi.Rect("Shadow", content, Vector2.zero, Vector2.one, new Vector2(10f, -14f), new Vector2(10f, -14f));
            OnlineUi.Fill(shadow, new Color(0.08f, 0.02f, 0.12f, 0.45f), false);
            sheet = OnlineUi.Fill(OnlineUi.Rect("Sheet", content, Vector2.zero, Vector2.one), Color.white, false);
            panZoom = viewport.gameObject.AddComponent<SheetPanZoom>();
            var first = config.MemoSheets[0];
            panZoom.Bind(viewport, content, first.rect.width / first.rect.height);
            panZoom.WheelZooms = true;
            marks = content.gameObject.AddComponent<SheetMarks>();
            marks.Bind(content);

            previous = Arrow(u, "Previous", false, () => Flip(-1));
            next = Arrow(u, "Next", true, () => Flip(1));

            var back = OnlineUi.Box("ToTable", u, new Vector2(0f, 0f), new Vector2(130f, 40f), new Vector2(200f, 80f));
            OnlineUiParts.Button(back, "UI_SmallButton", "탁자로", 32, config.BoldFont, ShowTable, OnlineUiParts.Ink);
            back.name = "ToTable";

            dots = OnlineUi.Box("Dots", u, new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(600f, 32f));
            dots.pivot = new Vector2(0.5f, 0.5f);
            var layout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var foundRect = OnlineUi.Box("Found", u, new Vector2(1f, 0f), new Vector2(-130f, 28f), new Vector2(340f, 104f));
            found = OnlineUiParts.Button(foundRect, "UI_BigButton", "찾았어!", 44, config.BoldFont, SendFound);
            foundLabel = foundRect.GetComponentInChildren<Text>();

            toast = OnlineUi.Box("Toast", u, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(560f, 64f));
            toast.pivot = new Vector2(0.5f, 1f);
            OnlineUiParts.Draw(toast, "LOFT_Toast");
            OnlineUi.Label(OnlineUi.Rect("Text", toast, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f)),
                "아래층에 신호를 보냈어요 ✓", 28, config.BoldFont, OnlineUiParts.Cream);
            toast.gameObject.SetActive(false);
            unfolded.SetActive(false);
        }

        private Button Arrow(RectTransform parent, string name, bool right, UnityEngine.Events.UnityAction onClick)
        {
            var rect = OnlineUi.Box(name, parent, new Vector2(right ? 1f : 0f, 0.5f), new Vector2(right ? -22f : 22f, 50f),
                new Vector2(92f, 200f));
            rect.pivot = new Vector2(right ? 1f : 0f, 0.5f);
            var button = OnlineUiParts.Button(rect, "LOFT_Arrow", null, 0, config.BoldFont, onClick);
            if (right) rect.localScale = new Vector3(-1f, 1f, 1f);
            if (right) rect.pivot = new Vector2(0f, 0.5f);
            return button;
        }

        private void Unfold(int slot)
        {
            var sheets = tabs[tabIndex].Sheets;
            page = Mathf.Clamp(slot, 0, sheets.Length - 1);
            int index = sheets[page];
            seen.Add(index);
            sheet.sprite = config.MemoSheets[index];
            panZoom.ShowWholeSheet();
            marks.Show(index, easy);
            SetUnfolded(true);
            RefreshControls();

            unfoldedGroup.alpha = 0f;
            var paper = (RectTransform)sheet.transform.parent;
            LMotion.Create(0f, 1f, 0.22f).WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    unfoldedGroup.alpha = t;
                    paper.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, t);
                }).AddTo(unfolded);
        }

        private void Flip(int step)
        {
            var sheets = tabs[tabIndex].Sheets;
            int target = page + step;
            if (target < 0 || target >= sheets.Length) return;
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
            previous.gameObject.SetActive(count > 1);
            next.gameObject.SetActive(count > 1);
            previous.interactable = page > 0;
            next.interactable = page < count - 1;
            // "탁자로" only makes sense for a tab with several sheets on the table.
            previous.transform.parent.Find("ToTable").gameObject.SetActive(count > 1);

            while (dotImages.Count < count)
            {
                var dot = OnlineUi.Rect("Dot", dots, Vector2.zero, Vector2.zero);
                dotImages.Add(OnlineUiParts.Draw(dot, "LOFT_Dot_Unseen"));
            }
            for (int i = 0; i < dotImages.Count; i++)
            {
                bool shown = i < count && count > 1;
                dotImages[i].gameObject.SetActive(shown);
                if (!shown) continue;
                bool current = i == page;
                dotImages[i].sprite = OnlineUiParts.Get(current ? "LOFT_Dot_Current" : seen.Contains(tab.Sheets[i])
                    ? "LOFT_Dot_Seen" : "LOFT_Dot_Unseen");
                dotImages[i].rectTransform.sizeDelta = Vector2.one * (current ? 30f : 18f);
            }

            found.gameObject.SetActive(tab.CanSignal);
            found.interactable = !signalSent;
            foundLabel.text = signalSent ? "보냈어 ✓" : "찾았어!";
        }

        private void SendFound()
        {
            if (signalSent) return;
            if (!session.Signal.Send())
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
            }
            if (easyMode != easy)
            {
                easy = easyMode;
                if (page >= 0) marks.Show(tabs[tabIndex].Sheets[page], easy);
            }

            // Point at the tab the narrator needs next.
            blinkTab = phase switch
            {
                RoundPhase.Consult or RoundPhase.Storage => 0,
                RoundPhase.Prescribe => 1,
                _ => -1
            };
            RefreshTabs();

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
