using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>A small always-on-top line that tells the maker whether the narrator and the voice link are still there.</summary>
    [DisallowMultipleComponent]
    public sealed class OnlineStatusBadge : MonoBehaviour
    {
        private Text label;
        private GameObject visual;

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
            visual = canvas.gameObject;
            visual.SetActive(false);
        }

        private void Update()
        {
            bool show = OnlineSession.CurrentRole == OnlineRole.Maker;
            if (visual.activeSelf != show)
                visual.SetActive(show);
            if (!show)
                return;

            var session = OnlineSession.Instance;
            string peer = session.PeerConnected ? "해설자 연결됨" : "해설자 없음";
            label.text = $"온라인 · {peer} · {session.Voice.Describe()}";
        }
    }
}
