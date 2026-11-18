using System;
using System.Collections.Generic;
using System.Linq;
using PotionShop.Art;
using PotionShop.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PotionShop.Editor
{
    [CustomEditor(typeof(PortraitTestDriver))]
    public sealed class PortraitTestDriverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var driver = (PortraitTestDriver)target;
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(driver.customers))));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(driver.palette))));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(driver.view))));

            var customerId = serializedObject.FindProperty(nameof(driver.customerId));
            var hairValue = serializedObject.FindProperty(nameof(driver.hair));
            var eyeValue = serializedObject.FindProperty(nameof(driver.eyes));
            var customer = new DropdownField("인물") { name = "portrait-test-customer" };
            var hair = new DropdownField("Hair") { name = "portrait-test-hair" };
            var eyes = new DropdownField("Eyes") { name = "portrait-test-eyes" };
            root.Add(customer);
            root.Add(hair);
            root.Add(eyes);
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(driver.endingTint))));

            var profiles = new List<CustomerProfile>();
            (CustomerCatalog, PortraitColorCatalog, PotionShop.Portraits.PortraitView, string, AppearanceValue, AppearanceValue)
                ReadSelection() => (driver.customers, driver.palette, driver.view, driver.customerId, driver.hair, driver.eyes);
            var previousSelection = ReadSelection();

            string CustomerLabel(string id)
            {
                var profile = profiles.FirstOrDefault(p => p.Id == id);
                return profile != null && profile.BasePortrait == null ? $"{id} (BASE 미연결)" : id;
            }
            customer.formatListItemCallback = CustomerLabel;
            customer.formatSelectedValueCallback = CustomerLabel;

            void RefreshSelection()
            {
                if (EditorApplication.isPlaying && previousSelection != ReadSelection())
                    driver.Apply();
                previousSelection = ReadSelection();
                profiles = driver.customers?.Profiles.Where(p => p != null)
                    .OrderByDescending(p => p.BasePortrait != null).ThenBy(p => p.Id, StringComparer.Ordinal).ToList()
                    ?? new List<CustomerProfile>();
                customer.choices = profiles.Select(p => p.Id).ToList();
                customer.SetValueWithoutNotify(driver.customerId);
                customer.SetEnabled(profiles.Count > 0);
                var profile = driver.SelectedProfile;
                SetColorChoices(hair, profile?.HairColors, driver.hair, AppearanceValue.None);
                SetColorChoices(eyes, profile?.EyeColors, driver.eyes, AppearanceValue.Unspecified);
            }

            customer.RegisterValueChangedCallback(evt =>
            {
                var profile = profiles.FirstOrDefault(p => p.Id == evt.newValue);
                if (profile == null) return;
                serializedObject.UpdateIfRequiredOrScript();
                customerId.stringValue = profile.Id;
                hairValue.intValue = (int)PortraitTestDriver.ResolveColor(profile.HairColors, driver.hair, AppearanceValue.None);
                eyeValue.intValue = (int)PortraitTestDriver.ResolveColor(profile.EyeColors, driver.eyes);
                serializedObject.ApplyModifiedProperties();
                RefreshSelection();
            });
            void SetColor(SerializedProperty property, string value)
            {
                if (!Enum.TryParse<AppearanceValue>(value, out var color)) return;
                serializedObject.UpdateIfRequiredOrScript();
                property.intValue = (int)color;
                serializedObject.ApplyModifiedProperties();
                RefreshSelection();
            }
            hair.RegisterValueChangedCallback(evt => SetColor(hairValue, evt.newValue));
            eyes.RegisterValueChangedCallback(evt => SetColor(eyeValue, evt.newValue));
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshSelection());
            RefreshSelection();

            var actions = new VisualElement();
            void Button(string text, Action action) => actions.Add(new Button(action) { text = text });
            Button("SO 입력 적용 / 초기화", driver.Apply);
            Button("한 번 깜빡이기", driver.BlinkOnce); Button("감은 눈 유지", driver.CloseEyes);
            Button("자동 깜빡임", driver.AutoBlink); Button("깜빡임 정지", driver.StopBlink);
            Button("엔딩 틴트", driver.Tint); Button("슬라임 교체", driver.Slime);
            Button("전체 합성 복원", () => driver.SetLayer(-1));
            string[] names = { "뒷머리", "왼쪽 홍채", "오른쪽 홍채", "베이스", "감은 눈", "앞머리" };
            for (int i = 0; i < names.Length; i++) { int slot = i; Button(names[i] + "만 표시", () => driver.SetLayer(slot)); }
            Button("연결한 리그 재생", driver.PlayRig); Button("리그 정지", driver.StopRig);
            root.Add(actions);
            const string playHint = "Play Mode에서 인물과 색 선택이 즉시 반영됩니다.";
            var status = new HelpBox(playHint, HelpBoxMessageType.Info);
            root.Add(status);
            actions.SetEnabled(EditorApplication.isPlaying);
            root.schedule.Execute(() =>
            {
                actions.SetEnabled(EditorApplication.isPlaying);
                status.text = EditorApplication.isPlaying ? driver.Status ?? playHint : playHint;
            }).Every(200);
            return root;
        }

        private static void SetColorChoices(DropdownField field, IReadOnlyList<AppearanceValue> allowed,
            AppearanceValue value, AppearanceValue emptyValue)
        {
            var choices = allowed?.Select(color => color.ToString()).Distinct().ToList() ?? new List<string>();
            if (allowed != null && choices.Count == 0 && emptyValue == AppearanceValue.None)
                choices.Add(emptyValue.ToString());
            field.choices = choices;
            field.SetValueWithoutNotify(choices.Count > 0
                ? PortraitTestDriver.ResolveColor(allowed, value, emptyValue).ToString()
                : string.Empty);
            field.SetEnabled(choices.Count > 1);
        }
    }
}
