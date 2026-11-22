using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's upstairs workshop, laid out like the counter downstairs: a header with the hourglass timer,
    /// the stack of memo sheets on the left, the open sheet large in the middle, and a parchment note on the right
    /// that follows the maker's progress. The result arrives as a stamped card over the desk, as it does downstairs.
    /// The narrator never sees the customer, the storage room, or the answers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftView : MonoBehaviour
    {
        private static readonly string[] StepNames = { "접객", "재료 탐색", "처방", "결과" };
        private const float Margin = 36f;
        private const float Gutter = 22f;
        private const float LeftWidth = 300f;
        private const float RightWidth = 350f;
        private const float HeaderHeight = 84f;
        private static readonly Color Panel = new(0.13f, 0.085f, 0.06f, 0.96f);
        private static readonly Color PanelEdge = new(0.80f, 0.62f, 0.30f, 0.9f);

        private OnlineCoopConfig config;
        private OnlineSession session;
        private RectTransform[] tabs;
        private Image[] tabPapers;
        private CanvasGroup sheetGroup;
        private Image sheet;
        private SheetPanZoom panZoom;
        private SheetMarks marks;
        private Image timerFill;
        private Text timer;
        private Text[] steps;
        private Text guidance;
        private RectTransform guidanceBox;
        private Text status;
        private Text wholeLabel;
        private LoftResultCard resultCard;
        private GameObject menu;
        private int shownSheet = -1;
        private bool shownEasy;
        private string shownGuidance;
        private float roundLimit;

        private void Start()
        {
            config = OnlineCoopConfig.Load();
            session = OnlineSession.Instance;
            Build();
            ShowSheet(0);
        }

        private void Build()
        {
            var root = (RectTransform)OnlineUi.Canvas("LoftCanvas", 0, transform).transform;

            // The painted desk stays in the background, darkened so the panels read as things lying on it.
            OnlineUi.Fill(OnlineUi.Rect("Backdrop", root, Vector2.zero, Vector2.one), OnlineUi.Wood, false);
            var picture = OnlineUi.Rect("Desk", root, Vector2.zero, Vector2.one);
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            var painting = OnlineUi.Fill(picture, new Color(0.42f, 0.38f, 0.36f), false);
            painting.sprite = config.DeskBackground;

            BuildStack(root);
            BuildHeader(root);
            BuildSheet(root);
            BuildNews(root);

            resultCard = gameObject.AddComponent<LoftResultCard>();
            resultCard.Build(root, config);
            BuildMenu(root);
        }

        /// <summary>Left column: the stack of sheets, each with its name, as a tidy pile the narrator flips through.</summary>
        private void BuildStack(RectTransform root)
        {
            var column = Frame(OnlineUi.Rect("Stack", root, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(Margin, Margin), new Vector2(Margin + LeftWidth, -Margin)));
            Heading(column, "메모 뭉치", OnlineUi.Parchment);

            var sprites = config.MemoSheets;
            tabs = new RectTransform[sprites.Length];
            tabPapers = new Image[sprites.Length];
            var list = OnlineUi.Rect("List", column, Vector2.zero, Vector2.one, new Vector2(18f, 16f), new Vector2(-18f, -84f));
            for (int i = 0; i < sprites.Length; i++)
            {
                int index = i;
                var cell = OnlineUi.Rect("Sheet" + i, list, new Vector2(0f, 1f - (i + 1f) / sprites.Length),
                    new Vector2(1f, 1f - (float)i / sprites.Length), new Vector2(0f, 5f), new Vector2(0f, -5f));
                // Each tab is a strip of paper, a touch askew, like sheets sticking out of a pile.
                cell.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -0.9f : 0.8f);
                var paper = OnlineUi.Fill(cell, Color.white);
                paper.sprite = config.NoteSlip;
                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = paper;
                button.onClick.AddListener(() => ShowSheet(index));

                var thumb = OnlineUi.Fill(OnlineUi.Rect("Thumb", cell, new Vector2(0f, 0f), new Vector2(0.30f, 1f),
                    new Vector2(10f, 8f), new Vector2(0f, -8f)), Color.white, false);
                thumb.sprite = sprites[i];
                thumb.preserveAspect = true;
                OnlineUi.Label(OnlineUi.Rect("Name", cell, new Vector2(0.30f, 0f), Vector2.one, new Vector2(6f, 0f),
                    new Vector2(-10f, 0f)), config.MemoLabels[i], 26, config.BoldFont, OnlineUi.Ink, TextAnchor.MiddleLeft);
                tabs[i] = cell;
                tabPapers[i] = paper;
            }
        }

        /// <summary>Header over the open sheet: where the narrator is, and the same hourglass bar the maker sees.</summary>
        private void BuildHeader(RectTransform root)
        {
            var bar = Frame(OnlineUi.Rect("Header", root, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(Margin + LeftWidth + Gutter, -Margin - HeaderHeight),
                new Vector2(-Margin - RightWidth - Gutter, -Margin)));

            var plate = OnlineUi.Box("Plate", bar, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(210f, 64f));
            var plateImage = OnlineUi.Fill(plate, config.NamePlate != null ? Color.white : OnlineUi.Accent, false);
            plateImage.sprite = config.NamePlate;
            OnlineUi.Label(OnlineUi.Rect("Text", plate, Vector2.zero, Vector2.one), "위층 공방", 30, config.BoldFont,
                OnlineUi.Parchment);

            timer = OnlineUi.Label(OnlineUi.Box("Clock", bar, new Vector2(1f, 0.5f), new Vector2(-22f, 0f),
                new Vector2(120f, 60f)), "--:--", 36, config.BoldFont, OnlineUi.Parchment, TextAnchor.MiddleRight);
            var frame = OnlineUi.Box("TimerFrame", bar, new Vector2(1f, 0.5f), new Vector2(-152f, 0f), new Vector2(420f, 35f));
            var frameImage = OnlineUi.Fill(frame, Color.white, false);
            frameImage.sprite = config.TimerFrame;
            timerFill = OnlineUi.Fill(OnlineUi.Rect("Fill", frame, Vector2.zero, Vector2.one, new Vector2(8f, 8f),
                new Vector2(-8f, -8f)), Color.white, false);
            timerFill.sprite = config.TimerFill;
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            timerFill.fillAmount = 0f;
            var glass = OnlineUi.Fill(OnlineUi.Box("Hourglass", bar, new Vector2(1f, 0.5f), new Vector2(-582f, 0f),
                new Vector2(46f, 60f)), Color.white, false);
            glass.sprite = config.TimerHourglass;
            glass.preserveAspect = true;
        }

        /// <summary>Centre: the open sheet, always large. Wheel moves down the page, drag pans.</summary>
        private void BuildSheet(RectTransform root)
        {
            var panel = Frame(OnlineUi.Rect("Sheet", root, Vector2.zero, Vector2.one,
                new Vector2(Margin + LeftWidth + Gutter, Margin),
                new Vector2(-Margin - RightWidth - Gutter, -Margin - HeaderHeight - Gutter)));

            var viewport = OnlineUi.Rect("Viewport", panel, Vector2.zero, Vector2.one, new Vector2(14f, 14f),
                new Vector2(-14f, -14f));
            OnlineUi.Fill(viewport, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            sheetGroup = viewport.gameObject.AddComponent<CanvasGroup>();
            var content = OnlineUi.Rect("Page", viewport, Vector2.zero, Vector2.zero);
            sheet = OnlineUi.Fill(content, Color.white, false);
            sheet.preserveAspect = true;
            panZoom = viewport.gameObject.AddComponent<SheetPanZoom>();
            var first = config.MemoSheets[0];
            panZoom.Bind(viewport, content, first.rect.width / first.rect.height);
            marks = content.gameObject.AddComponent<SheetMarks>();
            marks.Bind(content);

            var tools = OnlineUi.Box("Tools", panel, new Vector2(1f, 0f), new Vector2(-26f, 26f), new Vector2(250f, 46f));
            OnlineUi.Button(OnlineUi.Rect("ZoomOut", tools, new Vector2(0f, 0f), new Vector2(0.2f, 1f)), "−", 28,
                config.BoldFont, () => panZoom.ZoomOut(), Panel);
            OnlineUi.Button(OnlineUi.Rect("ZoomIn", tools, new Vector2(0.23f, 0f), new Vector2(0.43f, 1f)), "+", 28,
                config.BoldFont, () => panZoom.ZoomIn(), Panel);
            var whole = OnlineUi.Button(OnlineUi.Rect("Whole", tools, new Vector2(0.46f, 0f), new Vector2(1f, 1f)),
                string.Empty, 20, config.BoldFont, () => panZoom.ToggleWholeSheet(), Panel);
            wholeLabel = OnlineUi.Label(OnlineUi.Rect("Label", whole.transform, Vector2.zero, Vector2.one), string.Empty,
                20, config.BoldFont, OnlineUi.Parchment);
        }

        /// <summary>Right column: the note from downstairs, a checklist of the round and what to do now.</summary>
        private void BuildNews(RectTransform root)
        {
            var note = OnlineUi.Rect("News", root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margin - RightWidth, -Margin - 660f), new Vector2(-Margin, -Margin));
            var paper = OnlineUi.Fill(note, config.ParchmentPanel != null ? Color.white : OnlineUi.Parchment, false);
            paper.sprite = config.ParchmentPanel;
            Heading(note, "아래층 소식", OnlineUi.Ink, 56f);
            Rule(note, 128f);

            steps = new Text[StepNames.Length];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = OnlineUi.Label(Line(note, 148f + i * 46f, 42f), StepNames[i], 27, config.BoldFont,
                    OnlineUi.Faded, TextAnchor.MiddleLeft);
            Rule(note, 344f);

            var pill = OnlineUi.Box("Now", note, new Vector2(0f, 1f), new Vector2(46f, -364f), new Vector2(136f, 36f));
            OnlineUi.Fill(pill, OnlineUi.Accent, false);
            OnlineUi.Label(OnlineUi.Rect("Text", pill, Vector2.zero, Vector2.one), "지금 할 일", 20, config.BoldFont,
                Color.white);
            guidanceBox = Line(note, 412f, 200f);
            guidance = OnlineUi.Label(guidanceBox, string.Empty, 25, config.BodyFont, OnlineUi.Ink, TextAnchor.UpperLeft);

            // Connection and voice sit in a small dark panel under the note; they matter only when something is wrong.
            var link = Frame(OnlineUi.Rect("Link", root, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin - RightWidth, Margin), new Vector2(-Margin, Margin + 150f)));
            status = OnlineUi.Label(OnlineUi.Rect("Text", link, Vector2.zero, Vector2.one, new Vector2(20f, 14f),
                new Vector2(-20f, -14f)), string.Empty, 18, config.BodyFont, OnlineUi.Parchment, TextAnchor.UpperLeft);
        }

        /// <summary>Esc opens a small note to leave the loft, so no button has to sit on the desk.</summary>
        private void BuildMenu(RectTransform root)
        {
            menu = OnlineUi.Rect("Menu", root, Vector2.zero, Vector2.one).gameObject;
            OnlineUi.Fill((RectTransform)menu.transform, new Color(0.05f, 0.03f, 0.02f, 0.8f));
            var card = OnlineUi.Box("Card", menu.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 420f));
            card.pivot = new Vector2(0.5f, 0.5f);
            var paper = OnlineUi.Fill(card, Color.white, false);
            paper.sprite = config.NoteCard;
            OnlineUi.Label(OnlineUi.Rect("Title", card, new Vector2(0f, 0.74f), new Vector2(1f, 0.92f)), "위층 공방", 36,
                config.BoldFont, OnlineUi.Ink);
            OnlineUi.Wooden(OnlineUi.Button(OnlineUi.Rect("Resume", card, new Vector2(0.14f, 0.44f), new Vector2(0.86f, 0.62f)),
                "계속하기", 26, config.BoldFont, () => menu.SetActive(false), OnlineUi.WoodLight), config.WoodButton);
            OnlineUi.Wooden(OnlineUi.Button(OnlineUi.Rect("Leave", card, new Vector2(0.14f, 0.20f), new Vector2(0.86f, 0.38f)),
                "공방에서 나가기", 26, config.BoldFont, () => Leave().Forget(), OnlineUi.WoodLight), config.WoodButton);
            menu.SetActive(false);
        }

        /// <summary>A dark panel with a thin warm edge: one of the two materials every loft panel is made of.</summary>
        private static RectTransform Frame(RectTransform rect)
        {
            // A hairline of brass around a dark wooden field. Children go on top of the field.
            OnlineUi.Fill(rect, PanelEdge, false);
            OnlineUi.Fill(OnlineUi.Rect("Field", rect, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f)),
                new Color(Panel.r, Panel.g, Panel.b, 1f), false);
            return rect;
        }

        private void Heading(RectTransform panel, string text, Color color, float top = 18f) =>
            OnlineUi.Label(OnlineUi.Rect("Heading", panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -top - 52f), new Vector2(0f, -top)), "✦  " + text + "  ✦", 30, config.BoldFont, color);

        private static void Rule(RectTransform panel, float top) =>
            OnlineUi.Fill(OnlineUi.Rect("Rule", panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(46f, -top - 2f), new Vector2(-42f, -top)), new Color(0.20f, 0.13f, 0.09f, 0.28f), false);

        private static RectTransform Line(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Line", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -top - height),
                new Vector2(-42f, -top));

        private void ShowSheet(int index)
        {
            if (index == shownSheet || index < 0 || index >= config.MemoSheets.Length)
                return;
            shownSheet = index;
            sheet.sprite = config.MemoSheets[index];
            panZoom.ResetView();
            marks.Show(index, shownEasy);
            for (int i = 0; i < tabs.Length; i++)
            {
                // The open sheet's tab is pulled out of the pile; the others sit back and dimmer.
                bool open = i == index;
                tabPapers[i].color = open ? Color.white : new Color(0.72f, 0.68f, 0.62f);
                tabs[i].anchoredPosition = new Vector2(open ? 14f : 0f, tabs[i].anchoredPosition.y);
            }
            sheetGroup.alpha = 0f;
            LMotion.Create(0f, 1f, 0.16f).WithEase(Ease.OutQuad).Bind(alpha => sheetGroup.alpha = alpha)
                .AddTo(sheetGroup.gameObject);
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && !resultCard.Visible)
                menu.SetActive(!menu.activeSelf);

            if (session.EasyMode != shownEasy)
            {
                shownEasy = session.EasyMode;
                marks.Show(shownSheet, shownEasy);
            }
            wholeLabel.text = panZoom.ShowsWholeSheet ? "크게 보기" : "전체 보기";

            var sync = session.Sync;
            bool live = session.Role == OnlineRole.Narrator && session.PeerConnected && sync.HasSnapshot;
            var snapshot = live ? sync.Latest : default;

            bool running = live && snapshot.TimerRunning;
            float remaining = running ? sync.RemainingNow : 0f;
            // The narrator is not told the time limit; the longest time seen this round stands in for it.
            roundLimit = running ? Mathf.Max(roundLimit, remaining) : 0f;
            timer.text = running ? RoundDirector.FormatClock(remaining) : "--:--";
            timerFill.fillAmount = running && roundLimit > 0f ? Mathf.Clamp01(remaining / roundLimit) : 0f;

            int step = !live ? -1 : snapshot.Phase switch
            {
                RoundPhase.Consult or RoundPhase.Preparing => 0,
                RoundPhase.Storage => 1,
                RoundPhase.Prescribe => 2,
                RoundPhase.Ending or RoundPhase.Result => 3,
                _ => -1
            };
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i].color = i == step ? OnlineUi.Accent : i < step ? OnlineUi.Faded : OnlineUi.Ink;
                steps[i].text = (i < step ? "◆  " : i == step ? "◈  " : "◇  ") + StepNames[i];
            }

            SetGuidance(!live ? WaitingText() : Guide(snapshot));
            resultCard.Show(live && snapshot.HasResult && snapshot.Phase == RoundPhase.Result, snapshot);

            string link = session.Role != OnlineRole.Narrator ? "연결이 끊어졌습니다" :
                session.PeerConnected ? "아래층과 연결됨" : "아래층에 연결하는 중…";
            status.text = link + "\n" + session.Voice.Describe() +
                (shownEasy ? "\n쉬움 모드: 노랑은 증상, 초록은 구별점" : string.Empty) + "\nEsc: 메뉴";
        }

        private string WaitingText() => session.Role != OnlineRole.Narrator
            ? "아래층과 연결이 끊어졌습니다. Esc를 눌러 공방에서 나가 다시 참가하세요."
            : "아래층과 연결하는 중입니다.";

        private static string Guide(RoundSnapshot snapshot) => snapshot.Phase switch
        {
            RoundPhase.Waiting => "아직 손님이 없습니다. 표지를 읽으며 기다리세요.",
            RoundPhase.Preparing or RoundPhase.Consult =>
                "손님이 왔습니다. 전해 듣는 증상이 적힌 메모 한 장을 찾으세요.",
            RoundPhase.Storage when snapshot.Brewed => "솥에 재료가 들어갔습니다. '건넬 때' 메모로 넘기세요!",
            RoundPhase.Storage => "친구가 창고에 있습니다. 메모 아래쪽의 재료 세 개를 말로 설명하세요.",
            RoundPhase.Prescribe => "'건넬 때' 메모 차례입니다. 손님 생김새를 물어 병과 잉크를 알려 주세요.",
            RoundPhase.Ending => "손님이 약을 받았습니다. 결과를 기다리세요.",
            RoundPhase.Result => "이번 손님이 끝났습니다.",
            _ => string.Empty
        };

        private void SetGuidance(string text)
        {
            if (text == shownGuidance)
                return;
            shownGuidance = text;
            guidance.text = text;
            // A short pop so the narrator notices the step changed while reading a memo.
            LMotion.Create(1.08f, 1f, 0.35f).WithEase(Ease.OutBack)
                .Bind(scale => guidanceBox.localScale = new Vector3(scale, scale, 1f)).AddTo(gameObject);
        }

        private async UniTaskVoid Leave()
        {
            await session.LeaveAsync();
            // Build index 0 is whichever screen the game starts on (title or counter).
            SceneManager.LoadScene(0);
        }
    }
}
