using TenCandles.Core;
using TenCandles.Lifetime;
using TenCandles.Waves;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Big "YEAR 5" card when a wave starts, with the era name when it changes.
    public class YearBannerUI : MonoBehaviour
    {
        const float Duration = 1.8f;

        Text title;
        Text subtitle;
        CanvasGroup group;
        float shownAt = -10f;
        bool eraChangedThisYear;

        public void Build(RectTransform root)
        {
            var skin = UIFactory.Skin;
            var rt = UIFactory.Rect("YearBanner", root);
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 220f));
            group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            title = UIFactory.Label(rt, "Title", "", 110, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(900f, 140f));
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.7f), 4f);

            subtitle = UIFactory.Label(rt, "Subtitle", "", 36, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(900f, 60f));
            UIFactory.AddOutline(subtitle, new Color(0f, 0f, 0f, 0.7f), 2f);
        }

        void OnEnable()
        {
            GameEvents.EraChanged += OnEraChanged;
            GameEvents.WaveStarted += OnWaveStarted;
        }

        void OnDisable()
        {
            GameEvents.EraChanged -= OnEraChanged;
            GameEvents.WaveStarted -= OnWaveStarted;
        }

        // ThemeManager raises EraChanged from its own WaveStarted handler, which may run before or after ours.
        void OnEraChanged(int era)
        {
            eraChangedThisYear = true;
            RefreshSubtitle();
        }

        int currentYear;

        void OnWaveStarted(int year)
        {
            if (title == null) return;
            title.text = year >= Balance.TotalYears ? "FINAL YEAR" : $"AGE {year}";
            shownAt = Time.unscaledTime;
            currentYear = year;
            eraChangedThisYear = year > 1 && (year - 1) % 2 == 0;
            RefreshSubtitle();
        }

        void RefreshSubtitle()
        {
            if (subtitle == null) return;
            var era = ThemeManager.Instance != null ? ThemeManager.Instance.CurrentEra : null;
            int yearInDecade = WaveGenerator.YearOfAge(currentYear);
            var slot = LifetimeManager.Instance != null ? LifetimeManager.Instance.CurrentSlot : null;
            if (currentYear == 1) subtitle.text = $"{Story.HeroPossessive} birthday is coming. Guard the cake!";
            else if (currentYear >= Balance.TotalYears) subtitle.text = "The Orc Warlord is coming. This is your last stand.";
            else if (yearInDecade == Balance.YearsPerDecade) subtitle.text = "The Orc Warlord is coming. Hold on until the birthday!";
            else if (yearInDecade == 1 && slot != null) subtitle.text = $"{slot.Stage.Display()} {(slot.IsRepeat ? "goes on" : "begins")}: ages {slot.AgeFrom}-{slot.AgeTo}";
            else if (WaveManager.Instance != null && WaveManager.Instance.LaneOpensThisYear(currentYear)) subtitle.text = "Monsters found the second road. Guard both paths!";
            else subtitle.text = eraChangedThisYear && era != null ? $"{Story.HeroPossessive} party grows: {era.displayName}" : "";
        }

        void Update()
        {
            if (group == null) return;
            float t = (Time.unscaledTime - shownAt) / Duration;
            if (t < 0f || t > 1f)
            {
                group.alpha = 0f;
                return;
            }
            group.alpha = t < 0.15f ? t / 0.15f : t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
            float s = t < 0.15f ? Mathf.Lerp(1.4f, 1f, t / 0.15f) : 1f;
            group.transform.localScale = Vector3.one * s;
        }
    }
}
