using System.Linq;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop.Portraits
{
    public sealed class PortraitIrisDetails : MonoBehaviour
    {
        private static readonly int HueShiftId = Shader.PropertyToID("_HueShift");
        [SerializeField] private Material hueShiftTemplate;
        private Material hueMaterial;

        public IrisDetailPair Resolve(CustomerProfile profile) =>
            profile.IrisDetails.FirstOrDefault(pair =>
                pair != null && pair.style == profile.IrisDetailStyle && (pair.left != null || pair.right != null));

        public Material GetHueMaterial(IrisDetailPair detail, Color eyeColor)
        {
            if (detail == null || hueShiftTemplate == null)
            {
                ReleaseMaterial();
                return null;
            }

            Color.RGBToHSV(eyeColor, out float hue, out float saturation, out _);
            float shift = saturation > 0f ? Mathf.DeltaAngle(detail.sourceHueDegrees, hue * 360f) : 0f;
            if (hueMaterial != null && hueMaterial.GetFloat(HueShiftId) == shift)
                return hueMaterial;

            // UGUI Mask caches a stencil copy of each base material.
            // A new base material makes the masked Images pick up the new shift.
            ReleaseMaterial();
            hueMaterial = new Material(hueShiftTemplate) { hideFlags = HideFlags.DontSave };
            hueMaterial.SetFloat(HueShiftId, shift);
            return hueMaterial;
        }

        private void ReleaseMaterial()
        {
            if (hueMaterial == null) return;
            Destroy(hueMaterial);
            hueMaterial = null;
        }

        private void OnDestroy() => ReleaseMaterial();
    }
}
