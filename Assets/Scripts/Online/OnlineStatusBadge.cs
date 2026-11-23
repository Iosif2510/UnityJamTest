using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>A small always-on-top line that tells the maker whether the narrator and the voice link are still there.</summary>
    [DisallowMultipleComponent]
    public sealed class OnlineStatusBadge : MonoBehaviour
    {
        private const float ToastSeconds = 4f;

        private Text label;
        private GameObject visual;
        private RectTransform toast;
        private float shownToastFor = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (OnlineCoopConfig.Load() == null)
                return;
            var root = new GameObject("OnlineStatusBadge");
            DontDestroyOnLoad(root);
            root.AddComponent<OnlineStatusBadge>();
        }

        private void Start()
        {
            var config = OnlineCoopConfig.Load();
            var canvas = OnlineUi.Canvas("BadgeCanvas", 90, transform);
            Destroy(canvas.GetComponent<GraphicRaycaster>());
            var box = OnlineUi.Box("Badge", canvas.transform, new Vector2(0f, 0f), new Vector2(24f, 20f),
                new Vector2(720f, 44f));
            OnlineUi.Fill(box, new Color(0f, 0f, 0f, 0.55f), false);
            label = OnlineUi.Label(OnlineUi.Rect("Text", box, Vector2.zero, Vector2.one, new Vector2(16f, 0f),
                new Vector2(-16f, 0f)), string.Empty, 22, config.BodyFont, Color.white, TextAnchor.MiddleLeft);

            // "찾았어!" from upstairs: a dark strip at the top of the screen for a few seconds.
            toast = OnlineUi.Box("FoundToast", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -120f),
                new Vector2(720f, 72f));
            OnlineUiParts.Draw(toast, "LOFT_Toast");
            OnlineUi.Label(OnlineUi.Rect("Text", toast, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f)),
                "위층에서 메모를 찾았어요! 창고로 내려가세요", 28, config.BoldFont, OnlineUiParts.Cream);
            toast.gameObject.SetActive(false);

            visual = canvas.gameObject;
            visual.SetActive(false);
        }

        private void Update()
        {
            // Hidden on the walk downstairs, whose own strip already says the same.
            bool show = OnlineSession.CurrentRole == OnlineRole.Maker && OnlineSession.Instance.Arrived;
            if (visual.activeSelf != show)
                visual.SetActive(show);
            if (!show)
                return;

            var session = OnlineSession.Instance;
            string peer = session.PeerConnected ? "해설자 연결됨" : "해설자 없음";
            label.text = $"온라인 · {peer} · {session.Voice.Describe()}";

            float at = session.Signal.ReceivedAt;
            bool showToast = Time.unscaledTime - at < ToastSeconds;
            if (toast.gameObject.activeSelf != showToast)
                toast.gameObject.SetActive(showToast);
            if (showToast && at != shownToastFor)
            {
                shownToastFor = at;
                LMotion.Create(0.6f, 1f, 0.3f).WithEase(Ease.OutBack)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(scale => toast.localScale = new Vector3(scale, scale, 1f)).AddTo(toast.gameObject);
            }
        }
    }
}
