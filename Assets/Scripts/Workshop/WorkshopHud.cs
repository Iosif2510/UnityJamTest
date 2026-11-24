using System;
using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using PotionShop.Data;
using TMPro;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WorkshopHud : MonoBehaviour
    {
        [SerializeField] private GameObject timer;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private UnityEngine.UI.Image timerFill;
        [SerializeField] private TextMeshProUGUI promptTitle;
        [SerializeField] private TextMeshProUGUI promptAction;
        [SerializeField] private TextMeshProUGUI keyText;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private UnityEngine.UI.RawImage[] pocketPreviews;
        [SerializeField] private IngredientCatalog ingredients;
        [Header("Prompt icons (drawn instead of the key text)")]
        [SerializeField] private Sprite leftClickIcon;
        [SerializeField] private Sprite rightClickIcon;
        [SerializeField] private Sprite leftDragIcon;

        private readonly Dictionary<string, string> kindNamesByVariant = new(StringComparer.Ordinal);
        private Art.UiTypography typography;
        private WorkshopPromptRows promptRows;
        private MotionHandle timerSway;
        private MotionHandle pocketPop;
        private int displayedSeconds = -1;
        private TimerUrgency urgency;

        private void Awake()
        {
            promptRows = new WorkshopPromptRows(promptAction, keyText, new Dictionary<string, Sprite>
            {
                ["좌클릭"] = leftClickIcon, ["우클릭"] = rightClickIcon, ["좌클릭 드래그"] = leftDragIcon,
            });
            typography = new Art.UiTypography();
            typography.Apply(transform);
            foreach (var kind in ingredients.Kinds)
            {
                if (kind.Status != ContentStatus.Active)
                    continue;

                foreach (var variant in kind.Variants)
                    // only the kind shows: which of its three it is, the player tells by its look and the memos
                    kindNamesByVariant.Add(variant.Id, $"이름 모를 {kind.DisplayName}");
            }
        }

        private void OnDestroy() => typography?.Dispose();

        internal PocketModelDisplay CreatePocketDisplay(Camera playerCamera)
        {
            SetPocketVisible(false);
            return new PocketModelDisplay(pocketPreviews, playerCamera);
        }

        /// <summary>The slot row (PocketDisplay) appears only once something is in it, with a small pop.</summary>
        internal void SetPocketVisible(bool visible)
        {
            if (pocketPreviews.Length == 0) return;
            var row = pocketPreviews[0].transform.parent.parent;
            if (row.gameObject.activeSelf == visible) return;
            pocketPop.TryCancel();
            row.gameObject.SetActive(visible);
            if (visible)
                pocketPop = LMotion.Create(Vector3.one * 0.8f, Vector3.one, 0.22f).WithEase(Ease.OutBack)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).BindToLocalScale(row);
        }

        /// <summary>
        /// An ingredient went into slot <paramref name="index"/>: that slot pops with a rising note. When the last
        /// slot fills, every slot frame glints gold one after another.
        /// </summary>
        internal void PopSlot(int index, bool full)
        {
            if (index < 0 || index >= pocketPreviews.Length) return;
            var slot = pocketPreviews[index].transform.parent;
            if (!slot.gameObject.activeInHierarchy) return;
            OrderHudMotion.Bounce(slot, 1.3f);
            GameSfx.Play("Blip", 0.55f, 1f + index * 0.12f, 0.02f);
            if (!full) return;
            for (int i = 0; i < pocketPreviews.Length; i++)
            {
                var frame = pocketPreviews[i].transform.parent.GetComponent<UnityEngine.UI.Graphic>();
                if (frame == null) continue;
                var rest = frame.color;
                var glint = new Color(1f, 0.9f, 0.45f, rest.a);
                LMotion.Create(0f, 1f, 0.55f).WithDelay(0.12f + i * 0.08f)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t => { if (frame != null) frame.color = Color.Lerp(rest, glint, Mathf.Sin(t * Mathf.PI)); })
                    .AddTo(frame.gameObject);
            }
        }

        /// <summary>All ingredients are gathered: a chime, and the slot row and the prompt pop.</summary>
        internal void CueAllGathered()
        {
            GameSfx.Play("StepChime", 0.8f, 1.12f);
            if (pocketPreviews.Length > 0 && pocketPreviews[0].transform.parent.parent.gameObject.activeInHierarchy)
                OrderHudMotion.Bounce(pocketPreviews[0].transform.parent.parent, 1.15f);
            if (promptAction != null)
                OrderHudMotion.Bounce(promptAction.transform.parent, 1.12f);
        }

        internal void SetTimerVisible(bool visible)
        {
            timer.SetActive(visible);
            RefreshTimerSway();
        }

        private void OnEnable() => RefreshTimerSway();

        private void OnDisable()
        {
            timerSway.TryCancel();
            pocketPop.TryCancel();
            if (timerText != null) timerText.rectTransform.localRotation = Quaternion.identity;
        }

        // Only the digits sway; the hourglass, label and bar stay still.
        private void RefreshTimerSway()
        {
            timerSway.TryCancel();
            if (timer.activeInHierarchy && isActiveAndEnabled) timerSway = OrderHudMotion.SwayTimer(timerText.rectTransform);
        }

        internal void UpdateTimer(double remainingSeconds, int timeLimit)
        {
            timerFill.fillAmount = Mathf.Clamp01((float)(remainingSeconds / timeLimit));
            // The storage room already ticks through the last seconds, so no heartbeat here.
            urgency ??= new TimerUrgency(timerText, playHeartbeat: false);
            urgency.Show(remainingSeconds);
            int seconds = (int)Math.Ceiling(remainingSeconds);
            if (seconds == displayedSeconds)
                return;

            displayedSeconds = seconds;
            timerText.text = RoundDirector.FormatClock(seconds);
        }

        internal void SetCrosshairVisible(bool visible) => crosshair.SetActive(visible);

        private bool crosshairFocused;
        private MotionHandle crosshairMotion;

        /// <summary>The crosshair opens up over something a click would act on.</summary>
        internal void SetCrosshairFocus(bool focused)
        {
            if (focused == crosshairFocused || crosshair == null) return;
            crosshairFocused = focused;
            crosshairMotion.TryCancel();
            var rect = crosshair.transform;
            float from = rect.localScale.x, to = focused ? 1.8f : 1f;
            crosshairMotion = LMotion.Create(from, to, 0.14f).WithEase(focused ? Ease.OutBack : Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(s => { if (rect != null) rect.localScale = Vector3.one * s; });
        }

        internal void ShowIngredient(string variantId, string action, string key = null)
        {
            string name = kindNamesByVariant.GetValueOrDefault(variantId, "이름 모를 재료");
            ShowPrompt(name, action, key);
        }

        internal void ShowPrompt(string title = "", string action = "", string key = "")
        {
            SetPromptTitle(title);
            promptRows.Show(action, key);
        }

        // The ingredient's name is shown only before it is picked up; once held, the ingredient itself fills
        // the middle of the screen and only its two actions are shown below.
        internal void ShowHeldIngredient(string leftAction, string leftKey)
        {
            SetPromptTitle("");
            promptRows.ShowHeld(leftAction, leftKey);
        }

        private void SetPromptTitle(string title)
        {
            promptTitle.text = title ?? "";
            promptTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }
    }
}
