using System;
using System.Linq;
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
        private Image easyToggle;
        private Text voice;
        private Text travelDots;
        private Text travelStatus;
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

        /// <summary>Mockup 07: the guide memo to read together on the left, the role window on the right.</summary>
        private void BuildChoosing(Transform root)
        {
            choosing = OnlineUi.Rect("Choosing", root, Vector2.zero, Vector2.one).gameObject;
            var c = (RectTransform)choosing.transform;
            OnlineUiParts.Backdrop(c, "BG_Briefing");

            var paper = OnlineUi.Box("Cover", c, new Vector2(0f, 0.5f), new Vector2(620f, 0f), new Vector2(700f, 988f));
            paper.pivot = new Vector2(0.5f, 0.5f);
            paper.localRotation = Quaternion.Euler(0f, 0f, -1.2f);
            OnlineUi.Fill(OnlineUi.Rect("Shadow", paper, Vector2.zero, Vector2.one, new Vector2(12f, -16f), new Vector2(12f, -16f)),
                new Color(0.05f, 0.02f, 0.06f, 0.5f), false);
            var cover = OnlineUi.Fill(OnlineUi.Rect("Sheet", paper, Vector2.zero, Vector2.one), Color.white, false);
            cover.sprite = config.MemoSheets[0];
            cover.preserveAspect = true;

            var board = OnlineUi.Box("Window", c, new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(640f, 960f));
            OnlineUiParts.Draw(board, "UI_Window", true);
            OnlineUi.Label(Line(board, 78f, 64f), "누가 위층으로 갈까요?", 42, config.BoldFont, OnlineUiParts.Ink,
                TextAnchor.MiddleLeft);
            OnlineUi.Label(Line(board, 146f, 64f), "왼쪽 안내서를 같이 읽고, 한 사람이 자기 자리를 고르세요. 남은 사람은 다른 자리로 가요.",
                22, config.BodyFont, OnlineUiParts.Muted, TextAnchor.UpperLeft);

            RoleButton(board, 230f, "PickLoft", "위층 · 메모 담당", "손님은 못 보고, 메모만 봐요",
                () => OnlineSession.Instance.Lobby.Pick(OnlineRole.Narrator));
            RoleButton(board, 424f, "PickCounter", "아래층 · 손님 담당", "손님을 맞고, 재료를 집어요",
                () => OnlineSession.Instance.Lobby.Pick(OnlineRole.Maker));

            voice = OnlineUi.Label(Line(board, 618f, 56f), string.Empty, 22, config.BodyFont, OnlineUiParts.Ink,
                TextAnchor.MiddleLeft);

            var easyRow = Line(board, 690f, 92f);
            OnlineUiParts.Draw(easyRow, "NET_WaitBar");
            OnlineUi.Label(OnlineUi.Rect("Label", easyRow, Vector2.zero, Vector2.one, new Vector2(28f, 0f), new Vector2(-140f, 0f)),
                "쉬움 모드", 32, config.BoldFont, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            var toggleRect = OnlineUi.Box("EasyMode", easyRow, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(88f, 48f));
            easyToggle = OnlineUi.Fill(toggleRect, Color.white);
            var toggle = toggleRect.gameObject.AddComponent<Button>();
            toggle.targetGraphic = easyToggle;
            toggle.onClick.AddListener(() => OnlineSession.Instance.Lobby.RequestEasyMode(!OnlineSession.Instance.EasyMode));
            easyLabel = OnlineUi.Label(Line(board, 790f, 60f), string.Empty, 20, config.BodyFont, OnlineUiParts.Muted,
                TextAnchor.UpperLeft);

            OnlineUiParts.Button(OnlineUi.Box("Leave", board, new Vector2(0f, 0f), new Vector2(48f, 44f), new Vector2(220f, 72f)),
                "UI_SmallButton", "연결 끊기", 28, config.BoldFont, () => OnlineSession.Instance.LeaveAsync().Forget(),
                OnlineUiParts.Ink);
        }

        private void RoleButton(RectTransform board, float top, string name, string title, string detail,
            UnityEngine.Events.UnityAction pick)
        {
            var rect = Named(Line(board, top, 176f), name);
            OnlineUiParts.Button(rect, "LOFT_Tab", null, 0, config.BoldFont, pick);
            OnlineUi.Label(OnlineUi.Rect("Title", rect, new Vector2(0f, 0.48f), Vector2.one, new Vector2(36f, 0f),
                new Vector2(-24f, -18f)), title, 36, config.BoldFont, OnlineUiParts.Ink, TextAnchor.LowerLeft);
            OnlineUi.Label(OnlineUi.Rect("Detail", rect, Vector2.zero, new Vector2(1f, 0.48f), new Vector2(36f, 18f),
                new Vector2(-24f, -8f)), detail, 24, config.BodyFont, OnlineUiParts.Muted, TextAnchor.UpperLeft);
        }

        /// <summary>Mockup 08: the stairs, a window saying where you are going, and the link status at the bottom.</summary>
        private void BuildTravelling(Transform root)
        {
            travelling = OnlineUi.Rect("Travelling", root, Vector2.zero, Vector2.one).gameObject;
            var t = (RectTransform)travelling.transform;
            OnlineUiParts.Backdrop(t, "BG_Travel");
            travelGroup = travelling.AddComponent<CanvasGroup>();

            var window = OnlineUi.Box("Window", t, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(980f, 640f));
            window.pivot = new Vector2(0.5f, 0.5f);
            OnlineUiParts.Draw(window, "UI_Window", true);
            var icon = OnlineUi.Box("Icon", window, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(168f, 150f));
            OnlineUiParts.Draw(icon, "NET_TravelIcon").preserveAspect = true;
            travelTitle = OnlineUi.Label(OnlineUi.Rect("Title", window, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(60f, -320f), new Vector2(-60f, -230f)), string.Empty, 56, config.BoldFont, OnlineUiParts.Ink);
            travelDetail = OnlineUi.Label(OnlineUi.Rect("Detail", window, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(70f, -420f), new Vector2(-70f, -330f)), string.Empty, 30, config.BoldFont, OnlineUiParts.Muted);
            travelDots = OnlineUi.Label(OnlineUi.Rect("Dots", window, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -500f), new Vector2(0f, -430f)), "● ● ●", 30, config.BoldFont, OnlineUiParts.Ink);

            var strip = OnlineUi.Rect("Status", t, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 64f));
            OnlineUi.Fill(strip, new Color(0.1f, 0.05f, 0.03f, 0.88f), false);
            travelStatus = OnlineUi.Label(OnlineUi.Rect("Text", strip, Vector2.zero, Vector2.one), string.Empty, 24,
                config.BoldFont, OnlineUiParts.Cream);
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
                easyToggle.sprite = OnlineUiParts.Get(session.EasyMode ? "NET_Toggle_On" : "NET_Toggle_Off");
                easyLabel.text = "쉬움 모드에서는 메모의 꼭 필요한 말에 표시가 돼요. 처음 하는 팀에게 권해요.";
                voice.text = "<color=#3f9a5a>●</color> 친구 연결됨 · 고르는 중…   " + session.Voice.Describe();
                AutoPick(session);
            }

            if (travelling.activeSelf)
            {
                // Three dots that fill in turn while walking.
                int lit = (int)(Time.unscaledTime * 3f) % 4;
                travelDots.text = string.Concat(Enumerable.Range(0, 3).Select(i => i < lit ? "● " : "○ "));
                travelStatus.text = session != null && session.PeerConnected
                    ? "● 온라인 · 친구 연결됨 · " + session.Voice.Describe()
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

        private static RectTransform Line(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Line", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -top - height),
                new Vector2(-48f, -top));

        private static RectTransform Named(RectTransform rect, string name)
        {
            rect.name = name;
            return rect;
        }
    }
}
