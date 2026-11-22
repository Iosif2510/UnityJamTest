using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// Shown to both players as soon as they are connected: they read the cover note together, then one of them
    /// picks who goes upstairs. After a short "on the way" screen the narrator moves to the loft and the maker
    /// is left at the counter, where the customer can now be received.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BriefingPanel : MonoBehaviour
    {
        private const float TravelSeconds = 2.4f;

        private OnlineCoopConfig config;
        private GameObject choosing;
        private GameObject travelling;
        private CanvasGroup travelGroup;
        private Text travelTitle;
        private Text travelDetail;
        private Text easyLabel;
        private Text voice;
        private bool travelStarted;
        private float choosingSince = -1f;

        private void Start()
        {
            config = OnlineCoopConfig.Load();
            var root = OnlineUi.Canvas("BriefingCanvas", 60, transform).transform;

            choosing = OnlineUi.Rect("Choosing", root, Vector2.zero, Vector2.one).gameObject;
            var c = (RectTransform)choosing.transform;
            OnlineUi.Fill(c, OnlineUi.Wood);

            // The cover note, large enough for both players to read on their own screens.
            var cover = OnlineUi.Fill(OnlineUi.Rect("Cover", c, Vector2.zero, Vector2.one, new Vector2(50f, 30f),
                new Vector2(-640f, -30f)), Color.white, false);
            cover.sprite = config.MemoSheets[0];
            cover.preserveAspect = true;

            var board = OnlineUi.Rect("Board", c, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-600f, 30f),
                new Vector2(-40f, -30f));
            OnlineUi.Fill(board, OnlineUi.Parchment, false);
            OnlineUi.Label(Line(board, 36f, 60f), "누가 위층으로 갈까요?", 40, config.BoldFont, OnlineUi.Ink);
            OnlineUi.Label(Line(board, 110f, 120f),
                "왼쪽 쪽지를 같이 읽은 다음, 한 사람이 자기 자리를 고르세요. 다른 사람은 남은 자리로 갑니다.",
                24, config.BodyFont, OnlineUi.Ink, TextAnchor.UpperLeft);

            OnlineUi.Button(Named(Line(board, 250f, 96f), "PickLoft"), "내가 위층으로\n(메모 담당)", 28, config.BoldFont,
                () => OnlineSession.Instance.Lobby.Pick(OnlineRole.Narrator));
            OnlineUi.Button(Named(Line(board, 366f, 96f), "PickCounter"), "내가 아래층에\n(손님 담당)", 28, config.BoldFont,
                () => OnlineSession.Instance.Lobby.Pick(OnlineRole.Maker));

            var easy = OnlineUi.Button(Named(Line(board, 520f, 64f), "EasyMode"), string.Empty, 26, config.BoldFont,
                () => OnlineSession.Instance.Lobby.RequestEasyMode(!OnlineSession.Instance.EasyMode), OnlineUi.WoodLight);
            easyLabel = OnlineUi.Label(OnlineUi.Rect("Label", easy.transform, Vector2.zero, Vector2.one), string.Empty, 26,
                config.BoldFont, Color.white);
            OnlineUi.Label(Line(board, 592f, 90f),
                "쉬움 모드에서는 메모에서 꼭 필요한 말에 표시가 됩니다. 처음 하는 팀에게 권합니다.",
                22, config.BodyFont, OnlineUi.Faded, TextAnchor.UpperLeft);

            voice = OnlineUi.Label(Line(board, 880f, 40f), string.Empty, 20, config.BodyFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);
            OnlineUi.Button(OnlineUi.Box("Leave", board, new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(480f, 54f)), "연결 끊기", 24, config.BoldFont,
                () => OnlineSession.Instance.LeaveAsync().Forget(), OnlineUi.WoodLight);

            travelling = OnlineUi.Rect("Travelling", root, Vector2.zero, Vector2.one).gameObject;
            var t = (RectTransform)travelling.transform;
            OnlineUi.Fill(t, OnlineUi.Wood);
            travelGroup = travelling.AddComponent<CanvasGroup>();
            travelTitle = OnlineUi.Label(OnlineUi.Rect("Title", t, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 0f), new Vector2(0f, 120f)), string.Empty, 72, config.BoldFont, OnlineUi.Parchment);
            travelDetail = OnlineUi.Label(OnlineUi.Rect("Detail", t, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -90f), new Vector2(0f, -10f)), string.Empty, 32, config.BodyFont, OnlineUi.Parchment);

            choosing.SetActive(false);
            travelling.SetActive(false);
        }

        private void Update()
        {
            var session = OnlineSession.Exists ? OnlineSession.Instance : null;
            var role = session != null ? session.Role : OnlineRole.Offline;

            bool choose = role == OnlineRole.Undecided && session.PeerConnected;
            if (choosing.activeSelf != choose)
            {
                choosing.SetActive(choose);
                choosingSince = choose ? Time.unscaledTime : -1f;
            }
            if (choose)
            {
                easyLabel.text = session.EasyMode ? "쉬움 모드: 켜짐" : "쉬움 모드: 꺼짐";
                voice.text = session.Voice.Describe();
                AutoPick(session);
            }

            if ((role is OnlineRole.Maker or OnlineRole.Narrator) && !session.Arrived && !travelStarted)
                Travel(session, role).Forget();
        }

        /// <summary>Lets two instances be driven without clicking: -onlineRole maker|narrator [-onlineEasy].</summary>
        private void AutoPick(OnlineSession session)
        {
            if (choosingSince < 0f || Time.unscaledTime - choosingSince < 1.5f)
                return;
            string wanted = OnlineSession.CommandLineValue("-onlineRole");
            if (wanted == null)
                return;
            choosingSince = float.PositiveInfinity;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-onlineEasy") >= 0)
                session.Lobby.RequestEasyMode(true);
            session.Lobby.Pick(wanted == "narrator" ? OnlineRole.Narrator : OnlineRole.Maker);
        }

        private async UniTaskVoid Travel(OnlineSession session, OnlineRole role)
        {
            travelStarted = true;
            bool upstairs = role == OnlineRole.Narrator;
            travelTitle.text = upstairs ? "위층 공방으로 가는 중…" : "아래층 카운터로 가는 중…";
            travelDetail.text = upstairs
                ? "메모 뭉치를 맡았습니다. 화면은 볼 수 없으니 말로만 안내하세요."
                : "손님을 맡았습니다. 메모는 볼 수 없으니 말로만 물어보세요.";
            travelling.SetActive(true);
            travelGroup.alpha = 0f;
            _ = LMotion.Create(0f, 1f, 0.35f).WithEase(Ease.OutQuad).Bind(alpha => travelGroup.alpha = alpha)
                .AddTo(gameObject);

            var token = this.GetCancellationTokenOnDestroy();
            await UniTask.Delay(TimeSpan.FromSeconds(TravelSeconds), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);
            if (session == null || session.Role != role)
            {
                // The connection dropped on the way; fall back to the title/counter as it was.
                travelling.SetActive(false);
                travelStarted = false;
                return;
            }

            session.MarkArrived();
            if (upstairs)
            {
                SceneManager.LoadScene(OnlineCoopConfig.LoftScene);
                return;
            }

            _ = LMotion.Create(1f, 0f, 0.35f).WithEase(Ease.InQuad).Bind(alpha => travelGroup.alpha = alpha)
                .AddTo(gameObject);
            await UniTask.Delay(TimeSpan.FromSeconds(0.35f), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);
            travelling.SetActive(false);
            travelStarted = false;
        }

        private static RectTransform Line(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Line", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -top - height),
                new Vector2(-40f, -top));

        private static RectTransform Named(RectTransform rect, string name)
        {
            rect.name = name;
            return rect;
        }
    }
}
