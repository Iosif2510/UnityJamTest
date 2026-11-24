using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PotionShop.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionShop.Online
{
    /// <summary>
    /// After the hand-over both friends watch the end together: when the maker hands the potion over, the narrator
    /// is sent what is needed to rebuild the same moment (the customer, the order's seed, the ingredients, bottle, ink,
    /// the seconds left and the brew's colour), walks down to the counter and sees the bottle and ink being picked,
    /// the potion made, drunk and judged, in their own language. Dismissing the result takes them back upstairs
    /// (unless the roles have just swapped and the counter is theirs now).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SharedEnding : MonoBehaviour
    {
        private const string MessageName = "potion.ending";

        /// <summary>One hand-over, as the narrator needs it.</summary>
        public sealed class Handover
        {
            public int Seed;
            public string PreviousDisease;
            public string CustomerId;
            public BottleShape Bottle;
            public int Ink;
            public double RemainingSeconds;
            public string[] Ingredients;
            public Color Brew;
        }

        private static Handover pending;

        /// <summary>True from a watched hand-over until the next customer: the loft's own result card stays away.</summary>
        public static bool Watched { get; private set; }

        private OnlineSession session;
        private NetworkManager registeredOn;
        private bool travelling;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pending = null;
            Watched = false;
        }

        /// <summary>The hand-over waiting to be shown at the counter, taken once.</summary>
        internal static bool TakePending(out Handover handover)
        {
            handover = pending;
            pending = null;
            return handover != null;
        }

        private void Awake()
        {
            session = GetComponent<OnlineSession>();
            RoundDirector.Served += OnServed;
        }

        private void OnDestroy() => RoundDirector.Served -= OnServed;

        private void Update()
        {
            var network = session.Network;
            if (network == null || !network.IsListening || network.CustomMessagingManager == null)
                registeredOn = null;
            else if (registeredOn != network)
            {
                registeredOn = network;
                network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            }

            // a new customer downstairs: the next result is a new one
            if (Watched && session.Role == OnlineRole.Narrator && session.Sync.HasSnapshot &&
                session.Sync.Latest.Phase is RoundPhase.Waiting or RoundPhase.Preparing or RoundPhase.Consult)
                Watched = false;
        }

        // ---- maker ----------------------------------------------------------------------------------------------

        private void OnServed(GeneratedOrder order, IReadOnlyList<string> ingredients, BottleShape bottle, int ink,
            double remainingSeconds)
        {
            var network = session.Network;
            if (session.Role != OnlineRole.Maker || !session.PeerConnected || network == null || !network.IsListening ||
                network.CustomMessagingManager == null || order == null || ingredients == null)
                return;
            var brew = CauldronColor.LastBrew ?? Color.clear;
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            writer.WriteValueSafe(order.Seed);
            writer.WriteValueSafe(order.PreviousDiseaseId ?? string.Empty);
            writer.WriteValueSafe(order.Customer.Id);
            writer.WriteValueSafe((byte)bottle);
            writer.WriteValueSafe((byte)ink);
            writer.WriteValueSafe(remainingSeconds);
            writer.WriteValueSafe((byte)ingredients.Count);
            foreach (string id in ingredients) writer.WriteValueSafe(id ?? string.Empty);
            writer.WriteValueSafe(brew.r);
            writer.WriteValueSafe(brew.g);
            writer.WriteValueSafe(brew.b);
            writer.WriteValueSafe(brew.a);
            if (network.IsServer)
                network.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
            else
                network.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer);
        }

        // ---- narrator -------------------------------------------------------------------------------------------

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (session.Role != OnlineRole.Narrator) return;
            var handover = new Handover();
            reader.ReadValueSafe(out handover.Seed);
            reader.ReadValueSafe(out handover.PreviousDisease);
            reader.ReadValueSafe(out handover.CustomerId);
            reader.ReadValueSafe(out byte bottle);
            reader.ReadValueSafe(out byte ink);
            reader.ReadValueSafe(out handover.RemainingSeconds);
            reader.ReadValueSafe(out byte count);
            handover.Ingredients = new string[count];
            for (int i = 0; i < count; i++) reader.ReadValueSafe(out handover.Ingredients[i]);
            reader.ReadValueSafe(out float r);
            reader.ReadValueSafe(out float g);
            reader.ReadValueSafe(out float b);
            reader.ReadValueSafe(out float a);
            handover.Bottle = (BottleShape)bottle;
            handover.Ink = ink;
            handover.Brew = new Color(r, g, b, a);
            pending = handover;
            Watched = true;
            Debug.Log("[Online] The maker handed the potion over; going down to watch.");
            GoDown().Forget();
        }

        /// <summary>Walks down to the counter, where <see cref="OrderSceneView"/> picks the hand-over up.</summary>
        private async UniTaskVoid GoDown()
        {
            if (travelling) return;
            travelling = true;
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                while (SceneManager.GetActiveScene().name != OnlineCoopConfig.CounterScene &&
                       !SceneDimmer.Instance.TryLoad(OnlineCoopConfig.CounterScene))
                    await UniTask.Delay(TimeSpan.FromSeconds(0.2), DelayType.UnscaledDeltaTime, cancellationToken: token);
            }
            finally { travelling = false; }
        }

        /// <summary>The replay is over: back upstairs if the loft is still this player's floor.</summary>
        internal static void ReturnUpstairs()
        {
            if (OnlineSession.CurrentRole == OnlineRole.Narrator)
                SceneDimmer.Instance.TryLoad(OnlineCoopConfig.LoftScene);
        }
    }
}
