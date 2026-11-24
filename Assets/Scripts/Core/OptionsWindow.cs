using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using PotionShop.Online;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The contents of the title's options window, on five tabs: 소리 (master, music, effects and the customers'
    /// voices), 조작 (mouse speed and inverted look in the storage room), 화면 (full screen and a calm camera),
    /// 언어 and 제작자 (the team and what the game was made with, from Resources/credits.json). The last tab shown
    /// is remembered, so the window comes back on it after the title is rebuilt in a new language.
    /// </summary>
    internal sealed class OptionsWindow
    {
        private static readonly string[] Tabs = { "소리", "조작", "화면", "언어", "제작자" };
        private static readonly Color Faded = new(0.290f, 0.180f, 0.118f, 0.68f);
        private const float TabsTop = 118f;
        private const float PagesTop = 206f;

        private static int shownTab;

        private readonly Font font;
        private readonly List<(Image board, Text label, GameObject page)> tabs = new();
        private MemoLanguagePicker languages;
        private Toggle fullScreen;
        private float lastMurmur;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => shownTab = 0;

        public OptionsWindow(RectTransform card, Font font)
        {
            this.font = font;
            OnlineUi.Label(Row(card, 44f, 60f, 64f), Loc.T("옵션"), 42, font, OnlineUiParts.Ink, TextAnchor.MiddleLeft);

            var strip = Row(card, TabsTop, 62f, 56f);
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                var cell = OnlineUi.Rect(Tabs[i], strip, new Vector2((float)i / Tabs.Length, 0f),
                    new Vector2((float)(i + 1) / Tabs.Length, 1f), new Vector2(5f, 0f), new Vector2(-5f, 0f));
                var button = OnlineUiParts.Button(cell, "UI_SmallButton", Loc.T(Tabs[i]), 26, font, () =>
                {
                    if (shownTab != index) GameSfx.Play("PageTurn", 0.5f, 1f, 0.05f);
                    Show(index);
                });
                var page = OnlineUi.Rect(Tabs[i] + " Page", card, Vector2.zero, Vector2.one, new Vector2(64f, 40f),
                    new Vector2(-64f, -PagesTop)).gameObject;
                tabs.Add(((Image)button.targetGraphic, cell.GetComponentInChildren<Text>(), page));
            }

            BuildSound((RectTransform)tabs[0].page.transform);
            BuildControls((RectTransform)tabs[1].page.transform);
            BuildScreen((RectTransform)tabs[2].page.transform);
            BuildLanguage((RectTransform)tabs[3].page.transform);
            BuildCredits((RectTransform)tabs[4].page.transform);
            Show(shownTab);
        }

        /// <summary>Brings the shown values up to date before the window opens.</summary>
        public void Refresh()
        {
            languages?.Refresh();
            if (fullScreen != null) fullScreen.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        private void Show(int index)
        {
            shownTab = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == index;
                tabs[i].page.SetActive(on);
                // the open tab is the bright one with red lettering; the rest sit back
                tabs[i].board.color = on ? OnlineUiParts.SmallButtonTint : new Color(0.86f, 0.74f, 0.58f);
                if (tabs[i].label != null) tabs[i].label.color = on ? OnlineUiParts.StampRed : Faded;
            }
        }

        // ---- pages ----------------------------------------------------------------------------------------------

        private void BuildSound(RectTransform page)
        {
            const float labelWidth = 250f;
            VolumeSlider.Build(Row(page, 10f, 56f), font, OnlineUiParts.Ink, Loc.T("전체 소리"), labelWidth, 0f, 1f,
                () => GameSfx.MasterVolume, v => GameSfx.MasterVolume = v, Percent);
            VolumeSlider.Build(Row(page, 90f, 56f), font, OnlineUiParts.Ink, Loc.T("음악"), labelWidth, 0f, 1f,
                () => GameSettings.Music, v => GameSettings.Music = v, Percent);
            VolumeSlider.Build(Row(page, 170f, 56f), font, OnlineUiParts.Ink, Loc.T("효과음"), labelWidth, 0f, 1f,
                () => GameSettings.Effects, v => GameSettings.Effects = v, Percent);
            VolumeSlider.Build(Row(page, 250f, 56f), font, OnlineUiParts.Ink, Loc.T("손님 목소리"), labelWidth, 0f, 1f,
                () => GameSettings.Voices, v =>
                {
                    GameSettings.Voices = v;
                    // a murmur at the new level, now and then while dragging
                    if (Time.unscaledTime - lastMurmur < 0.18f) return;
                    lastMurmur = Time.unscaledTime;
                    GameSfx.PlayVoice("Voice_a_n", 0.5f, 1f, 0.08f);
                }, Percent);
        }

        private void BuildControls(RectTransform page)
        {
            VolumeSlider.Build(Row(page, 10f, 56f), font, OnlineUiParts.Ink, Loc.T("마우스 감도"), 250f, 0.25f, 2.5f,
                () => GameSettings.MouseSpeed, v => GameSettings.MouseSpeed = v, v => v.ToString("0.0") + "×");
            Tick(Row(page, 100f, 56f), Loc.T("위아래 반전"), GameSettings.InvertY, on => GameSettings.InvertY = on);
            Note(Row(page, 190f, 80f), Loc.T("창고에서 둘러볼 때 쓰여요."));
        }

        private void BuildScreen(RectTransform page)
        {
            fullScreen = Tick(Row(page, 10f, 56f), Loc.T("전체 화면"), Screen.fullScreen,
                on => Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            Tick(Row(page, 100f, 56f), Loc.T("화면 흔들림 줄이기"), GameSettings.CalmCamera, on => GameSettings.CalmCamera = on);
            Note(Row(page, 170f, 80f), Loc.T("걸을 때 시야가 출렁이지 않고, 부딪혀도 화면이 튀지 않아요. 멀미가 나면 켜 보세요."));
        }

        private void BuildLanguage(RectTransform page)
        {
            OnlineUi.Label(Row(page, 20f, 40f), Loc.IsKorean ? "언어 · Language" : Loc.T("언어"), 26, font,
                OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            languages = new MemoLanguagePicker(Row(page, 80f, 64f), font);
            Note(Row(page, 170f, 80f), Loc.T("메모와 손님의 말, 화면의 글이 모두 바뀌어요."));
        }

        private void BuildCredits(RectTransform page)
        {
            var credits = LoadCredits();
            string faded = Hex(Faded);
            var text = new StringBuilder();
            text.Append("<size=30>").Append(Loc.T("만든 사람들")).Append("</size>\n");
            if (credits?["team"] is JArray team)
                foreach (var person in team)
                    text.Append($"<color=#{faded}>{Loc.T((string)person["role"])}</color>   <b>{(string)person["name"]}</b>\n");
            if (credits?["sponsors"] is JArray sponsors && sponsors.Count > 0)
            {
                var names = new List<string>();
                foreach (var sponsor in sponsors) names.Add(Loc.T((string)sponsor));
                text.Append("\n<size=30>").Append(Loc.T("후원")).Append("</size>\n").Append(string.Join(",  ", names)).Append('\n');
            }
            if (credits?["event"] is JValue jam)
                text.Append($"\n<color=#{faded}>{Loc.T((string)jam)}</color>\n");
            if (credits?["fonts"] is JValue fonts)
                text.Append($"<size=19><color=#{faded}>{Loc.T("글꼴")}  {(string)fonts}</color></size>\n");
            var label = OnlineUi.Label(OnlineUi.Rect("Credits", page, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -6f)),
                text.ToString().TrimEnd(), 24, font, OnlineUiParts.Ink, TextAnchor.UpperCenter);
            label.supportRichText = true;
            label.lineSpacing = 1.15f;
        }

        private static JObject LoadCredits()
        {
            var asset = Resources.Load<TextAsset>("credits");
            if (asset == null) return null;
            try { return JObject.Parse(asset.text); }
            catch (Exception e)
            {
                Debug.LogWarning("[Options] credits.json could not be read: " + e.Message);
                return null;
            }
        }

        // ---- parts ----------------------------------------------------------------------------------------------

        /// <summary>A label and a ticked box (an ink rim behind a cream square, a red square when on).</summary>
        private Toggle Tick(RectTransform row, string label, bool on, Action<bool> set)
        {
            const float labelWidth = 330f;
            OnlineUi.Label(OnlineUi.Rect("Label", row, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(labelWidth, 0f)),
                label, 26, font, OnlineUiParts.Ink, TextAnchor.MiddleLeft);
            // the rim is a sibling drawn first (a child would draw over the box)
            var rim = OnlineUi.Box("Rim", row, new Vector2(0f, 0.5f), new Vector2(labelWidth + 7f, 0f), new Vector2(54f, 54f));
            rim.pivot = new Vector2(0f, 0.5f);
            OnlineUi.Fill(rim, OnlineUiParts.Ink, false);
            var box = OnlineUi.Box("Box", row, new Vector2(0f, 0.5f), new Vector2(labelWidth + 10f, 0f), new Vector2(48f, 48f));
            box.pivot = new Vector2(0f, 0.5f);
            var boxImage = OnlineUi.Fill(box, new Color(0.99f, 0.95f, 0.87f));
            var tick = OnlineUi.Rect("Tick", box, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = OnlineUi.Fill(tick, OnlineUiParts.StampRed, false);
            toggle.SetIsOnWithoutNotify(on);
            toggle.onValueChanged.AddListener(value =>
            {
                set(value);
                GameSfx.Play("Tick", 0.8f);
            });
            return toggle;
        }

        private void Note(RectTransform row, string text) =>
            OnlineUi.Label(row, text, 21, font, Faded, TextAnchor.UpperLeft);

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGBA(c);

        private static RectTransform Row(RectTransform parent, float top, float height, float margin = 0f) =>
            OnlineUi.Rect("Row", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(margin, -top - height),
                new Vector2(-margin, -top));
    }
}
