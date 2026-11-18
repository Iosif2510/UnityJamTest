using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotionShop.Data
{
    public enum PortraitColorUsage { Legacy, Hair, Eye }

    [Serializable]
    public sealed class PortraitColor
    {
        public AppearanceValue value;
        public string colorId;
        public string displayName;
        public PortraitColorUsage usage;
        public Color color = Color.white;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Art/Portrait Colors")]
    public sealed class PortraitColorCatalog : ScriptableObject
    {
        [SerializeField] private List<PortraitColor> colors = new();

        public IReadOnlyList<PortraitColor> Colors => colors;

        public Color Get(PortraitSlot slot, AppearanceValue value)
        {
            if (value == AppearanceValue.None) return Color.white;
            var usage = slot == PortraitSlot.Hair ? PortraitColorUsage.Hair : PortraitColorUsage.Eye;
            string id = ColorId(slot, value);
            var entry = colors.FirstOrDefault(c => c.usage == usage && c.colorId == id)
                ?? colors.FirstOrDefault(c => c.usage == PortraitColorUsage.Legacy && c.value == value);
            if (entry == null) throw new InvalidOperationException($"Portrait color missing: {slot}/{value}");
            return entry.color;
        }

        public static string ColorId(PortraitSlot slot, AppearanceValue value) =>
            (slot == PortraitSlot.Hair ? "HAIR_" : "EYE_") + value.ToString().ToUpperInvariant();

        public static AppearanceValue ResolveId(PortraitSlot slot, string id)
        {
            if (id != null && id.Length > (slot == PortraitSlot.Hair ? 5 : 4) &&
                Enum.TryParse(id.Substring(slot == PortraitSlot.Hair ? 5 : 4), true, out AppearanceValue value) &&
                value is >= AppearanceValue.Black and <= AppearanceValue.Orange && id == ColorId(slot, value))
                return value;
            throw new InvalidOperationException($"Invalid portrait color ID: {slot}/{id}");
        }
    }
}
