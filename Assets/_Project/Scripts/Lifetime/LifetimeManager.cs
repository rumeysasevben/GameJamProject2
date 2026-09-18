using System.Collections.Generic;
using TenCandles.Core;
using TenCandles.Waves;
using UnityEngine;

namespace TenCandles.Lifetime
{
    // Owns the RunState: which decade slot is active, the age, and the stats derived from them
    // (candle cap and lanes per tier, tower capacity per age). GameManager drives it; it never changes state itself.
    // The Stay / Advance rules themselves live in BirthdayController.
    public sealed class LifetimeManager : MonoBehaviour
    {
        public static LifetimeManager Instance { get; private set; }

        public RunState Run { get; private set; }
        public DecadeSlot CurrentSlot => Run?.CurrentSlot;
        public int Age => Run != null ? Run.CurrentAge : 0;
        // Age-derived capacity plus any TowerCapacityAdd bonus (the Established passive).
        public int TowerCapacity => TowerCapacityFor(Mathf.Max(1, Age)) + StatRegistry.TowerCapacityBonus;
        // Options for the birthday on screen; only meaningful in GameState.Birthday.
        public BirthdayOptions Options { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // The run's seeded RNG. Outside a run (menus, tools) a fixed-seed fallback keeps callers deterministic.
        static readonly System.Random Fallback = new System.Random(0);
        public static System.Random RunRandom => Instance != null && Instance.Run != null ? Instance.Run.Rng : Fallback;
        static readonly System.Random CombatFallback = new System.Random(1);
        public static System.Random CombatRandom => Instance != null && Instance.Run != null ? Instance.Run.CombatRng : CombatFallback;

        public static int TowerCapacityFor(int age) => Balance.TowerCapacityBase + age / Balance.TowerCapacityPerAge;

        // First age at which capacity goes up again.
        public static int NextCapacityAge(int age) => (age / Balance.TowerCapacityPerAge + 1) * Balance.TowerCapacityPerAge;

        // Indexed by tier, not stage: the shrinking cap is ageing, so Stay cannot dodge it.
        public static int CandleCapFor(int tier) => Balance.TierCandleCap[Mathf.Clamp(tier, 0, Balance.TierCandleCap.Length - 1)];

        // §2/§6: the candle bar in a run. Tier cap, plus bonus candles from cards and passives, minus every candle
        // wished away. Wish legality keeps it at WishMinCandleCap or above; the floor here is only a safety net.
        public static int RunningCandleCap(int tier, int bonusCandles, int candlesWished)
            => Mathf.Max(Balance.WishMinCandleCap, CandleCapFor(tier) + bonusCandles - candlesWished);

        // Recomputes the clock's cap for the current tier, after a wish or a MaxCandlesAdd.
        public void RefreshCandleCap() => ApplyCandleCap(Run.CurrentTier);

        void ApplyCandleCap(int tier)
        {
            var clock = CandleClock.Instance;
            if (clock != null && Run != null) clock.SetCandleCap(RunningCandleCap(tier, StatRegistry.BonusCandles, Run.CandlesWished));
        }

        public static int ActiveLanes(int mapEntrances, int tier) => WaveGenerator.ActiveLanes(mapEntrances, tier);

        // Birthdays close decades 1-3; the end of the fourth decade is the end of the run.
        public static bool IsBirthdayAge(int age) => age > 0 && age % Balance.YearsPerDecade == 0 && age < Balance.TotalYears;

        // The wish shown before each birthday (§6). Its gifts come from the Lifetime cards in the card pool.
        public WishSystem Wish { get; private set; }

        public void BeginRun(int seed)
        {
            Run = BirthdayController.NewRun(seed);
            Wish = new WishSystem(Run, LevelUpManager.Instance != null ? LevelUpManager.Instance.Pool : null, RefreshCandleCap);
            EnterSlot(Run.Slots[0]);
        }

        public void AdvanceAge() => Run.CurrentAge++;

        public BirthdayOptions OfferBirthday() => Options = BirthdayController.Evaluate(Run);

        // Tower ids granted by the last birthday, for the UI.
        public IReadOnlyList<string> LastGrants { get; private set; } = new List<string>();

        // Appends the next decade slot. Call at a birthday age, before AdvanceAge. Returns false if the choice is illegal.
        public bool ResolveBirthday(BirthdayChoice choice, IEnumerable<TowerUnlock> catalogue)
        {
            if (!BirthdayController.Evaluate(Run).IsLegal(choice)) return false;

            LastGrants = BirthdayController.Resolve(Run, choice, catalogue);
            DecadeSlot slot = Run.Slots[Run.CurrentTier + 1];

            // The mastery passive is a permanent stat entry. Applied before EnterSlot, so a Long Summer candle
            // counts as a bonus candle on top of the new tier's cap.
            MasteryPassive passive = choice == BirthdayChoice.Stay ? BirthdayController.PassiveFor(slot.Stage) : null;
            if (passive != null) UpgradeEffect.Apply(passive.Effect, passive.Value, default, passive.Name);

            GameEvents.RaiseBirthdayChosen(choice, slot.Stage);
            EnterSlot(slot);
            return true;
        }

        void EnterSlot(DecadeSlot slot)
        {
            ApplyCandleCap(slot.TierIndex);
            GameEvents.RaiseStageChanged(slot.Stage, slot.TierIndex);
        }
    }
}
