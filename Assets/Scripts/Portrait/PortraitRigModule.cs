using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Portraits
{
    public sealed class PortraitRigModule : MonoBehaviour
    {
        [SerializeField] private PortraitLayers layers;
        private PortraitRigDefinition rig;
        private Matrix4x4 headBindInverse;
        private GameObject instance;
        public bool Ready => rig != null && rig.baseSkin != null && rig.animator != null && rig.animator.runtimeAnimatorController != null;
        public void Load(GameObject prefab)
        {
            Clear();
            if (prefab == null) return;
            instance = Instantiate(prefab, transform, false);
            rig = instance.GetComponent<PortraitRigDefinition>();
            if (rig == null) { Debug.LogError("Rig prefab requires PortraitRigDefinition", prefab); Clear(); return; }
            Bind(layers.baseImage, rig.baseSkin); Bind(layers.eyeClosed, rig.closedEyeSkin);
            Bind(layers.hairFront, rig.frontHairSkin); Bind(layers.hairBack, rig.backHairSkin);
            if (rig.headBone != null) headBindInverse = (rig.transform.worldToLocalMatrix * rig.headBone.localToWorldMatrix).inverse;
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true)) renderer.forceRenderingOff = true;
        }
        private static void Bind(Image image, UnityEngine.U2D.Animation.SpriteSkin skin)
        { if (image is PortraitSkinnedImage output) output.Bind(skin); }
        public void Play() { if (rig != null && rig.animator != null) { rig.animator.enabled = true; rig.animator.speed = 1; } }
        public void Stop() { if (rig != null && rig.animator != null) rig.animator.speed = 0; }
        public void Clear()
        {
            Bind(layers.baseImage, null); Bind(layers.eyeClosed, null); Bind(layers.hairFront, null); Bind(layers.hairBack, null);
            foreach (var image in new[] { layers.irisLeft, layers.irisRight })
            { image.rectTransform.anchoredPosition = Vector2.zero; image.rectTransform.localRotation = Quaternion.identity; image.rectTransform.localScale = Vector3.one; }
            if (instance != null) { instance.SetActive(false); Destroy(instance); }
            instance = null; rig = null;
        }
        private void LateUpdate()
        {
            if (rig == null || rig.headBone == null || layers.baseImage.sprite == null) return;
            var delta = rig.transform.worldToLocalMatrix * rig.headBone.localToWorldMatrix * headBindInverse;
            var sprite = layers.baseImage.sprite;
            foreach (var image in new[] { layers.irisLeft, layers.irisRight })
            {
                var rt = image.rectTransform; var anchor = (rt.anchorMin + rt.anchorMax) * .5f;
                var point = (Vector2.Scale(anchor, sprite.rect.size) - sprite.pivot) / sprite.pixelsPerUnit;
                var offset = (Vector2)delta.MultiplyPoint3x4(point) - point;
                var size = ((RectTransform)rt.parent).rect.size;
                rt.anchoredPosition = Vector2.Scale(offset * sprite.pixelsPerUnit, new Vector2(size.x / sprite.rect.width, size.y / sprite.rect.height));
                rt.localRotation = delta.rotation; rt.localScale = delta.lossyScale;
            }
        }
        private void OnDisable() => Stop();
        private void OnDestroy() { if (instance != null) Destroy(instance); }
    }
}
