using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's upstairs workshop: a painted desk with the memo sheets lying on it, in the same hand-painted
    /// look as the counter downstairs. A sheet is picked up to be read and put back afterwards, and a parchment
    /// note follows the maker's progress. The narrator never sees the customer, the storage room, or the answers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftView : MonoBehaviour
    {
        private static readonly string[] StepNames = { "접객", "재료 탐색", "처방", "결과" };
        private const float PictureAspect = 16f / 9f;

        private OnlineCoopConfig config;
        private OnlineSession session;
        private DeskSheets deskSheets;
        private GameObject reading;
        private CanvasGroup readingGroup;
        private Image sheet;
        private SheetPanZoom panZoom;
        private SheetMarks marks;
        private Text[] steps;
        private Text timer;
        private Text guidance;
        private Text connection;
        private Text voice;
        private Text result;
        private Text mode;
        private Text wholeLabel;
        private Text deskHint;
        private int shownSheet = -1;
        private bool shownEasy;
        private string shownGuidance;

        private void Start()
        {
            config = OnlineCoopConfig.Load();
            session = OnlineSession.Instance;
            Build();
        }

        private void Build()
        {
            var root = OnlineUi.Canvas("LoftCanvas", 0, transform).transform;
            OnlineUi.Fill(OnlineUi.Rect("Backdrop", root, Vector2.zero, Vector2.one), OnlineUi.Wood, false);

            // The desk picture covers the screen; everything placed "on the desk" is positioned inside this rect so
            // it stays on the same spot of the painting whatever the screen shape.
            var picture = OnlineUi.Rect("Desk", root, Vector2.zero, Vector2.one);
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = PictureAspect;
            var painting = OnlineUi.Fill(picture, Color.white, false);
            painting.sprite = config.DeskBackground;
            if (config.DeskBackground == null)
                painting.color = OnlineUi.WoodLight;

            deskSheets = picture.gameObject.AddComponent<DeskSheets>();
            deskSheets.Build(picture, config.MemoSheets);
            deskSheets.Picked += PickUp;

            // The clock sits beside the painted hourglass.
            timer = OnlineUi.Label(OnlineUi.Rect("Timer", picture, new Vector2(0.280f, 0.745f), new Vector2(0.368f, 0.845f)),
                "--:--", 54, config.BoldFont, OnlineUi.Parchment);
            timer.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            deskHint = OnlineUi.Label(OnlineUi.Rect("DeskHint", picture, new Vector2(0.05f, 0.015f), new Vector2(0.62f, 0.075f)),
                string.Empty, 26, config.BoldFont, OnlineUi.Parchment);

            BuildBoard(root);
            BuildReading(root);
        }

        /// <summary>The parchment note that tells the narrator what is happening downstairs.</summary>
        private void BuildBoard(Transform root)
        {
            var board = OnlineUi.Box("Board", root, new Vector2(1f, 0.5f), new Vector2(-28f, -20f), new Vector2(430f, 840f));
            var paper = OnlineUi.Fill(board, config.ParchmentPanel != null ? Color.white : OnlineUi.Parchment, false);
            paper.sprite = config.ParchmentPanel;

            OnlineUi.Label(Line(board, 70f, 52f), "위층 공방", 36, config.BoldFont, OnlineUi.Ink);
            steps = new Text[StepNames.Length];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = OnlineUi.Label(Line(board, 140f + i * 46f, 42f), StepNames[i], 27, config.BoldFont,
                    OnlineUi.Faded, TextAnchor.MiddleLeft);

            guidance = OnlineUi.Label(Line(board, 340f, 170f), string.Empty, 24, config.BodyFont, OnlineUi.Ink,
                TextAnchor.UpperLeft);
            result = OnlineUi.Label(Line(board, 520f, 130f), string.Empty, 22, config.BodyFont, OnlineUi.Ink,
                TextAnchor.UpperLeft);

            mode = OnlineUi.Label(Foot(board, 196f, 26f), string.Empty, 17, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            voice = OnlineUi.Label(Foot(board, 168f, 26f), string.Empty, 17, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            connection = OnlineUi.Label(Foot(board, 140f, 26f), string.Empty, 17, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            OnlineUi.Wooden(OnlineUi.Button(OnlineUi.Box("Leave", board, new Vector2(0.5f, 0f), new Vector2(0f, 64f),
                new Vector2(300f, 62f)), "공방에서 나가기", 23, config.BoldFont, () => Leave().Forget(), OnlineUi.WoodLight),
                config.WoodButton);
        }

        /// <summary>The sheet in the narrator's hands. Hidden while every sheet is on the desk.</summary>
        private void BuildReading(Transform root)
        {
            reading = OnlineUi.Rect("Reading", root, Vector2.zero, Vector2.one).gameObject;
            readingGroup = reading.AddComponent<CanvasGroup>();
            // Dims the desk; clicking beside the sheet puts it back.
            var dim = OnlineUi.Fill(OnlineUi.Rect("Dim", reading.transform, Vector2.zero, Vector2.one),
                new Color(0.05f, 0.03f, 0.02f, 0.78f));
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(PutDown);

            var viewport = OnlineUi.Rect("Viewport", reading.transform, Vector2.zero, Vector2.one,
                new Vector2(120f, 20f), new Vector2(-560f, -20f));
            OnlineUi.Fill(viewport, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = OnlineUi.Rect("Sheet", viewport, Vector2.zero, Vector2.zero);
            sheet = OnlineUi.Fill(content, Color.white, false);
            sheet.preserveAspect = true;
            panZoom = viewport.gameObject.AddComponent<SheetPanZoom>();
            var first = config.MemoSheets[0];
            panZoom.Bind(viewport, content, first.rect.width / first.rect.height);
            marks = content.gameObject.AddComponent<SheetMarks>();
            marks.Bind(content);

            var tools = OnlineUi.Box("Tools", reading.transform, new Vector2(1f, 0f), new Vector2(-28f, 20f),
                new Vector2(430f, 60f));
            OnlineUi.Wooden(OnlineUi.Button(OnlineUi.Rect("PutDown", tools, new Vector2(0f, 0f), new Vector2(0.40f, 1f)),
                "내려놓기", 23, config.BoldFont, PutDown, OnlineUi.WoodLight), config.WoodButton);
            OnlineUi.Button(OnlineUi.Rect("ZoomOut", tools, new Vector2(0.43f, 0f), new Vector2(0.56f, 1f)), "−", 32,
                config.BoldFont, () => panZoom.ZoomOut(), OnlineUi.WoodLight);
            OnlineUi.Button(OnlineUi.Rect("ZoomIn", tools, new Vector2(0.58f, 0f), new Vector2(0.71f, 1f)), "+", 32,
                config.BoldFont, () => panZoom.ZoomIn(), OnlineUi.WoodLight);
            var whole = OnlineUi.Button(OnlineUi.Rect("Whole", tools, new Vector2(0.73f, 0f), new Vector2(1f, 1f)),
                string.Empty, 20, config.BoldFont, () => panZoom.ToggleWholeSheet(), OnlineUi.WoodLight);
            wholeLabel = OnlineUi.Label(OnlineUi.Rect("Label", whole.transform, Vector2.zero, Vector2.one), string.Empty,
                20, config.BoldFont, Color.white);

            // The status note stays readable above the dimmed desk.
            reading.transform.SetSiblingIndex(reading.transform.GetSiblingIndex() - 1);
            reading.SetActive(false);
        }

        private static RectTransform Line(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Line", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(54f, -top - height),
                new Vector2(-48f, -top));

        /// <summary>A line measured up from the bottom of the board, so it never slides under the leave button.</summary>
        private static RectTransform Foot(RectTransform parent, float bottom, float height) =>
            OnlineUi.Rect("Foot", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(54f, bottom),
                new Vector2(-48f, bottom + height));

        private void PickUp(int index)
        {
            if (index < 0 || index >= config.MemoSheets.Length)
                return;
            shownSheet = index;
            sheet.sprite = config.MemoSheets[index];
            reading.SetActive(true);
            panZoom.ResetView();
            marks.Show(index, shownEasy);
            readingGroup.alpha = 0f;
            LMotion.Create(0f, 1f, 0.18f).WithEase(Ease.OutQuad).Bind(alpha => readingGroup.alpha = alpha)
                .AddTo(reading);
        }

        private void PutDown()
        {
            shownSheet = -1;
            reading.SetActive(false);
        }

        private void Update()
        {
            bool holding = reading.activeSelf;
            if (holding && (Mouse.current?.rightButton.wasPressedThisFrame == true ||
                            Keyboard.current?.escapeKey.wasPressedThisFrame == true))
            {
                PutDown();
                holding = false;
            }

            if (session.EasyMode != shownEasy)
            {
                shownEasy = session.EasyMode;
                if (holding)
                    marks.Show(shownSheet, shownEasy);
            }
            wholeLabel.text = panZoom.ShowsWholeSheet ? "크게 보기" : "전체 보기";
            deskHint.text = holding ? string.Empty : deskSheets.Hovered >= 0
                ? config.MemoLabels[deskSheets.Hovered] + " · 눌러서 집기"
                : "책상 위 종이를 눌러서 집어 보세요";
            mode.text = shownEasy ? "쉬움 모드 · 노랑은 증상, 초록은 구별점" : string.Empty;

            var sync = session.Sync;
            bool live = session.Role == OnlineRole.Narrator && session.PeerConnected && sync.HasSnapshot;
            var snapshot = live ? sync.Latest : default;

            timer.text = live && snapshot.TimerRunning ? RoundDirector.FormatClock(sync.RemainingNow) : "--:--";
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
                steps[i].color = i == step ? OnlineUi.Accent : i < step ? OnlineUi.Ink : OnlineUi.Faded;
                steps[i].text = (i == step ? "▶ " : i < step ? "✓ " : "   ") + StepNames[i];
            }

            SetGuidance(!live ? WaitingText() : Guide(snapshot));
            result.text = live && snapshot.HasResult && snapshot.Phase == RoundPhase.Result ? Describe(snapshot) : string.Empty;
            voice.text = session.Voice.Describe();
            connection.text = session.Role != OnlineRole.Narrator ? "연결이 끊어졌습니다" :
                session.PeerConnected ? "아래층과 연결됨" : "아래층에 연결하는 중…";
        }

        private string WaitingText() => session.Role != OnlineRole.Narrator
            ? "아래층과 연결이 끊어졌습니다. 공방에서 나가 다시 참가하세요."
            : "아래층과 연결하는 중입니다.";

        private static string Guide(RoundSnapshot snapshot) => snapshot.Phase switch
        {
            RoundPhase.Waiting => "아직 손님이 없습니다. 표지를 읽으며 기다리세요.",
            RoundPhase.Preparing or RoundPhase.Consult =>
                "손님이 왔습니다. 제조자가 전하는 증상을 듣고, 메모 여섯 장에서 맞는 장을 찾으세요.",
            RoundPhase.Storage when snapshot.Brewed => "솥에 재료가 들어갔습니다. 접객 매뉴얼로 바꿔 주세요!",
            RoundPhase.Storage => "제조자가 창고에 있습니다. 메모 아래쪽의 재료 세 개를 말로 설명해 찾게 하세요.",
            RoundPhase.Prescribe => "접객 매뉴얼 차례입니다. 손님의 생김새를 물어보고 병과 잉크를 알려 주세요.",
            RoundPhase.Ending => "손님이 약을 받았습니다. 결과를 기다리세요.",
            RoundPhase.Result => "이번 손님이 끝났습니다. 메모를 처음 순서로 되돌려 두세요.",
            _ => string.Empty
        };

        private static string Describe(RoundSnapshot s)
        {
            if (s.TimedOut)
                return "시간 초과 · 0점";
            string stamp = s.Stamp == 2 ? "명약" : s.Stamp == 1 ? "그럭저럭" : "조제 실패";
            return $"재료 {s.CorrectIngredients} / 3   + {s.IngredientPoints}\n" +
                   $"물약병 {(s.BottleCorrect ? "일치" : "불일치")}   + {s.BottlePoints}\n" +
                   $"잉크 {(s.InkCorrect ? "일치" : "불일치")}   + {s.InkPoints}\n" +
                   $"남은 시간   + {s.TimeBonus}\n" +
                   $"총점 {s.Total}점 · {stamp}";
        }

        private void SetGuidance(string text)
        {
            if (text == shownGuidance)
                return;
            shownGuidance = text;
            guidance.text = text;
            // A short pop so the narrator notices the step changed while reading a memo.
            var rect = guidance.rectTransform;
            LMotion.Create(1.08f, 1f, 0.35f).WithEase(Ease.OutQuad)
                .Bind(scale => rect.localScale = new Vector3(scale, scale, 1f)).AddTo(gameObject);
        }

        private async UniTaskVoid Leave()
        {
            await session.LeaveAsync();
            // Build index 0 is whichever screen the game starts on (title or counter).
            SceneManager.LoadScene(0);
        }
    }
}
