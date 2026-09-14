using UnityEngine;
using UnityEngine.EventSystems;

namespace TenCandles
{
    // Builds every screen-space UI element at startup.
    public class HUD : MonoBehaviour
    {
        public const float TopBarHeight = 90f;
        public const float SidePanelWidth = 250f;

        [SerializeField] UISkin skin;

        // The in-game panels; the main menu hides them so the title screen stays clean.
        public static Canvas GameplayCanvas { get; private set; }

        // Start, not Awake: the panels read BuildManager and friends, which must exist first.
        void Start()
        {
            UIFactory.Init(skin);
            EnsureEventSystem();

            // Below the HUD: darkening and vignette sit over the world but under the panels.
            var fx = UIFactory.Canvas("FX Canvas", transform, 0);
            fx.gameObject.AddComponent<ScreenEffectsUI>().Build();

            var hud = UIFactory.Canvas("HUD Canvas", transform, 10);
            GameplayCanvas = hud;
            var root = (RectTransform)hud.transform;
            root.gameObject.AddComponent<PopupsUI>().Build(root);
            root.gameObject.AddComponent<TopBarUI>().Build(root);
            root.gameObject.AddComponent<BuildPanelUI>().Build(root);
            root.gameObject.AddComponent<WavePanelUI>().Build(root);
            root.gameObject.AddComponent<TowerInfoUI>().Build(root);
            root.gameObject.AddComponent<BuildRingUI>().Build(root);
            root.gameObject.AddComponent<HostSpeechUI>().Build(root);
            root.gameObject.AddComponent<HintUI>().Build(root);
            root.gameObject.AddComponent<YearBannerUI>().Build(root);

            var overlay = UIFactory.Canvas("Overlay Canvas", transform, 20);
            var overlayRoot = (RectTransform)overlay.transform;
            overlayRoot.gameObject.AddComponent<LevelUpUI>().Build(overlayRoot);
            overlayRoot.gameObject.AddComponent<MainMenuUI>().Build(overlayRoot);
            overlayRoot.gameObject.AddComponent<PauseMenuUI>().Build(overlayRoot);
            overlayRoot.gameObject.AddComponent<EndScreenUI>().Build(overlayRoot);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
