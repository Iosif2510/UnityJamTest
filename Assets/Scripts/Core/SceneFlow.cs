using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class SceneFlow : MonoBehaviour
    {
        [SerializeField] private string nextSceneName;

        private bool isLoading;

        public void LoadNextScene()
        {
            if (isLoading)
                return;

            if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                Debug.LogError($"Scene '{nextSceneName}' is not enabled in the build scene list.", this);
                return;
            }

            isLoading = SceneDimmer.Instance.TryLoad(nextSceneName);
        }
    }
}
