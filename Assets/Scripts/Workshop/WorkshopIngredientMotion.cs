using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

namespace PotionShop
{
    internal static class WorkshopIngredientMotion
    {
        internal const float ThrowInterval = .20f;

        internal static async UniTask Store(WarehouseIngredient ingredient, Camera view, CancellationToken token)
        {
            ingredient.StopTransfer();
            var model = ingredient.transform;
            var originalPosition = model.localPosition;
            var originalScale = model.localScale;
            var from = view.WorldToViewportPoint(model.position);
            from.z = Mathf.Max(from.z, view.nearClipPlane + .12f);
            var below = new Vector3(.5f, -.25f, from.z);
            try
            {
                await LMotion.Create(0f, 1f, .26f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithEase(Ease.InQuad).Bind(t =>
                    {
                        if (model == null || view == null) return;
                        model.position = view.ViewportToWorldPoint(Vector3.LerpUnclamped(from, below, t));
                        model.localScale = originalScale * Mathf.LerpUnclamped(1f, .18f, t);
                    }).ToUniTask(token);
                token.ThrowIfCancellationRequested();
                ingredient.gameObject.SetActive(false);
            }
            finally
            {
                if (model != null)
                {
                    model.localPosition = originalPosition;
                    model.localScale = originalScale;
                }
            }
        }

        private static Material trailMaterial;

        /// <summary>A short soft streak behind a thrown ingredient.</summary>
        private static TrailRenderer Trail(Transform model)
        {
            var go = new GameObject("ThrowTrail");
            go.transform.SetParent(model, false);
            var trail = go.AddComponent<TrailRenderer>();
            if (trailMaterial == null)
                trailMaterial = new Material(Shader.Find("Sprites/Default"));
            trail.sharedMaterial = trailMaterial;
            trail.time = .14f;
            trail.minVertexDistance = .02f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, .07f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, .97f, .88f), 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.45f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        internal static async UniTask Throw(WarehouseIngredient ingredient, Camera view, Vector3 end,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var model = ingredient.transform;
            var rest = model.localScale;
            var originalRotation = model.rotation;
            var start = view.ViewportToWorldPoint(new Vector3(.5f, -.16f, Mathf.Max(.72f, view.nearClipPlane + .3f)));
            float height = Mathf.Clamp(Vector3.Distance(start, end) * .27f, .4f, .95f);
            model.position = start;
            model.localScale = rest * .72f;
            ingredient.gameObject.SetActive(true);
            GameSfx.Play("Whoosh", .7f, 1f, .1f);
            var trail = Trail(model);
            try
            {
                await LMotion.Create(0f, 1f, .36f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        if (model == null) return;
                        var arc = Vector3.up * (4f * height * t * (1f - t));
                        model.position = Vector3.LerpUnclamped(start, end, t) + arc;
                        model.rotation = originalRotation * Quaternion.Euler(t * 280f, t * 130f, t * 80f);
                        model.localScale = rest * Mathf.LerpUnclamped(.72f, 1f, t);
                    }).ToUniTask(token);
            }
            finally
            {
                if (trail != null) UnityEngine.Object.Destroy(trail.gameObject);
                if (model != null)
                {
                    model.gameObject.SetActive(false);
                    model.localScale = rest;
                    model.rotation = originalRotation;
                }
            }
        }
    }
}
