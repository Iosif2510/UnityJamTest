using Cysharp.Threading.Tasks;
using PotionShop.Online;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The counter's way back to the title between customers. Playing together, it leaves the room first, so the
    /// friend upstairs is sent back to their own title too.
    /// </summary>
    [RequireComponent(typeof(Button)), DisallowMultipleComponent]
    public sealed class BackToTitleButton : MonoBehaviour
    {
        private bool leaving;

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => Leave().Forget());

        private async UniTaskVoid Leave()
        {
            if (leaving) return;
            leaving = true;
            if (OnlineSession.Exists && OnlineSession.CurrentRole != OnlineRole.Offline)
                await OnlineSession.Instance.LeaveAsync();
            if (this == null) return;
            leaving = SceneDimmer.Instance.TryLoad(OnlineCoopConfig.TitleScene);
        }
    }
}
