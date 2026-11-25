using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// In a browser each shader the storeroom draws with is compiled the first time it is used, which is what makes
    /// the first walk into the storeroom stall. While the title or the counter is up, the storeroom's shader variants
    /// (Resources/Shaders/StoreroomVariants, recorded by Potion Shop/Build/Record Storeroom Shaders) are compiled a
    /// few per frame instead, so the storeroom opens with them ready. Desktop builds compile them at load anyway.
    /// </summary>
    public sealed class ShaderWarmup : MonoBehaviour
    {
        private const string Collection = "Shaders/StoreroomVariants";
        private const string Storeroom = "02_Workshop";
        private const int PerFrame = 4;

        private ShaderVariantCollection variants;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            var variants = Resources.Load<ShaderVariantCollection>(Collection);
            if (variants == null || variants.isWarmedUp) return;
            var go = new GameObject("Shader Warmup");
            DontDestroyOnLoad(go);
            go.AddComponent<ShaderWarmup>().variants = variants;
        }

        private void Update()
        {
            // not while the storeroom itself is loading or open: it compiles what it needs there anyway
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Storeroom) return;
            if (variants == null || variants.WarmUpProgressively(PerFrame))
                Destroy(gameObject);
        }
    }
}
