using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop.Online
{
    /// <summary>
    /// In co-op the two friends trade floors after every customer: once the maker sends the customer off (the result
    /// is dismissed), the maker asks <see cref="RoleLobby"/> to swap, and when the roles flip each player is told so on
    /// a banner and walks to the other floor (the counter or the loft).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoleSwap : MonoBehaviour
    {
        private const float BannerSeconds = 1.7f;

        private OnlineSession session;
        private OnlineRole shownRole = OnlineRole.Offline;
        private RoundPhase? lastPhase;
        private bool swapping;
        private RectTransform banner;
        private CanvasGroup bannerGroup;
        private Text title, detail;

        private void Awake() => session = GetComponent<OnlineSession>();

        private void Update()
        {
            var role = session.Role;
            bool playing = role is OnlineRole.Maker or OnlineRole.Narrator;

            // The roles flipped (on either player): go to the other floor.
            if (playing && (shownRole is OnlineRole.Maker or OnlineRole.Narrator) && role != shownRole && session.Arrived)
                Travel(role).Forget();
            shownRole = role;

            // The maker just sent a customer off: time to trade places.
            var round = RoundDirector.Instance;
            RoundPhase? phase = role == OnlineRole.Maker && round != null ? round.Phase : null;
            if (phase == RoundPhase.Waiting && lastPhase == RoundPhase.Result && session.Arrived && session.PeerConnected)
                AskSoon().Forget();
            lastPhase = phase;
        }

        private async UniTaskVoid AskSoon()
        {
            // let the customer's goodbye and the waiting screen register first
            await UniTask.Delay(TimeSpan.FromSeconds(0.7), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            if (session.Role == OnlineRole.Maker && session.PeerConnected && RoundDirector.Instance != null &&
                RoundDirector.Instance.Phase == RoundPhase.Waiting)
                session.Lobby.RequestSwap();
        }

        private async UniTaskVoid Travel(OnlineRole role)
        {
            if (swapping) return;
            swapping = true;
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                bool upstairs = role == OnlineRole.Narrator;
                Show(upstairs);
                GameSfx.Play("StepChime", 0.8f);
                await LMotion.Create(0f, 1f, 0.3f).WithEase(Ease.OutBack).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        bannerGroup.alpha = Mathf.Clamp01(t * 1.5f);
                        banner.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, t);
                    }).ToUniTask(token);
                await UniTask.Delay(TimeSpan.FromSeconds(BannerSeconds), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);

                string scene = upstairs ? OnlineCoopConfig.LoftScene : OnlineCoopConfig.CounterScene;
                // the dimmer may be busy for a moment (an entrance or another walk): try again until it takes it
                while (!SceneDimmer.Instance.TryLoad(scene))
                {
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == scene) break;
                    await UniTask.Delay(TimeSpan.FromSeconds(0.2), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);
                }
                // the banner stays over the dark walk, then lifts with it
                await UniTask.Delay(TimeSpan.FromSeconds(1.1), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime, cancellationToken: token);
                await LMotion.Create(1f, 0f, 0.35f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(a => bannerGroup.alpha = a).ToUniTask(token);
            }
            finally
            {
                if (banner != null) banner.parent.gameObject.SetActive(false);
                swapping = false;
            }
        }

        private void Show(bool upstairs)
        {
            if (banner == null) Build();
            banner.parent.gameObject.SetActive(true);
            bannerGroup.alpha = 0f;
            title.text = "역할 교대!";
            detail.text = upstairs
                ? "이번엔 위층 공방에서 메모를 찾아 알려 주세요"
                : "이번엔 아래층 카운터에서 손님을 받아요";
        }

        private void Build()
        {
            var config = OnlineCoopConfig.Load();
            // above the scene dimmer, so the news stays readable while the floors change
            var canvas = OnlineUi.Canvas("RoleSwapCanvas", SceneDimmer.SortingOrder + 2, transform);
            Destroy(canvas.GetComponent<GraphicRaycaster>());
            var root = (RectTransform)canvas.transform;
            banner = OnlineUi.Box("Banner", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 260f));
            banner.pivot = new Vector2(0.5f, 0.5f);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            OnlineUiParts.Draw(banner, "UI_Window");
            title = OnlineUi.Label(OnlineUi.Rect("Title", banner, new Vector2(0f, 0.5f), Vector2.one, new Vector2(40f, 0f),
                new Vector2(-40f, -30f)), string.Empty, 64, config.BoldFont, OnlineUiParts.Ink);
            detail = OnlineUi.Label(OnlineUi.Rect("Detail", banner, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(40f, 40f),
                new Vector2(-40f, -6f)), string.Empty, 30, config.BoldFont, OnlineUiParts.Ink);
            root.gameObject.SetActive(false);
        }
    }
}
