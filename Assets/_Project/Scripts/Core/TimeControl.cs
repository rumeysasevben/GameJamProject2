using UnityEngine;

namespace TenCandles
{
    // The only script that writes Time.timeScale, so the level-up pause and hit-stops never fight.
    public class TimeControl : MonoBehaviour
    {
        static bool paused;
        static bool menuPaused;
        static float hitStopUntil;
        static float hitStopScale = 1f;

        public static bool IsPaused => paused || menuPaused;
        // The Esc pause menu. Separate from the level-up pause so closing one never unpauses the other.
        public static bool IsMenuPaused => menuPaused;

        public static void SetPaused(bool value)
        {
            paused = value;
            Apply();
        }

        public static void SetMenuPaused(bool value)
        {
            menuPaused = value;
            Apply();
        }

        // Freezes (or slows) the game for a few real-time seconds.
        public static void HitStop(float seconds, float scale = 0f)
        {
            float until = Time.unscaledTime + seconds;
            if (Time.unscaledTime >= hitStopUntil) hitStopScale = scale;
            else hitStopScale = Mathf.Min(hitStopScale, scale);
            hitStopUntil = Mathf.Max(hitStopUntil, until);
            Apply();
        }

        void Awake()
        {
            paused = false;
            menuPaused = false;
            hitStopUntil = 0f;
            hitStopScale = 1f;
            Time.timeScale = 1f;
        }

        void Update() => Apply();

        void OnDestroy() => Time.timeScale = 1f;

        static void Apply()
        {
            if (paused || menuPaused) Time.timeScale = 0f;
            else if (Time.unscaledTime < hitStopUntil) Time.timeScale = hitStopScale;
            else Time.timeScale = 1f;
        }
    }
}
