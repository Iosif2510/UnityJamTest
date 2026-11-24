using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// Gives every button in the game the same tactile press: it dips a little while held, springs back on release,
    /// and ticks when clicked. Installed on all buttons automatically, including ones built at runtime; buttons that
    /// already animate or sound on their own are skipped by name (see <see cref="OwnsScale"/> / <see cref="OwnsSound"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private const float Pressed = 0.94f;
        private Button button;
        private Vector3 rest;
        private bool scales;
        private bool sounds;
        private MotionHandle motion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("ButtonFeelInstaller");
            DontDestroyOnLoad(go);
            go.AddComponent<Installer>();
        }

        /// <summary>Looks for new buttons a few times a second (online screens build theirs in code).</summary>
        private sealed class Installer : MonoBehaviour
        {
            private float next;

            private void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + 0.4f;
                foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!b.TryGetComponent<ButtonFeel>(out _))
                        b.gameObject.AddComponent<ButtonFeel>();
            }
        }

        // These animate their own scale (choice bounce, card hover, serve pop, title menu): leave their scale alone.
        private static bool OwnsScale(string n) =>
            n.StartsWith("Bottle_") || n.StartsWith("Ink_") || n == "ServeButton" || n == "Paper" || n == "Found" ||
            n.StartsWith("Menu ");

        // These play their own, more specific sound.
        private static bool OwnsSound(string n) =>
            (OwnsScale(n) && !n.StartsWith("Menu ")) || n.StartsWith("Tab ") || n == "Previous" || n == "Next";

        private void Awake()
        {
            button = GetComponent<Button>();
            rest = transform.localScale;
            scales = !OwnsScale(name);
            sounds = !OwnsSound(name);
        }

        private bool Live => button != null && button.IsInteractable();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!scales || !Live || eventData.button != PointerEventData.InputButton.Left) return;
            Animate(Pressed, 0.06f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!scales) return;
            Animate(1f, 0.28f, Ease.OutBack);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (sounds && Live && eventData.button == PointerEventData.InputButton.Left)
                GameSfx.Play("Click", 0.7f, 1f, 0.05f);
        }

        private void Animate(float to, float seconds, Ease ease)
        {
            motion.TryCancel();
            var t = transform;
            var baseScale = rest;
            float from = baseScale.x != 0f ? t.localScale.x / baseScale.x : 1f;
            motion = LMotion.Create(from, to, seconds).WithEase(ease).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(s => { if (t != null) t.localScale = baseScale * s; });
        }

        private void OnDisable()
        {
            motion.TryCancel();
            if (scales) transform.localScale = rest;
        }
    }
}
