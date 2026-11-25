using System;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// A labelled slider row: the master volume (<see cref="GameSfx.MasterVolume"/>, saved) in the loft's Esc menu,
    /// and any of the settings sliders in the title's options window. Releasing the handle plays a tick at the new
    /// level.
    /// </summary>
    internal static class VolumeSlider
    {
        public static Slider Build(RectTransform row, Font font, Color textColor) =>
            Build(row, font, textColor, Loc.T("소리"), 90f, 0f, 1f, () => GameSfx.MasterVolume, v => GameSfx.MasterVolume = v);

        /// <param name="readout">Optional text for the value at the row's right end (e.g. "1.2×").</param>
        public static Slider Build(RectTransform row, Font font, Color textColor, string label, float labelWidth,
            float min, float max, Func<float> get, Action<float> set, Func<float, string> readout = null)
        {
            OnlineUi.Label(OnlineUi.Rect("Label", row, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(labelWidth, 0f)),
                label, 26, font, textColor, TextAnchor.MiddleLeft);
            float right = readout != null ? -110f : 0f;
            var area = OnlineUi.Rect("Slider", row, Vector2.zero, Vector2.one, new Vector2(labelWidth, 8f), new Vector2(right, -8f));
            var track = OnlineUi.Rect("Track", area, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -5f),
                new Vector2(0f, 5f));
            OnlineUi.Fill(track, new Color(0.3f, 0.2f, 0.14f, 0.85f), false);
            var fillArea = OnlineUi.Rect("Fill Area", track, Vector2.zero, Vector2.one);
            var fill = OnlineUi.Rect("Fill", fillArea, Vector2.zero, Vector2.one);
            OnlineUi.Fill(fill, new Color(1f, 0.78f, 0.32f), false);
            var handleArea = OnlineUi.Rect("Handle Area", area, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            var handle = OnlineUi.Rect("Handle", handleArea, Vector2.zero, Vector2.zero);
            handle.sizeDelta = new Vector2(26f, 26f);
            var knob = OnlineUi.Fill(handle, new Color(1f, 0.96f, 0.88f));
            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(get());

            Text value = null;
            if (readout != null)
            {
                value = OnlineUi.Label(OnlineUi.Rect("Value", row, new Vector2(1f, 0f), Vector2.one, new Vector2(-96f, 0f),
                    Vector2.zero), readout(get()), 24, font, textColor, TextAnchor.MiddleRight);
            }
            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                if (value != null) value.text = readout(v);
            });
            var trigger = area.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            up.callback.AddListener(_ => GameSfx.Play("Tick", 0.8f));
            trigger.triggers.Add(up);
            return slider;
        }
    }
}
