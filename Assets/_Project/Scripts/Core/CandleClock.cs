using TenCandles.Core;
using UnityEngine;

namespace TenCandles
{
    // The only owner of timeRemaining. Every gain, cost and penalty goes through here.
    public class CandleClock : MonoBehaviour
    {
        public static CandleClock Instance { get; private set; }

        public float TimeRemaining { get; private set; }
        public float MaxTime { get; private set; }
        // Current cap in candles: the stage's cap plus any candles granted by cards.
        public int CandleCap { get; private set; }
        public int CandleCount => CandleCap;
        public int LitCandles => litCandles;

        // GameManager turns this on for Intermission and Wave only.
        public bool IsDraining { get; set; }

        int litCandles;
        bool ranOut;

        void Awake()
        {
            Instance = this;
            CandleCap = Balance.BaseCandleCount;
            MaxTime = CandleCap * Balance.SecondsPerCandle;
            TimeRemaining = Balance.StartTime;
            litCandles = LitCount(TimeRemaining);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => GameEvents.RaiseTimeChanged(TimeRemaining, MaxTime);

        void Update()
        {
            if (IsDraining) SetTime(TimeRemaining - Balance.DrainRatePerSecond * StatRegistry.DrainMultiplier * Time.deltaTime);
        }

        // Kill rewards, early-call bonus, card bonuses. Anything above MaxTime is lost and reported as TimeOverflow (§3.2).
        public void Add(float seconds, string reason = null)
        {
            if (seconds <= 0f) return;
            float wasted = Mathf.Max(0f, TimeRemaining + seconds - MaxTime);
            SetTime(TimeRemaining + seconds);
            if (reason != null) GameEvents.RaiseTimeAdjusted(seconds, reason);
            if (wasted > 0f && !ranOut) GameEvents.RaiseTimeOverflow(wasted);
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

        // Stage change or card effect. Lowering the cap clamps TimeRemaining to the new MaxTime at once.
        public void SetCandleCap(int candles)
        {
            candles = Mathf.Max(1, candles);
            if (candles == CandleCap) return;

            CandleCap = candles;
            MaxTime = candles * Balance.SecondsPerCandle;
            // Candles cut off by a lower cap leave the bar; they are not "blown out" by an enemy.
            litCandles = Mathf.Min(litCandles, candles);
            GameEvents.RaiseCandleCountChanged(candles);
            SetTime(TimeRemaining);
        }

        // MaxCandlesAdd ("The Eleventh Candle"): one more candle on the bar, unlit (§11.1). Raising the cap never
        // fills it; the player still has to earn the seconds.
        public void AddCandle() => SetCandleCap(CandleCap + 1);

        public int FullCandles => Mathf.FloorToInt(TimeRemaining / Balance.SecondsPerCandle);
        public float PartialFill => (TimeRemaining % Balance.SecondsPerCandle) / Balance.SecondsPerCandle;

        // 0..1 height of candle i, straight from the GDD formula.
        public float CandleFill(int index) => Mathf.Clamp01(TimeRemaining / Balance.SecondsPerCandle - index);

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

        static int LitCount(float time) => Mathf.CeilToInt(time / Balance.SecondsPerCandle - 0.0001f);
    }
}
