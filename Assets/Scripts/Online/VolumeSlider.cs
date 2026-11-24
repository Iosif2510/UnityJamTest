using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// A master volume slider (<see cref="GameSfx.MasterVolume"/>, saved), in the title's options window and the
    /// loft's Esc menu. Releasing the handle plays a tick at the new level.
    /// </summary>
    internal static class VolumeSlider
    {
        public static Slider Build(RectTransform row, Font font, Color textColor)
        {
            OnlineUi.Label(OnlineUi.Rect("Label", row, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(90f, 0f)),
                "소리", 26, font, textColor, TextAnchor.MiddleLeft);
            var area = OnlineUi.Rect("Slider", row, Vector2.zero, Vector2.one, new Vector2(90f, 8f), new Vector2(0f, -8f));
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
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(GameSfx.MasterVolume);
            slider.onValueChanged.AddListener(v => GameSfx.MasterVolume = v);
            var trigger = area.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            up.callback.AddListener(_ => GameSfx.Play("Tick", 0.8f));
            trigger.triggers.Add(up);
            return slider;
        }
    }
}
