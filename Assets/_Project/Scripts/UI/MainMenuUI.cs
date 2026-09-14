using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // The main menu, shown in the Boot state: Play, Maps, How to Play, Settings, Quit.
    // The chosen map stays visible behind it; the Maps page dims it less so you can see the layout.
    public class MainMenuUI : MonoBehaviour
    {
        enum Page { Main, Maps, HowTo, Settings }

        const float DimMain = 0.62f;
        const float DimMaps = 0.15f;

        RectTransform root;
        Image dim;
        UISkin skin;
        Page page = Page.Main;
        readonly Dictionary<Page, RectTransform> pages = new Dictionary<Page, RectTransform>();

        Text mapName;
        Button rerollButton;
        Text shakeLabel, hintsLabel, fullscreenLabel;
        RectTransform hero;
        GameObject worldParty;
        float pageShownAt;

        public void Build(RectTransform parent)
        {
            skin = UIFactory.Skin;
            dim = UIFactory.Image(parent, "MainMenu", new Color(skin.overlay.r, skin.overlay.g, skin.overlay.b, DimMain));
            root = dim.rectTransform;
            root.Fill();

            pages[Page.Main] = BuildMain();
            pages[Page.Maps] = BuildMaps();
            pages[Page.HowTo] = BuildHowTo();
            pages[Page.Settings] = BuildSettings();
            Show(Page.Main);
        }

        // ---------------------------------------------------------------- pages

        RectTransform BuildMain()
        {
            var p = PageRoot("Main");

            var title = UIFactory.Label(p, "Title", "TEN CANDLES", 120, TextAnchor.MiddleCenter, skin.accent);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(1600f, 160f));
            UIFactory.AddOutline(title, new Color(0.12f, 0.08f, 0.04f, 0.9f), 5f);

            var tagline = UIFactory.Label(p, "Tagline", $"{Story.HeroPossessive} birthday is almost here. Only you can save the cake.", 30, TextAnchor.MiddleCenter, skin.text);
            tagline.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 255f), new Vector2(1500f, 50f));

            // Left: the menu. Right: Farum and the cake.
            string[] labels = { "PLAY", "MAPS", "HOW TO PLAY", "SETTINGS", "QUIT" };
            System.Action[] actions = { Play, () => Show(Page.Maps), () => Show(Page.HowTo), () => Show(Page.Settings), Quit };
            float y = 110f;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == "QUIT" && !GameSettings.CanQuit) continue;
                MenuButton(p, labels[i], new Vector2(-380f, y), new Vector2(460f, 88f), actions[i], i == 0 ? 40 : 32, i == 0);
                y -= 104f;
            }

            if (skin.hero != null)
            {
                var heroImage = UIFactory.Image(p, "Farum", Color.white, skin.hero);
                heroImage.preserveAspect = true;
                heroImage.raycastTarget = false;
                hero = heroImage.rectTransform;
                hero.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(430f, -110f), new Vector2(300f, 300f));
            }
            if (skin.cake != null)
            {
                var cake = UIFactory.Image(p, "Cake", Color.white, skin.cake);
                cake.preserveAspect = true;
                cake.raycastTarget = false;
                cake.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(530f, -190f), new Vector2(260f, 300f));
            }

            var credits = UIFactory.Label(p, "Credits", "Art: Craftpix, Kenney  ·  Font: Black Ops One", 18, TextAnchor.LowerCenter, skin.mutedText);
            credits.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1200f, 30f));
            return p;
        }

        RectTransform BuildMaps()
        {
            var p = PageRoot("Maps");

            // Everything sits along the top edge: roads, pads and the cake are never up there, so the preview stays readable.
            var bar = UIFactory.Panel(p, "Bar", skin.panelColor).rectTransform;
            bar.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1500f, 150f));

            var title = UIFactory.Label(bar, "Title", "CHOOSE YOUR MAP", 22, TextAnchor.UpperCenter, skin.accent);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 30f));

            MenuButton(bar, "<", new Vector2(-660f, 0f), new Vector2(90f, 110f), () => MapLoader.Instance?.Previous(), 44, false);
            MenuButton(bar, ">", new Vector2(660f, 0f), new Vector2(90f, 110f), () => MapLoader.Instance?.Next(), 44, false);

            mapName = UIFactory.Label(bar, "MapName", "", 36, TextAnchor.MiddleCenter, skin.text);
            mapName.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(1100f, 50f));

            rerollButton = MenuButton(bar, "NEW ROAD  <size=18>[R]</size>", new Vector2(-330f, -44f), new Vector2(300f, 50f), () => MapLoader.Instance?.Reroll(), 22, false);
            MenuButton(bar, "BACK  <size=18>[ESC]</size>", new Vector2(0f, -44f), new Vector2(300f, 50f), () => Show(Page.Main), 22, false);
            MenuButton(bar, "PLAY  <size=18>[ENTER]</size>", new Vector2(330f, -44f), new Vector2(300f, 50f), Play, 22, true);
            return p;
        }

        RectTransform BuildHowTo()
        {
            var p = PageRoot("HowTo");
            var panel = UIFactory.Panel(p, "Panel", skin.panelColor).rectTransform;
            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1320f, 760f));

            var title = UIFactory.Label(panel, "Title", "HOW TO PLAY", 56, TextAnchor.MiddleCenter, skin.accent);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1200f, 80f));

            string good = ColorUtility.ToHtmlStringRGB(skin.good);
            string bad = ColorUtility.ToHtmlStringRGB(skin.danger);
            string accent = ColorUtility.ToHtmlStringRGB(skin.accent);
            string rules =
                $"Monsters are coming for {Story.HeroPossessive} birthday cake. You have ten candles.\n\n" +
                "Your candles are your clock <i>and</i> your wallet: they burn down all the time.\n" +
                $"You pay for towers with seconds. Every monster you defeat gives you seconds <color=#{good}>back</color>.\n" +
                $"If a monster reaches the cake, you lose a <color=#{bad}>whole candle</color>.\n" +
                "Live forty years, one decade at a time. Every few years you choose a gift.\n" +
                "Each birthday brings a new stage: more towers allowed, but fewer candles.\n\n" +
                $"<color=#{accent}>CONTROLS</color>\n" +
                "Click a glowing pad to open the build ring  ·  1-4 build a tower\n" +
                "Click a tower, then U to upgrade it  ·  Right-click to cancel\n" +
                "SPACE calls the next year early (you get half the wait back)  ·  ESC pauses";
            var body = UIFactory.Label(panel, "Rules", rules, 26, TextAnchor.UpperCenter, skin.text);
            body.lineSpacing = 1.2f;
            body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1220f, 520f));

            MenuButton(panel, "BACK  <size=18>[ESC]</size>", new Vector2(0f, -310f), new Vector2(340f, 70f), () => Show(Page.Main), 26, false);
            return p;
        }

        RectTransform BuildSettings()
        {
            var p = PageRoot("Settings");
            var panel = UIFactory.Panel(p, "Panel", skin.panelColor).rectTransform;
            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 560f));

            var title = UIFactory.Label(panel, "Title", "SETTINGS", 56, TextAnchor.MiddleCenter, skin.accent);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 80f));

            shakeLabel = SettingRow(panel, "Screen shake", 110f, () => GameSettings.ScreenShake = !GameSettings.ScreenShake);
            hintsLabel = SettingRow(panel, "Hints", 10f, () => GameSettings.Hints = !GameSettings.Hints);
            if (GameSettings.CanToggleFullscreen)
                fullscreenLabel = SettingRow(panel, "Fullscreen", -90f, () => GameSettings.Fullscreen = !GameSettings.Fullscreen);

            MenuButton(panel, "BACK  <size=18>[ESC]</size>", new Vector2(0f, -210f), new Vector2(340f, 70f), () => Show(Page.Main), 26, false);
            return p;
        }

        Text SettingRow(RectTransform parent, string name, float y, System.Action toggle)
        {
            var label = UIFactory.Label(parent, name, name, 32, TextAnchor.MiddleLeft, skin.text);
            label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, y), new Vector2(380f, 60f));
            var button = MenuButton(parent, "", new Vector2(200f, y), new Vector2(180f, 64f), toggle, 28, false);
            return button.GetComponentInChildren<Text>();
        }

        // ---------------------------------------------------------------- helpers

        RectTransform PageRoot(string name)
        {
            var p = UIFactory.Rect(name, root);
            p.Fill();
            return p;
        }

        Button MenuButton(RectTransform parent, string label, Vector2 position, Vector2 size, System.Action onClick, int fontSize, bool primary)
        {
            var button = UIFactory.Button(parent, label, skin.panelLight, onClick);
            button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            if (primary) UIFactory.AddOutline((Image)button.targetGraphic, skin.accent, 4f);
            var text = UIFactory.Label(button.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, primary ? skin.accent : skin.text);
            text.rectTransform.Fill();
            return button;
        }

        void Show(Page next)
        {
            page = next;
            pageShownAt = Time.unscaledTime;
            foreach (var kv in pages) kv.Value.gameObject.SetActive(kv.Key == next);
        }

        void Play()
        {
            // Farum must be listening before the game starts, or the opening line is missed.
            if (worldParty != null) worldParty.SetActive(true);
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static string OnOff(bool value) => value ? "ON" : "OFF";

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (root == null) return;
            var gm = GameManager.Instance;
            bool visible = gm != null && gm.State == GameState.Boot;
            root.gameObject.SetActive(visible);
            if (HUD.GameplayCanvas != null) HUD.GameplayCanvas.enabled = !visible;
            // The real cake and Farum would double up with the menu art; only the map preview shows them.
            if (worldParty == null)
            {
                var cake = FindAnyObjectByType<CakeView>();
                if (cake != null) worldParty = cake.gameObject;
            }
            if (worldParty != null) worldParty.SetActive(!visible || page == Page.Maps);
            if (!visible) return;

            float targetDim = page == Page.Maps ? DimMaps : DimMain;
            Color c = dim.color;
            c.a = Mathf.MoveTowards(c.a, targetDim, Time.unscaledDeltaTime * 2.5f);
            dim.color = c;

            HandleKeys();

            switch (page)
            {
                case Page.Main:
                    if (hero != null)
                    {
                        // Farum bounces with excitement on the title screen.
                        float hop = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                        hero.anchoredPosition = new Vector2(430f, -110f + hop * 22f);
                    }
                    break;
                case Page.Maps:
                    var maps = MapLoader.Instance;
                    if (maps != null && maps.Current != null)
                    {
                        string muted = ColorUtility.ToHtmlStringRGB(skin.mutedText);
                        mapName.text = $"{maps.Current.displayName}  <color=#{muted}><size=24>{maps.Index + 1} / {maps.Count}</size></color>";
                        rerollButton.gameObject.SetActive(maps.IsGenerated);
                    }
                    break;
                case Page.Settings:
                    shakeLabel.text = OnOff(GameSettings.ScreenShake);
                    hintsLabel.text = OnOff(GameSettings.Hints);
                    if (fullscreenLabel != null) fullscreenLabel.text = OnOff(GameSettings.Fullscreen);
                    break;
            }
        }

        void HandleKeys()
        {
            // Ignore the key press that opened this page.
            if (Time.unscaledTime - pageShownAt < 0.05f) return;

            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
            if (page == Page.Main)
            {
                if (confirm) Play();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                Show(Page.Main);
                return;
            }

            if (page == Page.Maps)
            {
                var maps = MapLoader.Instance;
                if (maps == null) return;
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) maps.Previous();
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) maps.Next();
                if (Input.GetKeyDown(KeyCode.R)) maps.Reroll();
                if (confirm) Play();
            }
        }
    }
}
