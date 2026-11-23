using LitMotion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// Voice at a glance, on both floors: the friend's floor icon throbs with rings while they talk, and your own
    /// microphone sits next to it, dimmed and struck through with an [M] key hint while it is muted.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceIndicator : MonoBehaviour
    {
        private const float Disc = 66f;
        private const float RingEvery = 0.42f;

        /// <summary>Forces a state for screenshots outside a real call (speaking, muted).</summary>
        public static (bool speaking, bool muted)? Preview;

        private GameObject visual;
        private RectTransform root;
        private RectTransform friend;
        private Image friendIcon;
        private Image mic;
        private Image slash;
        private RectTransform keyHint;
        private OnlineRole shownRole = OnlineRole.Offline;
        private bool wasSpeaking;
        private bool shownMuted;
        private float nextRing;
        private MotionHandle muteMotion;

        private static Sprite discSprite, ringSprite, micSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (OnlineCoopConfig.Load() == null)
                return;
            var go = new GameObject("VoiceIndicator");
            DontDestroyOnLoad(go);
            go.AddComponent<VoiceIndicator>();
        }

        private void Start()
        {
            var config = OnlineCoopConfig.Load();
            var canvas = OnlineUi.Canvas("VoiceCanvas", 91, transform);
            Destroy(canvas.GetComponent<GraphicRaycaster>());
            root = OnlineUi.Box("Voice", canvas.transform, Vector2.zero, new Vector2(28f, 76f),
                new Vector2(Disc * 2f + 64f, Disc));

            // the friend: their floor's icon in a round frame
            friend = OnlineUi.Box("Friend", root, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(Disc, Disc));
            friend.pivot = new Vector2(0.5f, 0.5f);
            friend.anchoredPosition = new Vector2(Disc * 0.5f, 0f);
            Frame(friend);
            // clipped to a circle inside the rim
            var clip = OnlineUi.Rect("Clip", friend, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Fill(clip, DiscSprite, Color.white);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var iconRect = OnlineUi.Rect("Icon", clip, Vector2.zero, Vector2.one, new Vector2(-6f, -6f), new Vector2(6f, 6f));
            friendIcon = iconRect.gameObject.AddComponent<Image>();
            friendIcon.raycastTarget = false;
            friend.Find("Rim").SetAsLastSibling();

            // your microphone
            var micDisc = OnlineUi.Box("Mic", root, new Vector2(0f, 0.5f), new Vector2(Disc * 1.5f + 12f, 0f),
                new Vector2(Disc, Disc));
            micDisc.pivot = new Vector2(0.5f, 0.5f);
            Frame(micDisc);
            mic = Fill(OnlineUi.Rect("Glyph", micDisc, Vector2.zero, Vector2.one, new Vector2(12f, 12f),
                new Vector2(-12f, -12f)), MicSprite, OnlineUiParts.Ink);
            var slashRect = OnlineUi.Box("Slash", micDisc, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Disc * 0.92f, 6f));
            slashRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            slash = OnlineUi.Fill(slashRect, OnlineUiParts.StampRed, false);

            // the key that toggles it
            keyHint = OnlineUi.Box("Key", root, new Vector2(0f, 0.5f), new Vector2(Disc * 2f + 22f, -14f), new Vector2(34f, 34f));
            keyHint.pivot = new Vector2(0f, 0.5f);
            var keyImage = Fill(keyHint, DiscSprite, OnlineUiParts.Cream);
            keyImage.type = Image.Type.Simple;
            OnlineUi.Label(OnlineUi.Rect("Text", keyHint, Vector2.zero, Vector2.one), "M", 20, config.BoldFont,
                OnlineUiParts.Ink, TextAnchor.MiddleCenter);

            visual = canvas.gameObject;
            visual.SetActive(false);
            ShowMuted(false, instant: true);
        }

        private void Update()
        {
            var role = OnlineSession.CurrentRole;
            var voice = role != OnlineRole.Offline ? OnlineSession.Instance.Voice : null;
            bool onFloor = role == OnlineRole.Maker ? OnlineSession.Instance.Arrived
                : role == OnlineRole.Narrator && SceneManager.GetActiveScene().name == "03_Loft";
            bool show = Preview != null || (onFloor && voice != null && voice.State == VoiceState.On);
            if (visual.activeSelf != show)
                visual.SetActive(show);
            if (!show)
                return;

            if (role != shownRole || friendIcon.sprite == null)
            {
                shownRole = role;
                // Downstairs it sits above the status badge; upstairs just right of the left column, clear of its text.
                root.anchoredPosition = role == OnlineRole.Narrator ? new Vector2(336f, 28f) : new Vector2(28f, 76f);
                // The friend is on the other floor.
                friendIcon.sprite = OnlineUiParts.Get(role == OnlineRole.Narrator ? "NET_RoleIcon_Downstairs"
                    : "NET_RoleIcon_Upstairs");
            }

            bool speaking = Preview?.speaking ?? (voice.PeerPresent && voice.PeerSpeaking);
            bool muted = Preview?.muted ?? voice.MicMuted;
            Speaking(speaking);
            if (muted != shownMuted) ShowMuted(muted, instant: false);
        }

        private void Speaking(bool speaking)
        {
            float now = Time.unscaledTime;
            if (speaking && (!wasSpeaking || now >= nextRing))
            {
                nextRing = now + RingEvery;
                Ring();
            }
            wasSpeaking = speaking;
            // the frame breathes a little bigger while they talk, and settles when they stop
            float target = speaking ? 1.08f + Mathf.Sin(now * 14f) * 0.03f : 1f;
            float s = Mathf.Lerp(friend.localScale.x, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            friend.localScale = new Vector3(s, s, 1f);
            friendIcon.color = Color.Lerp(friendIcon.color, speaking ? Color.white : new Color(0.82f, 0.78f, 0.72f),
                1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
        }

        /// <summary>One ring spreading out of the friend's frame.</summary>
        private void Ring()
        {
            var rect = OnlineUi.Box("Ring", root, new Vector2(0f, 0.5f), new Vector2(Disc * 0.5f, 0f), new Vector2(Disc, Disc));
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetSiblingIndex(0);
            var image = Fill(rect, RingSprite, OnlineUiParts.StampRed);
            var go = rect.gameObject;
            LMotion.Create(0f, 1f, 0.8f).WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (go != null) Destroy(go); })
                .Bind(t =>
                {
                    if (rect == null) return;
                    float s = 1f + t * 0.65f;
                    rect.localScale = new Vector3(s, s, 1f);
                    var c = OnlineUi.Accent;
                    c.a = (1f - t) * 0.85f;
                    image.color = c;
                });
        }

        private void ShowMuted(bool muted, bool instant)
        {
            shownMuted = muted;
            muteMotion.TryCancel();
            float from = mic.color.a, to = muted ? 0.38f : 1f;
            slash.gameObject.SetActive(muted);
            keyHint.gameObject.SetActive(muted);
            if (instant)
            {
                Apply(to);
                return;
            }
            muteMotion = LMotion.Create(from, to, 0.2f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).Bind(Apply);
            if (muted)
                OrderHudMotion.Bounce(keyHint, 1.25f);
        }

        private void Apply(float alpha)
        {
            var c = OnlineUiParts.Ink;
            c.a = alpha;
            mic.color = c;
        }

        private static void Frame(RectTransform rect)
        {
            Fill(rect, DiscSprite, OnlineUiParts.Cream);
            var rim = OnlineUi.Rect("Rim", rect, Vector2.zero, Vector2.one);
            Fill(rim, RingSprite, OnlineUiParts.Ink);
        }

        private static Image Fill(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite DiscSprite => discSprite != null ? discSprite : discSprite = Draw(128, (x, y) => 60f - Len(x, y));
        private static Sprite RingSprite => ringSprite != null ? ringSprite : ringSprite = Draw(128, (x, y) => 3.5f - Mathf.Abs(Len(x, y) - 59f));

        /// <summary>A microphone: rounded capsule, a cradle arc under it, a stem and a foot.</summary>
        private static Sprite MicSprite => micSprite != null ? micSprite : micSprite = Draw(128, (x, y) =>
        {
            float capsule = 15f - Capsule(x, y, 0f, 10f, 0f, 38f);
            float r = Len(x, y - 8f);
            float cradle = y < 8f ? 4.5f - Mathf.Abs(r - 28f) : -99f;
            float stem = Mathf.Min(4.5f - Mathf.Abs(x), Mathf.Min(y + 34f, -20f - y));
            float foot = Mathf.Min(4.5f - Mathf.Abs(y + 36f), 18f - Mathf.Abs(x));
            return Mathf.Max(Mathf.Max(capsule, cradle), Mathf.Max(stem, foot));
        });

        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float Capsule(float x, float y, float ax, float ay, float bx, float by)
        {
            float t = Mathf.Clamp01((y - ay) / (by - ay));
            return Len(x - Mathf.Lerp(ax, bx, t), y - Mathf.Lerp(ay, by, t));
        }

        /// <summary>A white mask from a signed "inside" distance in pixels around the centre (positive inside).</summary>
        private static Sprite Draw(int size, System.Func<float, float, float> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(inside(x + 0.5f - half, y + 0.5f - half) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
