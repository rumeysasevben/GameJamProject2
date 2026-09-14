using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Victory or defeat card with the run's numbers.
    public class EndScreenUI : MonoBehaviour
    {
        [SerializeField] float defeatDelay = 1.6f;

        RectTransform panel;
        Text title;
        Text subtitle;
        Text stats;
        UISkin skin;
        int kills, leaks;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            panel = UIFactory.Image(root, "EndScreen", skin.overlay).rectTransform;
            panel.Fill();

            title = UIFactory.Label(panel, "Title", "", 100, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 280f), new Vector2(1700f, 150f));
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.8f), 5f);

            subtitle = UIFactory.Label(panel, "Subtitle", "", 40, TextAnchor.MiddleCenter, skin.text);
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1600f, 60f));

            stats = UIFactory.Label(panel, "Stats", "", 34, TextAnchor.MiddleCenter, skin.mutedText);
            stats.lineSpacing = 1.25f;
            stats.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(900f, 300f));

            var menu = UIFactory.Button(panel, "Menu", skin.panelLight, () => GameManager.Instance.BackToMenu());
            menu.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, -270f), new Vector2(440f, 100f));
            UIFactory.Label(menu.transform, "Label", "MAIN MENU  <size=26>[M]</size>", 38, TextAnchor.MiddleCenter, skin.text).rectTransform.Fill();

            var button = UIFactory.Button(panel, "Again", skin.panelLight, () => GameManager.Instance.Restart());
            button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, -270f), new Vector2(440f, 100f));
            UIFactory.AddOutline((Image)button.targetGraphic, skin.accent, 4f);
            var label = UIFactory.Label(button.transform, "Label", "PLAY AGAIN  <size=26>[R]</size>", 38, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
            label.rectTransform.Fill();

            panel.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnKilled;
            GameEvents.EnemyLeaked += OnLeaked;
            GameEvents.GameOver += OnGameOver;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnKilled;
            GameEvents.EnemyLeaked -= OnLeaked;
            GameEvents.GameOver -= OnGameOver;
        }

        void OnKilled(Enemy e, float reward) => kills++;
        void OnLeaked(Enemy e) => leaks++;

        void OnGameOver(bool victory)
        {
            if (panel != null) StartCoroutine(Show(victory));
        }

        IEnumerator Show(bool victory)
        {
            float delay = victory && FXManager.Instance != null ? FXManager.Instance.FinaleSeconds : defeatDelay;
            yield return new WaitForSecondsRealtime(delay);

            var gm = GameManager.Instance;
            var clock = CandleClock.Instance;
            var build = BuildManager.Instance;
            var levelUp = LevelUpManager.Instance;

            if (victory)
            {
                title.text = $"HAPPY BIRTHDAY, {Story.Hero.ToUpperInvariant()}!";
                title.color = skin.accent;
                subtitle.text = $"You kept {Story.HeroPossessive} candles burning for ten years. The cake is safe.";
            }
            else
            {
                title.text = "LIGHTS OUT";
                title.color = skin.danger;
                subtitle.text = $"You lost {Story.HeroPossessive} last candle in year {gm.Year}.";
            }

            int years = victory ? gm.FinalYear : Mathf.Max(0, gm.Year - 1);
            stats.text =
                $"Years you survived   <color=#{Hex(skin.text)}>{years}</color>\n" +
                $"Monsters you defeated   <color=#{Hex(skin.text)}>{kills}</color>\n" +
                $"Candles you lost   <color=#{Hex(skin.text)}>{leaks}</color>\n" +
                $"Towers you built   <color=#{Hex(skin.text)}>{(build != null ? build.TowersBuilt : 0)}</color>\n" +
                $"Gifts you chose   <color=#{Hex(skin.text)}>{(levelUp != null ? levelUp.CardsTaken : 0)}</color>\n" +
                $"Time you had left   <color=#{Hex(skin.text)}>{UIFactory.FormatSeconds(clock != null ? clock.TimeRemaining : 0f)}</color>";

            panel.gameObject.SetActive(true);
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
