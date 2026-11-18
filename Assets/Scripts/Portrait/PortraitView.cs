using System;
using System.Linq;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop.Portraits
{
    public sealed class PortraitView : MonoBehaviour
    {
        [SerializeField] private PortraitLayers layers;
        [SerializeField] private PortraitColorModule colors;
        [SerializeField] private PortraitBlink blink;
        [SerializeField] private PortraitRigModule rig;
        public PortraitLayers Layers => layers;
        public PortraitBlink Blink => blink;
        public PortraitRigModule Rig => rig;
        public void Show(CustomerProfile profile, AppearanceValue hair, AppearanceValue eyes, PortraitColorCatalog palette)
        {
            if (profile == null || palette == null) throw new ArgumentNullException("Portrait profile/palette");
            if ((hair != AppearanceValue.None && !profile.HairColors.Contains(hair)) ||
                (hair == AppearanceValue.None && profile.HairColors.Count > 0) || !profile.EyeColors.Contains(eyes))
                throw new InvalidOperationException($"{profile.Id}: unsupported portrait colors");
            blink.Stop(); layers.Compose(profile); colors.Apply(palette.Get(hair), palette.Get(eyes));
            if (rig != null) rig.Load(profile.RigPrefab);
            blink.Play();
        }
        public void Show(GeneratedOrder order, PortraitColorCatalog palette) => Show(order.Customer,
            order.Portrait.First(p => p.Slot == PortraitSlot.Hair).Appearance,
            order.Portrait.First(p => p.Slot == PortraitSlot.Eyes).Appearance, palette);
        public void Tint(Color tint) => colors.AnimateTint(tint);
        public void ShowSlime(Sprite sprite)
        {
            if (sprite == null) throw new InvalidOperationException("Assign slime portrait in Customers.");
            blink.Stop();
            if (rig != null) rig.Clear();
            colors.Apply(Color.white, Color.white);
            foreach (var image in layers.All) image.enabled = false;
            layers.slime.sprite = sprite; layers.slime.color = Color.white; layers.slime.enabled = true;
        }
    }
}
