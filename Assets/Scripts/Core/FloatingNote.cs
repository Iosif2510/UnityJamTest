using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// A short line that floats up above a button ("아직 찾고 있어요", "친구가 내려오는 중…") and fades away. One note per
    /// button: a new one replaces the old.
    /// </summary>
    internal sealed class FloatingNote : MonoBehaviour
    {
        private MotionHandle motion;
        private CanvasGroup group;

        /// <summary>Shows <paramref name="text"/> above <paramref name="above"/>; 0 seconds keeps it until <see cref="Hide"/>.</summary>
        internal static FloatingNote Show(RectTransform above, string text, float seconds)
        {
            if (above == null) return null;
            Hide(above);
            var go = new GameObject("FloatingNote", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(above, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(520f, 56f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var note = go.AddComponent<FloatingNote>();
            note.group = go.GetComponent<CanvasGroup>();
            note.group.blocksRaycasts = false;
            note.group.interactable = false;

            var label = go.AddComponent<Text>();
            var source = above.GetComponentInChildren<Text>(true);
            label.font = source != null ? source.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font != null && label.font.name == "WebCJK") label.fontStyle = FontStyle.Bold;
            label.fontSize = 30;
            label.alignment = TextAnchor.LowerCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.color = new Color(1f, 0.965f, 0.886f);
            label.raycastTarget = false;
            label.text = text;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.11f, 0.06f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            float total = seconds > 0f ? seconds : 0.35f;
            note.motion = LMotion.Create(0f, 1f, total).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() =>
                {
                    if (seconds > 0f && note != null) Destroy(note.gameObject);
                })
                .Bind(t =>
                {
                    if (note == null) return;
                    if (seconds > 0f)
                    {
                        // in, hold, then out while drifting up
                        note.group.alpha = t < 0.15f ? t / 0.15f : t > 0.7f ? (1f - t) / 0.3f : 1f;
                        rect.anchoredPosition = new Vector2(0f, 10f + 24f * t);
                    }
                    else
                    {
                        note.group.alpha = t;
                        rect.anchoredPosition = new Vector2(0f, 10f + 14f * t);
                    }
                });
            return note;
        }

        /// <summary>Removes the note above <paramref name="above"/>, if any.</summary>
        internal static void Hide(RectTransform above)
        {
            if (above == null) return;
            var old = above.GetComponentInChildren<FloatingNote>(true);
            if (old != null) Destroy(old.gameObject);
        }

        private void OnDestroy() => motion.TryCancel();
    }
}
