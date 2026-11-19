using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LitMotion;

namespace PotionShop.Portraits
{
    public sealed class PortraitColorModule : MonoBehaviour
    {
        [SerializeField] private PortraitLayers layers;
        private Color hair = Color.white;
        private Color leftIris = Color.white, rightIris = Color.white;
        private Color bakedIris = Color.white;
        private Color currentTint = Color.white;
        private MotionHandle tintMotion;
        public void Apply(Color hairColor, Color eyeColor)
        {
            tintMotion.TryCancel();
            hair = hairColor;
            bakedIris = eyeColor;
            var material = layers.irisDetails != null
                ? layers.irisDetails.GetHueMaterial(layers.ActiveIrisDetail, eyeColor)
                : null;
            leftIris = ApplyIrisColor(layers.irisLeft, material, eyeColor);
            rightIris = ApplyIrisColor(layers.irisRight, material, eyeColor);
            SetTint(Color.white);
        }

        private static Color ApplyIrisColor(UnityEngine.UI.Image image, Material material, Color eyeColor)
        {
            bool hasHueShift = image.sprite != null && material != null;
            image.material = hasHueShift ? material : null;
            return hasHueShift ? new Color(1f, 1f, 1f, eyeColor.a) : eyeColor;
        }
        public void AnimateTint(Color tint) => StartTint(tint, .35f, MotionScheduler.Update);

        internal UniTask FadeTint(Color tint, float duration, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return StartTint(tint, duration, MotionScheduler.UpdateIgnoreTimeScale).ToUniTask(token);
        }

        private MotionHandle StartTint(Color tint, float duration, IMotionScheduler scheduler)
        {
            tintMotion.TryCancel();
            Color initial = currentTint;
            tintMotion = LMotion.Create(0f, 1f, duration)
                .WithScheduler(scheduler)
                .WithEase(Ease.OutQuad)
                .Bind(t => SetTint(Color.Lerp(initial, tint, t)));
            return tintMotion;
        }
        public void SetTint(Color tint)
        {
            currentTint = tint;
            layers.hairBack.color = layers.hairFront.color = hair * tint;
            layers.irisLeft.color = leftIris * tint;
            layers.irisRight.color = rightIris * tint;
            if (layers.irisMask != null)
                layers.irisMask.color = layers.UsesBakedIris ? bakedIris * tint : Color.white;
            layers.baseImage.color = layers.eyeClosed.color = layers.slime.color = tint;
        }
        private void OnDisable() => tintMotion.TryCancel();
    }
}
