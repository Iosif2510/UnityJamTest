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
                    kindNamesByVariant.Add(variant.Id, kind.DisplayName);
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
            int seconds = (int)Math.Ceiling(remainingSeconds);
            if (seconds == displayedSeconds)
                return;

            displayedSeconds = seconds;
            timerText.text = RoundDirector.FormatClock(seconds);
        }

        internal void SetCrosshairVisible(bool visible) => crosshair.SetActive(visible);

        internal void ShowIngredient(string variantId, string action, string key = null)
        {
            string name = kindNamesByVariant.GetValueOrDefault(variantId, "재료");
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
