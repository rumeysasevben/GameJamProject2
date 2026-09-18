using System;
using System.Collections.Generic;
using TenCandles.Core;

namespace TenCandles.Lifetime
{
    // How a tower becomes buildable (spec §9.1).
    public enum UnlockSource { Starting, StageVisit, StageMastery }

    // The unlock part of a TowerData, so the rules can run without Unity assets (tests, balance tools).
    public readonly struct TowerUnlock
    {
        public readonly string Id;
        public readonly UnlockSource Source;
        public readonly DecadeStage Stage;

        public TowerUnlock(string id, UnlockSource source, DecadeStage stage)
        {
            Id = id;
            Source = source;
            Stage = stage;
        }
    }

    public readonly struct BirthdayOptions
    {
        public readonly bool StayLegal, AdvanceLegal;
        // Empty when the option is legal.
        public readonly string StayBlockedReason, AdvanceBlockedReason;

        public BirthdayOptions(bool stayLegal, string stayReason, bool advanceLegal, string advanceReason)
        {
            StayLegal = stayLegal;
            StayBlockedReason = stayReason ?? "";
            AdvanceLegal = advanceLegal;
            AdvanceBlockedReason = advanceReason ?? "";
        }

        public bool IsLegal(BirthdayChoice choice) => choice == BirthdayChoice.Stay ? StayLegal : AdvanceLegal;
    }

    // A permanent stat entry granted with a stage's mastery (§5.1), applied through the card pipeline.
    public sealed class MasteryPassive
    {
        public readonly string Name;
        public readonly CardEffect Effect;
        public readonly float Value;
        public readonly string Description;

        public MasteryPassive(string name, CardEffect effect, float value, string description)
        {
            Name = name;
            Effect = effect;
            Value = value;
            Description = description;
        }
    }

    // Spec §5: Stay / Advance legality, building the next decade slot, mastery / visit tower grants and mastery passives.
    // Pure rules over RunState; LifetimeManager applies the results to the clock and raises events.
    public static class BirthdayController
    {
        static readonly MasteryPassive LongSummer = new MasteryPassive("Long Summer", CardEffect.ExtraCandle, Balance.LongSummerCandles,
            $"+{Balance.LongSummerCandles} candle");
        static readonly MasteryPassive BoundlessEnergy = new MasteryPassive("Boundless Energy", CardEffect.FireRate, Balance.BoundlessEnergyFireRate,
            $"+{Math.Round(Balance.BoundlessEnergyFireRate * 100f)}% tower fire rate");
        static readonly MasteryPassive Established = new MasteryPassive("Established", CardEffect.TowerCapacityAdd, Balance.EstablishedTowerCapacity,
            $"+{Balance.EstablishedTowerCapacity} tower allowed");

        // Null for Old Age: birthdays are at 10/20/30 only, so Old Age can never be stayed in.
        public static MasteryPassive PassiveFor(DecadeStage stage) => stage switch
        {
            DecadeStage.Childhood => LongSummer,
            DecadeStage.Youth => BoundlessEnergy,
            DecadeStage.Adulthood => Established,
            _ => null
        };

        // Every passive this run has earned, in order: one per repeated decade.
        public static List<MasteryPassive> Passives(RunState run)
        {
            var list = new List<MasteryPassive>();
            foreach (DecadeSlot slot in run.Slots)
                if (slot != null && slot.IsRepeat && PassiveFor(slot.Stage) != null) list.Add(PassiveFor(slot.Stage));
            return list;
        }

        public static DecadeStage Next(DecadeStage s, BirthdayChoice c)
            => c == BirthdayChoice.Stay ? s : (DecadeStage)Math.Min(3, (int)s + 1);

        public static DecadeSlot MakeSlot(int tier, DecadeStage stage, bool isRepeat) => new DecadeSlot
        {
            TierIndex = tier,
            Stage = stage,
            IsRepeat = isRepeat,
            AgeFrom = tier * Balance.YearsPerDecade + 1,
            AgeTo = tier * Balance.YearsPerDecade + Balance.YearsPerDecade
        };

        // Slot 0 is always Childhood.
        public static RunState NewRun(int seed)
        {
            var run = new RunState { Seed = seed, CurrentAge = 1, Rng = new Random(seed), CombatRng = new Random(unchecked(seed * 31 + 7)), WishRng = new Random(unchecked(seed * 31 + 13)) };
            run.Slots[0] = MakeSlot(0, DecadeStage.Childhood, false);
            run.VisitedStages.Add(DecadeStage.Childhood);
            return run;
        }

        // Options for the birthday that closes the run's current decade.
        public static BirthdayOptions Evaluate(RunState run)
        {
            DecadeSlot current = run.CurrentSlot;
            if (current.TierIndex >= Balance.DecadesPerRun - 1)
                return new BirthdayOptions(false, "No birthday after the last decade.", false, "No birthday after the last decade.");

            bool stayLegal = ConsecutiveSlots(run, current.TierIndex) < Balance.MaxVisitsPerStage;
            string stayReason = stayLegal ? "" : $"You have already spent {Balance.MaxVisitsPerStage} decades in {current.Stage.Display()}.";

            bool advanceLegal = current.Stage != DecadeStage.OldAge;
            string advanceReason = advanceLegal ? "" : "Old Age is the last stage.";

            return new BirthdayOptions(stayLegal, stayReason, advanceLegal, advanceReason);
        }

        // Appends the next slot and applies the grants. Returns the tower ids granted by this choice.
        public static List<string> Resolve(RunState run, BirthdayChoice choice, IEnumerable<TowerUnlock> catalogue)
        {
            BirthdayOptions options = Evaluate(run);
            if (!options.IsLegal(choice))
                throw new InvalidOperationException($"{choice} is illegal at age {run.CurrentAge}: " +
                    (choice == BirthdayChoice.Stay ? options.StayBlockedReason : options.AdvanceBlockedReason));

            DecadeSlot current = run.CurrentSlot;
            int tier = current.TierIndex + 1;
            DecadeStage stage = Next(current.Stage, choice);
            run.Slots[tier] = MakeSlot(tier, stage, stage == current.Stage);
            run.Choices.Add(choice);

            var granted = new List<string>();
            if (choice == BirthdayChoice.Stay)
            {
                // The mastery of the stage being repeated, kept for the rest of the run.
                foreach (string id in GrantIds(catalogue, UnlockSource.StageMastery, stage))
                    if (!run.Masteries.Contains(id))
                    {
                        run.Masteries.Add(id);
                        granted.Add(id);
                    }
            }
            else if (run.VisitedStages.Add(stage))
            {
                granted.AddRange(GrantIds(catalogue, UnlockSource.StageVisit, stage));
            }
            return granted;
        }

        // Starting towers always; visit towers of every stage lived in; mastery towers once earned by Stay.
        public static bool IsUnlocked(RunState run, TowerUnlock tower) => tower.Source switch
        {
            UnlockSource.Starting => true,
            UnlockSource.StageVisit => run != null && run.VisitedStages.Contains(tower.Stage),
            _ => run != null && run.Masteries.Contains(tower.Id)
        };

        // Tower ids a choice would grant from the current slot, for the birthday screen.
        public static List<string> PreviewGrants(RunState run, BirthdayChoice choice, IEnumerable<TowerUnlock> catalogue)
        {
            DecadeStage stage = Next(run.CurrentSlot.Stage, choice);
            if (choice == BirthdayChoice.Advance && run.VisitedStages.Contains(stage)) return new List<string>();
            return GrantIds(catalogue, choice == BirthdayChoice.Stay ? UnlockSource.StageMastery : UnlockSource.StageVisit, stage);
        }

        // Spec §5.2 names, keyed by the choice sequence ("AAS" etc.). Null for an unfinished or unnamed life.
        public static string LifeName(IReadOnlyList<BirthdayChoice> choices) => Code(choices) switch
        {
            "AAA" => "Full Life",
            "AAS" => "Late Bloomer",
            "ASA" => "Long Youth",
            "SAA" => "Slow Childhood",
            "SAS" => "Never Grew Up",
            _ => null
        };

        public static string Code(IReadOnlyList<BirthdayChoice> choices)
        {
            var chars = new char[choices.Count];
            for (int i = 0; i < chars.Length; i++) chars[i] = choices[i] == BirthdayChoice.Stay ? 'S' : 'A';
            return new string(chars);
        }

        // How many slots up to and including `tier` share that slot's stage, counting back without a break.
        static int ConsecutiveSlots(RunState run, int tier)
        {
            int count = 0;
            for (int t = tier; t >= 0 && run.Slots[t] != null && run.Slots[t].Stage == run.Slots[tier].Stage; t--) count++;
            return count;
        }

        static List<string> GrantIds(IEnumerable<TowerUnlock> catalogue, UnlockSource source, DecadeStage stage)
        {
            var ids = new List<string>();
            if (catalogue == null) return ids;
            foreach (var t in catalogue)
                if (t.Source == source && t.Stage == stage && !string.IsNullOrEmpty(t.Id) && !ids.Contains(t.Id)) ids.Add(t.Id);
            return ids;
        }
    }
}
