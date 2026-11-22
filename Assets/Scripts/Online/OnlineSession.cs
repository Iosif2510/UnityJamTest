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
    public enum OnlineRole { Offline, Maker, Narrator }

    /// <summary>
    /// Owns the two-player connection. The maker hosts and plays the existing scenes; the narrator joins and stays
    /// in the loft. Nothing here is required for offline play.
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
                    root.AddComponent<VoiceChat>();
                }
                return instance;
            }
        }

        public static bool Exists => instance != null;
        public static OnlineRole CurrentRole => instance != null ? instance.Role : OnlineRole.Offline;

        public OnlineRole Role { get; private set; }
        public string JoinCode { get; private set; }
        public bool UsesCloud { get; private set; }
        public bool PeerConnected { get; private set; }
        public bool Busy => busy;
        public NetworkManager Network { get; private set; }
        public RoundSync Sync => GetComponent<RoundSync>();
        public VoiceChat Voice => GetComponent<VoiceChat>();

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
                var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
                cloudSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                Begin(OnlineRole.Maker, cloudSession.Code, true);
                Voice.JoinAsync(cloudSession.Code).Forget();
            });
        }

        public async UniTask<string> JoinCloudAsync(string code)
        {
            code = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0)
                return "참가 코드를 입력하세요.";
            return await Run(async () =>
            {
                await SignInAsync();
                EnsureNetwork();
                cloudSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Begin(OnlineRole.Narrator, cloudSession.Code, true);
                Voice.JoinAsync(cloudSession.Code).Forget();
            });
        }

        /// <summary>Direct connection without Unity services. No in-game voice; meant for same-network play and testing.</summary>
        public string HostLan()
        {
            if (busy || Role != OnlineRole.Offline) return "이미 연결 중입니다.";
            EnsureNetwork();
            Transport.SetConnectionData("127.0.0.1", LanPort, "0.0.0.0");
            if (!Network.StartHost())
                return "방을 열지 못했습니다. 포트 7777이 사용 중인지 확인하세요.";
            Begin(OnlineRole.Maker, null, false);
            return null;
        }

        public string JoinLan(string address)
        {
            if (busy || Role != OnlineRole.Offline) return "이미 연결 중입니다.";
            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            EnsureNetwork();
            Transport.SetConnectionData(address, LanPort);
            if (!Network.StartClient())
                return "연결을 시작하지 못했습니다.";
            Begin(OnlineRole.Narrator, null, false);
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
            JoinCode = null;
            UsesCloud = false;
            PeerConnected = false;
            Changed?.Invoke();
        }

        private UnityTransport Transport => (UnityTransport)Network.NetworkConfig.NetworkTransport;

        private async UniTask<string> Run(Func<UniTask> connect)
        {
            if (busy || Role != OnlineRole.Offline)
                return "이미 연결 중입니다.";
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
                return "온라인 서비스에 연결하지 못했습니다.\n" + exception.Message;
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
            Network = root.AddComponent<NetworkManager>();
            // The maker changes scenes alone; the narrator never follows, so Netcode must not sync scenes.
            Network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            root.SetActive(true);
            DontDestroyOnLoad(root);

            Network.OnClientConnectedCallback += _ => RefreshPeer();
            Network.OnClientDisconnectCallback += _ => RefreshPeer();
        }

        private void Begin(OnlineRole role, string code, bool cloud)
        {
            Role = role;
            JoinCode = code;
            UsesCloud = cloud;
            RefreshPeer();
        }

        private void RefreshPeer()
        {
            if (Network == null) return;
            PeerConnected = Role == OnlineRole.Maker
                ? Network.IsListening && Network.ConnectedClientsIds.Count > 1
                : Network.IsConnectedClient;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (Role == OnlineRole.Offline || Network == null) return;
            bool connected = Role == OnlineRole.Maker
                ? Network.IsListening && Network.ConnectedClientsIds.Count > 1
                : Network.IsConnectedClient;
            if (connected != PeerConnected)
            {
                PeerConnected = connected;
                Debug.Log($"[Online] {Role}: peer {(connected ? "connected" : "disconnected")}.");
                Changed?.Invoke();
            }
        }
    }
}
