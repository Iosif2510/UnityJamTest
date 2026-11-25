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
    /// After each customer the two friends vote whether to trade floors. Both see the same window: pressing 바꾸기 or
    /// 그대로 puts that player's P1/P2 mark on it, and once both have picked the button below reads "위치로!" (same pick)
    /// or "주사위 굴리기!" (split). When both pressed it, a split is settled by one die each, the higher roll's pick
    /// winning (the host rolls, a tie is rolled again). No pick within <see cref="VoteSeconds"/> counts as "keep".
    /// The picks go through <see cref="PairReady"/>; a swap then goes through <see cref="RoleLobby"/> as before.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwapVote : MonoBehaviour
    {
        private const string MessageName = "potion.vote";
        private const byte Open = 0, OpenRequest = 3;
        private const sbyte SwapPick = 1, KeepPick = 2;
        private const float VoteSeconds = 30f;
        private const float RollSeconds = 1.6f;
        private const float OutcomeSeconds = 2.2f;

        private OnlineSession session;
        private NetworkManager registeredOn;

        // this player's window
        private GameObject window;
        private CanvasGroup group;
        private Text question, note, myDie, friendDie, myDieLabel, friendDieLabel;
        private Text swapTag, keepTag, goLabel;
        private Button swapButton, keepButton, goButton;
        private Image goImage;
        private bool settling;
        private GameObject diceRow;
        private bool open;
        private float openedAt;
        /// <summary>True while the window is up: the counter waits before the next customer.</summary>
        public bool Voting => open;

        private void Awake() => session = GetComponent<OnlineSession>();

        private void Start()
        {
            var pairs = session.Pairs;
            pairs.Went += OnWent;
            // the host rolls the dice for a split vote, so both screens show the same ones
            pairs.Extra = topic => topic == PairTopic.Swap ? Roll() : null;
        }

        private void OnDestroy()
        {
            if (session != null && session.Pairs != null) session.Pairs.Went -= OnWent;
        }

        private byte[] Roll()
        {
            var pairs = session.Pairs;
            if (pairs.Choice(PairTopic.Swap, 0) == pairs.Choice(PairTopic.Swap, 1)) return new byte[] { 0, 0 };
            byte hostRoll, guestRoll;
            do
            {
                hostRoll = (byte)UnityEngine.Random.Range(1, 7);
                guestRoll = (byte)UnityEngine.Random.Range(1, 7);
            } while (hostRoll == guestRoll);
            return new[] { hostRoll, guestRoll };
        }

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

            if (settling) return;
            var pairs = session.Pairs;
            bool picked = pairs.Choice(PairTopic.Swap, pairs.Me) > 0;
            float left = VoteSeconds - (Time.unscaledTime - openedAt);
            if (left <= 0f && !pairs.IsReady(PairTopic.Swap, pairs.Me))
            {
                // too slow: "그대로", and ready
                if (!picked) pairs.Choose(PairTopic.Swap, KeepPick);
                if (BothPicked(pairs)) pairs.SetReady(PairTopic.Swap);
            }
            note.text = picked ? string.Empty : Loc.F("{0}초 안에 고르지 않으면 그대로 해요", Mathf.Max(0, Mathf.CeilToInt(left)));
            RefreshPicks(pairs);
        }

        // ---- host ---------------------------------------------------------------------------------------------

        private void HostOpen()
        {
            session.Pairs.Reset(PairTopic.Swap);
            Send(Open, 0, null);
            ShowWindow();
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
            }
        }

        private void Pick(sbyte pick)
        {
            var pairs = session.Pairs;
            if (settling || pairs.Choice(PairTopic.Swap, pairs.Me) == pick) return;
            pairs.Choose(PairTopic.Swap, pick);
            GameSfx.Play("Tick", 0.8f);
        }

        private void PressGo()
        {
            var pairs = session.Pairs;
            if (settling || !BothPicked(pairs)) return;
            pairs.SetReady(PairTopic.Swap);
            GameSfx.Play("StampThud", 0.6f);
        }

        private static bool BothPicked(PairReady pairs) =>
            pairs.Choice(PairTopic.Swap, 0) > 0 && pairs.Choice(PairTopic.Swap, 1) > 0;

        /// <summary>The P1/P2 marks on 바꾸기 / 그대로 and what the button below says.</summary>
        private void RefreshPicks(PairReady pairs)
        {
            swapTag.text = Tags(pairs, SwapPick);
            keepTag.text = Tags(pairs, KeepPick);
            bool both = BothPicked(pairs);
            bool meReady = pairs.IsReady(PairTopic.Swap, pairs.Me);
            bool live = both && !meReady;
            goButton.interactable = live;
            goImage.color = live ? new Color(1f, 0.82f, 0.42f) : new Color(0.62f, 0.6f, 0.58f);
            goLabel.fontSize = meReady ? 24 : 30;
            goLabel.text = meReady ? Loc.T("친구 기다리는 중…")
                : !both ? Loc.T("둘 다 고르면 시작")
                : pairs.Choice(PairTopic.Swap, 0) == pairs.Choice(PairTopic.Swap, 1) ? Loc.T("위치로!")
                : Loc.T("주사위 굴리기!");
        }

        private static string Tags(PairReady pairs, sbyte pick)
        {
            bool p1 = pairs.Choice(PairTopic.Swap, 0) == pick, p2 = pairs.Choice(PairTopic.Swap, 1) == pick;
            return p1 && p2 ? "P1 P2" : p1 ? "P1" : p2 ? "P2" : string.Empty;
        }

        private void OnWent(PairTopic topic, byte[] extra)
        {
            if (topic != PairTopic.Swap || !open || settling) return;
            var pairs = session.Pairs;
            sbyte hostPick = pairs.Choice(PairTopic.Swap, 0), guestPick = pairs.Choice(PairTopic.Swap, 1);
            byte hostRoll = extra != null && extra.Length >= 2 ? extra[0] : (byte)0;
            byte guestRoll = extra != null && extra.Length >= 2 ? extra[1] : (byte)0;
            bool swap = hostPick == guestPick || hostRoll == 0
                ? hostPick == SwapPick
                : (hostRoll > guestRoll ? hostPick : guestPick) == SwapPick;
            // seen from each player: "my" die first
            if (session.IsHost) ShowOutcome(swap, hostRoll, guestRoll).Forget();
            else ShowOutcome(swap, guestRoll, hostRoll).Forget();
        }

        // ---- window -------------------------------------------------------------------------------------------

        private void ShowWindow()
        {
            if (window == null) Build();
            open = true;
            settling = false;
            openedAt = Time.unscaledTime;
            question.text = Loc.T("다음 손님은 역할을 바꿔서 할까요?");
            note.text = string.Empty;
            diceRow.SetActive(false);
            swapButton.gameObject.SetActive(true);
            keepButton.gameObject.SetActive(true);
            goButton.gameObject.SetActive(true);
            swapButton.interactable = keepButton.interactable = true;
            RefreshPicks(session.Pairs);
            window.SetActive(true);
            group.alpha = 0f;
            GameSfx.Play("Paper", 0.6f, 1.05f, 0.05f);
            LMotion.Create(0f, 1f, 0.25f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(a => group.alpha = a).AddTo(window);
        }

        private async UniTaskVoid ShowOutcome(bool swap, byte myRoll, byte friendRoll)
        {
            if (window == null || !open) ShowWindow();
            settling = true;
            var token = this.GetCancellationTokenOnDestroy();
            swapButton.gameObject.SetActive(false);
            keepButton.gameObject.SetActive(false);
            goButton.gameObject.SetActive(false);
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
            settling = false;
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
            var card = OnlineUi.Box("Window", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 520f));
            card.pivot = new Vector2(0.5f, 0.5f);
            group = card.gameObject.AddComponent<CanvasGroup>();
            OnlineUiParts.Draw(card, "UI_Window", true);
            question = OnlineUi.Label(OnlineUi.Rect("Question", card, new Vector2(0f, 1f), Vector2.one, new Vector2(48f, -150f),
                new Vector2(-48f, -50f)), string.Empty, 38, font, OnlineUiParts.Ink);

            swapButton = OnlineUiParts.Button(OnlineUi.Box("Swap", card, new Vector2(0.5f, 0.5f), new Vector2(-180f, 0f),
                new Vector2(320f, 96f)), "UI_SmallButton", Loc.T("바꾸기"), 32, font, () => Pick(SwapPick));
            keepButton = OnlineUiParts.Button(OnlineUi.Box("Keep", card, new Vector2(0.5f, 0.5f), new Vector2(180f, 0f),
                new Vector2(320f, 96f)), "UI_SmallButton", Loc.T("그대로"), 32, font, () => Pick(KeepPick));
            swapTag = Tag((RectTransform)swapButton.transform, font);
            keepTag = Tag((RectTransform)keepButton.transform, font);

            // below: "위치로!" / "주사위 굴리기!" once both have picked
            var goRect = OnlineUi.Box("Go", card, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(360f, 84f));
            goRect.pivot = new Vector2(0.5f, 0.5f);
            goButton = OnlineUiParts.Button(goRect, "UI_SmallButton", Loc.T("주사위 굴리기!"), 30, font, PressGo);
            goImage = (Image)goButton.targetGraphic;
            goLabel = goRect.GetComponentInChildren<Text>();

            diceRow = OnlineUi.Rect("Dice", card, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -110f),
                new Vector2(0f, 40f)).gameObject;
            (myDie, myDieLabel) = Die((RectTransform)diceRow.transform, -150f, font);
            (friendDie, friendDieLabel) = Die((RectTransform)diceRow.transform, 150f, font);

            note = OnlineUi.Label(OnlineUi.Rect("Note", card, Vector2.zero, new Vector2(1f, 0f), new Vector2(48f, 24f),
                new Vector2(-48f, 72f)), string.Empty, 24, font, OnlineUiParts.InkSoft);
            window.SetActive(false);
        }

        /// <summary>The empty slot on the right of a choice where P1 / P2 shows.</summary>
        private static Text Tag(RectTransform button, Font font)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                var lr = label.rectTransform;
                lr.offsetMax = new Vector2(lr.offsetMax.x - 90f, lr.offsetMax.y);
            }
            var tag = OnlineUi.Label(OnlineUi.Box("Who", button, new Vector2(1f, 0.5f), new Vector2(-16f, 0f),
                new Vector2(100f, 60f)), string.Empty, 28, font, OnlineUiParts.StampRed, TextAnchor.MiddleRight);
            ((RectTransform)tag.transform).pivot = new Vector2(1f, 0.5f);
            return tag;
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
