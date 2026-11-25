using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PotionShop
{
    /// <summary>
    /// The storeroom's 3D resolution, from the "화면 품질" option (<see cref="GameSettings.ScreenQuality"/>):
    /// high 100%, medium 80%, low 60%, or automatic. Automatic keeps the quality level's own scale on the desktop;
    /// in a browser, where the GPU work per pixel slows the storeroom down (many lamps, haze, bloom), it draws fewer
    /// pixels while the frame rate stays low and climbs back when there is room. Only the storeroom is scaled: its UI
    /// is an overlay and stays sharp, while the counter and the loft draw their UI through the camera and keep the
    /// full scale.
    /// </summary>
    public sealed class AdaptiveRenderScale : MonoBehaviour
    {
        private const string Storeroom = "02_Workshop";
        private const float Lowest = 0.55f;
        private const float Step = 0.08f;
        private const float SlowFrame = 1f / 28f; // below about 28 fps: draw fewer pixels
        private const float FastFrame = 1f / 50f; // above about 50 fps: draw more again
        private const float Window = 1.5f;        // seconds of frames per decision

        private UniversalRenderPipelineAsset pipeline;
        private float designed;
        private float elapsed;
        private int frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<AdaptiveRenderScale>() != null) return;
            var go = new GameObject("Adaptive Render Scale");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveRenderScale>();
        }

        private void Start()
        {
            pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) { enabled = false; return; }
            designed = pipeline.renderScale;
        }

        private void Update()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Storeroom)
            {
                Set(designed);
                elapsed = 0f;
                frames = 0;
                return;
            }

            switch (GameSettings.ScreenQuality)
            {
                case GameSettings.Quality.High: Set(1f); return;
                case GameSettings.Quality.Medium: Set(0.8f); return;
                case GameSettings.Quality.Low: Set(0.6f); return;
            }
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                Set(designed);
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < Window) return;
            float average = elapsed / frames;
            elapsed = 0f;
            frames = 0;
            float scale = pipeline.renderScale;
            if (average > SlowFrame && scale > Lowest)
                Set(Mathf.Max(Lowest, scale - Step));
            else if (average < FastFrame && scale < designed)
                Set(Mathf.Min(designed, scale + Step));
        }

        private void Set(float scale)
        {
            if (!Mathf.Approximately(pipeline.renderScale, scale)) pipeline.renderScale = scale;
        }

        private void OnDestroy()
        {
            // the asset is shared: in the editor it must not keep the changed scale after play
            if (pipeline != null) pipeline.renderScale = designed;
        }
    }
}
