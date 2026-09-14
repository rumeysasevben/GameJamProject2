using TenCandles.Core;
using UnityEngine;

namespace TenCandles.Lifetime
{
    // Owns the RunState: which decade slot is active, the age, and the stats derived from them
    // (candle cap per stage, tower capacity per age). GameManager drives it; it never changes state itself.
    public sealed class LifetimeManager : MonoBehaviour
    {
        public static LifetimeManager Instance { get; private set; }

        public RunState Run { get; private set; }
        public DecadeSlot CurrentSlot => Run?.CurrentSlot;
        public int Age => Run != null ? Run.CurrentAge : 0;
        public int TowerCapacity => TowerCapacityFor(Mathf.Max(1, Age));

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static int TowerCapacityFor(int age) => Balance.TowerCapacityBase + age / Balance.TowerCapacityPerAge;

        // First age at which capacity goes up again.
        public static int NextCapacityAge(int age) => (age / Balance.TowerCapacityPerAge + 1) * Balance.TowerCapacityPerAge;

        // Indexed by stage. With Advance-only runs (Phase 1) stage and tier are the same.
        public static int CandleCapFor(DecadeStage stage) => Balance.StageCandleCap[(int)stage];

        public static DecadeStage Next(DecadeStage s, BirthdayChoice c)
            => c == BirthdayChoice.Stay ? s : (DecadeStage)Mathf.Min(3, (int)s + 1);

        // Birthdays close decades 1-3; the end of the fourth decade is the end of the run.
        public static bool IsBirthdayAge(int age) => age > 0 && age % Balance.YearsPerDecade == 0 && age < Balance.TotalYears;

        public void BeginRun(int seed)
        {
            Run = new RunState { Seed = seed, CurrentAge = 1 };
            Run.Slots[0] = MakeSlot(0, DecadeStage.Childhood, false);
            EnterSlot(Run.Slots[0]);
        }

        public void AdvanceAge() => Run.CurrentAge++;

        // Appends the next decade slot. Call at a birthday age, before AdvanceAge.
        public void ResolveBirthday(BirthdayChoice choice)
        {
            DecadeSlot current = Run.CurrentSlot;
            int tier = current.TierIndex + 1;
            Debug.Assert(tier < Balance.DecadesPerRun, "No birthday after the last decade");
            Debug.Assert(choice == BirthdayChoice.Stay || current.Stage != DecadeStage.OldAge, "Advance is illegal from OldAge");

            DecadeStage stage = Next(current.Stage, choice);
            var slot = MakeSlot(tier, stage, stage == current.Stage);
            Run.Slots[tier] = slot;
            Run.Choices.Add(choice);
            GameEvents.RaiseBirthdayChosen(choice, stage);
            EnterSlot(slot);
        }

        void EnterSlot(DecadeSlot slot)
        {
            Run.VisitedStages.Add(slot.Stage);

            var clock = CandleClock.Instance;
            if (clock != null)
            {
                // Candles granted by cards stay on top of the stage cap.
                int previousStageCap = slot.TierIndex > 0 ? CandleCapFor(Run.Slots[slot.TierIndex - 1].Stage) : Balance.BaseCandleCount;
                int bonus = Mathf.Max(0, clock.CandleCap - previousStageCap);
                clock.SetCandleCap(CandleCapFor(slot.Stage) + bonus);
            }

            GameEvents.RaiseStageChanged(slot.Stage, slot.TierIndex);
        }

        static DecadeSlot MakeSlot(int tier, DecadeStage stage, bool isRepeat) => new DecadeSlot
        {
            TierIndex = tier,
            Stage = stage,
            IsRepeat = isRepeat,
            AgeFrom = tier * Balance.YearsPerDecade + 1,
            AgeTo = tier * Balance.YearsPerDecade + Balance.YearsPerDecade
        };
    }
}
