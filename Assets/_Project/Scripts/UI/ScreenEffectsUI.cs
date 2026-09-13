using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // The party gets darker with every candle that goes out; a vignette pulses on each loss.
    public class ScreenEffectsUI : MonoBehaviour
    {
        // GDD: 3 % per candle out. Players spend most candles on towers early, so the real
        // "nearly black" warning comes from the extra ramp on the last two candles.
        [SerializeField] float darkenPerCandle = 0.03f;
        [SerializeField] float lastCandlesDarkness = 0.14f;
        [SerializeField] float maxDarkness = 0.72f;
        [SerializeField] float defeatDarkness = 0.9f;

        Image darkness;
        Image vignette;
        float vignettePulse;
        float darknessShown;
        bool victory, defeat;

        public void Build()
        {
            var skin = UIFactory.Skin;
            var root = (RectTransform)transform;

            darkness = UIFactory.Image(root, "Darkness", new Color(0.02f, 0.02f, 0.02f, 0f));
            darkness.rectTransform.Fill();
            darkness.raycastTarget = false;

            vignette = UIFactory.Image(root, "Vignette", new Color(0.35f, 0f, 0.05f, 0f), skin.vignette);
            vignette.rectTransform.Fill();
            vignette.raycastTarget = false;
        }

        void OnEnable()
        {
            GameEvents.CandleExtinguished += OnCandleOut;
            GameEvents.GameOver += OnGameOver;
        }

        void OnDisable()
        {
            GameEvents.CandleExtinguished -= OnCandleOut;
            GameEvents.GameOver -= OnGameOver;
        }

        void OnCandleOut(int index) => vignettePulse = 1f;

        void OnGameOver(bool won)
        {
            victory = won;
            defeat = !won;
            if (won) darknessShown = 0f; // lights back on at once
        }

        void Update()
        {
            if (darkness == null) return;
            var clock = CandleClock.Instance;
            float dt = Time.unscaledDeltaTime;

            float target = 0f;
            if (defeat) target = defeatDarkness;
            else if (!victory && clock != null)
            {
                int lit = clock.LitCandles;
                target = (clock.CandleCount - lit) * darkenPerCandle + Mathf.Max(0, 3 - lit) * lastCandlesDarkness;
                target = Mathf.Min(maxDarkness, target);
            }

            darknessShown = Mathf.MoveTowards(darknessShown, target, dt * (defeat ? 0.6f : 0.25f));
            darkness.color = new Color(0.02f, 0.02f, 0.02f, darknessShown);

            // Heartbeat on the last candle.
            float heartbeat = 0f;
            if (!victory && !defeat && clock != null && clock.LitCandles <= 1)
                heartbeat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 5f)), 8f) * 0.45f;

            vignettePulse = Mathf.MoveTowards(vignettePulse, 0f, dt * 1.6f);
            vignette.color = new Color(0.35f, 0f, 0.05f, Mathf.Max(vignettePulse * 0.85f, heartbeat));
        }
    }
}
