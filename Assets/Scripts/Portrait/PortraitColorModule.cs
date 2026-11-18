using PotionShop.Data;
using UnityEngine;
using LitMotion;

namespace PotionShop.Portraits
{
    public sealed class PortraitColorModule : MonoBehaviour
    {
        [SerializeField] private PortraitLayers layers;
        private Color hair = Color.white, eyes = Color.white;
        private Color currentTint = Color.white;
        private MotionHandle tintMotion;
        public void Apply(Color hairColor, Color eyeColor)
        { tintMotion.TryCancel(); hair = hairColor; eyes = eyeColor; SetTint(Color.white); }
        public void AnimateTint(Color tint)
        {
            tintMotion.TryCancel(); var initial = currentTint;
            tintMotion = LMotion.Create(0f, 1f, .35f).WithEase(Ease.OutQuad).Bind(t => SetTint(Color.Lerp(initial, tint, t)));
        }
        public void SetTint(Color tint)
        {
            currentTint = tint;
            layers.hairBack.color = layers.hairFront.color = hair * tint;
            layers.irisLeft.color = layers.irisRight.color = eyes * tint;
            layers.baseImage.color = layers.eyeClosed.color = layers.slime.color = tint;
        }
        private void OnDisable() => tintMotion.TryCancel();
    }
}
