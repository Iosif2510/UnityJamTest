using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// Adds the online co-op entry at runtime to the screen the game waits on: the title, or the counter while its
    /// curtain is closed. It only connects the two players; who goes upstairs is picked afterwards on the
    /// <see cref="BriefingPanel"/>. The existing buttons keep starting the game, so offline play is unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleOnlinePanel : MonoBehaviour
    {
        private OnlineCoopConfig config;
        private GameObject panel;
        private GameObject choices;
        private Text status;
        private Text codeLabel;
        private InputField codeInput;
        private InputField addressInput;
        private Button leaveButton;
        private GameObject closeButton;
        private GameObject openButton;

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

            openButton = OnlineUi.Button(OnlineUi.Box("OpenOnline", root, new Vector2(1f, 0f), new Vector2(-40f, 40f),
                new Vector2(300f, 76f)), "온라인 협동", 32, config.BoldFont, () => panel.SetActive(true)).gameObject;

            panel = OnlineUi.Rect("Panel", root, Vector2.zero, Vector2.one).gameObject;
            OnlineUi.Fill((RectTransform)panel.transform, new Color(0f, 0f, 0f, 0.6f));
            var card = OnlineUi.Box("Card", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 760f));
            card.pivot = new Vector2(0.5f, 0.5f);
            OnlineUi.Fill(card, OnlineUi.Parchment);

            OnlineUi.Label(Row(card, 30f, 70f), "온라인 협동", 44, config.BoldFont, OnlineUi.Ink);
            OnlineUi.Label(Row(card, 104f, 64f),
                "한 사람은 아래층에서 손님을 맞고, 다른 사람은 위층 공방에서 메모를 보며 목소리로 안내합니다. 자리는 연결된 뒤에 정합니다.",
                24, config.BodyFont, OnlineUi.Ink);

            choices = OnlineUi.Rect("Choices", card, Vector2.zero, Vector2.one).gameObject;
            var c = (RectTransform)choices.transform;
            OnlineUi.Button(Row(c, 190f, 84f), "방 만들기", 32, config.BoldFont, () => HostCloud().Forget());

            codeInput = OnlineUi.Input(Cell(c, 300f, 76f, 0f, 0.48f), "참가 코드", 32, config.BodyFont, 12);
            OnlineUi.Button(Cell(c, 300f, 76f, 0.5f, 1f), "코드로 참가", 28, config.BoldFont,
                () => JoinCloud().Forget());

            OnlineUi.Label(Row(c, 410f, 40f), "같은 네트워크에서 직접 연결 (게임 내 보이스 없음)", 22, config.BodyFont,
                OnlineUi.Faded);
            addressInput = OnlineUi.Input(Cell(c, 456f, 64f, 0f, 0.38f), "방을 연 PC 주소", 26, config.BodyFont, 64);
            OnlineUi.Button(Cell(c, 456f, 64f, 0.40f, 0.69f), "직접 참가", 26, config.BoldFont,
                () => Finish(OnlineSession.Instance.JoinLan(addressInput.text)), OnlineUi.WoodLight);
            OnlineUi.Button(Cell(c, 456f, 64f, 0.71f, 1f), "직접 방 열기", 26, config.BoldFont,
                () => Finish(OnlineSession.Instance.HostLan()), OnlineUi.WoodLight);

            codeLabel = OnlineUi.Label(Row(card, 200f, 150f), string.Empty, 96, config.BoldFont, OnlineUi.Accent);
            status = OnlineUi.Label(Row(card, 540f, 110f), string.Empty, 26, config.BodyFont, OnlineUi.Ink);

            leaveButton = OnlineUi.Button(OnlineUi.Box("Leave", card, new Vector2(0f, 0f), new Vector2(60f, 30f),
                new Vector2(240f, 64f)), "연결 끊기", 26, config.BoldFont, () => OnlineSession.Instance.LeaveAsync().Forget(),
                OnlineUi.WoodLight);
            closeButton = OnlineUi.Button(OnlineUi.Box("Close", card, new Vector2(1f, 0f), new Vector2(-60f, 30f),
                new Vector2(240f, 64f)), "닫기", 26, config.BoldFont, () => panel.SetActive(false), OnlineUi.WoodLight)
                .gameObject;

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

        private void Update()
        {
            // On the counter the entry only belongs to the closed-curtain wait between customers.
            var round = RoundDirector.Instance;
            bool waiting = round == null || round.Phase == RoundPhase.Waiting;
            var role = OnlineSession.CurrentRole;
            bool entry = waiting && role != OnlineRole.Undecided;
            if (openButton.activeSelf != entry)
                openButton.SetActive(entry);

            // Connected but not yet paired or assigned: the customer must not be received, so the card stays up
            // while waiting for the friend, and gives way to the briefing once they are here.
            bool pairing = role == OnlineRole.Undecided && !OnlineSession.Instance.PeerConnected;
            bool briefing = role == OnlineRole.Undecided && !pairing;
            if (pairing && !panel.activeSelf)
                panel.SetActive(true);
            if ((!waiting || briefing) && panel.activeSelf)
                panel.SetActive(false);
            if (closeButton.activeSelf == pairing)
                closeButton.SetActive(!pairing);
        }

        private void OnDestroy()
        {
            if (OnlineSession.Exists)
                OnlineSession.Instance.Changed -= Refresh;
        }

        private static RectTransform Row(RectTransform parent, float top, float height) =>
            OnlineUi.Rect("Row", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(60f, -top - height),
                new Vector2(-60f, -top));

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

        private void Finish(string error)
        {
            Subscribe();
            Refresh();
            if (error != null && status != null)
                status.text = error;
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
            leaveButton.gameObject.SetActive(!offline);
            codeLabel.text = offline ? string.Empty : session.JoinCode ?? "직접 연결";
            codeLabel.fontSize = !offline && session.JoinCode == null ? 56 : 96;

            if (busy)
                status.text = "연결하는 중…";
            else if (offline)
                status.text = string.Empty;
            else if (!session.PeerConnected)
                status.text = !session.IsHost ? "방에 들어가는 중…" : session.UsesCloud
                    ? "이 코드를 친구에게 알려 주세요. 친구를 기다리는 중…"
                    : "친구가 이 PC의 주소로 직접 참가하기를 기다리는 중…";
            else
                status.text = role == OnlineRole.Maker
                    ? "친구가 위층 공방에 있습니다. 닫기를 누르고 손님을 받으세요."
                    : "친구와 연결됐습니다.";
        }
    }
}
