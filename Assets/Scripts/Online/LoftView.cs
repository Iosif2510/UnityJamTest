using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's upstairs workshop: the memo sheets on a desk, and a side board that follows the maker's
    /// progress downstairs. The narrator never sees the customer, the storage room, or the answers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftView : MonoBehaviour
    {
        private static readonly string[] StepNames = { "접객", "재료 탐색", "처방", "결과" };

        private OnlineCoopConfig config;
        private OnlineSession session;
        private Image sheet;
        private SheetPanZoom panZoom;
        private SheetMarks marks;
        private Text mode;
        private Text wholeLabel;
        private bool shownEasy;
        private Image[] thumbnailFrames;
        private Text[] steps;
        private Text timer;
        private Text guidance;
        private Text connection;
        private Text voice;
        private Text result;
        private int shownSheet = -1;
        private string shownGuidance;

        private void Start()
        {
            config = OnlineCoopConfig.Load();
            session = OnlineSession.Instance;
            Build();
            ShowSheet(0);
        }

        private void Build()
        {
            var root = OnlineUi.Canvas("LoftCanvas", 0, transform).transform;
            OnlineUi.Fill(OnlineUi.Rect("Desk", root, Vector2.zero, Vector2.one), OnlineUi.Wood, false);

            // Left: every sheet is always on the desk, as the printed stack was. One narrow column that divides
            // the full height, so no sheet can fall off the bottom of the screen.
            var shelf = OnlineUi.Rect("Sheets", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 24f),
                new Vector2(224f, -24f));
            OnlineUi.Fill(shelf, OnlineUi.WoodLight, false);
            var sprites = config.MemoSheets;
            thumbnailFrames = new Image[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                int index = i;
                var cell = OnlineUi.Rect("Sheet" + i, shelf, new Vector2(0f, 1f - (i + 1f) / sprites.Length),
                    new Vector2(1f, 1f - (float)i / sprites.Length), new Vector2(8f, 4f), new Vector2(-8f, -4f));
                thumbnailFrames[i] = OnlineUi.Fill(cell, OnlineUi.Parchment);
                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = thumbnailFrames[i];
                button.onClick.AddListener(() => ShowSheet(index));
                var picture = OnlineUi.Fill(OnlineUi.Rect("Picture", cell, Vector2.zero, new Vector2(0.4f, 1f),
                    new Vector2(6f, 6f), new Vector2(0f, -6f)), Color.white, false);
                picture.sprite = sprites[i];
                picture.preserveAspect = true;
                OnlineUi.Label(OnlineUi.Rect("Name", cell, new Vector2(0.4f, 0f), Vector2.one, new Vector2(4f, 0f),
                    new Vector2(-4f, 0f)), config.MemoLabels[i], 22, config.BoldFont, OnlineUi.Ink);
            }

            // Centre: the sheet being read, as wide as the space allows. Wheel moves down the page, drag pans.
            var viewport = OnlineUi.Rect("Viewport", root, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(248f, 24f), new Vector2(-448f, -24f));
            OnlineUi.Fill(viewport, new Color(0f, 0f, 0f, 0.25f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = OnlineUi.Rect("Sheet", viewport, Vector2.zero, Vector2.zero);
            sheet = OnlineUi.Fill(content, Color.white, false);
            sheet.preserveAspect = true;
            panZoom = viewport.gameObject.AddComponent<SheetPanZoom>();
            var first = sprites[0];
            panZoom.Bind(viewport, content, first.rect.width / first.rect.height);
            marks = content.gameObject.AddComponent<SheetMarks>();
            marks.Bind(content);

            // Zoom controls stay on the corner of the viewport, outside the masked sheet.
            var tools = OnlineUi.Box("Zoom", root, new Vector2(1f, 0f), new Vector2(-464f, 40f), new Vector2(244f, 56f));
            OnlineUi.Button(OnlineUi.Rect("ZoomOut", tools, new Vector2(0f, 0f), new Vector2(0.24f, 1f)), "−", 34,
                config.BoldFont, () => panZoom.ZoomOut(), OnlineUi.WoodLight);
            OnlineUi.Button(OnlineUi.Rect("ZoomIn", tools, new Vector2(0.26f, 0f), new Vector2(0.5f, 1f)), "+", 34,
                config.BoldFont, () => panZoom.ZoomIn(), OnlineUi.WoodLight);
            var whole = OnlineUi.Button(OnlineUi.Rect("Whole", tools, new Vector2(0.52f, 0f), new Vector2(1f, 1f)),
                string.Empty, 22, config.BoldFont, () => panZoom.ToggleWholeSheet(), OnlineUi.WoodLight);
            wholeLabel = OnlineUi.Label(OnlineUi.Rect("Label", whole.transform, Vector2.zero, Vector2.one), string.Empty,
                22, config.BoldFont, Color.white);

            // Right: what is happening downstairs.
            var board = OnlineUi.Rect("Board", root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-424f, 24f),
                new Vector2(-24f, -24f));
            OnlineUi.Fill(board, OnlineUi.Parchment, false);
            OnlineUi.Label(Line(board, 24f, 56f), "위층 공방", 36, config.BoldFont, OnlineUi.Ink);
            timer = OnlineUi.Label(Line(board, 90f, 110f), "--:--", 84, config.BoldFont, OnlineUi.Accent);

            steps = new Text[StepNames.Length];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = OnlineUi.Label(Line(board, 220f + i * 52f, 48f), StepNames[i], 28, config.BoldFont,
                    OnlineUi.Faded, TextAnchor.MiddleLeft);

            guidance = OnlineUi.Label(Line(board, 450f, 190f), string.Empty, 25, config.BodyFont, OnlineUi.Ink,
                TextAnchor.UpperLeft);
            result = OnlineUi.Label(Line(board, 655f, 135f), string.Empty, 23, config.BodyFont, OnlineUi.Ink,
                TextAnchor.UpperLeft);
            OnlineUi.Label(Foot(board, 142f, 28f), "휠: 아래로 넘기기 · 끌기: 옮기기", 18, config.BodyFont,
                OnlineUi.Faded, TextAnchor.MiddleLeft);
            mode = OnlineUi.Label(Foot(board, 172f, 30f), string.Empty, 18, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            voice = OnlineUi.Label(Foot(board, 108f, 28f), string.Empty, 18, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            connection = OnlineUi.Label(Foot(board, 76f, 28f), string.Empty, 18, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            OnlineUi.Button(OnlineUi.Box("Leave", board, new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(340f, 50f)), "공방에서 나가기", 24, config.BoldFont, () => Leave().Forget(), OnlineUi.WoodLight);
        }

        private static RectTransform Line(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Line", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -top - height),
                new Vector2(-24f, -top));

        /// <summary>A line measured up from the bottom of the board, so it never slides under the leave button.</summary>
        private static RectTransform Foot(RectTransform parent, float bottom, float height) =>
            OnlineUi.Rect("Foot", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, bottom),
                new Vector2(-24f, bottom + height));

        private void ShowSheet(int index)
        {
            if (index == shownSheet || index < 0 || index >= config.MemoSheets.Length)
                return;
            shownSheet = index;
            sheet.sprite = config.MemoSheets[index];
            panZoom.ResetView();
            marks.Show(index, shownEasy);
            for (int i = 0; i < thumbnailFrames.Length; i++)
                thumbnailFrames[i].color = i == index ? OnlineUi.Accent : OnlineUi.Parchment;
        }

        private void Update()
        {
            if (session.EasyMode != shownEasy)
            {
                shownEasy = session.EasyMode;
                marks.Show(shownSheet, shownEasy);
            }
            wholeLabel.text = panZoom.ShowsWholeSheet ? "크게 보기" : "전체 보기";
            mode.text = shownEasy ? "쉬움 모드 · 노랑은 증상, 초록은 재료 구별점" : string.Empty;

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
