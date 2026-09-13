using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Click an empty pad: a ring of wooden signs pops up around it, one per tower, with its price.
    public class BuildRingUI : MonoBehaviour
    {
        const float Scale = 1.5f;
        const float PopSeconds = 0.16f;

        // Measured on the ring art (231x206 px, top-left origin): sign boards and their price plates.
        static readonly Vector2 ImageSize = new Vector2(231f, 206f);
        static readonly Vector2[] Boards = { new Vector2(41f, 40f), new Vector2(190f, 40f), new Vector2(41f, 160f), new Vector2(190f, 160f) };
        static readonly Vector2[] Plates = { new Vector2(43f, 74f), new Vector2(192f, 74f), new Vector2(43f, 194f), new Vector2(192f, 195f) };

        class Slot
        {
            public TowerData data;
            public RectTransform hit;
            public Image icon;
            public Text cost;
        }

        readonly List<Slot> slots = new List<Slot>();
        RectTransform canvas;
        RectTransform ring;
        UISkin skin;
        TowerSpot shownFor;
        float openedAt;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            var build = BuildManager.Instance;
            if (build == null || skin.buildRing == null) return;
            canvas = root;

            var image = UIFactory.Image(root, "BuildRing", Color.white, skin.buildRing);
            image.raycastTarget = false; // clicks in the middle fall through to the pad and close the ring
            ring = image.rectTransform;
            ring.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, ImageSize * Scale);

            for (int i = 0; i < build.Towers.Length && i < Boards.Length; i++)
            {
                int index = i;
                var slot = new Slot { data = build.Towers[i] };

                var button = UIFactory.Button(ring, "Slot" + i, new Color(1f, 1f, 1f, 0f), () => Choose(index));
                slot.hit = button.GetComponent<RectTransform>();
                slot.hit.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Local(new Vector2(Boards[i].x, 50f + (Boards[i].y - 40f))), new Vector2(80f, 70f) * Scale);

                slot.icon = UIFactory.Image(ring, "Icon", Color.white, slot.data.SpriteForLevel(1));
                slot.icon.preserveAspect = true;
                slot.icon.raycastTarget = false;
                slot.icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Local(Boards[i]) + new Vector2(0f, 14f), new Vector2(62f, 62f) * Scale);

                slot.cost = UIFactory.Label(ring, "Cost", "", 22, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
                slot.cost.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Local(Plates[i]), new Vector2(60f, 20f) * Scale);

                var key = UIFactory.Label(ring, "Key", $"{i + 1}", 16, TextAnchor.MiddleCenter, skin.mutedText, FontStyle.Bold);
                key.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Local(Boards[i]) + new Vector2(-44f, 26f), new Vector2(24f, 24f));
                slots.Add(slot);
            }
            ring.gameObject.SetActive(false);
        }

        static Vector2 Local(Vector2 pixel) => new Vector2(pixel.x - ImageSize.x * 0.5f, ImageSize.y * 0.5f - pixel.y) * Scale;

        void Choose(int index)
        {
            var build = BuildManager.Instance;
            if (build != null && build.RingSpot != null) build.TryBuild(slots[index].data, build.RingSpot);
        }

        void Update()
        {
            var build = BuildManager.Instance;
            if (ring == null || build == null) return;

            TowerSpot spot = build.CanBuildNow ? build.RingSpot : null;
            if (spot != shownFor)
            {
                shownFor = spot;
                openedAt = Time.unscaledTime;
                ring.gameObject.SetActive(spot != null);
                build.RingHover = null;
            }
            if (spot == null) return;

            var cam = Camera.main;
            if (cam == null) return;
            Vector2 screen = cam.WorldToScreenPoint(spot.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
            ring.anchoredPosition = local;

            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / PopSeconds);
            ring.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(t));

            TowerData hover = null;
            foreach (var s in slots)
            {
                float cost = build.BuildCost(s.data);
                bool affordable = build.CanAfford(cost);
                s.cost.text = cost <= 0f ? "FREE" : $"{cost:0}s";
                s.cost.color = !affordable ? skin.danger : cost <= 0f ? skin.good : skin.accent;
                s.icon.color = affordable ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.9f);

                bool over = RectTransformUtility.RectangleContainsScreenPoint(s.hit, Input.mousePosition, null);
                if (over) hover = s.data;
                float target = over ? 1.15f : 1f;
                float current = s.icon.rectTransform.localScale.x;
                s.icon.rectTransform.localScale = Vector3.one * Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            }
            build.RingHover = hover;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
