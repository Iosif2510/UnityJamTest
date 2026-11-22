using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// The round result as the narrator sees it: the same parchment card and stamps the maker gets downstairs,
    /// shown over the loft when the customer has left. Dismissing it only hides it for this round.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoftResultCard : MonoBehaviour
    {
        private OnlineCoopConfig config;
        private GameObject root;
        private RectTransform card;
        private Text labels;
        private Text values;
        private Text total;
        private Image stamp;
        private bool dismissed;
        private bool wasWanted;

        public bool Visible => root != null && root.activeSelf;

        public void Build(RectTransform canvas, OnlineCoopConfig coopConfig)
        {
            config = coopConfig;
            root = OnlineUi.Rect("Result", canvas, Vector2.zero, Vector2.one).gameObject;
            OnlineUi.Fill((RectTransform)root.transform, new Color(0.05f, 0.03f, 0.02f, 0.82f));

            card = OnlineUi.Box("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(600f, 800f));
            card.pivot = new Vector2(0.5f, 0.5f);
            var paper = OnlineUi.Fill(card, config.ParchmentPanel != null ? Color.white : OnlineUi.Parchment, false);
            paper.sprite = config.ParchmentPanel;

            var plate = OnlineUi.Box("Plate", card, new Vector2(0f, 1f), new Vector2(150f, -14f), new Vector2(230f, 76f));
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.localRotation = Quaternion.Euler(0f, 0f, 2f);
            var plateImage = OnlineUi.Fill(plate, config.NamePlate != null ? Color.white : OnlineUi.Accent, false);
            plateImage.sprite = config.NamePlate;
            OnlineUi.Label(OnlineUi.Rect("Text", plate, Vector2.zero, Vector2.one), "제작결과", 32, config.BoldFont,
                OnlineUi.Parchment);

            // Item names on the left, points on the right, as on the counter's result card.
            labels = OnlineUi.Label(OnlineUi.Rect("Labels", card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(86f, -330f), new Vector2(-80f, -110f)), string.Empty, 34, config.BoldFont, OnlineUi.Ink,
                TextAnchor.UpperLeft);
            values = OnlineUi.Label(OnlineUi.Rect("Values", card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(86f, -330f), new Vector2(-80f, -110f)), string.Empty, 34, config.BoldFont, OnlineUi.Ink,
                TextAnchor.UpperRight);
            labels.lineSpacing = values.lineSpacing = 1.25f;
            total = OnlineUi.Label(OnlineUi.Rect("Total", card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(86f, -440f), new Vector2(-80f, -370f)), string.Empty, 46, config.BoldFont, OnlineUi.Ink,
                TextAnchor.MiddleLeft);

            stamp = OnlineUi.Fill(OnlineUi.Box("Stamp", card, new Vector2(0.5f, 0f), new Vector2(0f, 262f),
                new Vector2(380f, 120f)), Color.white, false);
            stamp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            stamp.preserveAspect = true;

            OnlineUi.Wooden(OnlineUi.Button(OnlineUi.Box("Confirm", card, new Vector2(0.5f, 0f), new Vector2(0f, 62f),
                new Vector2(320f, 80f)), "확인", 32, config.BoldFont, () => dismissed = true, OnlineUi.WoodLight),
                config.WoodButton).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            root.SetActive(false);
        }

        /// <summary>Call every frame with whether a finished round is waiting to be shown.</summary>
        public void Show(bool wanted, RoundSnapshot snapshot)
        {
            if (!wanted)
                dismissed = false;
            bool show = wanted && !dismissed;
            if (show && !wasWanted)
                Fill(snapshot);
            wasWanted = wanted;
            if (root.activeSelf != show)
                root.SetActive(show);
        }

        private void Fill(RoundSnapshot s)
        {
            labels.text = s.TimedOut ? "재료\n물약병\n잉크\n시간 보너스"
                : $"재료  {s.CorrectIngredients} / 3\n물약병  {(s.BottleCorrect ? "일치" : "불일치")}\n" +
                  $"잉크  {(s.InkCorrect ? "일치" : "불일치")}\n시간 보너스";
            values.text = $"+{s.IngredientPoints}\n+{s.BottlePoints}\n+{s.InkPoints}\n+{s.TimeBonus}";
            total.text = $"총점  {s.Total}점";
            stamp.sprite = s.TimedOut ? config.StampSlow : s.Stamp == 2 ? config.StampGreat :
                s.Stamp == 1 ? config.StampOkay : config.StampSlime;
            stamp.enabled = stamp.sprite != null;

            // The card settles, then the stamp comes down on it.
            LMotion.Create(0.92f, 1f, 0.22f).WithEase(Ease.OutBack)
                .Bind(scale => card.localScale = new Vector3(scale, scale, 1f)).AddTo(root);
            var mark = stamp.rectTransform;
            mark.localScale = Vector3.zero;
            LMotion.Create(1.9f, 1f, 0.2f).WithDelay(0.45f).WithEase(Ease.InQuad)
                .Bind(scale => mark.localScale = new Vector3(scale, scale, 1f)).AddTo(root);
        }
    }
}
