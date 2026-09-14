using UnityEngine;

namespace TenCandles
{
    // Player options from the Settings page, saved between sessions.
    public static class GameSettings
    {
        const string ShakeKey = "TenCandles.ScreenShake";
        const string HintsKey = "TenCandles.Hints";

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set => PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
        }

        public static bool Hints
        {
            get => PlayerPrefs.GetInt(HintsKey, 1) == 1;
            set => PlayerPrefs.SetInt(HintsKey, value ? 1 : 0);
        }

        // Browsers own the window, so fullscreen is only offered on desktop builds.
        public static bool CanToggleFullscreen => Application.platform != RuntimePlatform.WebGLPlayer;

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreen = value;
        }

        public static bool CanQuit => Application.platform != RuntimePlatform.WebGLPlayer;
    }
}
