using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class WorkshopAudio : MonoBehaviour
    {
        [Header("Ingredients")]
        [SerializeField] private AudioSource[] ingredientEmitters;
        [SerializeField] private AudioSource pocketEmitter;
        [SerializeField] private string[] hardIngredientIds;
        [SerializeField] private AudioClip softPickup;
        [SerializeField] private AudioClip hardPickup;
        [SerializeField] private AudioClip softReturn;
        [SerializeField] private AudioClip hardReturn;

        [Header("Timer")]
        [SerializeField] private AudioSource countdown;
        [SerializeField] private AudioClip timeExpired;
        [SerializeField, Min(0f)] private float warningSeconds = 5f;

        private int nextEmitter;

        internal void PlayPickup(string variantId, Vector3 position) =>
            PlayAt(IsHard(variantId) ? hardPickup : softPickup, position);

        internal void PlayReturn(string variantId, Vector3 position) =>
            PlayAt(IsHard(variantId) ? hardReturn : softReturn, position);

        internal void PlayPocket() => pocketEmitter.Play();

        internal void UpdateCountdown(double remainingSeconds)
        {
            if (countdown == null)
                return;

            if (remainingSeconds > 0d && remainingSeconds <= warningSeconds)
            {
                if (!countdown.isPlaying)
                    countdown.Play();
            }
            else if (countdown.isPlaying)
            {
                countdown.Stop();
            }
        }

        internal void PlayTimeExpired()
        {
            countdown.Stop();
            // The result scene loads immediately, so let the alert outlive Workshop.
            AudioSource alert = Instantiate(countdown);
            alert.name = "Workshop Time Expired";
            DontDestroyOnLoad(alert.gameObject);
            alert.loop = false;
            alert.clip = timeExpired;
            alert.Play();
            DestroyAfterPlayback(alert).Forget();
        }

        private static async UniTask DestroyAfterPlayback(AudioSource alert)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(alert.clip.length + 0.2), DelayType.Realtime,
                cancellationToken: alert.GetCancellationTokenOnDestroy());
            Destroy(alert.gameObject);
        }

        private bool IsHard(string variantId) => Array.IndexOf(hardIngredientIds, variantId) >= 0;

        private void PlayAt(AudioClip clip, Vector3 position)
        {
            AudioSource emitter = ingredientEmitters[nextEmitter];
            nextEmitter = (nextEmitter + 1) % ingredientEmitters.Length;
            emitter.transform.position = position;
            emitter.clip = clip;
            emitter.Play();
        }
    }
}
