using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PotionShop
{
    internal sealed class PocketModelDisplay : IDisposable
    {
        private const float SlotSize = 72f;
        private const float SlotGap = 8f;
        private const float LabelWidth = 76f;
        private const int TextureSize = 128;

        private sealed class Slot
        {
            public Image Frame;
            public RawImage Preview;
            public RenderTexture Texture;
        }

        private readonly Slot[] slots;
        private readonly Camera previewCamera;
        private readonly Vector3 previewPosition;
        private readonly PotionShop.Art.UITheme theme;

        public PocketModelDisplay(Text pocketLabel, Camera playerCamera, int capacity, PotionShop.Art.UITheme theme = null)
        {
            this.theme = theme;
            pocketLabel.text = "Pocket";
            pocketLabel.alignment = TextAnchor.MiddleLeft;
            pocketLabel.rectTransform.sizeDelta =
                new Vector2(LabelWidth + capacity * (SlotSize + SlotGap) - SlotGap, SlotSize);

            slots = new Slot[capacity];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = CreateSlot(pocketLabel, i);

            // Keep the render-only mesh beyond the gameplay camera's far plane.
            previewPosition = playerCamera.transform.position + Vector3.down * (playerCamera.farClipPlane + 512f);
            previewCamera = new GameObject("PocketPreviewCamera").AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color32(24, 31, 32, 255);
            if (theme != null) previewCamera.backgroundColor = theme.pocketBackground;
            previewCamera.allowHDR = false;
            previewCamera.useOcclusionCulling = false;
            var cameraData = previewCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
        }

        public void ShowIngredient(int index, Transform ingredient)
        {
            Slot slot = slots[index];
            slot.Frame.color = new Color32(168, 217, 177, 255);
            if (theme != null) slot.Frame.color = theme.pocketFilled;

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
            {
                ReleaseTexture(slot);
                if (slot.Frame != null)
                    slot.Frame.color = theme != null ? theme.pocketEmpty : new Color32(39, 49, 50, 255);
            }
        }

        public void Dispose()
        {
            Clear();
            if (previewCamera != null)
                Object.Destroy(previewCamera.gameObject);

            foreach (var slot in slots)
                if (slot.Frame != null)
                    Object.Destroy(slot.Frame.gameObject);
        }

        private Slot CreateSlot(Text label, int index)
        {
            var frame = new GameObject($"PocketSlot_{index + 1}", typeof(RectTransform), typeof(Image))
                .GetComponent<Image>();
            var rect = frame.rectTransform;
            rect.SetParent(label.rectTransform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);
            rect.anchoredPosition = new Vector2(LabelWidth + index * (SlotSize + SlotGap), 0f);
            frame.color = new Color32(39, 49, 50, 255);
            if (theme != null) { frame.color = theme.pocketEmpty; frame.sprite = theme.pocketFrame; frame.type = Image.Type.Sliced; }
            frame.raycastTarget = false;

            var preview = new GameObject("Model", typeof(RectTransform), typeof(RawImage))
                .GetComponent<RawImage>();
            FitInside(preview.rectTransform, rect, 4f);
            preview.raycastTarget = false;
            preview.enabled = false;

            return new Slot { Frame = frame, Preview = preview };
        }

        private static void FitInside(RectTransform rect, RectTransform parent, float padding)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = -rect.offsetMin;
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
