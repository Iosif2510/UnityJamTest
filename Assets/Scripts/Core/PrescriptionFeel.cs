using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PotionShop
{
    /// <summary>
    /// The potion cabinet (packaging step, scene 3 art) made readable and lively, without touching the scene:
    /// the cabinet slides up with a drawer sound; pointing at a bottle or ink names it in a tag; the chosen bottle
    /// and ink stand up from the shelf while the rest dim; a line above the drawer says what to do and then what was
    /// picked; the serve button sits in the bottom-right corner; on serving, the chosen bottle flies to the customer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrescriptionFeel : MonoBehaviour
    {
        private const float Lift = 10f;
        private static readonly Color Dim = new(0.72f, 0.70f, 0.68f, 1f);
        private static readonly Color TagInk = new(1f, 0.96f, 0.88f);

        private sealed class Choice
        {
            public Button Button;
            public RectTransform Rect;
            public Image Image;
            public Vector2 Rest;
            public string Name;
            public MotionHandle Motion;
        }

        private readonly List<Choice> bottles = new();
        private readonly List<Choice> inks = new();
        private RectTransform panel;
        private Vector2 panelRest;
        private Text hint;
        private RectTransform tag;
        private Text tagText;
        private Choice chosenBottle, chosenInk;
        private RectTransform customer;
        private Vector2 customerRest;
        private bool customerMoved;
        private MotionHandle customerMotion;
        /// <summary>How far the customer steps left during packaging, so the cabinet's tall box doesn't cover the face.</summary>
        private const float CustomerShift = -215f;
        /// <summary>How far the cabinet sits below its scene position, so it covers less of the customer.</summary>
        private const float DrawerDrop = -60f;
        private bool built;

        /// <summary>Wires the cabinet once. Bottles in square/heart/round order; inks in ink-number order.</summary>
        public void Build(Button square, Button heart, Button round, Button[] inkButtons, Button serve, Text selected,
            Font font)
        {
            if (built) return;
            built = true;
            panel = (RectTransform)transform;
            panelRest = panel.anchoredPosition + new Vector2(0f, DrawerDrop);
            Add(bottles, square, "네모 병");
            Add(bottles, heart, "하트 병");
            Add(bottles, round, "동그란 병");
            foreach (var ink in inkButtons)
            {
                var label = ink.transform.Find("Label")?.GetComponent<Text>();
                Add(inks, ink, label != null ? label.text : ink.name);
            }

            // Serve: out of the way of the customer's face, in the bottom-right corner where a "next" button lives.
            var serveRect = (RectTransform)serve.transform;
            serveRect.anchorMin = serveRect.anchorMax = new Vector2(0f, 0f);
            serveRect.sizeDelta = new Vector2(210f, 92f);
            serveRect.anchoredPosition = new Vector2(905f, 92f - DrawerDrop); // stays on screen with the drawer lowered

            // What to do / what was picked, on a dark pill in the top-left corner.
            if (selected != null)
            {
                hint = selected;
                var rect = hint.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(-190f, 634f); // top-left, over the curtain, clear of the customer
                rect.sizeDelta = new Vector2(390f, 52f);
                hint.alignment = TextAnchor.MiddleCenter;
                hint.fontSize = 26;
                hint.color = TagInk;
                hint.raycastTarget = false;
                var back = new GameObject("HintBack", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                back.SetParent(rect, false);
                back.SetAsFirstSibling();
                back.anchorMin = Vector2.zero;
                back.anchorMax = Vector2.one;
                back.offsetMin = new Vector2(-6f, -2f);
                back.offsetMax = new Vector2(6f, 2f);
                var backImage = back.GetComponent<Image>();
                backImage.color = new Color(0.16f, 0.08f, 0.04f, 0.78f);
                backImage.raycastTarget = false;
                // The text draws over its own child only if it sits on a separate object.
                var textGo = new GameObject("Text", typeof(RectTransform));
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.SetParent(rect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                var copy = textGo.AddComponent<Text>();
                copy.font = hint.font != null ? hint.font : font;
                copy.fontSize = 26;
                copy.alignment = TextAnchor.MiddleCenter;
                copy.color = TagInk;
                copy.raycastTarget = false;
                hint.enabled = false;
                hint = copy;
                selected.gameObject.SetActive(true);
            }

            // The name tag that follows the pointer from item to item.
            tag = new GameObject("NameTag", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            tag.SetParent(panel, false);
            tag.anchorMin = tag.anchorMax = Vector2.zero;
            tag.pivot = new Vector2(0.5f, 0.5f);
            tag.sizeDelta = new Vector2(130f, 34f);
            var tagImage = tag.GetComponent<Image>();
            tagImage.color = new Color(0.16f, 0.08f, 0.04f, 0.88f);
            tagImage.raycastTarget = false;
            var tagLabel = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            tagLabel.SetParent(tag, false);
            tagLabel.anchorMin = Vector2.zero;
            tagLabel.anchorMax = Vector2.one;
            tagLabel.offsetMin = tagLabel.offsetMax = Vector2.zero;
            tagText = tagLabel.gameObject.AddComponent<Text>();
            tagText.font = font;
            tagText.fontSize = 20;
            tagText.alignment = TextAnchor.MiddleCenter;
            tagText.color = TagInk;
            tagText.raycastTarget = false;
            tag.gameObject.SetActive(false);
        }

        private void Add(List<Choice> list, Button button, string name)
        {
            var choice = new Choice
            {
                Button = button, Rect = (RectTransform)button.transform, Image = button.image, Name = name,
            };
            choice.Rest = choice.Rect.anchoredPosition;
            list.Add(choice);
            var trigger = button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Point(choice));
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (tag != null) tag.gameObject.SetActive(false); });
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        private void Point(Choice choice)
        {
            tagText.text = choice.Name;
            tag.sizeDelta = new Vector2(Mathf.Max(96f, 26f + choice.Name.Length * 21f), 34f);
            // Bottles get the tag above them; inks below, so it never covers the bottle row.
            bool ink = inks.Contains(choice);
            float offset = choice.Rect.rect.height * 0.5f + 22f;
            tag.anchoredPosition = choice.Rest + new Vector2(0f, ink ? -offset + 4f : offset + Lift);
            tag.SetAsLastSibling();
            tag.gameObject.SetActive(true);
            GameSfx.Play("Hover", 0.6f, 1f, 0.05f);
        }

        /// <summary>The packaging step begins: reset choices and slide the cabinet up into view.</summary>
        public void Open(RectTransform customerPortrait = null)
        {
            MoveCustomer(customerPortrait, true);
            chosenBottle = chosenInk = null;
            foreach (var c in bottles) Settle(c, false, false);
            foreach (var c in inks) Settle(c, false, false);
            Refresh(-1, -1);
            if (tag != null) tag.gameObject.SetActive(false);
            panel.anchoredPosition = panelRest + new Vector2(0f, -320f);
            LMotion.Create(-320f, 0f, 0.45f).WithEase(Ease.OutBack).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(y => { if (panel != null) panel.anchoredPosition = panelRest + new Vector2(0f, y); })
                .AddTo(gameObject);
            GameSfx.Play("Drawer", 0.8f);
        }

        /// <summary>Shows the current picks: indices into the bottle (square, heart, round) and ink lists, or -1.</summary>
        public void Refresh(int bottle, int ink)
        {
            chosenBottle = bottle >= 0 && bottle < bottles.Count ? bottles[bottle] : null;
            chosenInk = ink >= 0 && ink < inks.Count ? inks[ink] : null;
            foreach (var c in bottles) Settle(c, c == chosenBottle, chosenBottle != null);
            foreach (var c in inks) Settle(c, c == chosenInk, chosenInk != null);
            if (hint == null) return;
            hint.text = chosenBottle == null && chosenInk == null ? "물약병과 잉크를 하나씩 고르세요"
                : $"{(chosenBottle != null ? chosenBottle.Name : "물약병?")}  ·  {(chosenInk != null ? chosenInk.Name : "잉크?")}";
        }

        /// <summary>Chosen items stand up from the shelf; once a row has a pick, the others in it dim.</summary>
        private static void Settle(Choice c, bool chosen, bool rowPicked)
        {
            c.Motion.TryCancel();
            var rect = c.Rect;
            float from = rect.anchoredPosition.y - c.Rest.y;
            float to = chosen ? Lift : 0f;
            c.Motion = LMotion.Create(from, to, 0.18f).WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(y => { if (rect != null) rect.anchoredPosition = c.Rest + new Vector2(0f, y); });
            if (c.Image != null) c.Image.color = rowPicked && !chosen ? Dim : Color.white;
        }

        /// <summary>The packaging step ended without a serve (time ran out, scene change): put the customer back.</summary>
        public void Close() => MoveCustomer(customer, false);

        private void MoveCustomer(RectTransform portrait, bool aside)
        {
            if (portrait == null) return;
            if (!customerMoved)
            {
                if (!aside) return;
                customer = portrait;
                customerRest = portrait.anchoredPosition;
            }
            customerMoved = aside;
            customerMotion.TryCancel();
            var rect = customer;
            var rest = customerRest;
            float from = rect.anchoredPosition.x - rest.x, to = aside ? CustomerShift : 0f;
            customerMotion = LMotion.Create(from, to, 0.4f).WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                // aside and a little up, so the face clears the cabinet's tall box
                .Bind(x => { if (rect != null) rect.anchoredPosition = rest + new Vector2(x, x / CustomerShift * 30f); });
        }

        /// <summary>Serving: a copy of the chosen bottle arcs up to the customer and shrinks away.</summary>
        public void Serve(RectTransform customer)
        {
            Close();
            if (chosenBottle == null || chosenBottle.Image == null) return;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var fly = new GameObject("ServedBottle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            fly.SetParent(canvas.transform, false);
            var image = fly.GetComponent<Image>();
            image.sprite = chosenBottle.Image.sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            fly.sizeDelta = chosenBottle.Rect.rect.size;
            Vector3 from = chosenBottle.Rect.position;
            Vector3 to = customer != null ? customer.position : from + Vector3.up * 200f;
            fly.position = from;
            var root = canvas.transform;
            LMotion.Create(0f, 1f, 0.5f).WithEase(Ease.InOutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() => { if (fly != null) Destroy(fly.gameObject); })
                .Bind(t =>
                {
                    if (fly == null) return;
                    var p = Vector3.Lerp(from, to, t);
                    p.y += Mathf.Sin(t * Mathf.PI) * 140f * root.lossyScale.y;
                    fly.position = p;
                    fly.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.35f, t);
                    fly.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 12f);
                    image.color = new Color(1f, 1f, 1f, t < 0.8f ? 1f : (1f - t) / 0.2f);
                }).AddTo(fly.gameObject);
        }
    }
}
