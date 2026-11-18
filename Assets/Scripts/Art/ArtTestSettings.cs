#if UNITY_EDITOR
using PotionShop.Data;
using UnityEngine;

namespace PotionShop.Art
{
    public sealed class ArtTestSettings : ScriptableObject
    {
        public bool useFixedOrder;
        public int seed = 6174;
        public string customerId = "HUM_M1";
        public AppearanceValue hairColor = AppearanceValue.Black;
        public AppearanceValue eyeColor = AppearanceValue.Brown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtTestSettings>("Assets/Editor/ArtTestSettings.asset");
            if (settings == null || !settings.useFixedOrder) return;
            RoundDirector.EditorOrderFactory = (catalog, previous) => OrderGenerator.GenerateFixed(catalog,
                settings.seed, previous, string.IsNullOrWhiteSpace(settings.customerId) ? null : settings.customerId,
                settings.hairColor == AppearanceValue.Unspecified ? null : settings.hairColor,
                settings.eyeColor == AppearanceValue.Unspecified ? null : settings.eyeColor);
            Debug.Log("[Art Test] Fixed appearance test bypasses the uniform ink draw. Disable it for normal play.");
        }
    }
}
#endif
