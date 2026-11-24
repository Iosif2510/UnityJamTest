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

            Set(VoiceState.Connecting);
            try
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
                MicMuted = vivox.IsInputDeviceMuted;
                Set(VoiceState.On);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Voice] {exception}");
                Error = exception.Message;
                Set(VoiceState.Failed);
            }
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
            if (MicMuted) VivoxService.Instance.UnmuteInputDevice();
            else VivoxService.Instance.MuteInputDevice();
            MicMuted = !MicMuted;
            Changed?.Invoke();
        }

        public string Describe() => State switch
        {
            VoiceState.Connecting => Loc.T("보이스 연결 중…"),
            VoiceState.On when !PeerPresent => MicMuted ? Loc.T("마이크 꺼짐 [M] · 상대 대기 중") : Loc.T("마이크 켜짐 [M] · 상대 대기 중"),
            VoiceState.On => (MicMuted ? Loc.T("마이크 꺼짐 [M]") : Loc.T("마이크 켜짐 [M]")) + (PeerSpeaking ? Loc.T(" · 상대가 말하는 중") : ""),
            VoiceState.Failed => Loc.T("보이스 연결 실패 · 외부 음성 채팅을 사용하세요"),
            _ => Loc.T("게임 내 보이스 없음 · 외부 음성 채팅을 사용하세요")
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
