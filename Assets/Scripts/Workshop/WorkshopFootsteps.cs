using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(CharacterController))]
    public sealed class WorkshopFootsteps : MonoBehaviour
    {
        [SerializeField] private AudioSource emitter;
        [SerializeField] private AudioClip[] steps;
        [SerializeField, Min(0.1f)] private float stepDistance = 2.1f;

        private FirstPersonController player;
        private CharacterController controller;
        private float distance;
        private int nextStep;

        private void Awake()
        {
            player = GetComponent<FirstPersonController>();
            controller = GetComponent<CharacterController>();
        }

        private void LateUpdate()
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            if (!player.isActiveAndEnabled || !player.InputEnabled || !controller.isGrounded ||
                velocity.sqrMagnitude < 0.04f)
            {
                distance = stepDistance * 0.5f;
                return;
            }

            distance += velocity.magnitude * Time.deltaTime;
            if (distance < stepDistance)
                return;

            distance = Mathf.Repeat(distance, stepDistance);
            emitter.PlayOneShot(steps[nextStep]);
            nextStep = (nextStep + 1) % steps.Length;
        }
    }
}
