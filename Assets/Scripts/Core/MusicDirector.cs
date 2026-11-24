using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionShop
{
    /// <summary>
    /// Background music across scenes (Resources/Music, Suno instrumentals): the title theme on the title screen,
    /// a quiet attic piece in the loft, and in every scene a hurried piece that crossfades in when a customer's time
    /// drops under <see cref="HurrySeconds"/> and back out when the round's clock stops. The counter keeps its own
    /// scene music, which is ducked under the hurry piece. Scenes stay untouched.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        private const float HurrySeconds = 30f;
        private const float FadeSeconds = 1.5f;
        private const string CounterMusic = "Busy Shop Groove";

        private AudioSource baseMusic;
        private AudioSource hurry;
        private AudioSource sceneMusic;
        private float baseVolume;
        private float sceneVolume;
        private float hurryVolume = 0.3f;
        private float hurryMix;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("MusicDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<MusicDirector>().OnScene(SceneManager.GetActiveScene());
        }

        // Subscribed per instance, so a director from an earlier play session (no domain reload) never hears it.
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => OnScene(scene);

        private void Awake()
        {
            baseMusic = NewSource();
            hurry = NewSource();
            hurry.clip = Resources.Load<AudioClip>("Music/Hurry");
        }

        private AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private void OnScene(Scene scene)
        {
            // What this scene plays underneath: our own track, or the music the scene brought with it.
            (string clip, float volume) = scene.name switch
            {
                "00_Title" => ("Music/Title", 0.35f),
                "03_Loft" => ("Music/Loft", 0.2f),
                _ => (null, 0f)
            };
            var next = clip != null ? Resources.Load<AudioClip>(clip) : null;
            if (baseMusic.clip != next)
            {
                baseMusic.Stop();
                baseMusic.clip = next;
                if (next != null) baseMusic.Play();
            }
            baseVolume = volume;

            // The shop's music rides on the round, which outlives scenes: find that copy (a second one comes with each
            // visit to the counter and is thrown away at once, so keep it quiet). It plays at the counter and in the
            // storage room, and pauses wherever a scene has its own music (the title, the loft).
            AudioSource shop = null;
            var copies = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var source in copies)
                if (source.clip != null && source.clip.name == CounterMusic &&
                    (shop == null || source.gameObject.scene.name == "DontDestroyOnLoad"))
                    shop = source;
            foreach (var source in copies)
                if (source != shop && source.clip != null && source.clip.name == CounterMusic)
                    source.Stop();
            if (shop != sceneMusic)
            {
                sceneMusic = shop;
                if (shop != null) sceneVolume = shop.volume;
            }
            if (shop != null)
            {
                if (next != null) shop.Pause();
                else if (!shop.isPlaying)
                {
                    shop.UnPause();
                    if (!shop.isPlaying) shop.Play();
                }
            }
        }

        private void Update()
        {
            bool urgent = TryGetClock(out float remaining) && remaining > 0f && remaining <= HurrySeconds;
            hurryMix = Mathf.MoveTowards(hurryMix, urgent ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);

            if (hurryMix > 0f && !hurry.isPlaying && hurry.clip != null)
                hurry.Play();
            else if (hurryMix <= 0f && hurry.isPlaying)
                hurry.Stop(); // the next rush starts from the top again
            hurry.volume = hurryVolume * hurryMix;

            float keep = 1f - hurryMix;
            baseMusic.volume = baseVolume * keep;
            if (sceneMusic != null)
                sceneMusic.volume = sceneVolume * keep;
        }

        /// <summary>The running customer clock: the round on this machine, or the maker's clock seen from the loft.</summary>
        private static bool TryGetClock(out float remaining)
        {
            remaining = 0f;
            var round = RoundDirector.Instance;
            if (round != null && round.CurrentOrder != null &&
                round.Phase is RoundPhase.Consult or RoundPhase.Storage or RoundPhase.Prescribe)
            {
                remaining = (float)round.RemainingSeconds;
                return true;
            }
            if (Online.OnlineSession.CurrentRole == Online.OnlineRole.Narrator)
            {
                var sync = Online.OnlineSession.Instance.Sync;
                if (sync.HasSnapshot && sync.Latest.TimerRunning)
                {
                    remaining = sync.RemainingNow;
                    return true;
                }
            }
            return false;
        }
    }
}
