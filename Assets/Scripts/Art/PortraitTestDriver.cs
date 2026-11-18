#if UNITY_EDITOR
using System;
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
        private void Start() => Apply();

        [ContextMenu("Apply Selected Portrait")]
        public void Apply()
        {
            if (!Application.isPlaying) return;
            var profile = customers?.Profiles.FirstOrDefault(p => p.Id == customerId);
            if (profile == null || view == null || palette == null) { Status = "인물/뷰/색 참조를 확인하세요."; return; }
            try
            {
                view.Show(profile, hair, eyes, palette);
                Status = profile.BasePortrait == null ? "BASE 미연결: Inspector에서 PNG를 연결하세요." : "SO 입력 적용 완료";
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
            for (int i = 0; i < images.Length; i++) images[i].enabled = i == index && (i == 1 || i == 2 || images[i].sprite != null);
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
