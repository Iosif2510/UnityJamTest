using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>What the narrator may know about the maker's round. Never includes the customer's looks or the answers.</summary>
    public struct RoundSnapshot : IEquatable<RoundSnapshot>
    {
        public RoundPhase Phase;
        public float RemainingSeconds;
        public bool Brewed;
        public bool HasResult;
        public bool TimedOut;
        public byte CorrectIngredients;
        public bool BottleCorrect;
        public bool InkCorrect;
        public short IngredientPoints;
        public short BottlePoints;
        public short InkPoints;
        public short TimeBonus;
        public byte Stamp;

        public int Total => IngredientPoints + BottlePoints + InkPoints + TimeBonus;
        public bool TimerRunning => Phase is RoundPhase.Consult or RoundPhase.Storage or RoundPhase.Prescribe;

        /// <summary>Equality ignores the clock so that a ticking timer alone does not count as a change.</summary>
        public bool Equals(RoundSnapshot other) =>
            Phase == other.Phase && Brewed == other.Brewed && HasResult == other.HasResult &&
            TimedOut == other.TimedOut && CorrectIngredients == other.CorrectIngredients &&
            BottleCorrect == other.BottleCorrect && InkCorrect == other.InkCorrect &&
            IngredientPoints == other.IngredientPoints && BottlePoints == other.BottlePoints &&
            InkPoints == other.InkPoints && TimeBonus == other.TimeBonus && Stamp == other.Stamp;

        internal static RoundSnapshot Capture(RoundDirector round)
        {
            var snapshot = new RoundSnapshot { Phase = RoundPhase.Waiting };
            if (round == null)
                return snapshot;

            snapshot.Phase = round.Phase;
            snapshot.RemainingSeconds = (float)round.RemainingSeconds;
            snapshot.Brewed = round.Phase == RoundPhase.Storage && round.SubmittedIngredients != null;
            var result = round.Phase is RoundPhase.Ending or RoundPhase.Result ? round.Result : null;
            if (result == null)
                return snapshot;

            snapshot.HasResult = true;
            snapshot.TimedOut = result.TimedOut;
            snapshot.CorrectIngredients = (byte)result.CorrectIngredients;
            snapshot.BottleCorrect = result.BottleCorrect;
            snapshot.InkCorrect = result.InkCorrect;
            snapshot.IngredientPoints = (short)result.IngredientPoints;
            snapshot.BottlePoints = (short)result.BottlePoints;
            snapshot.InkPoints = (short)result.InkPoints;
            snapshot.TimeBonus = (short)result.TimeBonus;
            snapshot.Stamp = result.Stamp == RoundResult.FineBrewStamp ? (byte)2 :
                result.Stamp == RoundResult.PassableStamp ? (byte)1 : (byte)0;
            return snapshot;
        }

        internal void Write(ref FastBufferWriter writer)
        {
            byte flags = (byte)((Brewed ? 1 : 0) | (HasResult ? 2 : 0) | (TimedOut ? 4 : 0) |
                                (BottleCorrect ? 8 : 0) | (InkCorrect ? 16 : 0));
            writer.WriteValueSafe((byte)Phase);
            writer.WriteValueSafe(RemainingSeconds);
            writer.WriteValueSafe(flags);
            writer.WriteValueSafe(CorrectIngredients);
            writer.WriteValueSafe(IngredientPoints);
            writer.WriteValueSafe(BottlePoints);
            writer.WriteValueSafe(InkPoints);
            writer.WriteValueSafe(TimeBonus);
            writer.WriteValueSafe(Stamp);
        }

        internal static RoundSnapshot Read(ref FastBufferReader reader)
        {
            var snapshot = new RoundSnapshot();
            reader.ReadValueSafe(out byte phase);
            reader.ReadValueSafe(out snapshot.RemainingSeconds);
            reader.ReadValueSafe(out byte flags);
            reader.ReadValueSafe(out snapshot.CorrectIngredients);
            reader.ReadValueSafe(out snapshot.IngredientPoints);
            reader.ReadValueSafe(out snapshot.BottlePoints);
            reader.ReadValueSafe(out snapshot.InkPoints);
            reader.ReadValueSafe(out snapshot.TimeBonus);
            reader.ReadValueSafe(out snapshot.Stamp);
            snapshot.Phase = (RoundPhase)phase;
            snapshot.Brewed = (flags & 1) != 0;
            snapshot.HasResult = (flags & 2) != 0;
            snapshot.TimedOut = (flags & 4) != 0;
            snapshot.BottleCorrect = (flags & 8) != 0;
            snapshot.InkCorrect = (flags & 16) != 0;
            return snapshot;
        }
    }

    /// <summary>
    /// Mirrors the maker's round progress to the narrator. It only reads <see cref="RoundDirector"/>, so the offline
    /// game flow does not depend on it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundSync : MonoBehaviour
    {
        private const string MessageName = "potion.round";
        private const float ResendInterval = 0.5f;

        private OnlineSession session;
        private NetworkManager registeredOn;
        private RoundSnapshot lastSent;
        private float lastSentAt = float.NegativeInfinity;
        private float receivedAt;

        public RoundSnapshot Latest { get; private set; }
        public bool HasSnapshot { get; private set; }

        /// <summary>Remaining time extrapolated from the last snapshot.</summary>
        public float RemainingNow => !HasSnapshot || !Latest.TimerRunning
            ? Latest.RemainingSeconds
            : Mathf.Max(0f, Latest.RemainingSeconds - (Time.unscaledTime - receivedAt));

        public event Action<RoundSnapshot> Received;

        private void Awake() => session = GetComponent<OnlineSession>();

        private void Update()
        {
            var network = session.Network;
            if (network == null || !network.IsListening || network.CustomMessagingManager == null)
            {
                registeredOn = null;
                return;
            }

            if (registeredOn != network)
                Register(network);

            if (session.Role != OnlineRole.Maker || !network.IsServer || network.ConnectedClientsIds.Count < 2)
            {
                lastSentAt = float.NegativeInfinity;
                return;
            }

            var snapshot = RoundSnapshot.Capture(RoundDirector.Instance);
            if (snapshot.Equals(lastSent) && Time.unscaledTime - lastSentAt < ResendInterval)
                return;

            using var writer = new FastBufferWriter(32, Allocator.Temp);
            var buffer = writer;
            snapshot.Write(ref buffer);
            network.CustomMessagingManager.SendNamedMessageToAll(MessageName, buffer);
            lastSent = snapshot;
            lastSentAt = Time.unscaledTime;
        }

        private void Register(NetworkManager network)
        {
            registeredOn = network;
            HasSnapshot = false;
            network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (session.Role != OnlineRole.Narrator)
                return;
            var previous = Latest;
            bool first = !HasSnapshot;
            Latest = RoundSnapshot.Read(ref reader);
            if (first || !previous.Equals(Latest))
                Debug.Log($"[Online] Round {Latest.Phase}, brewed {Latest.Brewed}, result {Latest.HasResult}.");
            receivedAt = Time.unscaledTime;
            HasSnapshot = true;
            Received?.Invoke(Latest);
        }
    }
}
