using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>
    /// The two floors hear each other: a few of one player's sounds play for the other one muffled, as if through the
    /// floorboards. Upstairs hears ingredients splashing into the cauldron and the brew finishing; downstairs hears
    /// memos being turned and dealt in the loft. Nothing is shown; it only says "your friend is busy right now".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorSounds : MonoBehaviour
    {
        private const string MessageName = "potion.floor";

        // Sent by number; both sides share this list.
        private static readonly string[] Names = { "Splash", "BrewDone", "PageTurn", "CardDeal" };

        private const float Volume = 0.32f;
        private const float Cutoff = 650f;

        private OnlineSession session;
        private NetworkManager registeredOn;
        private AudioSource source;

        private void Awake()
        {
            session = GetComponent<OnlineSession>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            // through wood: the highs are gone and a little room is left
            gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = Cutoff;
        }

        /// <summary>Lets the friend on the other floor hear <paramref name="sound"/> (a Resources/Sfx name).</summary>
        public static void Send(string sound)
        {
            if (!OnlineSession.Exists) return;
            var session = OnlineSession.Instance;
            var network = session.Network;
            if (session.Role is not (OnlineRole.Maker or OnlineRole.Narrator) || !session.PeerConnected ||
                network == null || !network.IsListening || network.CustomMessagingManager == null)
                return;
            int id = Array.IndexOf(Names, sound);
            if (id < 0) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe((byte)id);
            if (network.IsServer)
                network.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
            else
                network.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer);
        }

        /// <summary>Plays <paramref name="sound"/> as it sounds from the other floor (also for checking it offline).</summary>
        public void Hear(string sound)
        {
            var clip = GameSfx.Load(sound);
            if (clip == null) return;
            source.pitch = 0.9f * (1f + UnityEngine.Random.Range(-0.05f, 0.05f));
            source.PlayOneShot(clip, Volume);
        }

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

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (session.Role is not (OnlineRole.Maker or OnlineRole.Narrator)) return;
            reader.ReadValueSafe(out byte id);
            if (id < Names.Length) Hear(Names[id]);
        }
    }
}
