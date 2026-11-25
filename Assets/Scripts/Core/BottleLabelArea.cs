using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// Where a label can go on each bottle picture so it never sticks out past the glass: the largest label-shaped
    /// box inside the bottle's silhouette (measured from the Potions/mask pictures, a little in from the edges and
    /// below the neck), as fractions of the picture. Labels use a share of that width to leave a margin.
    /// </summary>
    internal static class BottleLabelArea
    {
        private const float Margin = 0.88f;

        /// <summary>Label centre (fractions from the picture's bottom-left) and the widest label that fits.</summary>
        public static (Vector2 centre, float width) For(Sprite bottle)
        {
            string name = bottle != null ? bottle.name : "";
            if (name.Contains("circle")) return (new Vector2(0.496f, 0.394f), 0.6f * Margin);
            if (name.Contains("square")) return (new Vector2(0.484f, 0.372f), 0.745f * Margin);
            if (name.Contains("heart")) return (new Vector2(0.488f, 0.47f), 0.51f * Margin);
            return (new Vector2(0.5f, 0.4f), 0.5f * Margin);
        }
    }
}
