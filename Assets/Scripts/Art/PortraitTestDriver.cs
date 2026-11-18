#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using PotionShop.Data;
using PotionShop.Portraits;
using UnityEngine;

namespace PotionShop.Art
{
    public sealed class PortraitTestDriver : MonoBehaviour
    {
        public CustomerCatalog customers;
        public PortraitColorCatalog palette;
        public PortraitView view;
        public string customerId = "HUM_M1";
        public AppearanceValue hair = AppearanceValue.Black;
        public AppearanceValue eyes = AppearanceValue.Brown;
        public Color endingTint = new(0.5f, 1f, 0.5f);
        public string Status { get; private set; }
        public CustomerProfile SelectedProfile => customers?.Profiles.FirstOrDefault(p => p != null && p.Id == customerId);
        private void Start() => Apply();

        public static AppearanceValue ResolveColor(IReadOnlyList<AppearanceValue> choices, AppearanceValue value,
            AppearanceValue emptyValue = AppearanceValue.Unspecified)
        {
            if (choices.Count == 0) return emptyValue;
            return choices.Contains(value) ? value : choices[0];
        }

        [ContextMenu("Apply Selected Portrait")]
        public void Apply()
        {
            if (!Application.isPlaying) return;
            if (customers == null || view == null || palette == null)
            {
                Status = "Customers, Palette, View 참조를 확인하세요.";
                return;
            }
            var profile = SelectedProfile;
            if (profile == null)
            {
                Status = $"Customers에 '{customerId}' 인물이 없습니다.";
                return;
            }
            if (profile.EyeColors.Count == 0)
            {
                Status = $"{profile.Id}의 Eyes 목록이 비어 있습니다.";
                return;
            }
            hair = ResolveColor(profile.HairColors, hair, AppearanceValue.None);
            eyes = ResolveColor(profile.EyeColors, eyes);
            try
            {
                view.Show(profile, hair, eyes, palette);
                Status = profile.BasePortrait == null
                    ? $"{profile.Id}의 Base Portrait가 미연결 상태입니다."
                    : $"{profile.Id} / Hair {hair} / Eyes {eyes} 적용 완료";
            }
            catch (Exception e) { Status = e.Message; }
        }
        public void BlinkOnce() => view.Blink.BlinkOnce();
        public void CloseEyes() { view.Blink.Stop(); view.Blink.SetClosed(true); }
        public void AutoBlink() => view.Blink.Play();
        public void StopBlink() => view.Blink.Stop();
        public void Tint() => view.Tint(endingTint);
        public void Slime()
        {
            if (customers.SlimePortrait == null) { Status = "Customers의 Slime Portrait를 연결하세요."; return; }
            view.ShowSlime(customers.SlimePortrait);
        }
        public void SetLayer(int index)
        {
            var images = view.Layers.All.Take(6).ToArray();
            if (index < 0) { Apply(); return; }
            view.Blink.Stop();
            bool bakedIris = view.Layers.UsesBakedIris;
            for (int i = 0; i < images.Length; i++)
                images[i].enabled = i == index && (images[i].sprite != null || (!bakedIris && (i == 1 || i == 2)));
            if (bakedIris) view.Layers.irisMask.enabled = index == 1 || index == 2;
            view.Layers.slime.enabled = false;
        }
        public void PlayRig()
        {
            if (view.Rig == null || !view.Rig.Ready) { Status = "본 리그/Animator 미연결. 정적 테스트와 구분하세요."; return; }
            view.Rig.Play();
        }
        public void StopRig() { if (view.Rig != null) view.Rig.Stop(); }
    }
}
#endif
