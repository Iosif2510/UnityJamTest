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

            // Left: every sheet is always on the desk, as the printed stack was.
            var shelf = OnlineUi.Rect("Sheets", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(30f, 30f),
                new Vector2(330f, -30f));
            OnlineUi.Fill(shelf, OnlineUi.WoodLight, false);
            var sprites = config.MemoSheets;
            thumbnailFrames = new Image[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                int index = i;
                var cell = OnlineUi.Box("Sheet" + i, shelf, new Vector2(0f, 1f),
                    new Vector2(16f + i % 2 * 138f, -16f - i / 2 * 250f), new Vector2(130f, 240f));
                thumbnailFrames[i] = OnlineUi.Fill(cell, OnlineUi.Parchment);
                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = thumbnailFrames[i];
                button.onClick.AddListener(() => ShowSheet(index));
                var picture = OnlineUi.Fill(OnlineUi.Rect("Picture", cell, Vector2.zero, Vector2.one,
                    new Vector2(8f, 44f), new Vector2(-8f, -8f)), Color.white, false);
                picture.sprite = sprites[i];
                picture.preserveAspect = true;
                OnlineUi.Label(OnlineUi.Rect("Name", cell, Vector2.zero, new Vector2(1f, 0f), Vector2.zero,
                    new Vector2(0f, 44f)), config.MemoLabels[i], 20, config.BoldFont, OnlineUi.Ink);
            }

            // Centre: the sheet being read. Wheel zooms, drag pans.
            var viewport = OnlineUi.Rect("Viewport", root, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(360f, 30f), new Vector2(-510f, -30f));
            OnlineUi.Fill(viewport, new Color(0f, 0f, 0f, 0.25f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = OnlineUi.Rect("Sheet", viewport, Vector2.zero, Vector2.one);
            sheet = OnlineUi.Fill(content, Color.white, false);
            sheet.preserveAspect = true;
            panZoom = viewport.gameObject.AddComponent<SheetPanZoom>();
            panZoom.Bind(viewport, content);

            // Right: what is happening downstairs.
            var board = OnlineUi.Rect("Board", root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-480f, 30f),
                new Vector2(-30f, -30f));
            OnlineUi.Fill(board, OnlineUi.Parchment, false);
            OnlineUi.Label(Line(board, 24f, 56f), "위층 공방", 40, config.BoldFont, OnlineUi.Ink);
            timer = OnlineUi.Label(Line(board, 90f, 110f), "--:--", 92, config.BoldFont, OnlineUi.Accent);

            steps = new Text[StepNames.Length];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = OnlineUi.Label(Line(board, 220f + i * 52f, 48f), StepNames[i], 30, config.BoldFont,
                    OnlineUi.Faded, TextAnchor.MiddleLeft);

            guidance = OnlineUi.Label(Line(board, 450f, 190f), string.Empty, 28, config.BodyFont, OnlineUi.Ink,
                TextAnchor.UpperLeft);
            result = OnlineUi.Label(Line(board, 650f, 210f), string.Empty, 26, config.BodyFont, OnlineUi.Ink,
                TextAnchor.UpperLeft);
            voice = OnlineUi.Label(Line(board, 870f, 36f), string.Empty, 20, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            connection = OnlineUi.Label(Line(board, 908f, 36f), string.Empty, 20, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            OnlineUi.Button(OnlineUi.Box("Leave", board, new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(390f, 50f)), "공방에서 나가기", 24, config.BoldFont, () => Leave().Forget(), OnlineUi.WoodLight);
        }

        private static RectTransform Line(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Line", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -top - height),
                new Vector2(-30f, -top));

        private void ShowSheet(int index)
        {
            if (index == shownSheet || index < 0 || index >= config.MemoSheets.Length)
                return;
            shownSheet = index;
            sheet.sprite = config.MemoSheets[index];
            panZoom.ResetView();
            for (int i = 0; i < thumbnailFrames.Length; i++)
                thumbnailFrames[i].color = i == index ? OnlineUi.Accent : OnlineUi.Parchment;
        }

        private void Update()
        {
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
            SceneManager.LoadScene(OnlineCoopConfig.TitleScene);
        }
    }
}
