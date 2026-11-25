using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>Undecided: connected, but the two players have not yet picked who goes upstairs.</summary>
    public enum OnlineRole { Offline, Undecided, Maker, Narrator }

    /// <summary>
    /// Owns the two-player connection. Either player may host; who plays the maker (existing scenes) and who plays
    /// the narrator (the loft) is agreed afterwards through <see cref="RoleLobby"/>. Nothing here is required for
    /// offline play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineSession : MonoBehaviour
    {
        private const ushort LanPort = 7777;
        private static OnlineSession instance;

        private ISession cloudSession;
        private bool busy;

        public static OnlineSession Instance
        {
            get
            {
                if (instance == null)
                {
                    var root = new GameObject("OnlineSession");
                    DontDestroyOnLoad(root);
                    instance = root.AddComponent<OnlineSession>();
                    root.AddComponent<RoundSync>();
                    root.AddComponent<RoleLobby>();
                    root.AddComponent<VoiceChat>();
                    root.AddComponent<NarratorSignal>();
                    root.AddComponent<FloorSounds>();
                    root.AddComponent<RoleSwap>();
                    root.AddComponent<SwapVote>();
                    root.AddComponent<PairReady>();
                    root.AddComponent<SharedEnding>();
                }
                return instance;
            }
        }

        public static bool Exists => instance != null;
        public static OnlineRole CurrentRole => instance != null ? instance.Role : OnlineRole.Offline;

        public OnlineRole Role { get; private set; }
        /// <summary>True on the player who opened the room. Independent of the role played.</summary>
        public bool IsHost { get; private set; }
        /// <summary>Easy mode marks the useful notes on the narrator's sheets. Agreed in the lobby.</summary>
        public bool EasyMode { get; private set; }
        /// <summary>True once this player has finished the walk to their floor after the roles were picked.</summary>
        public bool Arrived { get; private set; }
        public string JoinCode { get; private set; }
        public bool UsesCloud { get; private set; }
        public bool PeerConnected { get; private set; }
        public bool Busy => busy;
        public NetworkManager Network { get; private set; }
        public RoundSync Sync => GetComponent<RoundSync>();
        public VoiceChat Voice => GetComponent<VoiceChat>();
        public RoleLobby Lobby => GetComponent<RoleLobby>();
        public PairReady Pairs => GetComponent<PairReady>();
        public NarratorSignal Signal => GetComponent<NarratorSignal>();

        /// <summary>Raised whenever role, code, or peer connection changes.</summary>
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public async UniTask<string> HostCloudAsync()
        {
            return await Run(async () =>
            {
                await SignInAsync();
                EnsureNetwork();
                var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork(await OnlineRelayRegion.PickAsync());
                cloudSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                Begin(true, cloudSession.Code, true);
                Voice.JoinAsync(cloudSession.Code).Forget();
            });
        }

        public async UniTask<string> JoinCloudAsync(string code)
        {
            code = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0)
                return Loc.T("참가 코드를 입력하세요.");
            return await Run(async () =>
            {
                await SignInAsync();
                EnsureNetwork();
                cloudSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Begin(false, cloudSession.Code, true);
                Voice.JoinAsync(cloudSession.Code).Forget();
            });
        }

        /// <summary>Direct connection without Unity services. No in-game voice; meant for same-network play and testing.</summary>
        public string HostLan()
        {
            if (busy || Role != OnlineRole.Offline) return Loc.T("이미 연결 중입니다.");
            EnsureNetwork();
            Transport.SetConnectionData("127.0.0.1", LanPort, "0.0.0.0");
            if (!Network.StartHost())
                return Loc.T("방을 열지 못했습니다. 포트 7777이 사용 중인지 확인하세요.");
            Begin(true, null, false);
            return null;
        }

        public string JoinLan(string address)
        {
            if (busy || Role != OnlineRole.Offline) return Loc.T("이미 연결 중입니다.");
            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            EnsureNetwork();
            Transport.SetConnectionData(address, LanPort);
            if (!Network.StartClient())
                return Loc.T("연결을 시작하지 못했습니다.");
            Begin(false, null, false);
            return null;
        }

        public async UniTask LeaveAsync()
        {
            var session = cloudSession;
            cloudSession = null;
            Voice.LeaveAsync().Forget();
            if (Network != null && Network.IsListening)
                Network.Shutdown();
            if (session != null)
            {
                try { await session.LeaveAsync(); }
                catch (Exception exception) { Debug.LogWarning($"[Online] Leave failed: {exception.Message}"); }
            }
            Role = OnlineRole.Offline;
            IsHost = false;
            EasyMode = false;
            Arrived = false;
            JoinCode = null;
            UsesCloud = false;
            PeerConnected = false;
            Changed?.Invoke();
        }

        /// <summary>Called by <see cref="RoleLobby"/> once the two players have agreed who goes upstairs.</summary>
        internal void AssignRole(OnlineRole role)
        {
            if (Role == OnlineRole.Offline || Role == role || role is not (OnlineRole.Maker or OnlineRole.Narrator))
                return;
            Role = role;
            Debug.Log($"[Online] Role assigned: {role}.");
            Changed?.Invoke();
        }

        internal void MarkArrived()
        {
            Arrived = true;
            Changed?.Invoke();
        }

        internal void SetEasyMode(bool easy)
        {
            if (EasyMode == easy)
                return;
            EasyMode = easy;
            Changed?.Invoke();
        }

        private UnityTransport Transport => (UnityTransport)Network.NetworkConfig.NetworkTransport;

        private async UniTask<string> Run(Func<UniTask> connect)
        {
            if (busy || Role != OnlineRole.Offline)
                return Loc.T("이미 연결 중입니다.");
            busy = true;
            Changed?.Invoke();
            try
            {
                await connect();
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] {exception}");
                cloudSession = null;
                if (Network != null && Network.IsListening)
                    Network.Shutdown();
                return Loc.T("온라인 서비스에 연결하지 못했습니다.\n") + exception.Message;
            }
            finally
            {
                busy = false;
                Changed?.Invoke();
            }
        }

        private static async UniTask SignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                string profile = CommandLineValue("-onlineProfile");
                if (!string.IsNullOrEmpty(profile))
                    options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        internal static string CommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private void EnsureNetwork()
        {
            if (Network != null)
                return;

            var root = new GameObject("OnlineNetwork");
            root.SetActive(false);
            var transport = root.AddComponent<UnityTransport>();
#if UNITY_WEBGL
            transport.UseWebSockets = true; // Relay in a browser is WebSockets only
#endif
            Network = root.AddComponent<NetworkManager>();
            // The two players are in different scenes for most of a round, so Netcode must not sync scenes.
            Network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            root.SetActive(true);
            DontDestroyOnLoad(root);

            Network.OnClientConnectedCallback += _ => RefreshPeer();
            Network.OnClientDisconnectCallback += _ => RefreshPeer();
        }

        private void Begin(bool host, string code, bool cloud)
        {
            Role = OnlineRole.Undecided;
            IsHost = host;
            EasyMode = false;
            Arrived = false;
            JoinCode = code;
            UsesCloud = cloud;
            RefreshPeer();
        }

        private void RefreshPeer()
        {
            if (Network == null) return;
            PeerConnected = IsPeerConnected();
            Changed?.Invoke();
        }

        private bool IsPeerConnected() => IsHost
            ? Network.IsListening && Network.ConnectedClientsIds.Count > 1
            : Network.IsConnectedClient;

        private void Update()
        {
            if (Role == OnlineRole.Offline || Network == null) return;
            bool connected = IsPeerConnected();
            if (connected != PeerConnected)
            {
                PeerConnected = connected;
                Debug.Log($"[Online] {Role}: peer {(connected ? "connected" : "disconnected")}.");
                Changed?.Invoke();
            }
        }
    }
}
