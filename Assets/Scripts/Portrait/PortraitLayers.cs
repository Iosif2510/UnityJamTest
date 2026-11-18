using System.Collections.Generic;
using PotionShop.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Portraits
{
    public sealed class PortraitLayers : MonoBehaviour
    {
        public Image hairBack;
        public Image irisLeft;
        public Image irisRight;
        public Image baseImage;
        public Image eyeClosed;
        public Image hairFront;
        public Image slime;
        public Image irisMask;
        public PortraitIrisDetails irisDetails;
        public IrisDetailPair ActiveIrisDetail { get; private set; }
        public IEnumerable<Image> All => new[] { hairBack, irisLeft, irisRight, baseImage, eyeClosed, hairFront, slime };

        public void Compose(CustomerProfile profile)
        {
            Set(hairBack, profile.HairBack); Set(baseImage, profile.BasePortrait);
            Set(eyeClosed, profile.EyeClosed); Set(hairFront, profile.HairFront);
            eyeClosed.enabled = false;
            slime.enabled = false;
            PlaceIris(irisLeft, profile.IrisRegions, 0);
            PlaceIris(irisRight, profile.IrisRegions, 1);
            ActiveIrisDetail = irisDetails != null ? irisDetails.Resolve(profile) : null;
            irisLeft.sprite = ActiveIrisDetail?.left;
            irisRight.sprite = ActiveIrisDetail?.right;
            if (irisMask != null)
            {
                var maskSprite = ActiveIrisDetail != null ? profile.IrisVariantsMask : profile.IrisMask;
                if (maskSprite == null) maskSprite = profile.IrisMask;
                irisMask.sprite = maskSprite;
                irisMask.GetComponent<Mask>().enabled = maskSprite != null;
                irisMask.enabled = maskSprite != null;
            }
        }

        private static void Set(Image image, Sprite sprite)
        { image.sprite = sprite; image.enabled = sprite != null; image.raycastTarget = false; }

        private static void PlaceIris(Image image, IReadOnlyList<Rect> regions, int index)
        {
            image.enabled = index < regions.Count;
            image.raycastTarget = false;
            if (!image.enabled) return;
            var r = regions[index];
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(r.xMin, r.yMin); rt.anchorMax = new Vector2(r.xMax, r.yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
