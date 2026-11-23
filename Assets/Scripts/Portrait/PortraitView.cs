using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop.Portraits
{
    public sealed class PortraitView : MonoBehaviour
    {
        private static readonly Color SlimeTransitionTint = new(0.49f, 1f, 0.53f);

        [SerializeField] private PortraitLayers layers;
        [SerializeField] private PortraitColorModule colors;
        [SerializeField] private PortraitBlink blink;
        [SerializeField] private PortraitRigModule rig;
        private MotionHandle landingMotion, speechMotion;
        private SlimeBurstGraphic slimeBurst;
        private Quaternion restingRotation;
        private float landingProgress = 1f;
        private float speech;
        private bool poseInitialized;
        private int speechDirection = -1;
        private float impatience;
        private float fidgetSince = -10f, nextFidget;
        private float look, lookTarget;
        public PortraitLayers Layers => layers;
        public PortraitBlink Blink => blink;
        public PortraitRigModule Rig => rig;
        public void Show(CustomerProfile profile, AppearanceValue hair, AppearanceValue eyes, PortraitColorCatalog palette) =>
            Show(profile, hair, eyes, palette, null);

        private void Show(CustomerProfile profile, AppearanceValue hair, AppearanceValue eyes, PortraitColorCatalog palette,
            Sprite expression)
        {
            if (profile == null || palette == null) throw new ArgumentNullException("Portrait profile/palette");
            if ((hair != AppearanceValue.None && !profile.HairColors.Contains(hair)) ||
                (hair == AppearanceValue.None && profile.HairColors.Count > 0) || !profile.EyeColors.Contains(eyes))
                throw new InvalidOperationException($"{profile.Id}: unsupported portrait colors");
            blink.Stop();
            rig?.Clear();
            layers.Compose(profile);
            var hairTint = profile.UseAccessoryTint ? profile.AccessoryTint : palette.Get(PortraitSlot.Hair, hair);
            colors.Apply(hairTint, palette.Get(PortraitSlot.Eyes, eyes));
            if (expression != null)
            {
                layers.ShowExpression(expression, profile.AngryExpressionAboveHair && expression == profile.AngryExpression);
                return;
            }
            rig?.Load(profile.RigPrefab, profile.SpineRig);
            blink.Play();
        }
        public void Show(GeneratedOrder order, PortraitColorCatalog palette, Sprite expression = null) => Show(order.Customer,
            order.Portrait.First(p => p.Slot == PortraitSlot.Hair).Appearance,
            order.Portrait.First(p => p.Slot == PortraitSlot.Eyes).Appearance, palette, expression);
        public void Tint(Color tint) => colors.AnimateTint(tint);
        public void SetTint(Color tint) => colors.SetTint(tint);

        internal async UniTask SlideIn(CanvasGroup opacity, CancellationToken token)
        {
            var rect = (RectTransform)transform;
            var parent = (RectTransform)rect.parent;
            var canvas = GetComponentInParent<Canvas>();
            var screenEdge = new Vector2(Screen.width, Screen.height * .5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenEdge, canvas.worldCamera, out var edge);
            var rest = rect.anchoredPosition;
            float distance = Mathf.Max(rect.rect.width, edge.x - rect.localPosition.x + rect.rect.width * .85f);
            rect.anchoredPosition = rest + Vector2.right * distance;
            opacity.alpha = 1f;
            try
            {
                await LMotion.Create(distance, 0f, .52f).WithEase(Ease.OutCubic)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(x => rect.anchoredPosition = rest + Vector2.right * x).ToUniTask(token);
                CaptureRotation();
                landingMotion.TryCancel();
                landingMotion = LMotion.Create(0f, 1f, .42f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(progress =>
                    {
                        landingProgress = progress;
                        ApplyGesture();
                    });
            }
            finally { if (rect != null) rect.anchoredPosition = rest; }
        }

        private SlimeJelly jelly;
        private SlimeJelly Jelly => jelly != null ? jelly
            : jelly = layers.slime.GetComponent<SlimeJelly>() ?? layers.slime.gameObject.AddComponent<SlimeJelly>();

        internal void Speak(float strength)
        {
            if (layers.slime != null && layers.slime.enabled) Jelly.Jiggle(0.5f * strength);
            CaptureRotation();
            speechMotion.TryCancel();
            speechDirection *= -1;
            speechMotion = LMotion.Create(0f, 1f, .43f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(progress =>
                {
                    speech = Mathf.Sin(progress * Mathf.PI * 2f) * (1f - progress) * strength * speechDirection;
                    ApplyGesture();
                });
        }

        private void CaptureRotation()
        {
            if (poseInitialized) return;
            restingRotation = transform.localRotation;
            poseInitialized = true;
        }

        private void ApplyGesture()
        {
            bool hasSpine = rig != null && rig.SpineRig != null;
            rig?.SetGesture(landingProgress, speech);
            float angle = (LandingSwing(landingProgress, 0) * 13 + speech * 19) * (hasSpine ? .45f : 1f);
            transform.localRotation = restingRotation * Quaternion.Euler(0, 0, angle + ImpatienceAngle() - look * 6f);
        }

        /// <summary>
        /// How restless the customer is about the time left (0 calm, 1 cross): the head tilts to one side and sways,
        /// and every few seconds the body gives a short impatient jiggle, more often the crosser they are.
        /// </summary>
        public void SetImpatience(float level)
        {
            level = Mathf.Clamp01(level);
            if (Mathf.Approximately(level, impatience)) return;
            CaptureRotation();
            if (impatience <= 0f) nextFidget = Time.unscaledTime + .4f;
            impatience = level;
            ApplyGesture();
        }

        private float ImpatienceAngle()
        {
            if (impatience <= 0f) return 0f;
            float t = Time.unscaledTime;
            float tilt = -4.5f * impatience + Mathf.Sin(t * 1.3f) * 1.2f * impatience;
            float age = t - fidgetSince;
            float fidget = age < .5f ? Mathf.Sin(age * 34f) * (1f - age / .5f) * 3.2f * impatience : 0f;
            return tilt + fidget;
        }

        /// <summary>
        /// Leans towards something the player is pointing at: -1 far left, 1 far right, 0 back upright.
        /// </summary>
        public void LookToward(float side)
        {
            CaptureRotation();
            lookTarget = Mathf.Clamp(side, -1f, 1f);
        }

        private void LateUpdate()
        {
            if (!poseInitialized) return;
            bool looking = !Mathf.Approximately(look, lookTarget) || Mathf.Abs(look) > 0.001f;
            if (looking)
            {
                look = Mathf.MoveTowards(Mathf.Lerp(look, lookTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 7f)),
                    lookTarget, Time.unscaledDeltaTime * 0.05f);
                if (impatience <= 0f)
                {
                    ApplyGesture();
                    return;
                }
            }
            if (impatience <= 0f) return;
            if (Time.unscaledTime >= nextFidget)
            {
                fidgetSince = Time.unscaledTime;
                nextFidget = fidgetSince + UnityEngine.Random.Range(1.6f, 2.8f) / Mathf.Lerp(1f, 1.6f, impatience);
                if (layers.slime != null && layers.slime.enabled) Jelly.Jiggle(.3f * impatience);
            }
            ApplyGesture();
        }

        internal static float LandingSwing(float progress, float delay)
        {
            if (progress < delay) return 0f;
            float t = Mathf.InverseLerp(delay, 1f, progress);
            return Mathf.Cos(t * Mathf.PI * 3.3f) * (1f - t) * (1f - t);
        }

        private void OnDisable() => ResetGesture();

        private void ResetGesture()
        {
            landingMotion.TryCancel();
            speechMotion.TryCancel();
            landingProgress = 1f;
            speech = 0f;
            impatience = 0f;
            look = lookTarget = 0f;
            if (poseInitialized) ApplyGesture();
        }

        internal async UniTask TransformIntoSlime(Sprite sprite, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            bool hadVisiblePortrait = isActiveAndEnabled;
            gameObject.SetActive(true);
            ResetGesture();
            var rest = transform.localScale;

            try
            {
                if (hadVisiblePortrait)
                {
                    await UniTask.WhenAll(colors.FadeTint(SlimeTransitionTint, .20f, token),
                        LMotion.Create(0f, 1f, .20f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                            .WithEase(Ease.InQuad).Bind(t => transform.localScale = Vector3.Scale(rest,
                                new Vector3(1 + .23f * t, 1 - .31f * t, 1))).ToUniTask(token));
                }

                token.ThrowIfCancellationRequested();
                // The pop: a sound and a held beat on the squashed pose before the slime bursts out.
                GameSfx.Play("SlimePop", 0.9f, 1f, 0.05f);
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.06), ignoreTimeScale: true, cancellationToken: token);
                ShowSlime(sprite);
                colors.SetTint(SlimeTransitionTint);
                var burst = GetSlimeBurst();
                burst.SetProgress(0);
                burst.gameObject.SetActive(true);
                await UniTask.WhenAll(colors.FadeTint(Color.white, .42f, token),
                    LMotion.Create(0f, 1f, .42f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                        .Bind(t =>
                        {
                            float bounce = Mathf.Cos(t * Mathf.PI * 3f) * (1 - t) * (1 - t);
                            transform.localScale = Vector3.Scale(rest, new Vector3(1 + .32f * bounce, 1 - .19f * bounce, 1));
                            burst.SetProgress(t);
                        }).ToUniTask(token));
            }
            finally
            {
                if (this != null) transform.localScale = rest;
                if (slimeBurst != null) slimeBurst.gameObject.SetActive(false);
                if (colors != null)
                    colors.SetTint(Color.white);
            }
        }

        private SlimeBurstGraphic GetSlimeBurst()
        {
            if (slimeBurst != null) return slimeBurst;
            var effect = new GameObject("Slime Burst", typeof(RectTransform));
            var rect = (RectTransform)effect.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            slimeBurst = effect.AddComponent<SlimeBurstGraphic>();
            slimeBurst.raycastTarget = false;
            return slimeBurst;
        }

        public void ShowSlime(Sprite sprite)
        {
            if (sprite == null) throw new InvalidOperationException("Assign slime portrait in Customers.");
            blink.Stop();
            if (rig != null) rig.Clear();
            colors.Apply(Color.white, Color.white);
            foreach (var image in layers.All) image.enabled = false;
            if (layers.irisMask != null) layers.irisMask.enabled = false;
            layers.slime.sprite = sprite; layers.slime.color = Color.white; layers.slime.enabled = true;
            // the slime's own rig: planted, breathing jelly that jiggles as it appears and when it talks
            Jelly.Jiggle(1f);
        }
    }
}
