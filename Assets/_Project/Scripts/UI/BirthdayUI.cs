using TenCandles.Core;
using TenCandles.Lifetime;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Shown at ages 10, 20 and 30. Phase 1: says Happy Birthday and moves on to the next stage.
    public class BirthdayUI : MonoBehaviour
    {
        const float InputDelay = 0.5f;

        RectTransform panel;
        Text title;
        Text subtitle;
        Text detail;
        float openedAt;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            panel = UIFactory.Image(root, "Birthday", skin.overlay).rectTransform;
            panel.Fill();

            title = UIFactory.Label(panel, "Title", "HAPPY BIRTHDAY!", 110, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1700f, 160f));
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.8f), 5f);

            subtitle = UIFactory.Label(panel, "Subtitle", "", 44, TextAnchor.MiddleCenter, skin.text);
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1600f, 70f));

            detail = UIFactory.Label(panel, "Detail", "", 32, TextAnchor.MiddleCenter, skin.mutedText);
            detail.lineSpacing = 1.25f;
            detail.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1400f, 160f));

            var button = UIFactory.Button(panel, "Continue", skin.panelLight, Continue);
            button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(520f, 100f));
            UIFactory.AddOutline((Image)button.targetGraphic, skin.accent, 4f);
            UIFactory.Label(button.transform, "Label", "CONTINUE  <size=26>[SPACE]</size>", 38, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold).rectTransform.Fill();

            panel.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEvents.BirthdayOffered += OnOffered;
            GameEvents.StateChanged += OnState;
        }

        void OnDisable()
        {
            GameEvents.BirthdayOffered -= OnOffered;
            GameEvents.StateChanged -= OnState;
        }

        void OnOffered(bool stayLegal, bool advanceLegal)
        {
            var gm = GameManager.Instance;
            var lifetime = LifetimeManager.Instance;
            if (panel == null || gm == null || lifetime == null) return;

            int age = gm.Age;
            DecadeStage next = LifetimeManager.Next(lifetime.CurrentSlot.Stage, BirthdayChoice.Advance);
            subtitle.text = $"{Story.Hero} turns {age}.";
            detail.text =
                $"Next decade: <color=#{Hex(skin.text)}>{next.Display()}</color>  (ages {age + 1}-{age + Balance.YearsPerDecade})\n" +
                $"Candles: <color=#{Hex(skin.text)}>{LifetimeManager.CandleCapFor(next)}</color>   ·   " +
                $"Towers allowed: <color=#{Hex(skin.text)}>{LifetimeManager.TowerCapacityFor(age + 1)}</color>";

            openedAt = Time.unscaledTime;
            panel.gameObject.SetActive(true);
        }

        void OnState(GameState state)
        {
            if (state != GameState.Birthday && panel != null) panel.gameObject.SetActive(false);
        }

        void Continue()
        {
            if (Time.unscaledTime - openedAt < InputDelay) return; // no accidental double clicks
            GameManager.Instance.ChooseBirthday(BirthdayChoice.Advance);
        }

        void Update()
        {
            if (panel == null || !panel.gameObject.activeSelf || TimeControl.IsMenuPaused) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Continue();

            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.35f);
            title.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, t) * (1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.02f);
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
