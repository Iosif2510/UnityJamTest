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
    /// is left at the counter, where the customer can now be received. Drawn from the designer's parts
    /// (handoff 조립_시안 07 and 08).
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
        private Image[] travelDots;
        private Text travelStatus;
        private Image easyToggle;
        private MemoSheetView cover;
        private Text voice;
        private bool travelStarted;
        private float choosingSince = -1f;

        private void Start()
        {
            config = OnlineCoopConfig.Load();
            var root = OnlineUi.Canvas("BriefingCanvas", 60, transform).transform;
            BuildChoosing(root);
            BuildTravelling(root);
            choosing.SetActive(false);
            travelling.SetActive(false);
        }

        private static RectTransform At(Transform parent, string name, Vector2 topLeft, Vector2 size) =>
            OnlineUi.Box(name, parent, new Vector2(0f, 1f), new Vector2(topLeft.x, -topLeft.y), size);

        /// <summary>Mockup 07: the guide memo to read together on the left, the role window on the right.</summary>
        private void BuildChoosing(Transform root)
        {
            choosing = OnlineUi.Rect("Choosing", root, Vector2.zero, Vector2.one).gameObject;
            var c = (RectTransform)choosing.transform;
            OnlineUiParts.Backdrop(c, "BG_Loft", new Color(0.62f, 0.58f, 0.55f));

            var paper = At(c, "Guide", new Vector2(64f, 224f), new Vector2(1120f, 630f));
            OnlineUiParts.Draw(OnlineUi.Rect("Shadow", paper, Vector2.zero, Vector2.one, new Vector2(-12f, -16f),
                new Vector2(12f, 8f)), "LOFT_PaperShadow");
            cover = new MemoSheetView(paper);
            cover.Show(0, false);

            var board = At(c, "Window", new Vector2(1220f, 64f), new Vector2(640f, 960f));
            OnlineUiParts.Draw(board, "UI_Window", true);
            OnlineUi.Label(Line(board, 64f, 60f), "누가 위층으로 갈까요?", 40, config.BoldFont, OnlineUiParts.Ink,
                TextAnchor.MiddleLeft);
            OnlineUi.Label(Line(board, 130f, 64f), "왼쪽 쪽지를 같이 읽고, 한 사람이 자리를 고르세요. 다른 사람은 남은 자리로 갑니다.",
                22, config.BoldFont, OnlineUiParts.InkSoft, TextAnchor.UpperLeft);

            RoleButton(board, 222f, "PickLoft", "NET_RoleIcon_Upstairs", "위층 · 메모 담당", "손님은 못 보고, 메모만 봐요",
                () => OnlineSession.Instance.Lobby.Pick(OnlineRole.Narrator));
            RoleButton(board, 418f, "PickCounter", "NET_RoleIcon_Downstairs", "아래층 · 손님 담당", "손님을 맞고, 재료를 집어요",
                () => OnlineSession.Instance.Lobby.Pick(OnlineRole.Maker));

            var lamp = At(board, "Lamp", new Vector2(52f, 624f), new Vector2(18f, 18f));
            OnlineUiParts.Lamp(lamp);
            OnlineUi.Label(Line(board, 612f, 42f, 80f), "친구 연결됨 · 고르는 중…", 24, config.BoldFont, OnlineUiParts.Ink,
                TextAnchor.MiddleLeft);
            voice = OnlineUi.Label(Line(board, 656f, 34f, 80f), string.Empty, 20, config.BoldFont, OnlineUiParts.InkSoft,
                TextAnchor.MiddleLeft);

            OnlineUi.Label(Line(board, 740f, 44f), "쉬움 모드", 30, config.BoldFont, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            OnlineUi.Label(Line(board, 786f, 32f), "메모의 꼭 필요한 곳에 상자가 쳐져요", 20, config.BoldFont,
                OnlineUiParts.InkSoft, TextAnchor.MiddleLeft);
            var toggleRect = OnlineUi.Box("EasyMode", board, new Vector2(1f, 1f), new Vector2(-52f, -754f), new Vector2(88f, 48f));
            easyToggle = OnlineUiParts.Draw(toggleRect, "NET_Toggle_Off", true);
            toggleRect.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                OnlineSession.Instance.Lobby.RequestEasyMode(!OnlineSession.Instance.EasyMode));

            // "연결 끊기" is a quiet underlined link, as in the mockup.
            var leave = At(board, "Leave", new Vector2(52f, 872f), new Vector2(140f, 40f));
            var leaveText = OnlineUi.Label(leave, "연결 끊기", 24, config.BoldFont, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            leaveText.raycastTarget = true;
            leave.gameObject.AddComponent<Button>().onClick.AddListener(() => OnlineSession.Instance.LeaveAsync().Forget());
            OnlineUi.Fill(OnlineUi.Rect("Underline", leave, Vector2.zero, new Vector2(0.72f, 0f), new Vector2(0f, -2f),
                new Vector2(0f, 0f)), OnlineUiParts.Ink, false);
        }

        private void RoleButton(RectTransform board, float top, string name, string icon, string title, string detail,
            UnityEngine.Events.UnityAction pick)
        {
            var rect = Line(board, top, 176f);
            rect.name = name;
            OnlineUiParts.Button(rect, "NET_RoleButton", null, 0, config.BoldFont, pick);
            var picture = OnlineUi.Box("Icon", rect, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(112f, 112f));
            picture.pivot = new Vector2(0f, 0.5f);
            OnlineUiParts.Draw(picture, icon);
            OnlineUi.Label(OnlineUi.Rect("Title", rect, new Vector2(0f, 0.5f), Vector2.one, new Vector2(156f, 0f),
                new Vector2(-20f, -16f)), title, 32, config.BoldFont, OnlineUiParts.Ink, TextAnchor.LowerLeft);
            OnlineUi.Label(OnlineUi.Rect("Detail", rect, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(156f, 16f),
                new Vector2(-20f, -8f)), detail, 22, config.BoldFont, OnlineUiParts.InkSoft, TextAnchor.UpperLeft);
        }

        /// <summary>Mockup 08: the stairs, a window saying where you are going, and the link status at the bottom.</summary>
        private void BuildTravelling(Transform root)
        {
            travelling = OnlineUi.Rect("Travelling", root, Vector2.zero, Vector2.one).gameObject;
            var t = (RectTransform)travelling.transform;
            OnlineUiParts.Backdrop(t, "BG_Travel");
            travelGroup = travelling.AddComponent<CanvasGroup>();

            var window = OnlineUi.Box("Window", t, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(960f, 612f));
            window.pivot = new Vector2(0.5f, 0.5f);
            OnlineUiParts.Draw(window, "UI_Window", true);
            var icon = OnlineUi.Box("Icon", window, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(150f, 150f));
            OnlineUiParts.Draw(icon, "NET_TravelIcon");
            travelTitle = OnlineUi.Label(Line(window, 246f, 70f), string.Empty, 48, config.BoldFont, OnlineUiParts.Ink);
            travelDetail = OnlineUi.Label(Line(window, 324f, 44f), string.Empty, 26, config.BoldFont, OnlineUiParts.InkSoft);
            travelDots = OnlineUiParts.WaitingDots(window, new Vector2(480f, -410f));

            var strip = OnlineUi.Rect("Status", t, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 64f));
            OnlineUiParts.Draw(strip, "NET_StatusStrip");
            OnlineUiParts.Lamp(OnlineUi.Box("Lamp", strip, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(18f, 18f)));
            travelStatus = OnlineUi.Label(OnlineUi.Rect("Text", strip, Vector2.zero, Vector2.one, new Vector2(68f, 0f),
                new Vector2(-40f, 0f)), string.Empty, 22, config.BoldFont, OnlineUiParts.Cream, TextAnchor.MiddleLeft);
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
                cover.Refresh();
                easyToggle.sprite = OnlineUiParts.Get(session.EasyMode ? "NET_Toggle_On" : "NET_Toggle_Off");
                voice.text = session.Voice.Describe();
                AutoPick(session);
            }

            if (travelling.activeSelf)
            {
                OnlineUiParts.AnimateDots(travelDots);
                travelStatus.text = session != null && session.PeerConnected
                    ? "온라인 · 친구 연결됨 · " + session.Voice.Describe()
                    : "연결이 끊어졌어요";
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
                ? "손님은 볼 수 없으니 친구 말을 듣고 메모를 찾아 주세요"
                : "메모는 볼 수 없으니 위층 친구에게 말로 물어보세요";
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

        private static RectTransform Line(RectTransform parent, float top, float height, float left = 52f) =>
            OnlineUi.Rect("Line", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(left, -top - height),
                new Vector2(-52f, -top));
    }
}
