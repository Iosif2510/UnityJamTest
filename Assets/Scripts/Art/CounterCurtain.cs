using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class CounterCurtain : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer openCurtain;
        [SerializeField] private SpriteRenderer closedCurtain;
        // Where the closed picture splits and where each half is gathered from, in its pixels (x from the left,
        // y from the top): the seam down the middle, the outer edges the halves bunch toward, and the rod's bottom.
        private const float SeamX = 962f, LeftEdgeX = 385f, RightEdgeX = 1560f, BelowRod = 100f;
        // how narrow a half gets once gathered, next to the open picture's tied drapes
        private const float Gathered = 0.3f;

        private SpriteRenderer leftHalf, rightHalf;
        private MotionHandle pull;

        public void SetClosed(bool closed)
        {
            pull.TryCancel();
            HideHalves();
            openCurtain.enabled = !closed;
            closedCurtain.enabled = closed;
            var visible = closed ? closedCurtain : openCurtain;
            var color = visible.color;
            color.a = 1f;
            visible.color = color;
        }

        public UniTask FadeTo(bool closed, CancellationToken token, Action onChanged = null) =>
            SceneDimmer.Instance.Cut(() =>
            {
                SetClosed(closed);
                onChanged?.Invoke();
            }, token);

        /// <summary>
        /// Draws the closed curtain open in view: it parts at the seam and each half slides and bunches up toward
        /// its side, settling into the open picture's tied drapes, with a swish of cloth.
        /// </summary>
        public async UniTask PullOpen(CancellationToken token)
        {
            if (!closedCurtain.enabled)
            {
                SetClosed(false);
                return;
            }
            BuildHalves();
            closedCurtain.enabled = false;
            openCurtain.enabled = true;
            var open = openCurtain.color;
            open.a = 1f;
            openCurtain.color = open;
            leftHalf.gameObject.SetActive(true);
            rightHalf.gameObject.SetActive(true);
            GameSfx.Play("Whoosh", 0.5f, 0.55f, 0.04f);
            pull.TryCancel();
            pull = LMotion.Create(0f, 1f, 0.8f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(t =>
                {
                    // a tug, then the cloth runs along the rod and slows as it bunches
                    float run = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.85f), 3f);
                    float width = Mathf.Lerp(1f, Gathered, run);
                    // the hem swings a little behind the top as the cloth moves
                    float swing = Mathf.Sin(run * Mathf.PI) * 0.04f;
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                    Pose(leftHalf, width, swing, alpha);
                    Pose(rightHalf, width, -swing, alpha);
                }).AddTo(gameObject);
            try { await pull.ToUniTask(token); }
            finally { HideHalves(); }
        }

        private static void Pose(SpriteRenderer half, float width, float swing, float alpha)
        {
            half.transform.localScale = new Vector3(width, 1f + Mathf.Abs(swing) * 0.3f, 1f);
            // swing > 0 pushes the hem right (the left half trailing as it runs left), < 0 the other way
            half.transform.localRotation = Quaternion.Euler(0f, 0f, swing * 20f);
            var c = half.color;
            c.a = alpha;
            half.color = c;
        }

        private void HideHalves()
        {
            if (leftHalf != null) leftHalf.gameObject.SetActive(false);
            if (rightHalf != null) rightHalf.gameObject.SetActive(false);
        }

        private void BuildHalves()
        {
            if (leftHalf != null) return;
            leftHalf = Half("Curtain Left", 0f, SeamX, LeftEdgeX);
            rightHalf = Half("Curtain Right", SeamX, closedCurtain.sprite.rect.width, RightEdgeX);
        }

        /// <summary>
        /// The closed picture's columns <paramref name="from"/>..<paramref name="to"/> below the rod, pivoted on
        /// <paramref name="edge"/> at the rod so it bunches toward that side, placed exactly where it is drawn whole.
        /// </summary>
        private SpriteRenderer Half(string name, float from, float to, float edge)
        {
            var whole = closedCurtain.sprite;
            var full = whole.rect;
            float height = full.height - BelowRod;
            // In play the picture sits trimmed in a sprite atlas: textureRect is where its kept pixels are on the
            // atlas, textureRectOffset where they start in the picture. Crop to what is kept, mapped onto the atlas.
            var kept = whole.textureRect;
            var offset = whole.textureRectOffset;
            float x0 = Mathf.Max(from, offset.x), x1 = Mathf.Min(to, offset.x + kept.width);
            float y0 = Mathf.Max(0f, offset.y), y1 = Mathf.Min(height, offset.y + kept.height);
            var area = new Rect(kept.x + x0 - offset.x, kept.y + y0 - offset.y, x1 - x0, y1 - y0);
            var pivot = new Vector2((edge - x0) / (x1 - x0), (height - y0) / (y1 - y0));
            // keep the picture's normal and mask maps, or the 2D lights draw the cut-out halves almost black
            var secondary = new SecondarySpriteTexture[whole.GetSecondaryTextureCount()];
            whole.GetSecondaryTextures(secondary);
            var sprite = Sprite.Create(whole.texture, area, pivot, whole.pixelsPerUnit, 0, SpriteMeshType.Tight,
                Vector4.zero, false, secondary);

            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(closedCurtain.transform, false);
            // the pivot's spot in the whole picture, relative to the whole picture's own pivot
            go.transform.localPosition = new Vector3(edge - whole.pivot.x, height - whole.pivot.y, 0f) / whole.pixelsPerUnit;
            var half = go.GetComponent<SpriteRenderer>();
            half.sprite = sprite;
            half.sortingLayerID = closedCurtain.sortingLayerID;
            half.sortingOrder = closedCurtain.sortingOrder;
            half.sharedMaterial = closedCurtain.sharedMaterial;
            go.SetActive(false);
            return half;
        }
    }
}
