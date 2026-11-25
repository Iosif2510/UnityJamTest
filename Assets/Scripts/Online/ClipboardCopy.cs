using UnityEngine;
using UnityEngine.EventSystems;

namespace PotionShop.Online
{
    /// <summary>
    /// Copies a text when its button is pressed, also in a browser. There the clipboard can only be written during
    /// the player's own click, so the text is handed to the page on the press and the page writes it on the release
    /// (Plugins/WebGL/BrowserClipboard.jslib). Elsewhere Unity's own clipboard is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClipboardCopy : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>The text to copy, asked for at the moment of the press.</summary>
        public System.Func<string> Text;

        /// <summary>Raised once the text has been handed over.</summary>
        public event System.Action Copied;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void PotionArmCopy(string text);
#endif

        public void OnPointerDown(PointerEventData eventData)
        {
            string text = Text?.Invoke();
            if (string.IsNullOrEmpty(text)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            PotionArmCopy(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
            Copied?.Invoke();
        }
    }
}
