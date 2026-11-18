using UnityEngine;
using UnityEngine.SceneManagement;

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

            isLoading = true;
            SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        }
    }
}
