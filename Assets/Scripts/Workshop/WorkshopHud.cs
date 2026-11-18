using System;
using System.Collections.Generic;
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
        [SerializeField] private GameObject keycap;
        [SerializeField] private TextMeshProUGUI keyText;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private UnityEngine.UI.RawImage[] pocketPreviews;
        [SerializeField] private IngredientCatalog ingredients;

        private readonly Dictionary<string, string> kindNamesByVariant = new(StringComparer.Ordinal);
        private int displayedSeconds = -1;

        private void Awake()
        {
            foreach (var kind in ingredients.Kinds)
            {
                if (kind.Status != ContentStatus.Active)
                    continue;

                foreach (var variant in kind.Variants)
                    kindNamesByVariant.Add(variant.Id, kind.DisplayName);
            }
        }

        internal PocketModelDisplay CreatePocketDisplay(Camera playerCamera)
        {
            return new PocketModelDisplay(pocketPreviews, playerCamera);
        }

        internal void SetTimerVisible(bool visible) => timer.SetActive(visible);

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
            promptTitle.text = title ?? "";
            promptAction.text = action ?? "";
            keyText.text = key ?? "";
            promptTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
            promptAction.gameObject.SetActive(!string.IsNullOrEmpty(action));
            keycap.SetActive(!string.IsNullOrEmpty(key));
        }
    }
}
