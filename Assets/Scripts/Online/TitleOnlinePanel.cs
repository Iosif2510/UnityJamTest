using LitMotion;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The online co-op window, added at runtime to the title and the counter; the title's 함께하기 opens it
    /// (<see cref="Open"/>). It only connects the two players; who goes upstairs is picked afterwards on the
    /// <see cref="BriefingPanel"/> (also kept on the counter, for a friend who reconnects mid-game).
    /// Drawn in the B style (handoff mockups 05 and 06): one window that first offers "방 만들기" or a code, and
    /// after making a room shows the code as letter tiles while waiting for the friend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleOnlinePanel : MonoBehaviour
    {
        private const int CodeLength = 6;

        private OnlineCoopConfig config;
        private GameObject panel;
        private GameObject choices;
        private GameObject lanRow;
        private GameObject waiting;
        private Text waitTitle;
        private Text waitDetail;
        private GameObject tiles;
        private Text[] tileLetters;
        private Text directLabel;
        private GameObject copyButton;
        private Text status;
        private Image[] waitDots;
        private Text error;
        private InputField codeInput;
        private InputField addressInput;
        private Button leaveButton;
        private GameObject closeButton;
        private static TitleOnlinePanel current;

        /// <summary>Opens the make-a-room / enter-a-code window (the title's 함께하기).</summary>
        internal static void Open()
        {
            if (current != null && current.panel != null) current.panel.SetActive(true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (scene, _) => Attach(scene);
            Attach(SceneManager.GetActiveScene());
        }

        private static void Attach(Scene scene)
        {
            if (scene.name != OnlineCoopConfig.TitleScene && scene.name != OnlineCoopConfig.CounterScene ||
                FindAnyObjectByType<TitleOnlinePanel>() != null)
                return;
            if (OnlineCoopConfig.Load() == null)
            {
                Debug.LogWarning("[Online] OnlineCoopConfig is missing from Resources; online co-op is disabled.");
                return;
            }
            new GameObject("TitleOnlinePanel").AddComponent<TitleOnlinePanel>();
        }

        private void Start()
        {
            config = OnlineCoopConfig.Load();
            var canvas = OnlineUi.Canvas("OnlineCanvas", 50, transform);
            var root = canvas.transform;

            current = this;

            panel = OnlineUi.Rect("Panel", root, Vector2.zero, Vector2.one).gameObject;
            // the shop is drawn behind on both the title and the counter, so a warm dim is enough
            OnlineUi.Fill((RectTransform)panel.transform, new Color(0.12f, 0.06f, 0.03f, 0.55f));
            var card = OnlineUi.Box("Window", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 770f));
            card.pivot = new Vector2(0.5f, 0.5f);
            OnlineUiParts.Draw(card, "UI_Window", true);

            closeButton = OnlineUiParts.Button(OnlineUi.Box("Close", card, new Vector2(1f, 1f), new Vector2(-44f, -40f),
                new Vector2(64f, 64f)), "UI_CloseButton", null, 0, config.BoldFont, () => panel.SetActive(false)).gameObject;

            BuildChoices(card);
            BuildWaiting(card);

            panel.SetActive(false);
            gameObject.AddComponent<BriefingPanel>();
            if (OnlineSession.Exists)
                Subscribe();
            Refresh();

            // Lets a build be driven without clicking, e.g. two instances on one PC: -onlineAuto host|join [-onlineAddress ip]
            string auto = OnlineSession.CommandLineValue("-onlineAuto");
            if (auto == "host" && OnlineSession.CurrentRole == OnlineRole.Offline)
                Finish(OnlineSession.Instance.HostLan());
            else if (auto == "join" && OnlineSession.CurrentRole == OnlineRole.Offline)
                Finish(OnlineSession.Instance.JoinLan(OnlineSession.CommandLineValue("-onlineAddress")));
        }

        /// <summary>Mockup 05: make a room, or type the friend's code. Direct LAN play folds out underneath.</summary>
        private void BuildChoices(RectTransform card)
        {
            choices = OnlineUi.Rect("Choices", card, Vector2.zero, Vector2.one).gameObject;
            var c = (RectTransform)choices.transform;
            OnlineUi.Label(Row(c, 56f, 60f), Loc.T("온라인 협동"), 42, config.BoldFont, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            OnlineUi.Label(Row(c, 120f, 64f), Loc.T("한 사람은 아래층에서 손님을 맞고,\n한 사람은 위층 공방에서 메모를 보며 목소리로 안내해요."),
                22, config.BoldFont, OnlineUiParts.InkSoft, TextAnchor.UpperLeft);

            OnlineUiParts.Button(Row(c, 206f, 96f), "UI_BigButton", Loc.T("방 만들기"), 40, config.BoldFont, () => HostCloud().Forget());

            OnlineUi.Fill(Row(c, 348f, 2f), new Color(0.47f, 0.35f, 0.25f, 0.45f), false);
            var or = OnlineUi.Box("Or", c, new Vector2(0.5f, 1f), new Vector2(0f, -332f), new Vector2(84f, 34f));
            OnlineUi.Fill(or, new Color(0.99f, 0.95f, 0.87f), false);
            OnlineUi.Label(OnlineUi.Rect("Text", or, Vector2.zero, Vector2.one), Loc.T("또는"), 22, config.BoldFont, OnlineUiParts.InkSoft);

            OnlineUi.Label(Row(c, 384f, 40f), Loc.T("친구에게 받은 코드"), 26, config.BoldFont, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            codeInput = Field(Cell(c, 432f, 88f, 0f, 0.66f), "K7Q2M9", 40, 12);
            codeInput.textComponent.alignment = TextAnchor.MiddleCenter;
            ((Text)codeInput.placeholder).alignment = TextAnchor.MiddleCenter;
            codeInput.onValueChanged.AddListener(text =>
            {
                string upper = text.ToUpperInvariant();
                if (upper != text) codeInput.SetTextWithoutNotify(upper);
            });
            OnlineUiParts.Button(Cell(c, 440f, 72f, 0.70f, 1f), "UI_SmallButton", Loc.T("들어가기"), 30, config.BoldFont,
                () => JoinCloud().Forget());

            // The LAN fold-out is marked with the arrow part turned on its side (the font has no triangle glyphs).
            var lanToggle = OnlineUi.Rect("LanToggle", Row(c, 590f, 48f), Vector2.zero, new Vector2(0.75f, 1f));
            var lanText = OnlineUi.Label(lanToggle, Loc.T("같은 네트워크에서 직접 연결"), 24, config.BoldFont, OnlineUiParts.Ink,
                TextAnchor.MiddleLeft);
            lanText.raycastTarget = true;
            var lanArrow = OnlineUi.Box("Arrow", lanToggle, new Vector2(0f, 0.5f), new Vector2(352f, 0f), new Vector2(18f, 18f));
            lanArrow.pivot = new Vector2(0.5f, 0.5f);
            OnlineUiParts.Triangle(lanArrow, OnlineUiParts.Ink);
            lanToggle.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                lanRow.SetActive(!lanRow.activeSelf);
                lanArrow.localRotation = Quaternion.Euler(0f, 0f, lanRow.activeSelf ? -90f : 0f); // points down when open
            });

            lanRow = OnlineUi.Rect("Lan", c, Vector2.zero, Vector2.one).gameObject;
            var l = (RectTransform)lanRow.transform;
            addressInput = Field(Cell(l, 644f, 72f, 0f, 0.42f), Loc.T("방을 연 PC 주소"), 24, 64);
            OnlineUiParts.Button(Cell(l, 644f, 72f, 0.44f, 0.71f), "UI_SmallButton", Loc.T("직접 참가"), 26, config.BoldFont,
                () => Finish(OnlineSession.Instance.JoinLan(addressInput.text)));
            OnlineUiParts.Button(Cell(l, 644f, 72f, 0.73f, 1f), "UI_SmallButton", Loc.T("직접 방 열기"), 26, config.BoldFont,
                () => Finish(OnlineSession.Instance.HostLan()));
            lanRow.SetActive(false);
#if UNITY_WEBGL
            // a browser can neither open a room on this PC nor reach one by its address
            lanToggle.parent.gameObject.SetActive(false);
#endif

            error = OnlineUi.Label(Row(card, 540f, 36f), string.Empty, 22, config.BoldFont, OnlineUiParts.StampRed);
        }

        /// <summary>Mockup 06: the room's code as letter tiles to read out, and a strip that says we are waiting.</summary>
        private void BuildWaiting(RectTransform card)
        {
            waiting = OnlineUi.Rect("Waiting", card, Vector2.zero, Vector2.one).gameObject;
            var w = (RectTransform)waiting.transform;
            waitTitle = OnlineUi.Label(Row(w, 70f, 64f), Loc.T("방을 만들었어요"), 44, config.BoldFont, OnlineUiParts.Ink);
            waitDetail = OnlineUi.Label(Row(w, 136f, 40f), Loc.T("이 코드를 친구에게 불러 주세요"), 24, config.BoldFont,
                OnlineUiParts.InkSoft);

            tiles = OnlineUi.Box("Tiles", w, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(700f, 120f)).gameObject;
            var t = (RectTransform)tiles.transform;
            t.pivot = new Vector2(0.5f, 1f);
            var layout = tiles.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f; // 96 wide tiles, 110 apart
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            tileLetters = new Text[CodeLength];
            for (int i = 0; i < CodeLength; i++)
            {
                var tile = OnlineUi.Rect("Tile", t, Vector2.zero, Vector2.zero);
                tile.sizeDelta = new Vector2(96f, 120f);
                OnlineUiParts.Draw(tile, "NET_CodeTile");
                tileLetters[i] = OnlineUi.Label(OnlineUi.Rect("Letter", tile, Vector2.zero, Vector2.one, new Vector2(0f, 6f),
                    Vector2.zero), string.Empty, 60, config.BoldFont, OnlineUiParts.Ink);
            }
            directLabel = OnlineUi.Label(Row(w, 196f, 120f), string.Empty, 40, config.BoldFont, OnlineUiParts.Ink);

            var copy = OnlineUi.Box("Copy", w, new Vector2(0.5f, 1f), new Vector2(0f, -342f), new Vector2(220f, 72f));
            copy.pivot = new Vector2(0.5f, 1f);
            // the copy happens on the press (a browser allows it only during the click itself), the button just shows it
            var copyBtn = OnlineUiParts.Button(copy, "UI_SmallButton", Loc.T("코드 복사"), 28, config.BoldFont, () => { });
            var copier = copy.gameObject.AddComponent<ClipboardCopy>();
            copier.Text = () => OnlineSession.Exists ? OnlineSession.Instance.JoinCode : null;
            var copyLabel = copy.GetComponentInChildren<Text>();
            copier.Copied += () =>
            {
                GameSfx.Play("Tick", 0.8f);
                if (copyLabel == null) return;
                copyLabel.text = Loc.T("복사했어요!");
                LMotion.Create(0f, 1f, 1.6f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithOnComplete(() => { if (copyLabel != null) copyLabel.text = Loc.T("코드 복사"); })
                    .RunWithoutBinding().AddTo(copy.gameObject);
            };
            copyButton = copyBtn.gameObject;

            var strip = Row(w, 446f, 84f);
            OnlineUiParts.Draw(strip, "NET_WaitBar");
            waitDots = OnlineUiParts.WaitingDots(strip, new Vector2(78f, -42f), 18f);
            status = OnlineUi.Label(OnlineUi.Rect("Text", strip, Vector2.zero, Vector2.one, new Vector2(124f, 2f),
                new Vector2(-30f, 0f)), string.Empty, 28, config.BoldFont, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            OnlineUi.Label(Row(w, 544f, 36f), Loc.T("친구가 들어오면 바로 역할 고르기로 넘어가요"), 22, config.BoldFont,
                OnlineUiParts.InkSoft);

            var leave = OnlineUi.Box("Leave", w, new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(200f, 72f));
            leave.pivot = new Vector2(0.5f, 0f);
            leaveButton = OnlineUiParts.Button(leave, "UI_SmallButton", Loc.T("방 닫기"), 28, config.BoldFont,
                () => OnlineSession.Instance.LeaveAsync().Forget());
            waiting.SetActive(false);
        }

        private InputField Field(RectTransform rect, string placeholder, int size, int limit)
        {
            var field = OnlineUi.Input(rect, placeholder, size, config.BoldFont, limit);
            var image = rect.GetComponent<Image>();
            image.sprite = OnlineUiParts.Get("NET_Input_Normal");
            image.type = Image.Type.Sliced;
            field.transition = Selectable.Transition.SpriteSwap;
            field.spriteState = new SpriteState
            {
                highlightedSprite = OnlineUiParts.Get("NET_Input_Focused"),
                selectedSprite = OnlineUiParts.Get("NET_Input_Focused"),
                pressedSprite = OnlineUiParts.Get("NET_Input_Focused"),
            };
            field.textComponent.rectTransform.offsetMin = new Vector2(26f, 4f);
            field.textComponent.rectTransform.offsetMax = new Vector2(-26f, -4f);
            field.placeholder.rectTransform.offsetMin = new Vector2(26f, 4f);
            field.placeholder.rectTransform.offsetMax = new Vector2(-26f, -4f);
            ((Text)field.placeholder).color = new Color(0.29f, 0.18f, 0.12f, 0.3f);
            field.textComponent.color = OnlineUiParts.Ink;
            return field;
        }

        private void Update()
        {
            // On the counter the entry only belongs to the closed-curtain wait between customers.
            var round = RoundDirector.Instance;
            bool waitingForCustomer = round == null || round.Phase == RoundPhase.Waiting;
            var role = OnlineSession.CurrentRole;

            // Connected but not yet paired or assigned: the customer must not be received, so the window stays up
            // while waiting for the friend, and gives way to the briefing once they are here.
            bool pairing = role == OnlineRole.Undecided && !OnlineSession.Instance.PeerConnected;
            bool briefing = role == OnlineRole.Undecided && !pairing;
            if (pairing && !panel.activeSelf)
                panel.SetActive(true);
            if ((!waitingForCustomer || briefing) && panel.activeSelf)
                panel.SetActive(false);
            if (closeButton.activeSelf == pairing)
                closeButton.SetActive(!pairing);

            if (waiting.activeSelf)
                OnlineUiParts.AnimateDots(waitDots);
        }

        private string waitingLine = string.Empty;

        private void OnDestroy()
        {
            if (current == this) current = null;
            if (OnlineSession.Exists)
                OnlineSession.Instance.Changed -= Refresh;
        }

        private static RectTransform Row(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Row", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(64f, -top - height),
                new Vector2(-64f, -top));

        private static RectTransform Cell(RectTransform parent, float top, float height, float from, float to)
        {
            var row = Row(parent, top, height);
            return OnlineUi.Rect("Cell", row, new Vector2(from, 0f), new Vector2(to, 1f));
        }

        private async UniTaskVoid HostCloud()
        {
            Subscribe();
            Finish(await OnlineSession.Instance.HostCloudAsync());
        }

        private async UniTaskVoid JoinCloud()
        {
            Subscribe();
            Finish(await OnlineSession.Instance.JoinCloudAsync(codeInput.text));
        }

        private void Finish(string message)
        {
            Subscribe();
            Refresh();
            if (error != null)
                error.text = message ?? string.Empty;
            if (message != null && codeInput != null)
            {
                var image = codeInput.GetComponent<Image>();
                image.sprite = OnlineUiParts.Get("NET_Input_Error");
            }
        }

        private bool subscribed;

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            OnlineSession.Instance.Changed += Refresh;
        }

        private void Refresh()
        {
            if (this == null || status == null)
                return;

            var session = OnlineSession.Exists ? OnlineSession.Instance : null;
            var role = session != null ? session.Role : OnlineRole.Offline;

            bool offline = role == OnlineRole.Offline;
            bool busy = session != null && session.Busy;
            choices.SetActive(offline && !busy);
            waiting.SetActive(!offline || busy);
            leaveButton.gameObject.SetActive(!offline);
            if (offline && !busy)
                return;

            string code = session?.JoinCode;
            bool hosting = session != null && session.IsHost;
            tiles.SetActive(code != null && hosting);
            copyButton.SetActive(code != null && hosting);
            for (int i = 0; i < tileLetters.Length; i++)
                tileLetters[i].text = code != null && i < code.Length ? code[i].ToString() : string.Empty;
            directLabel.text = code == null && hosting ? Loc.T("직접 연결로 방을 열었어요") : string.Empty;

            waitTitle.text = busy ? Loc.T("연결하는 중…") : hosting ? Loc.T("방을 만들었어요") : Loc.T("방에 들어가는 중…");
            waitDetail.text = hosting && code != null ? Loc.T("이 코드를 친구에게 불러 주세요")
                : hosting ? Loc.T("친구가 이 PC의 주소로 직접 참가하면 돼요") : string.Empty;

            if (busy)
                waitingLine = Loc.T("연결하는 중…");
            else if (!session.PeerConnected)
                waitingLine = hosting ? Loc.T("친구가 들어오길 기다리는 중…") : Loc.T("방에 들어가는 중…");
            else
                waitingLine = role == OnlineRole.Maker
                    ? Loc.T("친구가 위층 공방에 있어요. 닫고 손님을 받으세요.")
                    : Loc.T("친구와 연결됐어요");
            status.text = waitingLine;
        }
    }
}
