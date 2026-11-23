using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using PotionShop.Portraits;
using UnityEngine;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The beat between handing over the potion and seeing what it does: the customer lifts the bottle, tips it back
    /// with two gulps, sets it down, and holds still for a moment (a held breath) before the effect lands.
    /// </summary>
    internal static class CustomerDrink
    {
        private const float HoldSeconds = 0.55f;

        public static async UniTask Play(PortraitView portrait, Sprite bottle, Sprite inkLabel, CancellationToken token)
        {
            if (portrait == null || !portrait.isActiveAndEnabled) return;
            var rect = (RectTransform)portrait.transform;
            var box = rect.rect;
            Vector2 At(float fx, float fy) => new(box.xMin + box.width * fx, box.yMin + box.height * fy);

            if (bottle != null)
            {
                var go = new GameObject("DrinkingBottle", typeof(RectTransform), typeof(Image));
                var held = (RectTransform)go.transform;
                held.SetParent(rect, false);
                held.SetAsLastSibling();
                var image = go.GetComponent<Image>();
                image.sprite = bottle;
                image.preserveAspect = true;
                image.raycastTarget = false;
                float width = box.width * 0.26f;
                held.sizeDelta = new Vector2(width, width * bottle.rect.height / bottle.rect.width);
                var potion = PotionFill.On(held, bottle);
                potion.Colour = PotionFill.BrewDisplay;
                potion.SetLevel(1f);
                if (inkLabel != null) LabelStamp.On(held).Show(inkLabel, Color.black, animate: false);
                held.pivot = new Vector2(0.5f, 0.75f); // tips around the neck, near the lips
                Vector2 low = At(0.66f, 0.3f), mouth = At(0.56f, 0.6f);
                try
                {
                    // lift it to the lips
                    await LMotion.Create(0f, 1f, 0.38f).WithEase(Ease.OutCubic)
                        .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                        .Bind(t =>
                        {
                            held.anchoredPosition = Vector2.Lerp(low, mouth, t);
                            held.localRotation = Quaternion.Euler(0f, 0f, 40f * t);
                            potion.KeepLevel(40f * t);
                            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 3f));
                        }).ToUniTask(token);
                    // two gulps: tip back further, swallow, ease off a little
                    for (int i = 0; i < 2; i++)
                    {
                        GameSfx.Play("Gulp", 0.85f, 1f - i * 0.08f, 0.04f);
                        portrait.Speak(0.22f);
                        potion.FillTo(i == 0 ? 0.65f : 0.3f, 0.34f, slosh: false);
                        float from = 40f + i * 18f;
                        await LMotion.Create(0f, 1f, 0.34f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                            .Bind(t =>
                            {
                                float angle = from + Mathf.Sin(t * Mathf.PI) * 22f + t * 10f;
                                held.localRotation = Quaternion.Euler(0f, 0f, angle);
                                potion.KeepLevel(angle);
                                held.anchoredPosition = mouth + Vector2.up * (Mathf.Sin(t * Mathf.PI) * box.height * 0.012f);
                            }).ToUniTask(token);
                    }
                    // set it down
                    await LMotion.Create(0f, 1f, 0.26f).WithEase(Ease.InCubic)
                        .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                        .Bind(t =>
                        {
                            held.anchoredPosition = Vector2.Lerp(mouth, low, t);
                            held.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(78f, 0f, t));
                            potion.KeepLevel(Mathf.Lerp(78f, 0f, t));
                            image.color = new Color(1f, 1f, 1f, 1f - t);
                        }).ToUniTask(token);
                }
                finally
                {
                    if (go != null) Object.Destroy(go);
                }
            }

            // the held breath: a slight squash and a faint tremble, then let go
            var rest = rect.localScale;
            try
            {
                await LMotion.Create(0f, 1f, HoldSeconds).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        float squash = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI * 0.5f) * 0.03f;
                        float tremble = Mathf.Sin(t * 90f) * 0.004f * t;
                        rect.localScale = new Vector3(rest.x * (1f + squash + tremble), rest.y * (1f - squash), rest.z);
                    }).ToUniTask(token);
            }
            finally
            {
                if (rect != null) rect.localScale = rest;
            }
        }
    }
}
