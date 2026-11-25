using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// After each customer the two friends vote whether to trade floors. Both see the same window (swap / keep); if
    /// they agree, that is what happens. If not, each rolls a die and the higher roll's choice wins (a tie is rolled
    /// again). No vote within <see cref="VoteSeconds"/> counts as "keep". The host counts the ballots and rolls, so
    /// both players see the same dice; a swap then goes through <see cref="RoleLobby"/> as before.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwapVote : MonoBehaviour
    {
        private const string MessageName = "potion.vote";
        private const byte Open = 0, Ballot = 1, Outcome = 2, OpenRequest = 3;
        private const float VoteSeconds = 20f;
        private const float RollSeconds = 1.6f;
        private const float OutcomeSeconds = 2.2f;

        private OnlineSession session;
        private NetworkManager registeredOn;

        // host's count
        private bool counting;
        private sbyte hostBallot = -1, guestBallot = -1;

        // this player's window
        private GameObject window;
        private CanvasGroup group;
        private Text question, note, myDie, friendDie, myDieLabel, friendDieLabel;
        private Button swapButton, keepButton;
        private GameObject diceRow;
        private bool open;
        private float openedAt;
        private bool voted;

        /// <summary>True while the window is up: the counter waits before the next customer.</summary>
        public bool Voting => open;

        private void Awake() => session = GetComponent<OnlineSession>();

        /// <summary>Called on the maker when a customer has been sent off.</summary>
        public void Begin()
        {
            if (!session.PeerConnected || open) return;
            if (session.IsHost) HostOpen();
            else Send(OpenRequest, 0, NetworkManager.ServerClientId);
        }

        private void Update()
        {
            var network = session.Network;
            if (network == null || !network.IsListening || network.CustomMessagingManager == null)
            {
                registeredOn = null;
                if (open) Close();
                return;
            }
            if (registeredOn != network)
            {
                registeredOn = network;
                network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            }
            if (!open) return;
            if (!session.PeerConnected) { Close(); return; }

            float left = VoteSeconds - (Time.unscaledTime - openedAt);
            if (!voted)
            {
                note.text = Loc.F("{0}초 안에 고르지 않으면 그대로 해요", Mathf.Max(0, Mathf.CeilToInt(left)));
                if (left <= 0f) Cast(false);
            }
        }

        // ---- host ---------------------------------------------------------------------------------------------

        private void HostOpen()
        {
            counting = true;
            hostBallot = guestBallot = -1;
            Send(Open, 0, null);
            ShowWindow();
        }

        private void HostCount()
        {
            if (!counting || hostBallot < 0 || guestBallot < 0) return;
            counting = false;
            bool swap;
            byte hostRoll = 0, guestRoll = 0;
            if (hostBallot == guestBallot)
                swap = hostBallot == 1;
            else
            {
                do
                {
                    hostRoll = (byte)UnityEngine.Random.Range(1, 7);
                    guestRoll = (byte)UnityEngine.Random.Range(1, 7);
                } while (hostRoll == guestRoll);
                swap = (hostRoll > guestRoll ? hostBallot : guestBallot) == 1;
            }
            using (var writer = new FastBufferWriter(4, Allocator.Temp))
            {
                writer.WriteValueSafe(Outcome);
                writer.WriteValueSafe(swap ? (byte)1 : (byte)0);
                writer.WriteValueSafe(hostRoll);
                writer.WriteValueSafe(guestRoll);
                session.Network.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
            }
            ShowOutcome(swap, hostRoll, guestRoll).Forget();
        }

        // ---- messages -----------------------------------------------------------------------------------------

        private void Send(byte kind, byte value, ulong? to)
        {
            var network = session.Network;
            if (network == null || !network.IsListening || network.CustomMessagingManager == null) return;
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe(kind);
            writer.WriteValueSafe(value);
            if (to.HasValue) network.CustomMessagingManager.SendNamedMessage(MessageName, to.Value, writer);
            else network.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte kind);
            switch (kind)
            {
                case OpenRequest when session.IsHost && !open:
                    HostOpen();
                    break;
                case Open when !session.IsHost:
                    ShowWindow();
                    break;
                case Ballot when session.IsHost && counting:
                    reader.ReadValueSafe(out byte choice);
                    guestBallot = (sbyte)choice;
                    HostCount();
                    break;
                case Outcome when !session.IsHost:
                    reader.ReadValueSafe(out byte swap);
                    reader.ReadValueSafe(out byte hostRoll);
                    reader.ReadValueSafe(out byte guestRoll);
                    // seen from the guest: "my" die is the guest's
                    ShowOutcome(swap == 1, guestRoll, hostRoll).Forget();
                    break;
            }
        }

        private void Cast(bool swap)
        {
            if (voted) return;
            voted = true;
            swapButton.interactable = keepButton.interactable = false;
            note.text = Loc.T("친구의 선택을 기다리는 중…");
            GameSfx.Play("Tick", 0.8f);
            if (session.IsHost)
            {
                hostBallot = swap ? (sbyte)1 : (sbyte)0;
                HostCount();
            }
            else
                Send(Ballot, swap ? (byte)1 : (byte)0, NetworkManager.ServerClientId);
        }

        // ---- window -------------------------------------------------------------------------------------------

        private void ShowWindow()
        {
            if (window == null) Build();
            open = true;
            voted = false;
            openedAt = Time.unscaledTime;
            question.text = Loc.T("다음 손님은 역할을 바꿔서 할까요?");
            note.text = string.Empty;
            diceRow.SetActive(false);
            swapButton.gameObject.SetActive(true);
            keepButton.gameObject.SetActive(true);
            swapButton.interactable = keepButton.interactable = true;
            window.SetActive(true);
            group.alpha = 0f;
            GameSfx.Play("Paper", 0.6f, 1.05f, 0.05f);
            LMotion.Create(0f, 1f, 0.25f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(a => group.alpha = a).AddTo(window);
        }

        private async UniTaskVoid ShowOutcome(bool swap, byte myRoll, byte friendRoll)
        {
            if (window == null || !open) ShowWindow();
            var token = this.GetCancellationTokenOnDestroy();
            swapButton.gameObject.SetActive(false);
            keepButton.gameObject.SetActive(false);
            if (myRoll > 0)
            {
                // the votes split: both dice tumble, then land
                question.text = Loc.T("의견이 갈렸어요! 주사위로 정해요");
                note.text = string.Empty;
                diceRow.SetActive(true);
                myDieLabel.text = Loc.T("나");
                friendDieLabel.text = Loc.T("친구");
                for (float t = 0f; t < RollSeconds; t += 0.08f)
                {
                    myDie.text = UnityEngine.Random.Range(1, 7).ToString();
                    friendDie.text = UnityEngine.Random.Range(1, 7).ToString();
                    GameSfx.Play("Tick", 0.35f, 1.3f, 0.2f);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.08), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);
                }
                myDie.text = myRoll.ToString();
                friendDie.text = friendRoll.ToString();
                OrderHudMotion.Bounce((myRoll > friendRoll ? myDie : friendDie).transform.parent, 1.25f);
                GameSfx.Play("StampThud", 0.7f);
                await UniTask.Delay(TimeSpan.FromSeconds(0.6), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);
            }
            question.text = swap ? Loc.T("역할을 바꿔요!") : Loc.T("이대로 계속해요!");
            note.text = myRoll > 0
                ? (myRoll > friendRoll ? Loc.T("내 주사위가 더 높아요") : Loc.T("친구 주사위가 더 높아요"))
                : Loc.T("둘의 생각이 같아요");
            await UniTask.Delay(TimeSpan.FromSeconds(OutcomeSeconds), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);
            Close();
            if (swap && session.IsHost) session.Lobby.HostSwap();
        }

        private void Close()
        {
            open = false;
            counting = false;
            if (window != null) window.SetActive(false);
        }

        private void Build()
        {
            var config = OnlineCoopConfig.Load();
            var font = config.BoldFont;
            // above the scene dimmer, so it stays readable even on a floor change
            var canvas = OnlineUi.Canvas("SwapVoteCanvas", SceneDimmer.SortingOrder + 1, transform);
            var root = (RectTransform)canvas.transform;
            window = root.gameObject;
            OnlineUi.Fill(OnlineUi.Rect("Dim", root, Vector2.zero, Vector2.one), new Color(0.12f, 0.06f, 0.03f, 0.55f));
            var card = OnlineUi.Box("Window", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 440f));
            card.pivot = new Vector2(0.5f, 0.5f);
            group = card.gameObject.AddComponent<CanvasGroup>();
            OnlineUiParts.Draw(card, "UI_Window", true);
            question = OnlineUi.Label(OnlineUi.Rect("Question", card, new Vector2(0f, 1f), Vector2.one, new Vector2(48f, -150f),
                new Vector2(-48f, -50f)), string.Empty, 38, font, OnlineUiParts.Ink);

            swapButton = OnlineUiParts.Button(OnlineUi.Box("Swap", card, new Vector2(0.5f, 0.5f), new Vector2(-170f, -30f),
                new Vector2(280f, 88f)), "UI_SmallButton", Loc.T("바꾸기"), 32, font, () => Cast(true));
            keepButton = OnlineUiParts.Button(OnlineUi.Box("Keep", card, new Vector2(0.5f, 0.5f), new Vector2(170f, -30f),
                new Vector2(280f, 88f)), "UI_SmallButton", Loc.T("그대로"), 32, font, () => Cast(false));

            diceRow = OnlineUi.Rect("Dice", card, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -110f),
                new Vector2(0f, 40f)).gameObject;
            (myDie, myDieLabel) = Die((RectTransform)diceRow.transform, -150f, font);
            (friendDie, friendDieLabel) = Die((RectTransform)diceRow.transform, 150f, font);

            note = OnlineUi.Label(OnlineUi.Rect("Note", card, Vector2.zero, new Vector2(1f, 0f), new Vector2(48f, 34f),
                new Vector2(-48f, 90f)), string.Empty, 24, font, OnlineUiParts.InkSoft);
            window.SetActive(false);
        }

        private static (Text face, Text label) Die(RectTransform row, float x, Font font)
        {
            var box = OnlineUi.Box("Die", row, new Vector2(0.5f, 0.5f), new Vector2(x, 10f), new Vector2(110f, 110f));
            box.pivot = new Vector2(0.5f, 0.5f);
            OnlineUi.Fill(box, OnlineUiParts.Cream, false);
            var face = OnlineUi.Label(OnlineUi.Rect("Face", box, Vector2.zero, Vector2.one), "1", 64, font, OnlineUiParts.Ink);
            var label = OnlineUi.Label(OnlineUi.Box("Label", row, new Vector2(0.5f, 0.5f), new Vector2(x, -70f),
                new Vector2(200f, 36f)), string.Empty, 24, font, OnlineUiParts.Ink);
            ((RectTransform)label.transform).pivot = new Vector2(0.5f, 0.5f);
            return (face, label);
        }
    }
}
