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
        public bool UsesBakedIris { get; private set; }
        public IEnumerable<Image> All => new[] { hairBack, irisLeft, irisRight, baseImage, eyeClosed, hairFront, slime };
        private int? eyeClosedSiblingIndex;

        public void Compose(CustomerProfile profile)
        {
            RestoreEyeLayerOrder();
            Set(hairBack, profile.HairBack); Set(baseImage, profile.BasePortrait);
            Set(eyeClosed, profile.EyeClosed); Set(hairFront, profile.HairFront);
            eyeClosed.enabled = false;
            slime.enabled = false;
            UsesBakedIris = irisMask != null && profile.BakedIris != null;
            if (UsesBakedIris)
            {
                ActiveIrisDetail = null;
                Set(irisLeft, null);
                Set(irisRight, null);
                irisMask.GetComponent<Mask>().enabled = false;
                Set(irisMask, profile.BakedIris);
                return;
            }
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

        public void ShowExpression(Sprite sprite, bool aboveHair = false)
        {
            RestoreEyeLayerOrder();
            Set(eyeClosed, sprite);
            if (!aboveHair || sprite == null || hairFront == null || eyeClosed.transform.parent != hairFront.transform.parent)
                return;

            var eyeIndex = eyeClosed.transform.GetSiblingIndex();
            var hairIndex = hairFront.transform.GetSiblingIndex();
            if (eyeIndex < hairIndex)
            {
                eyeClosedSiblingIndex = eyeIndex;
                eyeClosed.transform.SetSiblingIndex(hairIndex);
            }
        }

        private void RestoreEyeLayerOrder()
        {
            if (!eyeClosedSiblingIndex.HasValue) return;
            eyeClosed.transform.SetSiblingIndex(eyeClosedSiblingIndex.Value);
            eyeClosedSiblingIndex = null;
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
