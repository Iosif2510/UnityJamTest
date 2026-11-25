using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PotionShop
{
    /// <summary>
    /// Draws the held ingredient on top of the scene so it never sinks into a shelf or wall when the player walks
    /// up close. The ingredient's renderers move to the HeldItem layer, which the player camera skips and an
    /// overlay camera stacked on it draws after clearing depth.
    /// </summary>
    internal sealed class HeldItemOverlay
    {
        private const string LayerName = "HeldItem";

        private readonly Camera baseCamera;
        private readonly Camera overlayCamera;
        private readonly int layer;
        private readonly List<(GameObject target, int layer)> restore = new();

        internal HeldItemOverlay(Camera playerCamera)
        {
            baseCamera = playerCamera;
            layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"Layer '{LayerName}' is missing; the held ingredient is drawn normally.", playerCamera);
                return;
            }

            var baseData = playerCamera.GetUniversalAdditionalCameraData();
            if (baseData == null || baseData.renderType != CameraRenderType.Base) return;

            var go = new GameObject("HeldItemCamera", typeof(Camera));
            go.transform.SetParent(playerCamera.transform, false);
            overlayCamera = go.GetComponent<Camera>();
            overlayCamera.cullingMask = 1 << layer;
            var data = overlayCamera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderShadows = false;
            data.renderPostProcessing = baseData.renderPostProcessing;
            baseData.cameraStack.Add(overlayCamera);
            playerCamera.cullingMask &= ~(1 << layer);
            SyncLens();
        }

        /// <summary>Keeps the overlay's lens in step with the player camera (field of view can change).</summary>
        internal void SyncLens()
        {
            if (overlayCamera == null) return;
            overlayCamera.fieldOfView = baseCamera.fieldOfView;
            overlayCamera.nearClipPlane = baseCamera.nearClipPlane;
            overlayCamera.farClipPlane = baseCamera.farClipPlane;
        }

        internal void Show(Component item)
        {
            Clear();
            if (overlayCamera == null || item == null) return;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                var target = renderer.gameObject;
                restore.Add((target, target.layer));
                target.layer = layer;
            }
        }

        /// <summary>Puts the ingredient back on its own layers, so it is drawn in the scene again.</summary>
        internal void Clear()
        {
            foreach (var (target, original) in restore)
                if (target != null) target.layer = original;
            restore.Clear();
        }
    }
}
