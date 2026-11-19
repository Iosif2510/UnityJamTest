using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop.Portraits
{
    public sealed class PortraitView : MonoBehaviour
    {
        private static readonly Color SlimeTransitionTint = new(0.49f, 1f, 0.53f);
        private const float SlimeTintInSeconds = .28f;
        private const float SlimeTintOutSeconds = .36f;

        [SerializeField] private PortraitLayers layers;
        [SerializeField] private PortraitColorModule colors;
        [SerializeField] private PortraitBlink blink;
        [SerializeField] private PortraitRigModule rig;
        public PortraitLayers Layers => layers;
        public PortraitBlink Blink => blink;
        public PortraitRigModule Rig => rig;
        public void Show(CustomerProfile profile, AppearanceValue hair, AppearanceValue eyes, PortraitColorCatalog palette) =>
            Show(profile, hair, eyes, palette, null);

        private void Show(CustomerProfile profile, AppearanceValue hair, AppearanceValue eyes, PortraitColorCatalog palette,
            Sprite expression)
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
            if (expression != null)
            {
                layers.ShowExpression(expression);
                return;
            }
            rig?.Load(profile.RigPrefab, profile.SpineRig);
            blink.Play();
        }
        public void Show(GeneratedOrder order, PortraitColorCatalog palette, Sprite expression = null) => Show(order.Customer,
            order.Portrait.First(p => p.Slot == PortraitSlot.Hair).Appearance,
            order.Portrait.First(p => p.Slot == PortraitSlot.Eyes).Appearance, palette, expression);
        public void Tint(Color tint) => colors.AnimateTint(tint);
        public void SetTint(Color tint) => colors.SetTint(tint);

        internal async UniTask TransformIntoSlime(Sprite sprite, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            bool hadVisiblePortrait = isActiveAndEnabled;
            gameObject.SetActive(true);

            try
            {
                if (hadVisiblePortrait)
                    await colors.FadeTint(SlimeTransitionTint, SlimeTintInSeconds, token);

                token.ThrowIfCancellationRequested();
                ShowSlime(sprite);
                colors.SetTint(SlimeTransitionTint);
                await colors.FadeTint(Color.white, SlimeTintOutSeconds, token);
            }
            finally
            {
                if (colors != null)
                    colors.SetTint(Color.white);
            }
        }

        public void ShowSlime(Sprite sprite)
        {
            if (sprite == null) throw new InvalidOperationException("Assign slime portrait in Customers.");
            blink.Stop();
            if (rig != null) rig.Clear();
            colors.Apply(Color.white, Color.white);
            foreach (var image in layers.All) image.enabled = false;
            if (layers.irisMask != null) layers.irisMask.enabled = false;
            layers.slime.sprite = sprite; layers.slime.color = Color.white; layers.slime.enabled = true;
        }
    }
}
