using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// Browser-only safety nets set up once at start (Plugins/WebGL/BrowserPointerLock.jslib): a refused mouse lock
    /// no longer pops up Unity's "An error occurred" box; the storeroom just asks for a click instead.
    /// </summary>
    internal static class BrowserGuards
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void PotionGuardPointerLock();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PotionGuardPointerLock();
#endif
        }
    }
}
