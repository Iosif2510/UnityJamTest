using TMPro;
using UnityEngine;

namespace PotionShop
{
    internal sealed class WorkshopPromptRows
    {
        private sealed class Row
        {
            private readonly RectTransform rect;
            private readonly TextMeshProUGUI action;
            private readonly TextMeshProUGUI key;
            private readonly UnityEngine.UI.HorizontalLayoutGroup layout;
            private readonly UnityEngine.UI.LayoutElement keySize;
            private readonly Vector2 position;
            private readonly Vector2 size;

            internal Row(TextMeshProUGUI action, TextMeshProUGUI key)
            {
                this.action = action;
                this.key = key;
                rect = (RectTransform)action.transform.parent;
                layout = rect.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                keySize = key.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>() ??
                          key.transform.parent.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                key.rectTransform.anchorMin = Vector2.zero;
                key.rectTransform.anchorMax = Vector2.one;
                key.rectTransform.offsetMin = key.rectTransform.offsetMax = Vector2.zero;
                key.enableAutoSizing = true;
                key.fontSizeMin = 12f;
                key.fontSizeMax = 17f;
                position = rect.anchoredPosition;
                size = rect.sizeDelta;
            }

            internal void Show(string caption, string shortcut, int heldRow = -1)
            {
                bool shown = !string.IsNullOrEmpty(caption);
                rect.gameObject.SetActive(shown);
                if (!shown) return;

                bool held = heldRow >= 0;
                rect.anchoredPosition = held ? new Vector2(0, -23f - heldRow * 30f) : position;
                rect.sizeDelta = held ? new Vector2(252f, 28f) : size;
                layout.childAlignment = held ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                action.text = caption;
                key.text = shortcut ?? string.Empty;
                keySize.preferredWidth = held || key.text.Length > 1 ? 59f : 24f;
                keySize.preferredHeight = 24f;
                key.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(shortcut));
            }
        }

        private readonly Row primary;
        private readonly Row rotate;
        private readonly Row restore;

        internal WorkshopPromptRows(TextMeshProUGUI action, TextMeshProUGUI key)
        {
            primary = new Row(action, key);
            rotate = CopyRow("Rotate Row");
            restore = CopyRow("Return Row");

            Row CopyRow(string name)
            {
                var source = action.transform.parent;
                var copy = Object.Instantiate(source.gameObject, source.parent, false);
                copy.name = name;
                var result = new Row(copy.transform.Find(action.name).GetComponent<TextMeshProUGUI>(),
                    copy.transform.Find(key.transform.parent.name + "/" + key.name).GetComponent<TextMeshProUGUI>());
                result.Show(null, null);
                return result;
            }
        }

        internal void Show(string action, string key)
        {
            primary.Show(action, key);
            rotate.Show(null, null);
            restore.Show(null, null);
        }

        internal void ShowHeld()
        {
            primary.Show("주머니에 넣기", "F", 0);
            rotate.Show("마우스로 돌리기", "R", 1);
            restore.Show("돌려놓기", "우클릭", 2);
        }
    }
}
