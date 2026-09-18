using System;
using System.Collections.Generic;
using TenCandles.Lifetime;

namespace TenCandles.Core
{
    [Serializable]
    public sealed class DecadeSlot
    {
        public int          TierIndex;     // d, 0..3 — drives difficulty
        public DecadeStage  Stage;         // which biome/content set
        public bool         IsRepeat;      // true if the previous slot had the same Stage
        public int          AgeFrom;       // TierIndex*10 + 1
        public int          AgeTo;         // TierIndex*10 + 10
    }

    // Everything that describes one lifetime. Owned by LifetimeManager.
    public sealed class RunState
    {
        public DecadeSlot[]        Slots = new DecadeSlot[Balance.DecadesPerRun];
        public List<BirthdayChoice> Choices = new();      // length 0..3
        public int   CurrentAge;                           // 1..40
        public int   CurrentTier => (CurrentAge - 1) / Balance.YearsPerDecade;
        public int   YearInDecade => ((CurrentAge - 1) % Balance.YearsPerDecade) + 1;
        public HashSet<DecadeStage> VisitedStages = new();
        public List<string> Masteries = new();             // tower ids granted by Stay
        public int   WishesMade;
        // Candles blown at wishes. Each one is off the candle cap for the rest of the run (§6).
        public int   CandlesWished;
        public int   HardYearsTier;
        public int   Seed;
        // Card draws and wave shuffles. Seeded from Seed by BirthdayController.NewRun, like the streams below.
        [NonSerialized] public System.Random Rng;
        // Combat rolls (crits) get their own stream so shot counts never shift card offers or wave order.
        [NonSerialized] public System.Random CombatRng;
        // Wish gift draws, likewise: runs that wish differently still see the same card offers and wave shuffles.
        [NonSerialized] public System.Random WishRng;

        public DecadeSlot CurrentSlot => Slots[Math.Clamp(CurrentTier, 0, Slots.Length - 1)];
    }
}
