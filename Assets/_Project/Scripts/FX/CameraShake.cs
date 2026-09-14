using UnityEngine;

namespace TenCandles
{
    // Trauma-style shake on real time, so it still plays during hit-stop.
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [SerializeField] float maxOffset = 0.35f;
        [SerializeField] float decayPerSecond = 2.2f;

        Vector3 origin;
        float trauma;

        void Awake()
        {
            Instance = this;
            origin = transform.localPosition;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Add(float amount)
        {
            if (Instance != null && GameSettings.ScreenShake) Instance.trauma = Mathf.Clamp01(Instance.trauma + amount);
        }

        void LateUpdate()
        {
            if (trauma <= 0f)
            {
                transform.localPosition = origin;
                return;
            }

            float t = Time.unscaledTime * 40f;
            float power = trauma * trauma * maxOffset;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * 2f * power;
            transform.localPosition = origin + offset;
            trauma = Mathf.Max(0f, trauma - decayPerSecond * Time.unscaledDeltaTime);
        }
    }
}
