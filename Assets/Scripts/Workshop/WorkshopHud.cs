using System;
using System.Collections.Generic;
using LitMotion;
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

        private readonly Dictionary<string, string> kindNamesByVariant = new(StringComparer.Ordinal);
        private Art.UiTypography typography;
        private WorkshopPromptRows promptRows;
        private MotionHandle timerSway;
        private int displayedSeconds = -1;

        private void Awake()
        {
            promptRows = new WorkshopPromptRows(promptAction, keyText);
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
            return new PocketModelDisplay(pocketPreviews, playerCamera);
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
            if (timer != null) timer.transform.localRotation = Quaternion.identity;
        }

        private void RefreshTimerSway()
        {
            timerSway.TryCancel();
            if (timer.activeInHierarchy && isActiveAndEnabled) timerSway = OrderHudMotion.SwayTimer((RectTransform)timer.transform);
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

        internal void ShowHeldIngredient(string variantId)
        {
            SetPromptTitle(kindNamesByVariant.GetValueOrDefault(variantId, "재료"));
            promptRows.ShowHeld();
        }

        private void SetPromptTitle(string title)
        {
            promptTitle.text = title ?? "";
            promptTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }
    }
}
