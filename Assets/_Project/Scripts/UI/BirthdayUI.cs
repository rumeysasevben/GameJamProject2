using System.Collections.Generic;
using System.Linq;
using TenCandles.Core;
using TenCandles.Lifetime;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Shown at ages 10, 20 and 30: Stay in this stage or Advance to the next (spec §5).
    // An illegal option stays visible but disabled, with the reason written on it.
    public class BirthdayUI : MonoBehaviour
    {
        const float InputDelay = 0.5f;

        class Option
        {
            public BirthdayChoice choice;
            public Button button;
            public Image background;
            public Text title;
            public Text body;
            public bool legal;
        }

        RectTransform panel;
        Text title;
        Text subtitle;
        Text detail;
        readonly Option[] options = new Option[2];
        float openedAt;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            panel = UIFactory.Image(root, "Birthday", skin.overlay).rectTransform;
            panel.Fill();

            title = UIFactory.Label(panel, "Title", "HAPPY BIRTHDAY!", 100, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(1700f, 150f));
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.8f), 5f);

            subtitle = UIFactory.Label(panel, "Subtitle", "", 42, TextAnchor.MiddleCenter, skin.text);
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1600f, 60f));

            // Tier-indexed, so the same whichever option is picked.
            detail = UIFactory.Label(panel, "Detail", "", 30, TextAnchor.MiddleCenter, skin.mutedText);
            detail.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1600f, 50f));

            for (int i = 0; i < options.Length; i++)
            {
                var o = new Option { choice = i == 0 ? BirthdayChoice.Stay : BirthdayChoice.Advance };
                o.button = UIFactory.Button(panel, o.choice.ToString(), skin.panelLight, () => Choose(o));
                o.button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 0.5f) * 720f, -110f), new Vector2(660f, 460f));
                o.background = (Image)o.button.targetGraphic;
                UIFactory.AddOutline(o.background, skin.accent, 4f);

                o.title = UIFactory.Label(o.button.transform, "Title", "", 50, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
                o.title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(620f, 70f));

                o.body = UIFactory.Label(o.button.transform, "Body", "", 28, TextAnchor.UpperCenter, skin.text);
                o.body.lineSpacing = 1.2f;
                o.body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(600f, 330f));
                options[i] = o;
            }

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
            var build = BuildManager.Instance;
            if (panel == null || gm == null || lifetime == null || lifetime.Run == null) return;

            RunState run = lifetime.Run;
            BirthdayOptions rules = lifetime.Options;
            DecadeSlot current = run.CurrentSlot;
            int age = gm.Age;
            int nextTier = current.TierIndex + 1;
            var waves = WaveManager.Instance;

            subtitle.text = $"{Story.Hero} turns {age}. How will the next ten years go?";
            detail.text =
                $"Ages {age + 1}-{age + Balance.YearsPerDecade}   ·   Candles {Candles(nextTier, current.TierIndex)}   ·   " +
                $"Towers allowed {LifetimeManager.TowerCapacityFor(age + 1)}" +
                (waves != null ? $"   ·   Roads {waves.ActiveLanes(age + 1)}" : "");

            DecadeStage stayStage = current.Stage;
            DecadeStage advanceStage = BirthdayController.Next(current.Stage, BirthdayChoice.Advance);
            IEnumerable<TowerUnlock> catalogue = build != null ? build.Catalogue : null;

            Fill(options[0], stayLegal, $"STAY  <size=28>[1]</size>",
                $"Another decade of {stayStage.Display()}.\n" +
                $"<color=#{Hex(skin.danger)}>Monsters have +{(Balance.StayHpMultiplier - 1f) * 100f:0}% health.</color>\n" +
                Passive(BirthdayController.PassiveFor(stayStage)) +
                Grants(run, BirthdayController.PreviewGrants(run, BirthdayChoice.Stay, catalogue), "Mastery", stayStage, build),
                rules.StayBlockedReason);

            Fill(options[1], advanceLegal, $"ADVANCE  <size=28>[2]</size>",
                advanceLegal ? $"Grow up into {advanceStage.Display()}.\n\n" +
                    Grants(run, BirthdayController.PreviewGrants(run, BirthdayChoice.Advance, catalogue), "Visit tower", advanceStage, build) : "",
                rules.AdvanceBlockedReason);

            openedAt = Time.unscaledTime;
            panel.gameObject.SetActive(true);
        }

        void Fill(Option o, bool legal, string heading, string text, string reason)
        {
            o.legal = legal;
            o.title.text = heading;
            o.title.color = legal ? skin.accent : skin.mutedText;
            o.body.text = legal ? text : $"{text}\n<color=#{Hex(skin.danger)}>Not possible: {reason}</color>";
            o.button.interactable = legal;
            o.background.color = legal ? skin.panelLight : skin.disabled;
            o.background.GetComponent<Outline>().enabled = legal;
        }

        // The next tier's cap, plus bonus candles from cards and Long Summer, which carry over (e.g. "7 + 3").
        static string Candles(int nextTier, int currentTier)
        {
            int cap = LifetimeManager.CandleCapFor(nextTier);
            int bonus = CandleClock.Instance != null ? Mathf.Max(0, CandleClock.Instance.CandleCap - LifetimeManager.CandleCapFor(currentTier)) : 0;
            return bonus > 0 ? $"{cap} + {bonus}" : cap.ToString();
        }

        string Passive(MasteryPassive passive) => passive == null ? "" :
            $"<color=#{Hex(skin.good)}>Passive: <b>{passive.Name}</b> ({passive.Description}) for the rest of this life.</color>\n";

        // What a choice unlocks, by display name. The §9.2 visit and mastery towers arrive with the content phases.
        static string Grants(RunState run, List<string> ids, string label, DecadeStage stage, BuildManager build)
        {
            if (ids.Count == 0)
                return label == "Mastery" && run.Masteries.Count > 0
                    ? $"{label}: none for {stage.Display()} yet.\nYou keep {string.Join(", ", run.Masteries.Select(id => Name(id, build)))}."
                    : $"{label}: none for {stage.Display()} yet.";
            return $"{label}: <b>{string.Join(", ", ids.Select(id => Name(id, build)))}</b>, yours for the rest of this life.";
        }

        static string Name(string id, BuildManager build)
        {
            var data = build != null ? build.Towers.FirstOrDefault(t => t != null && t.Unlock.Id == id) : null;
            return data != null ? data.displayName : id;
        }

        void OnState(GameState state)
        {
            if (state != GameState.Birthday && panel != null) panel.gameObject.SetActive(false);
        }

        void Choose(Option o)
        {
            if (!o.legal || Time.unscaledTime - openedAt < InputDelay) return; // no accidental double clicks
            GameManager.Instance.ChooseBirthday(o.choice);
        }

        void Update()
        {
            if (panel == null || !panel.gameObject.activeSelf || TimeControl.IsMenuPaused) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) Choose(options[0]);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Choose(options[1]);

            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.35f);
            title.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, t) * (1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.02f);
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
