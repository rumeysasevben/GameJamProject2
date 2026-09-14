using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Candle bar + year + clock. The biggest thing on screen.
    public class TopBarUI : MonoBehaviour
    {
        Text year;
        Text clock;
        Text caption;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            var bar = UIFactory.Panel(root, "TopBar", skin.panelColor).rectTransform;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, HUD.TopBarHeight);

            var candles = UIFactory.Rect("Candles", bar);
            candles.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, -8f), new Vector2(1100f, HUD.TopBarHeight));
            // Shrunk as a whole so candle, flame and glow keep their proportions.
            candles.localScale = Vector3.one * 0.55f;
            candles.gameObject.AddComponent<CandleBarUI>().Build(candles);

            year = UIFactory.Label(bar, "Year", "", 28, TextAnchor.MiddleRight, skin.text);
            year.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-190f, 10f), new Vector2(360f, 40f));

            clock = UIFactory.Label(bar, "Clock", "", 28, TextAnchor.MiddleRight, skin.accent);
            clock.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 10f), new Vector2(150f, 40f));

            caption = UIFactory.Label(bar, "Caption", "", 14, TextAnchor.MiddleRight, skin.mutedText);
            caption.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, -22f), new Vector2(700f, 22f));
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var cc = CandleClock.Instance;
            if (gm == null || cc == null || year == null) return;

            int shownYear = Mathf.Clamp(gm.Year, 1, gm.FinalYear);
            year.text = $"YEAR {shownYear} / {gm.FinalYear}";
            clock.text = UIFactory.FormatSeconds(cc.TimeRemaining);

            bool lastCandle = cc.LitCandles <= 1 && !gm.IsOver;
            clock.color = lastCandle && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f ? skin.danger : skin.accent;

            var era = ThemeManager.Instance != null ? ThemeManager.Instance.CurrentEra : null;
            string candles = $"You have {cc.LitCandles} of {cc.CandleCount} candles burning";
            caption.text = era != null ? $"{candles}  ·  {era.displayName}" : candles;
        }
    }
}
