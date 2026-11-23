using LitMotion;
using UnityEngine;

namespace PotionShop
{
    /// <summary>
    /// Moves the counter camera in on the customer while they are at the counter, so eyes, ears and fangs are
    /// easier to read, and back out for the title and the result. The screen-space UI is not affected.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CounterZoom : MonoBehaviour
    {
        [SerializeField, Tooltip("손님의 얼굴 근처에 있는 Transform (PortraitView)")] private Transform customer;
        [SerializeField, Range(1f, 2f)] private float zoom = 1.5f;
        [SerializeField, Tooltip("손님 기준점에서 얼굴까지의 높이 (월드 단위)")] private float faceHeight = 1.6f;
        [SerializeField, Tooltip("왼쪽 대사창을 피해 손님을 화면 오른쪽으로 두는 정도 (화면 너비 비율)")] private float panelShift = 0.12f;
        [SerializeField] private float duration = 1.1f;
        [SerializeField, Tooltip("손님이 걸어 들어와 자리에 설 때까지 기다리는 시간 (초). 접객을 시작할 때만 쓴다")] private float arrivalDelay = 1.3f;
        [SerializeField, Tooltip("확대해도 그림 밖이 보이지 않게 하는 장면의 절반 크기 (월드 단위)")] private Vector2 artHalfSize = new(12.8f, 7.2f);

        private Camera view;
        private float wideSize;
        private Vector3 widePosition;
        private bool zoomed;
        private MotionHandle motion;

        private void Awake()
        {
            view = GetComponent<Camera>();
            wideSize = view.orthographicSize;
            widePosition = transform.position;
        }

        private void Update()
        {
            var round = RoundDirector.Instance;
            bool wanted = customer != null && round != null &&
                round.Phase is RoundPhase.Consult or RoundPhase.Prescribe or RoundPhase.Ending;
            if (wanted == zoomed) return;
            zoomed = wanted;

            float fromSize = view.orthographicSize, toSize = wanted ? wideSize / zoom : wideSize;
            Vector3 from = transform.position, to = wanted ? FocusPosition(toSize) : widePosition;
            motion.TryCancel();
            // The customer walks in first; the camera only starts to close in once they have stopped.
            float delay = wanted && round.Phase == RoundPhase.Consult ? arrivalDelay : 0f;
            motion = LMotion.Create(0f, 1f, duration).WithDelay(delay).WithEase(Ease.InOutCubic).Bind(t =>
            {
                view.orthographicSize = Mathf.Lerp(fromSize, toSize, t);
                transform.position = Vector3.Lerp(from, to, t);
            }).AddTo(gameObject);
        }

        private Vector3 FocusPosition(float size)
        {
            float halfWidth = size * view.aspect;
            Vector3 target = customer.position + Vector3.up * faceHeight - Vector3.right * (halfWidth * 2f * panelShift);
            float limitX = Mathf.Max(0f, artHalfSize.x - halfWidth), limitY = Mathf.Max(0f, artHalfSize.y - size);
            return new Vector3(widePosition.x + Mathf.Clamp(target.x - widePosition.x, -limitX, limitX),
                widePosition.y + Mathf.Clamp(target.y - widePosition.y, -limitY, limitY), widePosition.z);
        }

        private void OnDisable() => motion.TryCancel();
    }
}
