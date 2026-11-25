using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>
    /// The narrator's "찾았어!": tells the maker downstairs that the right memo is open, so it is time to go to the
    /// storage room. It is a nudge only; the round itself does not depend on it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarratorSignal : MonoBehaviour
    {
        private const string MessageName = "potion.found";

        private OnlineSession session;
        private NetworkManager registeredOn;

        /// <summary>On the maker: unscaled time the last signal arrived, or -inf.</summary>
        public float ReceivedAt { get; private set; } = float.NegativeInfinity;

        /// <summary>Raised on the maker when the narrator found the memo.</summary>
        public event Action Received;

        private void Awake() => session = GetComponent<OnlineSession>();

        private void Update()
        {
            var network = session.Network;
            if (network == null || !network.IsListening || network.CustomMessagingManager == null)
            {
                registeredOn = null;
                return;
            }
            if (registeredOn == network)
                return;
            registeredOn = network;
            network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
        }

        /// <summary>Narrator only. Returns false when there is nobody downstairs to tell.</summary>
        public bool Send()
        {
            var network = session.Network;
            if (session.Role != OnlineRole.Narrator || !session.PeerConnected || network == null ||
                !network.IsListening || network.CustomMessagingManager == null)
                return false;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            if (network.IsServer)
                network.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
            else
                network.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer);
            return true;
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (session.Role != OnlineRole.Maker)
                return;
            ReceivedAt = Time.unscaledTime;
            Debug.Log("[Online] The narrator found the memo.");
            Received?.Invoke();
        }
    }
}
