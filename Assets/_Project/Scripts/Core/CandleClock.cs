using UnityEngine;

namespace TenCandles
{
    // The only owner of timeRemaining. Every gain, cost and penalty goes through here.
    public class CandleClock : MonoBehaviour
    {
        public const float SecondsPerCandle = 30f;
        public const int BaseCandleCount = 10;

        public static CandleClock Instance { get; private set; }

        [SerializeField] float drainRate = 1f;

        public float TimeRemaining { get; private set; }
        public float MaxTime { get; private set; }
        public int CandleCount => Mathf.RoundToInt(MaxTime / SecondsPerCandle);
        public int LitCandles => litCandles;

        // GameManager turns this on for Intermission and Wave only.
        public bool IsDraining { get; set; }

        int litCandles;
        bool ranOut;

        void Awake()
        {
            Instance = this;
            MaxTime = BaseCandleCount * SecondsPerCandle;
            TimeRemaining = MaxTime;
            litCandles = LitCount(TimeRemaining);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => GameEvents.RaiseTimeChanged(TimeRemaining, MaxTime);

        void Update()
        {
            if (IsDraining) SetTime(TimeRemaining - drainRate * StatRegistry.DrainMultiplier * Time.deltaTime);
        }

        // Kill rewards, early-call bonus, card bonuses. Anything above MaxTime is lost.
        public void Add(float seconds, string reason = null)
        {
            if (seconds <= 0f) return;
            SetTime(TimeRemaining + seconds);
            if (reason != null) GameEvents.RaiseTimeAdjusted(seconds, reason);
        }

        // Leak penalties. Allowed to hit zero.
        public void Drain(float seconds, string reason = null)
        {
            if (seconds <= 0f) return;
            if (reason != null) GameEvents.RaiseTimeAdjusted(-seconds, reason);
            SetTime(TimeRemaining - seconds);
        }

        public bool CanAfford(float cost) => TimeRemaining > cost;

        // Tower builds and upgrades. Refuses a purchase that would put out the last candle.
        public bool TrySpend(float cost, string reason = null)
        {
            if (!CanAfford(cost)) return false;
            Drain(cost, reason);
            return true;
        }

        // "The Eleventh Candle" card: one more candle on the bar, lit.
        public void AddCandle()
        {
            MaxTime += SecondsPerCandle;
            GameEvents.RaiseCandleCountChanged(CandleCount);
            Add(SecondsPerCandle, "You earned a new candle!");
        }

        // 0..1 height of candle i, straight from the GDD formula.
        public float CandleFill(int index) => Mathf.Clamp01(TimeRemaining / SecondsPerCandle - index);

        void SetTime(float value)
        {
            if (ranOut) return;

            TimeRemaining = Mathf.Clamp(value, 0f, MaxTime);

            int lit = LitCount(TimeRemaining);
            for (int i = litCandles - 1; i >= lit; i--) GameEvents.RaiseCandleExtinguished(i);
            for (int i = litCandles; i < lit; i++) GameEvents.RaiseCandleRestored(i);
            litCandles = lit;

            GameEvents.RaiseTimeChanged(TimeRemaining, MaxTime);

            if (TimeRemaining <= 0f)
            {
                ranOut = true;
                IsDraining = false;
                GameEvents.RaiseTimeRanOut();
            }
        }

        static int LitCount(float time) => Mathf.CeilToInt(time / SecondsPerCandle - 0.0001f);
    }
}
