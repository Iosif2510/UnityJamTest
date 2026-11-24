using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The cellar heard from where the player stands: the fireplace crackles from its hearth, the cauldron simmers
    /// from the middle of the room and each lantern breathes faintly on its wall, all louder as you come near and
    /// panned as you turn. Now and then the cellar itself speaks: a drip in a corner, a beam settling overhead, steps
    /// or the shop's bell upstairs, muffled through the floor. The room's echo is a stone room's.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CellarAmbience : MonoBehaviour
    {
        // the hall's floor, as found in the scene
        private static readonly Vector2 HalfSize = new(4.1f, 6.3f);
        private const float Ceiling = 3.6f;

        private AudioSource oneShots;
        private AudioLowPassFilter muffle;
        private AudioSource upstairs;
        private float nextDrip, nextCreak, nextUpstairs;

        private void Start()
        {
            var scene = gameObject.scene;
            var fire = CellarAtmosphere.Find(scene, "Fireplace Light");
            if (fire != null) Loop("Fire", fire.position + Vector3.down * 0.2f, "AmbFire", 0.55f, 1.2f, 9f);
            var cauldron = FindAnyObjectByType<WorkshopCauldron>();
            if (cauldron != null) Loop("Simmer", cauldron.transform.position + Vector3.up * 0.7f, "AmbCauldron", 0.4f, 0.8f, 6f);
            var lights = CellarAtmosphere.Find(scene, "CozyLights");
            if (lights != null)
                foreach (Transform lamp in lights)
                    if (lamp.name.StartsWith("Lantern Light"))
                        Loop("Lantern", lamp.position, "AmbLantern", 0.18f, 0.3f, 2.2f);

            // the stone room's echo, for every sound in the room
            foreach (var zone in FindObjectsByType<AudioReverbZone>(FindObjectsSortMode.None))
                zone.reverbPreset = AudioReverbPreset.Stoneroom;

            oneShots = Source("Cellar One-shots", 1f, 0.8f, 12f);
            upstairs = Source("Upstairs", 1f, 2f, 14f);
            muffle = upstairs.gameObject.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = 600f;

            float now = Time.time;
            nextDrip = now + Random.Range(3f, 7f);
            nextCreak = now + Random.Range(10f, 20f);
            nextUpstairs = now + Random.Range(12f, 25f);
        }

        private void Update()
        {
            var eyes = CellarAtmosphere.Eyes;
            if (eyes == null) return;
            float now = Time.time;
            if (now >= nextDrip)
            {
                nextDrip = now + Random.Range(5f, 12f);
                // somewhere along the walls, low down
                var at = Edge(Random.Range(0.05f, 0.5f));
                Play(oneShots, at, Random.value < 0.5f ? "Drip1" : "Drip2", Random.Range(0.35f, 0.6f), Random.Range(0.9f, 1.15f));
            }
            if (now >= nextCreak)
            {
                nextCreak = now + Random.Range(18f, 35f);
                var at = new Vector3(Random.Range(-HalfSize.x, HalfSize.x), Ceiling, Random.Range(-HalfSize.y, HalfSize.y));
                Play(oneShots, at, "BeamCreak", Random.Range(0.3f, 0.5f), Random.Range(0.85f, 1.1f));
            }
            if (now >= nextUpstairs)
            {
                nextUpstairs = now + Random.Range(20f, 40f);
                // the shop is above: steps over the player's head, or now and then the door bell
                var at = eyes.position + new Vector3(Random.Range(-3f, 3f), 2.6f, Random.Range(-3f, 3f));
                bool bell = Random.value < 0.25f;
                Play(upstairs, at, bell ? "DoorBell" : "UpstairsStep", bell ? 0.35f : 0.7f, bell ? 0.95f : Random.Range(0.9f, 1.1f));
            }
        }

        /// <summary>A point by a wall of the hall, <paramref name="height"/> above the floor.</summary>
        private static Vector3 Edge(float height)
        {
            bool alongX = Random.value < 0.5f;
            float sign = Random.value < 0.5f ? -1f : 1f;
            return alongX
                ? new Vector3(Random.Range(-HalfSize.x, HalfSize.x), height, sign * HalfSize.y)
                : new Vector3(sign * HalfSize.x, height, Random.Range(-HalfSize.y, HalfSize.y));
        }

        private static void Play(AudioSource source, Vector3 at, string clip, float volume, float pitch)
        {
            var sound = GameSfx.Load(clip);
            if (sound == null) return;
            source.transform.position = at;
            source.pitch = pitch;
            source.PlayOneShot(sound, volume);
        }

        private AudioSource Loop(string name, Vector3 at, string clip, float volume, float near, float far)
        {
            var source = Source(name, volume, near, far);
            source.transform.position = at;
            source.clip = GameSfx.Load(clip);
            source.loop = true;
            if (source.clip != null)
            {
                // start each loop at its own place, so the lanterns don't breathe in unison
                source.time = Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
            }
            return source;
        }

        private AudioSource Source(string name, float volume, float near, float far)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = near;
            source.maxDistance = far;
            source.dopplerLevel = 0f;
            source.volume = volume;
            return source;
        }
    }
}
