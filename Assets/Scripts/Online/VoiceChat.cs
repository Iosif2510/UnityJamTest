using System;
using Cysharp.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PotionShop.Online
{
    public enum VoiceState { Off, Connecting, On, Failed }

    /// <summary>Two-way voice between the counter and the loft over Vivox. Only available on cloud sessions.</summary>
    [DisallowMultipleComponent]
    public sealed class VoiceChat : MonoBehaviour
    {
        private bool subscribed;
        private VivoxParticipant peer;

        public VoiceState State { get; private set; }
        public bool MicMuted { get; private set; }
        public bool PeerPresent => peer != null;
#if UNITY_WEBGL
        // the browser's Vivox has no speech events, so nobody is ever shown talking there
        public bool PeerSpeaking => false;
#else
        public bool PeerSpeaking => peer != null && peer.SpeechDetected;
#endif
        public string Error { get; private set; }

        public event Action Changed;

        public async UniTask JoinAsync(string channel)
        {
            if (State is VoiceState.Connecting or VoiceState.On || string.IsNullOrEmpty(channel))
                return;
            // turned off in the options: the friends talk elsewhere, and the microphone is never asked for
            if (!GameSettings.VoiceChat)
                return;

            Set(VoiceState.Connecting);
            try
            {
                // a voice service that never answers (seen in browsers) must not keep "connecting" up all game
                await Connect(channel).Timeout(TimeSpan.FromSeconds(ConnectSeconds));
                MicMuted = false;
                SetMicLive(true);
                ApplyFriendVolume();
                Set(VoiceState.On);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Voice] {exception}");
                Error = exception.Message;
                Set(VoiceState.Failed);
            }
        }

        private const float ConnectSeconds = 20f;

        private async UniTask Connect(string channel)
        {
            var vivox = VivoxService.Instance;
            await vivox.InitializeAsync();
            if (!subscribed)
            {
                vivox.ParticipantAddedToChannel += OnParticipantAdded;
                vivox.ParticipantRemovedFromChannel += OnParticipantRemoved;
                subscribed = true;
            }
            if (!vivox.IsLoggedIn)
                await vivox.LoginAsync();
            await vivox.JoinGroupChannelAsync("potion_" + channel, ChatCapability.AudioOnly);
        }

        public async UniTask LeaveAsync()
        {
            if (State == VoiceState.Off)
                return;
            peer = null;
            Set(VoiceState.Off);
            try
            {
                var vivox = VivoxService.Instance;
                if (vivox != null && vivox.IsLoggedIn)
                {
                    await vivox.LeaveAllChannelsAsync();
                    await vivox.LogoutAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Voice] Leave failed: {exception.Message}");
            }
        }

        public void ToggleMic()
        {
            if (State != VoiceState.On)
                return;
            MicMuted = !MicMuted;
            SetMicLive(!MicMuted);
            Changed?.Invoke();
        }

        /// <summary>The friend's voice at the options' level (0 is silent).</summary>
        public void ApplyFriendVolume()
        {
            if (State != VoiceState.On) return;
            float level = GameSettings.FriendVoice;
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // the browser's Vivox has no volume: only all or nothing
                SetDevicesMuted("AudioOutputDevices", level <= 0.01f);
#else
                var vivox = VivoxService.Instance;
                if (level <= 0.01f) vivox.MuteOutputDevice();
                else
                {
                    vivox.UnmuteOutputDevice();
                    vivox.SetOutputDeviceVolume(Mathf.RoundToInt(Mathf.Lerp(-40f, 0f, level)));
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Voice] Friend volume not applied: {exception.Message}");
            }
        }

        /// <summary>
        /// Opens or closes this player's microphone. In a browser Vivox's own mute call does nothing (its web input
        /// devices leave Mute empty), so the microphone would stay live behind a "muted" label; there the capture
        /// switch underneath it is used instead.
        /// </summary>
        private static void SetMicLive(bool live)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                SetDevicesMuted("AudioInputDevices", !live);
#else
                if (live) VivoxService.Instance.UnmuteInputDevice();
                else VivoxService.Instance.MuteInputDevice();
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Voice] Microphone switch failed: {exception.Message}");
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>Sets Muted on Vivox's web input or output devices (its own setter reaches the browser).</summary>
        private static void SetDevicesMuted(string devices, bool muted)
        {
            const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            object service = VivoxService.Instance;
            object client = service.GetType().GetProperty("Client", any)?.GetValue(service);
            object audio = client?.GetType().GetProperty(devices, any)?.GetValue(client);
            var flag = audio?.GetType().GetProperty("Muted", any);
            if (flag == null) throw new InvalidOperationException("Vivox " + devices + " not reachable");
            flag.SetValue(audio, muted);
        }
#endif

        public string Describe() => State switch
        {
            VoiceState.Connecting => Loc.T("보이스 연결 중…"),
            VoiceState.On when !PeerPresent => MicMuted ? Loc.T("마이크 꺼짐 [M] · 상대 대기 중") : Loc.T("마이크 켜짐 [M] · 상대 대기 중"),
            VoiceState.On => (MicMuted ? Loc.T("마이크 꺼짐 [M]") : Loc.T("마이크 켜짐 [M]")) + (PeerSpeaking ? Loc.T(" · 상대가 말하는 중") : ""),
            VoiceState.Failed => Loc.T("보이스 연결 실패 · 마이크 권한을 확인하거나 외부 음성 채팅을 쓰세요"),
            _ => GameSettings.VoiceChat ? Loc.T("게임 내 보이스 없음 · 외부 음성 채팅을 사용하세요")
                : Loc.T("게임 음성 꺼 둠 (옵션) · 외부 음성 채팅을 쓰세요")
        };

        private void Update()
        {
            if (State != VoiceState.On || Keyboard.current?.mKey.wasPressedThisFrame != true)
                return;
            // Join codes and addresses are typed into input fields; do not treat those key presses as the mic toggle.
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || selected.GetComponent<InputField>() == null)
                ToggleMic();
        }

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            if (participant.IsSelf) return;
            peer = participant;
            Changed?.Invoke();
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            if (participant.IsSelf || peer == null || participant.PlayerId != peer.PlayerId) return;
            peer = null;
            Changed?.Invoke();
        }

        private void Set(VoiceState state)
        {
            State = state;
            Changed?.Invoke();
        }
    }
}
