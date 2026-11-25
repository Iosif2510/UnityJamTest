using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>The moments both friends have to agree on before the game goes on.</summary>
    public enum PairTopic : byte
    {
        /// <summary>Who goes upstairs, after connecting (choice: 1 upstairs, 2 downstairs).</summary>
        Role = 0,
        /// <summary>The hand-over: the potion is made and drunk on both screens at once.</summary>
        Ending = 1,
        /// <summary>"결과 보기": the result card opens on both screens at once.</summary>
        Result = 2,
        /// <summary>After a customer: swap floors or keep them (choice: 1 swap, 2 keep).</summary>
        Swap = 3,
    }

    /// <summary>
    /// Two-player agreement for a <see cref="PairTopic"/>: each friend may pick a choice and say they are ready; when
    /// both are ready (and the topic's own rule allows it) the host tells both to go at the same moment, with a few
    /// bytes of its own (the dice of a split vote). The host is P1 and the friend who joined is P2. The host keeps
    /// the count and repeats it to the other player, as <see cref="RoleLobby"/> does.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PairReady : MonoBehaviour
    {
        private const string MessageName = "potion.pair";
        private const byte Update_ = 0, State = 1, Go_ = 2, Reset_ = 3;
        private const int Topics = 4;

        private OnlineSession session;
        private NetworkManager registeredOn;
        private readonly sbyte[,] choices = new sbyte[Topics, 2];
        private readonly bool[,] ready = new bool[Topics, 2];

        /// <summary>Raised on both players when a topic's choices or ready marks change.</summary>
        public event Action<PairTopic> Changed;

        /// <summary>Raised on both players together when a topic goes ahead (with the host's extra bytes).</summary>
        public event Action<PairTopic, byte[]> Went;

        /// <summary>Host only: extra bytes sent along with a topic's go (e.g. dice), or null.</summary>
        public Func<PairTopic, byte[]> Extra;

        /// <summary>Host only: whether a topic may go once both are ready (default: yes).</summary>
        public Func<PairTopic, bool> Allows;

        /// <summary>0 for the host (P1), 1 for the friend who joined (P2).</summary>
        public int Me => session.IsHost ? 0 : 1;
        public int Friend => 1 - Me;

        private void Awake()
        {
            session = GetComponent<OnlineSession>();
            for (int t = 0; t < Topics; t++) Clear(t);
        }

        public sbyte Choice(PairTopic topic, int player) => choices[(int)topic, player];
        public bool IsReady(PairTopic topic, int player) => ready[(int)topic, player];

        /// <summary>This player picks <paramref name="choice"/> (a new pick takes back their ready mark).</summary>
        public void Choose(PairTopic topic, sbyte choice) => Set(topic, choice, false);

        /// <summary>This player is ready to go on.</summary>
        public void SetReady(PairTopic topic, bool isReady = true) => Set(topic, choices[(int)topic, Me], isReady);

        /// <summary>Starts a topic over on both players (no choices, nobody ready).</summary>
        public void Reset(PairTopic topic)
        {
            Clear((int)topic);
            Changed?.Invoke(topic);
            Send(Reset_, topic, 0, 0, 0, 0, null, null);
        }

        private void Set(PairTopic topic, sbyte choice, bool isReady)
        {
            int t = (int)topic;
            choices[t, Me] = choice;
            ready[t, Me] = isReady;
            Changed?.Invoke(topic);
            if (session.IsHost)
            {
                Broadcast(topic);
                TryGo(topic);
            }
            else
                Send(Update_, topic, (byte)choice, isReady ? (byte)1 : (byte)0, 0, 0, null, NetworkManager.ServerClientId);
        }

        private void Clear(int t)
        {
            choices[t, 0] = choices[t, 1] = -1;
            ready[t, 0] = ready[t, 1] = false;
        }

        // ---- host ----------------------------------------------------------------------------------------------

        private void TryGo(PairTopic topic)
        {
            int t = (int)topic;
            if (!ready[t, 0] || !ready[t, 1]) return;
            if (Allows != null && !Allows(topic)) return;
            byte[] extra = Extra?.Invoke(topic) ?? Array.Empty<byte>();
            Send(Go_, topic, 0, 0, 0, 0, extra, null);
            Went?.Invoke(topic, extra);
            // the moment is used up; the picks stay visible until the topic is reset
            ready[t, 0] = ready[t, 1] = false;
        }

        private void Broadcast(PairTopic topic)
        {
            int t = (int)topic;
            Send(State, topic, (byte)choices[t, 0], (byte)choices[t, 1], ready[t, 0] ? (byte)1 : (byte)0,
                ready[t, 1] ? (byte)1 : (byte)0, null, null);
        }

        // ---- messages ------------------------------------------------------------------------------------------

        private void Update()
        {
            var network = session.Network;
            if (network == null || !network.IsListening || network.CustomMessagingManager == null)
            {
                if (registeredOn != null)
                    for (int t = 0; t < Topics; t++) Clear(t);
                registeredOn = null;
                return;
            }
            if (registeredOn == network) return;
            registeredOn = network;
            for (int t = 0; t < Topics; t++) Clear(t);
            network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
        }

        private void Send(byte kind, PairTopic topic, byte a, byte b, byte c, byte d, byte[] extra, ulong? to)
        {
            var network = session.Network;
            if (network == null || !network.IsListening || network.CustomMessagingManager == null || !session.PeerConnected)
                return;
            using var writer = new FastBufferWriter(16 + (extra?.Length ?? 0), Allocator.Temp);
            writer.WriteValueSafe(kind);
            writer.WriteValueSafe((byte)topic);
            writer.WriteValueSafe(a);
            writer.WriteValueSafe(b);
            writer.WriteValueSafe(c);
            writer.WriteValueSafe(d);
            writer.WriteValueSafe((byte)(extra?.Length ?? 0));
            if (extra != null) foreach (byte x in extra) writer.WriteValueSafe(x);
            if (to.HasValue) network.CustomMessagingManager.SendNamedMessage(MessageName, to.Value, writer);
            else if (network.IsServer)
            {
                // to the other player only: a message the host sent to everyone would also come back to the host
                foreach (ulong client in network.ConnectedClientsIds)
                    if (client != NetworkManager.ServerClientId)
                        network.CustomMessagingManager.SendNamedMessage(MessageName, client, writer);
            }
            else network.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer);
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            // the host's own messages are never for the host
            if (session.IsHost && sender == NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out byte topicByte);
            reader.ReadValueSafe(out byte a);
            reader.ReadValueSafe(out byte b);
            reader.ReadValueSafe(out byte c);
            reader.ReadValueSafe(out byte d);
            reader.ReadValueSafe(out byte length);
            var extra = new byte[length];
            for (int i = 0; i < length; i++) reader.ReadValueSafe(out extra[i]);
            if (topicByte >= Topics) return;
            var topic = (PairTopic)topicByte;
            int t = topicByte;

            switch (kind)
            {
                case Update_ when session.IsHost:
                    choices[t, 1] = (sbyte)a;
                    ready[t, 1] = b == 1;
                    Changed?.Invoke(topic);
                    Broadcast(topic);
                    TryGo(topic);
                    break;
                case State when !session.IsHost:
                    choices[t, 0] = (sbyte)a;
                    choices[t, 1] = (sbyte)b;
                    ready[t, 0] = c == 1;
                    ready[t, 1] = d == 1;
                    Changed?.Invoke(topic);
                    break;
                case Go_ when !session.IsHost:
                    ready[t, 0] = ready[t, 1] = false;
                    Went?.Invoke(topic, extra);
                    break;
                case Reset_:
                    Clear(t);
                    Changed?.Invoke(topic);
                    if (session.IsHost) Send(Reset_, topic, 0, 0, 0, 0, null, null); // repeat to the other side
                    break;
            }
        }
    }
}
