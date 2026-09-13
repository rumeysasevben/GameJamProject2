using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // The ten (or more) candles. Pure view over CandleClock; never changes time.
    public class CandleBarUI : MonoBehaviour
    {
        // Matches the candle art (88x295 px), so the wax is never stretched.
        const float CandleWidth = 30f;
        const float CandleHeight = 100f;
        const float Spacing = 30f;
        const float SnapThreshold = 0.3f;

        class CandleView
        {
            public RectTransform root;
            public Image shadow;
            public Image wax;
            public Image flame;
            public Image glow;
            public Image smoke;
            public float shown;
            public float outAt = -10f;
            public float relitAt = -10f;
            public float bornAt;
            public float seed;
        }

        readonly List<CandleView> candles = new List<CandleView>();
        RectTransform row;
        UISkin skin;

        public float Width => candles.Count * (CandleWidth + Spacing);

        public void Build(RectTransform parent)
        {
            skin = UIFactory.Skin;
            row = (RectTransform)transform;
        }

        void OnEnable()
        {
            GameEvents.CandleExtinguished += OnOut;
            GameEvents.CandleRestored += OnRelit;
        }

        void OnDisable()
        {
            GameEvents.CandleExtinguished -= OnOut;
            GameEvents.CandleRestored -= OnRelit;
        }

        void OnOut(int index)
        {
            if (index < candles.Count) candles[index].outAt = Time.unscaledTime;
        }

        void OnRelit(int index)
        {
            if (index < candles.Count) candles[index].relitAt = Time.unscaledTime;
        }

        void AddCandle()
        {
            int i = candles.Count;
            var v = new CandleView { bornAt = Time.unscaledTime, seed = Random.value * 100f };

            v.root = UIFactory.Rect("Candle" + i, row);
            v.root.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (CandleWidth + Spacing), -16f), new Vector2(CandleWidth, CandleHeight));

            v.glow = UIFactory.Image(v.root, "Glow", new Color(1f, 0.7f, 0.3f, 0.35f), skin.soft);
            v.glow.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));

            v.shadow = UIFactory.Image(v.root, "Silhouette", new Color(1f, 1f, 1f, 0.07f), skin.candle);
            v.shadow.rectTransform.Fill();

            v.wax = UIFactory.Image(v.root, "Wax", skin.waxColors[i % skin.waxColors.Length], skin.candle);
            v.wax.rectTransform.Fill();
            v.wax.type = Image.Type.Filled;
            v.wax.fillMethod = Image.FillMethod.Vertical;
            v.wax.fillOrigin = (int)Image.OriginVertical.Bottom;

            // Not a child of the wax: it follows the melting top instead of being squashed with it.
            v.flame = UIFactory.Image(v.root, "Flame", skin.flameColor, skin.flame);
            v.flame.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(40f, 54f));

            v.smoke = UIFactory.Image(v.root, "Smoke", new Color(0.8f, 0.8f, 0.85f, 0f), skin.soft);
            v.smoke.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));

            var clock = CandleClock.Instance;
            v.shown = clock != null ? clock.CandleFill(i) : 1f;
            candles.Add(v);
        }

        void Update()
        {
            var clock = CandleClock.Instance;
            if (clock == null) return;

            while (candles.Count < clock.CandleCount) AddCandle();

            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            for (int i = 0; i < candles.Count; i++)
            {
                CandleView v = candles[i];
                float target = clock.CandleFill(i);

                // Smooth small changes; big hits (a leak) snap so the player feels them.
                if (v.shown - target > SnapThreshold) v.shown = target;
                else v.shown = Mathf.Lerp(v.shown, target, 1f - Mathf.Exp(-dt * 12f));

                // A burnt-out candle stays on the cake as a grey stub, so colour is never the only cue.
                const float StubHeight = 0.12f;
                bool lit = target > 0f;
                v.wax.fillAmount = lit ? v.shown : Mathf.Max(v.shown, StubHeight);
                v.wax.color = lit ? skin.waxColors[i % skin.waxColors.Length] : skin.waxOut;

                float top = CandleHeight * v.shown;
                float flicker = 1f + Mathf.Sin(now * 13f + v.seed) * 0.06f + Mathf.Sin(now * 29f + v.seed * 2f) * 0.04f;
                float relight = Mathf.Clamp01((now - v.relitAt) / 0.35f);
                float pop = relight < 1f ? 1f + Mathf.Sin(relight * Mathf.PI) * 0.6f : 1f;
                float born = Mathf.Clamp01((now - v.bornAt) / 0.5f);

                v.flame.enabled = lit;
                v.flame.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(now * 3f + v.seed) * 1.5f, top - 12f);
                v.flame.rectTransform.localScale = new Vector3(1f / flicker, flicker * pop, 1f) * (lit ? 1f : 0f);

                v.glow.enabled = lit;
                v.glow.rectTransform.anchoredPosition = new Vector2(0f, top + 14f);
                v.glow.color = new Color(1f, 0.7f, 0.3f, 0.28f * flicker * pop);

                float smokeT = (now - v.outAt) / 1.2f;
                if (smokeT >= 0f && smokeT < 1f)
                {
                    v.smoke.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(smokeT * 6f) * 6f, top + 10f + smokeT * 60f);
                    v.smoke.rectTransform.localScale = Vector3.one * (0.6f + smokeT * 1.4f);
                    v.smoke.color = new Color(0.8f, 0.8f, 0.85f, (1f - smokeT) * 0.8f);
                }
                else v.smoke.color = Color.clear;

                v.root.localScale = Vector3.one * (born < 1f ? Mathf.Lerp(0f, 1f, born) * (1f + Mathf.Sin(born * Mathf.PI) * 0.4f) : 1f);
            }
        }
    }
}
