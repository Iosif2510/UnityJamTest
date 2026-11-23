using System.Collections.Generic;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// Plays the short 2D feedback sounds (Resources/Sfx, made by Tools/Sfx/synth_sfx.py) from a small pool that
    /// survives scene loads, and owns the saved master volume.
    /// </summary>
    public sealed class GameSfx : MonoBehaviour
    {
        private const string VolumeKey = "audio.master";
        private const int Voices = 8;

        private static GameSfx instance;
        private readonly Dictionary<string, AudioClip> clips = new();
        private AudioSource[] voices;
        private int next;

        /// <summary>Master volume, 0–1, saved between sessions. Applied through the AudioListener.</summary>
        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, value);
                AudioListener.volume = value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedVolume() => AudioListener.volume = MasterVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <param name="name">File name in Resources/Sfx, e.g. "StampThud".</param>
        /// <param name="pitchJitter">Random pitch spread, so repeated sounds don't feel mechanical.</param>
        public static void Play(string name, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
        {
            var player = Instance;
            var clip = player.Clip(name);
            if (clip == null) return;
            var voice = player.voices[player.next];
            player.next = (player.next + 1) % Voices;
            voice.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            voice.PlayOneShot(clip, volume);
        }

        private static GameSfx Instance
        {
            get
            {
                if (instance != null) return instance;
                var root = new GameObject("GameSfx");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<GameSfx>();
                instance.voices = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    var source = root.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                    source.ignoreListenerPause = true;
                    instance.voices[i] = source;
                }
                return instance;
            }
        }

        private AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var clip) && clip != null) return clip;
            clip = Resources.Load<AudioClip>("Sfx/" + name);
            if (clip == null) Debug.LogWarning($"[Sfx] Missing Resources/Sfx/{name}.");
            else clips[name] = clip;
            return clip;
        }
    }
}
