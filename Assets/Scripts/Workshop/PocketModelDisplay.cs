using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PotionShop
{
    internal sealed class PocketModelDisplay : IDisposable
    {
        private const int TextureSize = 128;

        private sealed class Slot
        {
            public UnityEngine.UI.RawImage Preview;
            public RenderTexture Texture;
        }

        private readonly Slot[] slots;
        private readonly Camera previewCamera;
        private readonly Vector3 previewPosition;

        public PocketModelDisplay(UnityEngine.UI.RawImage[] previews, Camera playerCamera)
        {
            slots = new Slot[previews.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                previews[i].enabled = false;
                slots[i] = new Slot { Preview = previews[i] };
            }

            // Keep the render-only mesh beyond the gameplay camera's far plane.
            previewPosition = playerCamera.transform.position + Vector3.down * (playerCamera.farClipPlane + 512f);
            previewCamera = new GameObject("PocketPreviewCamera").AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color32(25, 15, 10, 255);
            previewCamera.allowHDR = false;
            previewCamera.useOcclusionCulling = false;
            var cameraData = previewCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
        }

        public void ShowIngredient(int index, Transform ingredient)
        {
            Slot slot = slots[index];
            var model = new GameObject("PocketPreviewMesh");
            model.transform.position = previewPosition;
            model.transform.localScale = ingredient.localScale;
            var bounds = new Bounds();
            bool hasMesh = false;
            try
            {
                CopyMeshes(ingredient, model.transform, ref bounds, ref hasMesh);
                if (!hasMesh)
                    return;

                float radius = Mathf.Max(0.02f, bounds.extents.magnitude);
                float distance = 0.35f + radius * 3f;
                var rotation = Quaternion.Euler(16f, -33f, 0f);
                previewCamera.transform.SetPositionAndRotation(
                    bounds.center - rotation * Vector3.forward * distance, rotation);
                previewCamera.orthographicSize = radius * 1.12f;
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = distance + radius + 0.5f;

                var texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"Pocket preview {index + 1}",
                    filterMode = FilterMode.Bilinear
                };
                slot.Texture = texture;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
                if (!RenderPipeline.SupportsRenderRequest(previewCamera, request) || !texture.Create())
                    return;

                RenderPipeline.SubmitRenderRequest(previewCamera, request);
                slot.Preview.texture = texture;
                slot.Preview.enabled = true;
            }
            finally
            {
                if (slot.Preview.texture == null)
                    ReleaseTexture(slot);
                model.SetActive(false);
                Object.Destroy(model);
            }
        }

        public void Clear()
        {
            foreach (var slot in slots)
                ReleaseTexture(slot);
        }

        public void Dispose()
        {
            Clear();
            if (previewCamera != null)
                Object.Destroy(previewCamera.gameObject);
        }

        private static void CopyMeshes(Transform source, Transform clone, ref Bounds bounds, ref bool hasMesh)
        {
            if (source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                source.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled)
            {
                clone.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = clone.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
                if (hasMesh)
                    bounds.Encapsulate(copy.bounds);
                else
                    bounds = copy.bounds;
                hasMesh = true;
            }

            foreach (Transform child in source)
            {
                if (!child.gameObject.activeSelf)
                    continue;

                var visual = new GameObject(child.name).transform;
                visual.SetParent(clone, false);
                visual.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
                visual.localScale = child.localScale;
                CopyMeshes(child, visual, ref bounds, ref hasMesh);
            }
        }

        private static void ReleaseTexture(Slot slot)
        {
            if (slot.Preview != null)
            {
                slot.Preview.enabled = false;
                slot.Preview.texture = null;
            }
            if (slot.Texture == null)
                return;
            slot.Texture.Release();
            Object.Destroy(slot.Texture);
            slot.Texture = null;
        }
    }
}
