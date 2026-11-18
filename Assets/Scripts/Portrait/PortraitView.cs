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
            blink.Stop();
            rig?.Clear();
            layers.Compose(profile);
            var hairTint = profile.UseAccessoryTint ? profile.AccessoryTint : palette.Get(PortraitSlot.Hair, hair);
            colors.Apply(hairTint, palette.Get(PortraitSlot.Eyes, eyes));
            rig?.Load(profile.RigPrefab, profile.SpineRig);
            blink.BindSpine(rig != null ? rig.SpineRig : null);
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
            blink.BindSpine(null);
            if (rig != null) rig.Clear();
            colors.Apply(Color.white, Color.white);
            foreach (var image in layers.All) image.enabled = false;
            if (layers.irisMask != null) layers.irisMask.enabled = false;
            layers.slime.sprite = sprite; layers.slime.color = Color.white; layers.slime.enabled = true;
        }
    }
}
