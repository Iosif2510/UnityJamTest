using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's upstairs workshop in the B style (handoff mockups 01–04): a top band with the name plate, the
    /// hourglass timer and chips that follow the maker downstairs, and the <see cref="LoftDesk"/> below it. The left
    /// column under the tabs says what to do now. The result arrives as a stamped card over the desk, leaving the
    /// top band visible. The narrator never sees the customer, the storage room, or the answers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftView : MonoBehaviour
    {
        private static readonly string[] StepNames = { "접객", "창고", "포장", "결과" };
        private const float BandHeight = 96f;

        private OnlineCoopConfig config;
        private OnlineSession session;
        private LoftDesk desk;
        private Image timerFill;
        private Text clock;
        private Image[] chips;
        private Text[] chipLabels;
        private int shownStep = -2;
        private Text guidance;
        private RectTransform guidanceBox;
        private Text status;
        private LoftResultCard resultCard;
        private GameObject menu;
        private string shownGuidance;
        private float roundLimit;
        private RoundPhase? lastPhase;

        private void Start()
        {
            config = OnlineCoopConfig.Load();
            session = OnlineSession.Instance;
            Build();
            desk.OpenStart();
        }

        private void Build()
        {
            var root = (RectTransform)OnlineUi.Canvas("LoftCanvas", 0, transform).transform;
            OnlineUiParts.Backdrop(root, "BG_Loft");
            BuildBand(root);

            desk = gameObject.AddComponent<LoftDesk>();
            desk.Build(root, config, session);
            BuildSide(root);

            resultCard = gameObject.AddComponent<LoftResultCard>();
            resultCard.Build(root, config, BandHeight);
            BuildMenu(root);
        }

        /// <summary>Top band: name plate, hourglass and bar with the time left, then where the maker is.</summary>
        private void BuildBand(RectTransform root)
        {
            var plate = OnlineUi.Box("Plate", root, new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(220f, 68f));
            OnlineUiParts.Draw(plate, "LOFT_Nameplate");
            OnlineUiParts.Outlined(OnlineUi.Label(OnlineUi.Rect("Text", plate, Vector2.zero, Vector2.one, new Vector2(0f, 4f),
                Vector2.zero), "위층 공방", 30, config.BoldFont, OnlineUiParts.Cream), OnlineUiParts.OutlineRed);

            var glass = OnlineUi.Box("Hourglass", root, new Vector2(0f, 1f), new Vector2(270f, -12f), new Vector2(54f, 68f));
            OnlineUiParts.Draw(glass, "UI_Timer_Hourglass").preserveAspect = true;

            var frame = OnlineUi.Box("TimerFrame", root, new Vector2(0f, 1f), new Vector2(330f, -14f), new Vector2(760f, 64f));
            OnlineUiParts.Draw(frame, "UI_Timer_Frame");
            // The fill keeps its own height (3-slice) and sits in the frame's groove.
            var fillArea = OnlineUi.Rect("FillArea", frame, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(14f, -18f),
                new Vector2(-14f, 18f));
            timerFill = OnlineUiParts.Draw(OnlineUi.Rect("Fill", fillArea, Vector2.zero, Vector2.one), "UI_Timer_Fill");
            clock = OnlineUiParts.Outlined(OnlineUi.Label(OnlineUi.Box("Clock", root, new Vector2(0f, 1f),
                new Vector2(1104f, -16f), new Vector2(130f, 60f)), "--:--", 38, config.BoldFont, OnlineUiParts.Cream,
                TextAnchor.MiddleLeft));

            var row = OnlineUi.Box("Chips", root, new Vector2(1f, 1f), new Vector2(-150f, -22f), new Vector2(560f, 48f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            chips = new Image[StepNames.Length];
            chipLabels = new Text[StepNames.Length];
            for (int i = 0; i < StepNames.Length; i++)
            {
                var chip = OnlineUi.Rect("Chip " + StepNames[i], row, Vector2.zero, Vector2.zero);
                chips[i] = OnlineUiParts.Draw(chip, "LOFT_Chip_Upcoming");
                var size = chip.gameObject.AddComponent<LayoutElement>();
                size.preferredHeight = 48f;
                size.preferredWidth = 120f;
                chipLabels[i] = OnlineUi.Label(OnlineUi.Rect("Text", chip, Vector2.zero, Vector2.one, new Vector2(0f, 3f),
                    Vector2.zero), StepNames[i], 24, config.BoldFont, OnlineUiParts.Ink);
            }
        }

        /// <summary>Left column under the tabs: what to do now, and the link and voice status in small type.</summary>
        private void BuildSide(RectTransform root)
        {
            // Between the last tab and the status lines, whatever the screen height, on a soft dark pad.
            guidanceBox = OnlineUi.Rect("Guide", root, Vector2.zero, new Vector2(0f, 1f));
            guidanceBox.pivot = new Vector2(0.5f, 1f); // the pop grows from the top; set before the offsets
            guidanceBox.offsetMin = new Vector2(30f, 132f);
            guidanceBox.offsetMax = new Vector2(310f, -768f);
            OnlineUiParts.Shade(OnlineUi.Rect("Shade", guidanceBox, Vector2.zero, Vector2.one, new Vector2(-14f, -10f),
                new Vector2(14f, 10f)), 0.6f);
            var heading = OnlineUi.Box("Now", guidanceBox, new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(140f, 48f));
            OnlineUiParts.Draw(heading, "LOFT_Chip_Current");
            OnlineUi.Label(OnlineUi.Rect("Text", heading, Vector2.zero, Vector2.one, new Vector2(0f, 3f), Vector2.zero),
                "지금 할 일", 22, config.BoldFont, OnlineUiParts.Ink);
            guidance = OnlineUiParts.Outlined(OnlineUi.Label(OnlineUi.Rect("Text", guidanceBox, Vector2.zero, Vector2.one,
                new Vector2(6f, 0f), new Vector2(-4f, -60f)), string.Empty, 23, config.BoldFont, OnlineUiParts.Cream,
                TextAnchor.UpperLeft));
            guidance.lineSpacing = 1.05f;
            guidance.verticalOverflow = VerticalWrapMode.Truncate;

            status = OnlineUiParts.Outlined(OnlineUi.Label(OnlineUi.Box("Status", root, new Vector2(0f, 0f),
                new Vector2(30f, 16f), new Vector2(300f, 104f)), string.Empty, 17, config.BoldFont, OnlineUiParts.Cream,
                TextAnchor.LowerLeft));
        }

        /// <summary>Esc opens a small window to leave the loft, so no button has to sit on the desk.</summary>
        private void BuildMenu(RectTransform root)
        {
            menu = OnlineUi.Rect("Menu", root, Vector2.zero, Vector2.one).gameObject;
            OnlineUi.Fill((RectTransform)menu.transform, new Color(0.12f, 0.06f, 0.03f, 0.72f));
            var card = OnlineUi.Box("Window", menu.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 690f));
            card.pivot = new Vector2(0.5f, 0.5f);
            OnlineUiParts.Draw(card, "UI_Window");
            OnlineUi.Label(OnlineUi.Rect("Title", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -130f),
                new Vector2(0f, -50f)), "위층 공방", 44, config.BoldFont, OnlineUiParts.Ink);
            OnlineUi.Label(OnlineUi.Rect("LanguageTitle", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(70f, -196f),
                new Vector2(-70f, -150f)), "메모 언어", 28, config.BoldFont, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            languages = new MemoLanguagePicker(OnlineUi.Rect("Languages", card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(70f, -276f), new Vector2(-70f, -204f)), config.BoldFont);
            VolumeSlider.Build(OnlineUi.Rect("Volume", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(70f, -350f),
                new Vector2(-70f, -298f)), config.BoldFont, OnlineUiParts.Ink);
            OnlineUiParts.Button(OnlineUi.Box("Resume", card, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(400f, 96f)),
                "UI_BigButton", "계속하기", 36, config.BoldFont, () => menu.SetActive(false))
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            OnlineUiParts.Button(OnlineUi.Box("Leave", card, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(320f, 80f)),
                "UI_SmallButton", "공방에서 나가기", 30, config.BoldFont, () => Leave().Forget())
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            menu.SetActive(false);
        }

        private MemoLanguagePicker languages;

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && !resultCard.Visible)
            {
                menu.SetActive(!menu.activeSelf);
                languages.Refresh();
            }

            var sync = session.Sync;
            bool live = session.Role == OnlineRole.Narrator && session.PeerConnected && sync.HasSnapshot;
            var snapshot = live ? sync.Latest : default;

            bool running = live && snapshot.TimerRunning;
            float remaining = running ? sync.RemainingNow : 0f;
            // The narrator is not told the time limit; the longest time seen this round stands in for it.
            roundLimit = running ? Mathf.Max(roundLimit, remaining) : 0f;
            clock.text = running ? RoundDirector.FormatClock(remaining) : "--:--";
            timerFill.fillAmount = 1f;
            float fill = running && roundLimit > 0f ? Mathf.Clamp01(remaining / roundLimit) : 0f;
            var fillRect = timerFill.rectTransform;
            fillRect.anchorMax = new Vector2(fill, 1f);
            timerFill.enabled = fill > 0.02f;

            RoundPhase? phase = live ? snapshot.Phase : null;
            int step = phase switch
            {
                RoundPhase.Consult or RoundPhase.Preparing => 0,
                RoundPhase.Storage => 1,
                RoundPhase.Prescribe => 2,
                RoundPhase.Ending or RoundPhase.Result => 3,
                _ => -1
            };
            if (step != shownStep)
                ShowStep(step);

            // A customer arriving (from the wait or the previous result) starts a new round on the desk.
            bool newCustomer = phase is RoundPhase.Consult or RoundPhase.Preparing &&
                               lastPhase is null or RoundPhase.Waiting or RoundPhase.Result or RoundPhase.Ending;
            lastPhase = phase;
            desk.Tick(phase, session.EasyMode, newCustomer);

            SetGuidance(!live ? WaitingText() : Guide(snapshot));
            resultCard.Show(live && snapshot.HasResult && snapshot.Phase == RoundPhase.Result, snapshot);

            string link = session.Role != OnlineRole.Narrator ? "연결이 끊어졌습니다" :
                session.PeerConnected ? "아래층과 연결됨" : "아래층에 연결하는 중…";
            status.text = link + "\n" + session.Voice.Describe() +
                (session.EasyMode ? "\n쉬움 모드: 노랑은 증상, 초록은 구별점" : string.Empty) + "\nEsc: 메뉴";
        }

        private void ShowStep(int step)
        {
            shownStep = step;
            for (int i = 0; i < chips.Length; i++)
            {
                bool done = step >= 0 && i < step;
                bool current = i == step;
                chips[i].sprite = OnlineUiParts.Get(current ? "LOFT_Chip_Current" : done ? "LOFT_Chip_Done" : "LOFT_Chip_Upcoming");
                chipLabels[i].text = StepNames[i] + (current && i == 0 ? " 중" : string.Empty);
                chipLabels[i].color = done ? OnlineUiParts.InkSoft : OnlineUiParts.Ink;
                var size = chips[i].GetComponent<LayoutElement>();
                size.preferredWidth = Mathf.Max(120f, 56f + chipLabels[i].text.Length * 24f);
            }
        }

        private string WaitingText() => session.Role != OnlineRole.Narrator
            ? "아래층과 연결이 끊어졌어요. Esc를 눌러 나간 뒤 다시 참가하세요."
            : "아래층과 연결하는 중이에요.";

        private static string Guide(RoundSnapshot snapshot) => snapshot.Phase switch
        {
            RoundPhase.Waiting => "아직 손님이 없어요. 안내서를 읽으며 기다리세요.",
            RoundPhase.Preparing or RoundPhase.Consult =>
                "손님이 왔어요. '질병과 재료'에서 들은 증상이 적힌 메모를 찾고 '찾았어!'를 누르세요.",
            RoundPhase.Storage when snapshot.Brewed => "솥에 재료가 들어갔어요. '포장' 탭으로 넘어가세요!",
            RoundPhase.Storage => "친구가 창고에 있어요. 메모 아래쪽의 재료 세 개를 말로 설명하세요.",
            RoundPhase.Prescribe => "'포장' 차례예요. 손님 생김새를 물어 병과 잉크를 알려 주세요.",
            RoundPhase.Ending => "손님이 약을 받았어요. 결과를 기다리세요.",
            RoundPhase.Result => "이번 손님이 끝났어요.",
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
