using System.Collections;
using UnityEngine;

namespace TenCandles
{
    // Every two years the party grows: background tint, decor groups, rising embers, music layers.
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

        [SerializeField] EraData[] eras;
        [SerializeField] SpriteRenderer background;
        [Tooltip("One group per era. Groups up to the current era are shown.")]
        [SerializeField] GameObject[] decorGroups;
        [SerializeField] int yearsPerEra = 2;
        [SerializeField] float crossfadeSeconds = 0.8f;

        public int EraIndex { get; private set; } = -1;
        public EraData CurrentEra => EraIndex >= 0 && EraIndex < eras.Length ? eras[EraIndex] : null;
        public int EraCount => eras.Length;

        Coroutine fade;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => GameEvents.WaveStarted += OnWaveStarted;
        void OnDisable() => GameEvents.WaveStarted -= OnWaveStarted;

        void Start() => SetEra(0, true);

        void OnWaveStarted(int year) => SetEra((year - 1) / yearsPerEra, false);

        public void SetEra(int index, bool instant)
        {
            index = Mathf.Clamp(index, 0, eras.Length - 1);
            if (index == EraIndex) return;
            EraIndex = index;

            for (int i = 0; i < decorGroups.Length; i++)
                if (decorGroups[i] != null) decorGroups[i].SetActive(i <= index);

            EraData era = eras[index];
            if (background != null)
            {
                if (fade != null) StopCoroutine(fade);
                if (instant) background.color = era.backgroundTint;
                else fade = StartCoroutine(Fade(background.color, era.backgroundTint));
            }

            GameEvents.RaiseEraChanged(index);
        }

        IEnumerator Fade(Color from, Color to)
        {
            for (float t = 0f; t < crossfadeSeconds; t += Time.unscaledDeltaTime)
            {
                background.color = Color.Lerp(from, to, t / crossfadeSeconds);
                yield return null;
            }
            background.color = to;
            fade = null;
        }
    }
}
