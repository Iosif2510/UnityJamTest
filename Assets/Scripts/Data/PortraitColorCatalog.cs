using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotionShop.Data
{
    [Serializable]
    public sealed class PortraitColor
    {
        public AppearanceValue value;
        public Color color = Color.white;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Art/Portrait Colors")]
    public sealed class PortraitColorCatalog : ScriptableObject
    {
        [SerializeField] private List<PortraitColor> colors = new();
        public Color Get(AppearanceValue value)
        {
            if (value == AppearanceValue.None) return Color.white;
            var entry = colors.FirstOrDefault(c => c.value == value);
            if (entry == null) throw new InvalidOperationException($"Portrait color missing: {value}");
            return entry.color;
        }
    }
}
