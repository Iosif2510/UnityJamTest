using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionShop
{
    /// <summary>
    /// Scene-level sound, set up at load so the scenes stay untouched: the counter's music loops instead of stopping
    /// after one play, and the storage room gets its quiet room tone (Workshop_Room_Tone, in Resources/Sfx).
    /// </summary>
    internal static class SceneAudio
    {
        private const string Music = "Busy Shop Groove";
        private const float RoomToneVolume = 0.35f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (scene, _) => Apply(scene);
            Apply(SceneManager.GetActiveScene());
        }

        private static void Apply(Scene scene)
        {
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source.gameObject.scene != scene || source.clip == null || source.clip.name != Music) continue;
                source.loop = true;
                if (source.playOnAwake && !source.isPlaying) source.Play();
            }

            if (scene.name == "02_Workshop")
            {
                var clip = Resources.Load<AudioClip>("Sfx/Workshop_Room_Tone");
                if (clip == null) return;
                var go = new GameObject("RoomTone");
                SceneManager.MoveGameObjectToScene(go, scene);
                var tone = go.AddComponent<AudioSource>();
                tone.clip = clip;
                tone.loop = true;
                tone.spatialBlend = 0f;
                tone.volume = RoomToneVolume;
                tone.Play();
            }
        }
    }
}
