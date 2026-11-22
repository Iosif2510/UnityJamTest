using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// Adds the online co-op entry to the title screen at runtime. The existing start button keeps starting the
    /// maker's game, so offline play is unchanged.
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (scene, _) => Attach(scene);
            Attach(SceneManager.GetActiveScene());
        }

        private static void Attach(Scene scene)
        {
            if (scene.name != OnlineCoopConfig.TitleScene || FindAnyObjectByType<TitleOnlinePanel>() != null)
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

            OnlineUi.Button(OnlineUi.Box("OpenOnline", root, new Vector2(1f, 0f), new Vector2(-40f, 40f),
                new Vector2(300f, 76f)), "온라인 협동", 32, config.BoldFont, () => panel.SetActive(true));

            panel = OnlineUi.Rect("Panel", root, Vector2.zero, Vector2.one).gameObject;
            OnlineUi.Fill((RectTransform)panel.transform, new Color(0f, 0f, 0f, 0.6f));
            var card = OnlineUi.Box("Card", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 760f));
            card.pivot = new Vector2(0.5f, 0.5f);
            OnlineUi.Fill(card, OnlineUi.Parchment);

            OnlineUi.Label(Row(card, 30f, 70f), "온라인 협동", 44, config.BoldFont, OnlineUi.Ink);
            OnlineUi.Label(Row(card, 104f, 64f),
                "제조자는 아래층에서 손님을 맞고, 해설자는 위층 공방에서 메모를 보며 목소리로 안내합니다.",
                24, config.BodyFont, OnlineUi.Ink);

            choices = OnlineUi.Rect("Choices", card, Vector2.zero, Vector2.one).gameObject;
            var c = (RectTransform)choices.transform;
            OnlineUi.Button(Row(c, 190f, 84f), "방 만들기 (제조자)", 32, config.BoldFont, () => HostCloud().Forget());

            codeInput = OnlineUi.Input(Cell(c, 300f, 76f, 0f, 0.48f), "참가 코드", 32, config.BodyFont, 12);
            OnlineUi.Button(Cell(c, 300f, 76f, 0.5f, 1f), "코드로 참가 (해설자)", 28, config.BoldFont,
                () => JoinCloud().Forget());

            OnlineUi.Label(Row(c, 410f, 40f), "같은 네트워크에서 직접 연결 (게임 내 보이스 없음)", 22, config.BodyFont,
                OnlineUi.Faded);
            addressInput = OnlineUi.Input(Cell(c, 456f, 64f, 0f, 0.38f), "제조자 PC 주소", 26, config.BodyFont, 64);
            OnlineUi.Button(Cell(c, 456f, 64f, 0.40f, 0.69f), "직접 참가", 26, config.BoldFont,
                () => Finish(OnlineSession.Instance.JoinLan(addressInput.text)), OnlineUi.WoodLight);
            OnlineUi.Button(Cell(c, 456f, 64f, 0.71f, 1f), "직접 방 열기", 26, config.BoldFont,
                () => Finish(OnlineSession.Instance.HostLan()), OnlineUi.WoodLight);

            codeLabel = OnlineUi.Label(Row(card, 200f, 150f), string.Empty, 96, config.BoldFont, OnlineUi.Accent);
            status = OnlineUi.Label(Row(card, 540f, 110f), string.Empty, 26, config.BodyFont, OnlineUi.Ink);

            leaveButton = OnlineUi.Button(OnlineUi.Box("Leave", card, new Vector2(0f, 0f), new Vector2(60f, 30f),
                new Vector2(240f, 64f)), "연결 끊기", 26, config.BoldFont, () => OnlineSession.Instance.LeaveAsync().Forget(),
                OnlineUi.WoodLight);
            OnlineUi.Button(OnlineUi.Box("Close", card, new Vector2(1f, 0f), new Vector2(-60f, 30f),
                new Vector2(240f, 64f)), "닫기", 26, config.BoldFont, () => panel.SetActive(false), OnlineUi.WoodLight);

            panel.SetActive(false);
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
            if (role == OnlineRole.Narrator)
            {
                // The narrator never plays the counter scenes; go upstairs as soon as the join succeeds.
                SceneManager.LoadScene(OnlineCoopConfig.LoftScene);
                return;
            }

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
                status.text = session.UsesCloud
                    ? "이 코드를 해설자에게 알려 주세요. 해설자를 기다리는 중…"
                    : "해설자가 이 PC의 주소로 직접 참가하기를 기다리는 중…";
            else
                status.text = "해설자가 위층 공방에 들어왔습니다. 닫기를 누르고 게임을 시작하세요.";
        }
    }
}
