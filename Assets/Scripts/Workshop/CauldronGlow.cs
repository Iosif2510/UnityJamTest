using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// The light over the cauldron takes the colour of what is in it (<see cref="CauldronColor.Current"/>) and
    /// swells a little with each bubble, brighter while a brew is boiling, so the room around it is washed in the
    /// potion's colour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CauldronGlow : MonoBehaviour
    {
        private Light glow;
        private CauldronColor colour;
        private WorkshopCauldron cauldron;
        private float baseIntensity, baseRange;
        private Color baseColour;
        private float swell, nextBubble;

        private void Start()
        {
            var t = CellarAtmosphere.Find(gameObject.scene, "Cauldron Glow");
            if (t == null || !t.TryGetComponent(out glow)) { enabled = false; return; }
            baseIntensity = glow.intensity;
            baseRange = glow.range;
            baseColour = glow.color;
            cauldron = FindAnyObjectByType<WorkshopCauldron>();
            colour = cauldron != null ? cauldron.GetComponentInChildren<CauldronColor>(true) : FindAnyObjectByType<CauldronColor>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float brewing = cauldron != null && cauldron.BrewProgress > 0f && cauldron.BrewProgress < 1f ? 1f : 0f;

            // the brew's colour, kept bright enough to light the room
            var target = baseColour;
            if (colour != null && colour.Current.a > 0f)
            {
                Color.RGBToHSV(colour.Current, out float h, out float s, out _);
                target = Color.HSVToRGB(h, Mathf.Clamp01(s * 0.9f), 1f);
            }
            glow.color = Color.Lerp(glow.color, target, 1f - Mathf.Exp(-dt * 2.5f));

            // a bubble now and then, more often and fuller while it boils
            if (Time.time >= nextBubble)
            {
                nextBubble = Time.time + Random.Range(0.25f, 0.9f) / (1f + brewing * 1.5f);
                swell = Mathf.Max(swell, Random.Range(0.12f, 0.3f) * (1f + brewing));
            }
            swell = Mathf.MoveTowards(swell, 0f, dt * 0.9f);
            float flicker = Mathf.PerlinNoise(Time.time * 3f, 0.37f) * 0.12f;
            glow.intensity = baseIntensity * (1f + brewing * 0.6f + swell + flicker);
            glow.range = baseRange * (1f + brewing * 0.25f + swell * 0.3f);
        }
    }
}
