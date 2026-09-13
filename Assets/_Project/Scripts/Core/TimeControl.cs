using UnityEngine;

namespace TenCandles
{
    // The only script that writes Time.timeScale, so the level-up pause and hit-stops never fight.
    public class TimeControl : MonoBehaviour
    {
        static bool paused;
        static float hitStopUntil;
        static float hitStopScale = 1f;

        public static bool IsPaused => paused;

        public static void SetPaused(bool value)
        {
            paused = value;
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
            hitStopUntil = 0f;
            hitStopScale = 1f;
            Time.timeScale = 1f;
        }

        void Update() => Apply();

        void OnDestroy() => Time.timeScale = 1f;

        static void Apply()
        {
            if (paused) Time.timeScale = 0f;
            else if (Time.unscaledTime < hitStopUntil) Time.timeScale = hitStopScale;
            else Time.timeScale = 1f;
        }
    }
}
