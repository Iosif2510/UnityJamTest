using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>
    /// Lets the two connected players agree who goes upstairs and whether to play in easy mode.
    /// The host arbitrates: the first role anyone picks decides both roles, and the host repeats the
    /// agreed state so a late or reconnecting client always catches up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoleLobby : MonoBehaviour
    {
        private const string RequestMessage = "potion.lobby.request";
        private const string StateMessage = "potion.lobby.state";
        private const float ResendInterval = 0.5f;
        private const byte PickRole = 0;
        private const byte SetEasy = 1;

        private OnlineSession session;
        private NetworkManager registeredOn;
        private OnlineRole hostRole = OnlineRole.Undecided;
        private float lastSentAt = float.NegativeInfinity;

        private void Awake() => session = GetComponent<OnlineSession>();

        /// <summary>Ask to play <paramref name="role"/>. The other player gets the remaining role.</summary>
        public void Pick(OnlineRole role)
        {
            if (session.Role != OnlineRole.Undecided || !session.PeerConnected ||
                role is not (OnlineRole.Maker or OnlineRole.Narrator))
                return;
            if (session.IsHost)
                Decide(role);
            else
                Request(PickRole, (byte)role);
        }

        public void RequestEasyMode(bool easy)
        {
            if (session.Role != OnlineRole.Undecided)
                return;
            if (session.IsHost)
            {
                session.SetEasyMode(easy);
                Broadcast();
            }
            else
                Request(SetEasy, easy ? (byte)1 : (byte)0);
        }

        private void Update()
        {
            var network = session.Network;
            if (session.Role == OnlineRole.Offline || network == null || !network.IsListening ||
                network.CustomMessagingManager == null)
            {
                registeredOn = null;
                hostRole = OnlineRole.Undecided;
                return;
            }

            if (registeredOn != network)
            {
                registeredOn = network;
                hostRole = OnlineRole.Undecided;
                network.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage, OnRequest);
                network.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, OnState);
            }

            if (session.IsHost && session.PeerConnected && Time.unscaledTime - lastSentAt >= ResendInterval)
                Broadcast();
        }

        private void Decide(OnlineRole roleOfHost)
        {
            if (hostRole != OnlineRole.Undecided)
                return;
            hostRole = roleOfHost;
            session.AssignRole(roleOfHost);
            Broadcast();
        }

        private void Request(byte kind, byte value)
        {
            var network = session.Network;
            if (network == null || !network.IsConnectedClient)
                return;
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe(kind);
            writer.WriteValueSafe(value);
            network.CustomMessagingManager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
        }

        private void Broadcast()
        {
            var network = session.Network;
            if (network == null || !network.IsServer)
                return;
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe((byte)hostRole);
            writer.WriteValueSafe(session.EasyMode ? (byte)1 : (byte)0);
            network.CustomMessagingManager.SendNamedMessageToAll(StateMessage, writer);
            lastSentAt = Time.unscaledTime;
        }

        private void OnRequest(ulong sender, FastBufferReader reader)
        {
            if (!session.IsHost)
                return;
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out byte value);
            if (kind == SetEasy && hostRole == OnlineRole.Undecided)
            {
                session.SetEasyMode(value != 0);
                Broadcast();
            }
            else if (kind == PickRole && (OnlineRole)value is OnlineRole.Maker or OnlineRole.Narrator)
                Decide(Other((OnlineRole)value));
        }

        private void OnState(ulong sender, FastBufferReader reader)
        {
            if (session.IsHost)
                return;
            reader.ReadValueSafe(out byte role);
            reader.ReadValueSafe(out byte easy);
            session.SetEasyMode(easy != 0);
            if ((OnlineRole)role is OnlineRole.Maker or OnlineRole.Narrator)
                session.AssignRole(Other((OnlineRole)role));
        }

        private static OnlineRole Other(OnlineRole role) =>
            role == OnlineRole.Maker ? OnlineRole.Narrator : OnlineRole.Maker;
    }
}
