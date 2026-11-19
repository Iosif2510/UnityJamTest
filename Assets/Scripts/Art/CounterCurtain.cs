using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PotionShop
{
    [DisallowMultipleComponent]
    public sealed class CounterCurtain : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer openCurtain;
        [SerializeField] private SpriteRenderer closedCurtain;
        public void SetClosed(bool closed)
        {
            openCurtain.enabled = !closed;
            closedCurtain.enabled = closed;
            var visible = closed ? closedCurtain : openCurtain;
            var color = visible.color;
            color.a = 1f;
            visible.color = color;
        }

        public UniTask FadeTo(bool closed, CancellationToken token, Action onChanged = null) =>
            SceneDimmer.Instance.Cut(() =>
            {
                SetClosed(closed);
                onChanged?.Invoke();
            }, token);
    }
}
