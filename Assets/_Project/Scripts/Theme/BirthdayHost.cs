using UnityEngine;

namespace TenCandles
{
    // Farum, whose birthday it is. Stands behind the cake, reacts to the fight and talks to the player.
    public class BirthdayHost : MonoBehaviour
    {
        public static BirthdayHost Instance { get; private set; }

        [SerializeField] Sprite sprite;
        [Tooltip("Height in world units.")]
        [SerializeField] float height = 1.25f;

        SpriteRenderer body;
        float hopAt = -10f, panicAt = -10f;
        bool celebrating, defeated;
        bool greeted;

        public string Line { get; private set; } = "";
        public float LineUntil { get; private set; }
        // Top of the head in world space, for the speech bubble.
        public Vector3 HeadPosition => body.bounds.center + Vector3.up * body.bounds.extents.y;

        static readonly string[] CandleLost =
        {
            "No! You let one of my candles go out!",
            "My candle! Don't let them near the cake!",
            "They're blowing out my candles! Stop them!"
        };

        static readonly string[] YearWon =
        {
            "You did it! One more year!",
            "The cake is safe. You're amazing!",
            "Another year of candles. Thank you!"
        };

        void Awake()
        {
            Instance = this;
            body = new GameObject("Farum").AddComponent<SpriteRenderer>();
            body.transform.SetParent(transform, false);
            body.sprite = sprite;
            body.sortingOrder = -10; // behind the cake, so Farum peeks over it
            if (sprite != null) body.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.StateChanged += OnState;
            GameEvents.CandleExtinguished += OnCandleOut;
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.GameOver += OnGameOver;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnState;
            GameEvents.CandleExtinguished -= OnCandleOut;
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.GameOver -= OnGameOver;
        }

        // Stand behind the cake, on the side away from the road.
        public void PlaceBeside(Vector2 roadDirection)
        {
            bool horizontal = Mathf.Abs(roadDirection.x) >= Mathf.Abs(roadDirection.y);
            transform.localPosition = horizontal ? new Vector3(0.45f, 0.35f, 0f) : new Vector3(-1.3f, -0.35f, 0f);
        }

        public void Say(string line, float seconds = 3f)
        {
            Line = line;
            LineUntil = Time.unscaledTime + seconds;
        }

        void OnState(GameState state)
        {
            if (state == GameState.Intermission && !greeted)
            {
                greeted = true;
                hopAt = Time.unscaledTime;
                Say($"My birthday is almost here! Will you keep my candles lit?", 4.5f);
            }
        }

        void OnCandleOut(int index)
        {
            if (defeated) return;
            panicAt = Time.unscaledTime;
            Say(CandleLost[Random.Range(0, CandleLost.Length)], 2.5f);
        }

        void OnWaveCleared(int year)
        {
            hopAt = Time.unscaledTime;
            Say(YearWon[Random.Range(0, YearWon.Length)], 2.5f);
        }

        void OnWaveStarted(int year)
        {
            if (year >= 10) Say("That's the Orc Warlord! You have to stop it!", 3f);
            else if (WaveManager.Instance != null && WaveManager.Instance.SecondRoadOpensThisYear(year)) Say("They're coming down the other road too! Watch both sides!", 3.5f);
        }

        void OnGameOver(bool victory)
        {
            celebrating = victory;
            defeated = !victory;
            Say(victory ? "Best birthday ever. Thank you!" : "My candles... they're all out.", 6f);
        }

        void Update()
        {
            if (body == null || body.sprite == null) return;
            float now = Time.unscaledTime;

            float y = 0f, squash = 1f, x = 0f, tilt = 0f;
            float breathe = Mathf.Sin(now * 2.2f) * 0.03f;

            float hop = (now - hopAt) / 0.5f;
            if (celebrating) hop = Mathf.Repeat(now * 1.6f, 1f);
            if (hop >= 0f && hop < 1f)
            {
                y = Mathf.Sin(hop * Mathf.PI) * 0.35f;
                squash = 1f + Mathf.Sin(hop * Mathf.PI) * 0.08f;
            }

            float panic = (now - panicAt) / 0.8f;
            if (panic >= 0f && panic < 1f)
            {
                x = Mathf.Sin(panic * 40f) * 0.06f * (1f - panic);
                tilt = Mathf.Sin(panic * 30f) * 6f * (1f - panic);
            }

            if (defeated)
            {
                squash = 0.88f;
                tilt = -8f;
            }

            float scale = height / body.sprite.bounds.size.y;
            body.transform.localPosition = new Vector3(x, y, 0f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            body.transform.localScale = new Vector3(scale / squash, scale * (squash + breathe), 1f);
            body.color = defeated ? new Color(0.6f, 0.6f, 0.65f) : Color.white;
        }
    }
}
