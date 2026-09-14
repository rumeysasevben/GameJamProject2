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
        public int TowerCapacity => TowerCapacityFor(Mathf.Max(1, Age));
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

        public static int ActiveLanes(int mapEntrances, int tier) => WaveGenerator.ActiveLanes(mapEntrances, tier);

        // Birthdays close decades 1-3; the end of the fourth decade is the end of the run.
        public static bool IsBirthdayAge(int age) => age > 0 && age % Balance.YearsPerDecade == 0 && age < Balance.TotalYears;

        public void BeginRun(int seed)
        {
            Run = BirthdayController.NewRun(seed);
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
            var clock = CandleClock.Instance;
            if (clock != null)
            {
                // Candles granted by cards stay on top of the tier cap.
                int previousCap = slot.TierIndex > 0 ? CandleCapFor(slot.TierIndex - 1) : Balance.BaseCandleCount;
                int bonus = Mathf.Max(0, clock.CandleCap - previousCap);
                clock.SetCandleCap(CandleCapFor(slot.TierIndex) + bonus);
            }

            GameEvents.RaiseStageChanged(slot.Stage, slot.TierIndex);
        }
    }
}
