using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // The birthday cake at the end of the road, with one small candle per candle on the clock.
    // Pure view: candles go out and relight with CandleClock's events, and the cake flinches on every leak.
    public class CakeView : MonoBehaviour
    {
        [SerializeField] Sprite cakeSprite;
        [SerializeField] Sprite waxSprite;
        [SerializeField] Sprite flameSprite;
        [SerializeField] Sprite glowSprite;
        [Tooltip("Cake width in world units.")]
        [SerializeField] float cakeWidth = 1.45f;
        [Tooltip("Candle wax height in world units.")]
        [SerializeField] float candleHeight = 0.22f;
        [Tooltip("Height of the candle row above the cake's centre, as a fraction of half the cake height.")]
        [SerializeField] float candleRowHeight = 0.12f;
        [Tooltip("Width of the candle row as a fraction of the cake width.")]
        [SerializeField] float rowWidth = 0.62f;

        class Candle
        {
            public Transform root;
            public SpriteRenderer flame, glow;
            public float seed, outAt = -10f, relitAt = -10f;
            public bool lit = true;
        }

        readonly List<Candle> candles = new List<Candle>();
        Transform body;
        SpriteRenderer cake;
        float hitAt = -10f;

        void Awake()
        {
            // Everything wobbles together when the cake is hit.
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            cake = new GameObject("Cake").AddComponent<SpriteRenderer>();
            cake.transform.SetParent(body, false);
            cake.sprite = cakeSprite;
            cake.sortingOrder = -9;
            if (cakeSprite != null) cake.transform.localScale = Vector3.one * (cakeWidth / cakeSprite.bounds.size.x);
        }

        void OnEnable()
        {
            GameEvents.CandleExtinguished += OnOut;
            GameEvents.CandleRestored += OnRelit;
            GameEvents.EnemyLeaked += OnLeak;
        }

        void OnDisable()
        {
            GameEvents.CandleExtinguished -= OnOut;
            GameEvents.CandleRestored -= OnRelit;
            GameEvents.EnemyLeaked -= OnLeak;
        }

        void OnOut(int index)
        {
            if (index >= candles.Count) return;
            candles[index].lit = false;
            candles[index].outAt = Time.unscaledTime;
            if (FXManager.Instance != null) FXManager.Instance.Puff(candles[index].flame.transform.position);
        }

        void OnRelit(int index)
        {
            if (index >= candles.Count) return;
            candles[index].lit = true;
            candles[index].relitAt = Time.unscaledTime;
        }

        void OnLeak(Enemy enemy) => hitAt = Time.unscaledTime;

        void Update()
        {
            var clock = CandleClock.Instance;
            if (clock == null || cakeSprite == null) return;
            if (candles.Count != clock.CandleCount) Rebuild(clock);

            float now = Time.unscaledTime;
            float hit = Mathf.Clamp01((now - hitAt) / 0.4f);
            float wobble = hit < 1f ? Mathf.Sin(hit * Mathf.PI * 4f) * (1f - hit) : 0f;
            body.localRotation = Quaternion.Euler(0f, 0f, wobble * 5f);
            body.localScale = new Vector3(1f + wobble * 0.06f, 1f - wobble * 0.06f, 1f);

            float flameScale = flameSprite != null ? candleHeight * 0.6f / flameSprite.bounds.size.y : 1f;
            foreach (var c in candles)
            {
                float flicker = 1f + Mathf.Sin(now * 13f + c.seed) * 0.08f + Mathf.Sin(now * 31f + c.seed) * 0.05f;
                float relight = Mathf.Clamp01((now - c.relitAt) / 0.35f);
                float pop = relight < 1f ? 1f + Mathf.Sin(relight * Mathf.PI) * 0.6f : 1f;
                float fade = c.lit ? 1f : 1f - Mathf.Clamp01((now - c.outAt) / 0.15f);

                c.flame.enabled = c.glow.enabled = fade > 0f;
                c.flame.transform.localScale = new Vector3(flameScale / flicker, flameScale * flicker * pop, 1f) * fade;
                c.glow.color = new Color(1f, 0.75f, 0.35f, 0.4f * flicker * fade);
            }
        }

        void Rebuild(CandleClock clock)
        {
            foreach (var c in candles) Destroy(c.root.gameObject);
            candles.Clear();

            int count = clock.CandleCount;
            float cakeHeight = cakeSprite.bounds.size.y * cake.transform.localScale.y;
            float rowY = cakeHeight * 0.5f * candleRowHeight;
            float width = cakeWidth * rowWidth;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                var c = new Candle { seed = Random.value * 100f, lit = i < clock.LitCandles };

                c.root = new GameObject("Candle" + i).transform;
                c.root.SetParent(body, false);
                // A gentle arc, alternating depth, so the row reads as the top of a round cake.
                float arc = (1f - Mathf.Pow(t * 2f - 1f, 2f)) * 0.06f;
                c.root.localPosition = new Vector3(Mathf.Lerp(-width, width, t) * 0.5f, rowY + arc + (i % 2) * 0.06f, 0f);
                int order = -8 + (i % 2 == 0 ? 1 : 0);

                // Wax pivot is its centre: shift it up so the candle stands on the row.
                Add(c.root, "Wax", waxSprite, order, new Vector3(0f, candleHeight * 0.5f, 0f), waxSprite != null ? candleHeight / waxSprite.bounds.size.y : 1f);
                c.glow = Add(c.root, "Glow", glowSprite, order, new Vector3(0f, candleHeight * 1.1f, 0f), glowSprite != null ? candleHeight * 1.4f / glowSprite.bounds.size.x : 1f);
                c.flame = Add(c.root, "Flame", flameSprite, order + 1, new Vector3(0f, candleHeight * 1.2f, 0f), 1f);
                candles.Add(c);
            }
        }

        static SpriteRenderer Add(Transform parent, string name, Sprite sprite, int order, Vector3 position, float scale)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = position;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
