using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TenCandles.EditorTools
{
    // Plays a full run with the AutoPlayer bot at fast-forward and writes a report.
    // Menu: Tools > Ten Candles > Autoplay Test. Batch: -executeMethod TenCandles.EditorTools.AutoTestRunner.RunFromCommandLine [-noCards]
    [InitializeOnLoad]
    public static class AutoTestRunner
    {
        const string ActiveKey = "TenCandles.AutoTest.Active";
        const string CardsKey = "TenCandles.AutoTest.Cards";
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const float StepSeconds = 1f / 30f;
        const float RealTimeLimit = 900f;

        static int errorCount;
        static double startedAt;

        static AutoTestRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/Ten Candles/Autoplay Test (with cards)")]
        static void RunWithCards() => Run(true);

        [MenuItem("Tools/Ten Candles/Autoplay Test (no cards)")]
        static void RunWithoutCards() => Run(false);

        public static void RunFromCommandLine() => Run(!Environment.GetCommandLineArgs().Contains("-noCards"));

        static void Run(bool cards)
        {
            // -map=N picks the map (index in the start screen picker) for this run.
            string mapArg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-map="));
            if (mapArg != null) PlayerPrefs.SetInt("TenCandles.Map", int.Parse(mapArg.Substring(5)));
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(CardsKey, cards);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                errorCount = 0;
                shotTaken = false;
                startedAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += CountErrors;
                EditorApplication.update += Watchdog;

                Time.captureDeltaTime = StepSeconds;
                if (Environment.GetCommandLineArgs().Contains("-menuShots"))
                {
                    menuStep = 0;
                    EditorApplication.update -= Watchdog;
                    EditorApplication.update += MenuShots;
                    return;
                }
                ApplyOverrides();
                // -seed=N fixes the run seed (card draws, wave shuffles) so balance changes can be compared run to run.
                string seedArg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-seed="));
                GameManager.SeedOverride = seedArg != null ? int.Parse(seedArg.Substring("-seed=".Length), System.Globalization.CultureInfo.InvariantCulture) : (int?)null;
                var bot = new GameObject("AutoPlayer").AddComponent<AutoPlayer>();
                bot.useCards = SessionState.GetBool(CardsKey, true);
                bot.reserveSeconds = ArgFloat("reserve", bot.reserveSeconds);
                bot.avoidExtraCandles = Environment.GetCommandLineArgs().Contains("-bot.noExtraCandles");
                bot.life = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-bot.life="))?.Substring("-bot.life=".Length).ToUpperInvariant();
                bot.wish = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-bot.wish="))?.Substring("-bot.wish=".Length).ToLowerInvariant() ?? bot.wish;
                bot.Finished = (victory, report) => Finish(victory, report);
                // -bot.shots: Logs/autoplay_birthday_<age>.png and Logs/autoplay_stage<tier>_<stage>.png
                if (Environment.GetCommandLineArgs().Contains("-bot.shots")) bot.Snapshot = tag => Capture($"Logs/autoplay_{tag}.png");
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(ActiveKey, false);
            }
        }

        // Balance experiments from the command line: -tc.rewardBase=1.8 -tc.hpExponent=1.11 ...
        // Matches any serialized float/int field on the scene's gameplay components or the AutoPlayer.
        static string overridesApplied = "";

        static void ApplyOverrides()
        {
            overridesApplied = "";
            var targets = new Component[]
            {
                UnityEngine.Object.FindAnyObjectByType<WaveManager>(), UnityEngine.Object.FindAnyObjectByType<TimeRules>(),
                UnityEngine.Object.FindAnyObjectByType<GameManager>(), UnityEngine.Object.FindAnyObjectByType<LevelUpManager>()
            };
            foreach (string arg in Environment.GetCommandLineArgs().Where(a => a.StartsWith("-tc.")))
            {
                string[] kv = arg.Substring(4).Split('=');
                if (kv.Length != 2) continue;
                foreach (var target in targets.Where(t => t != null))
                {
                    var field = target.GetType().GetField(kv[0], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (field == null) continue;
                    object value = field.FieldType == typeof(int)
                        ? (object)int.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture)
                        : float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                    field.SetValue(target, value);
                    overridesApplied += $" {kv[0]}={kv[1]}";
                }
            }
        }

        static float ArgFloat(string name, float fallback)
        {
            string arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-bot." + name + "="));
            return arg != null ? float.Parse(arg.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture) : fallback;
        }

        static void CountErrors(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorCount++;
        }

        static bool shotTaken;
        static int menuStep;

        // -bot.shot=<seconds>: saves Logs/autoplay_shot.png (world + HUD) once game time passes that mark.
        static void MaybeScreenshot()
        {
            if (shotTaken) return;
            float at = ArgFloat("shot", -1f);
            if (at < 0f || Time.timeSinceLevelLoad < at || Camera.main == null) return;
            shotTaken = true;
            Capture("Logs/autoplay_shot.png");
        }

        // -menuShots: screenshots of every main menu page and the pause menu, then quits.
        static void MenuShots()
        {
            if (!EditorApplication.isPlaying || Camera.main == null) return;
            float t = Time.timeSinceLevelLoad;
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            var pause = UnityEngine.Object.FindAnyObjectByType<PauseMenuUI>();
            if (menu == null) return;

            string[] pages = { "Main", "Maps", "HowTo", "Settings" };
            if (menuStep < pages.Length && t > 1f + menuStep)
            {
                ShowMenuPage(menu, pages[menuStep]);
                // Give the UI a frame to lay out before capturing.
                if (t > 1.5f + menuStep)
                {
                    Capture($"Logs/menu_{pages[menuStep].ToLowerInvariant()}.png");
                    menuStep++;
                }
            }
            else if (menuStep == pages.Length && t > 6f)
            {
                ShowMenuPage(menu, "Main");
                GameManager.Instance.StartGame();
                pause.GetType().GetMethod("SetOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(pause, new object[] { true });
                menuStep = pages.Length + 1;
            }
            // Game time is frozen from here on (pause, level-up), so these steps count editor updates.
            else if (menuStep > pages.Length)
            {
                int frame = ++menuStep - pages.Length;
                if (frame == 8)
                {
                    Capture("Logs/menu_pause.png");
                    pause.GetType().GetMethod("SetOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(pause, new object[] { false });
                    UnityEngine.Object.FindAnyObjectByType<BuildPanelUI>().Toggle();
                }
                else if (frame == 240)
                {
                    Capture("Logs/menu_hud_drawer_open.png");
                    UnityEngine.Object.FindAnyObjectByType<BuildPanelUI>().Toggle();
                }
                else if (frame == 270)
                {
                    Capture("Logs/menu_hud_drawer_closed.png");
                    TimeControl.SetPaused(true);
                    // Year 7 unlocks every rarity and guarantees at least one Rare or Epic.
                    LevelUpManager.Instance.Offer(7);
                }
                else if (frame == 300)
                {
                    Capture("Logs/menu_cards.png");
                    EditorApplication.update -= MenuShots;
                    if (Application.isBatchMode) EditorApplication.Exit(errorCount > 0 ? 2 : 0);
                    else EditorApplication.ExitPlaymode();
                }
            }
        }

        static void ShowMenuPage(MainMenuUI menu, string page)
        {
            var pageType = typeof(MainMenuUI).GetNestedType("Page", System.Reflection.BindingFlags.NonPublic);
            typeof(MainMenuUI).GetMethod("Show", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(menu, new[] { Enum.Parse(pageType, page) });
        }

        static void Capture(string path)
        {
            var cam = Camera.main;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f + (30 - c.sortingOrder) * 0.1f;
            }

            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.DestroyImmediate(rt);
        }

        static void Watchdog()
        {
            MaybeScreenshot();
            if (EditorApplication.timeSinceStartup - startedAt < RealTimeLimit) return;
            Finish(false, "TIMED OUT: the run never ended.");
        }

        static void Finish(bool victory, string report)
        {
            EditorApplication.update -= Watchdog;
            Application.logMessageReceived -= CountErrors;
            Time.captureDeltaTime = 0f;

            bool cards = SessionState.GetBool(CardsKey, true);
            var maps = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            string mapName = maps != null && maps.Current != null ? maps.Current.displayName : "?";
            string text = $"Ten Candles autoplay ({(cards ? "with cards" : "no cards")}) on {mapName}{overridesApplied}\n{report}\nErrors logged: {errorCount}\n";
            Directory.CreateDirectory("Logs");
            string tag = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-tag="))?.Substring(5) ?? (cards ? "cards" : "nocards");
            File.WriteAllText($"Logs/autoplay_{tag}.txt", text);
            Debug.Log("[TenCandles] " + text);

            if (Application.isBatchMode) EditorApplication.Exit(errorCount > 0 ? 2 : 0);
            else EditorApplication.ExitPlaymode();
        }
    }
}
