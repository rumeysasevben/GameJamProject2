using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Boot state: title, the four rules and the map picker, then straight into year one.
    public class StartScreenUI : MonoBehaviour
    {
        RectTransform panel;
        Text mapName;

        public void Build(RectTransform root)
        {
            var skin = UIFactory.Skin;
            // Light enough that the chosen map shows through.
            panel = UIFactory.Image(root, "StartScreen", new Color(skin.overlay.r, skin.overlay.g, skin.overlay.b, 0.72f)).rectTransform;
            panel.Fill();

            var title = UIFactory.Label(panel, "Title", "TEN CANDLES", 130, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 320f), new Vector2(1600f, 170f));
            UIFactory.AddOutline(title, new Color(0.12f, 0.08f, 0.04f, 0.9f), 5f);

            var subtitle = UIFactory.Label(panel, "Subtitle", $"{Story.HeroPossessive} birthday is almost here, and monsters want the cake. Only you can stop them.", 32, TextAnchor.MiddleCenter, skin.text);
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 212f), new Vector2(1500f, 90f));

            string good = ColorUtility.ToHtmlStringRGB(skin.good);
            string bad = ColorUtility.ToHtmlStringRGB(skin.danger);
            string rules =
                "Your candles are your clock <i>and</i> your wallet.\n" +
                $"You pay for towers with seconds. Every monster you defeat gives you seconds <color=#{good}>back</color>.\n" +
                $"Let a monster reach {Story.HeroPossessive} cake and you lose a <color=#{bad}>whole candle</color>.\n" +
                "Survive ten years. You get a gift after each one. You will need them.";
            var body = UIFactory.Label(panel, "Rules", rules, 28, TextAnchor.MiddleCenter, skin.mutedText);
            body.lineSpacing = 1.3f;
            body.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1400f, 260f));

            BuildMapPicker(skin);

            var button = UIFactory.Button(panel, "Start", skin.panelLight, () => GameManager.Instance.StartGame());
            button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(560f, 110f));
            UIFactory.AddOutline((Image)button.targetGraphic, skin.accent, 4f);
            var label = UIFactory.Label(button.transform, "Label", "LIGHT THE CANDLES  <size=26>[SPACE]</size>", 38, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
            label.rectTransform.Fill();
        }

        void BuildMapPicker(UISkin skin)
        {
            var maps = MapLoader.Instance;
            if (maps == null || maps.Count < 2) return;

            const float y = -165f;
            var prev = UIFactory.Button(panel, "PrevMap", skin.panelLight, () => MapLoader.Instance.Previous());
            prev.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, y), new Vector2(90f, 80f));
            UIFactory.Label(prev.transform, "Label", "<", 44, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold).rectTransform.Fill();

            var next = UIFactory.Button(panel, "NextMap", skin.panelLight, () => MapLoader.Instance.Next());
            next.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, y), new Vector2(90f, 80f));
            UIFactory.Label(next.transform, "Label", ">", 44, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold).rectTransform.Fill();

            mapName = UIFactory.Label(panel, "MapName", "", 38, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
            mapName.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(940f, 80f));
        }

        void Update()
        {
            if (panel == null) return;
            var gm = GameManager.Instance;
            bool visible = gm != null && gm.State == GameState.Boot;
            panel.gameObject.SetActive(visible);
            if (!visible) return;

            var maps = MapLoader.Instance;
            if (maps == null || mapName == null) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) maps.Previous();
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) maps.Next();
            if (Input.GetKeyDown(KeyCode.R)) maps.Reroll();
            string muted = ColorUtility.ToHtmlStringRGB(UIFactory.Skin.mutedText);
            string keys = maps.IsGenerated ? "[A/D]  new road [R]" : "[A/D]";
            mapName.text = $"{maps.Current.displayName}  <color=#{muted}><size=24>{maps.Index + 1}/{maps.Count}  {keys}</size></color>";
        }
    }
}
