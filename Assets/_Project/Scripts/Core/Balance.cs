namespace TenCandles.Core
{
    // Authoritative gameplay constants (spec §2). Every other gameplay number is derived from these.
    public static class Balance
    {
        // ---------- Candle clock ----------
        public const float SecondsPerCandle   = 30f;
        public const int   BaseCandleCount    = 10;
        public const float StartTime          = 300f;   // = 10 candles
        public const float DrainRatePerSecond = 1f;
        public const float LeakPenalty        = 30f;    // one full candle
        public const float IntermissionLength = 9f;
        public const float EarlyCallBonusMul  = 0.5f;   // × remaining intermission

        // ---------- Lifetime ----------
        public const int YearsPerDecade      = 10;
        public const int DecadesPerRun       = 4;
        public const int TotalYears          = 40;      // YearsPerDecade * DecadesPerRun
        public const int MaxVisitsPerStage   = 2;       // legality rule for Stay
        public const float StayHpMultiplier  = 1.30f;   // +30% enemy HP when Stay chosen

        // Candle cap per stage index 0..3
        public static readonly int[] StageCandleCap = { 10, 10, 9, 7 };

        // ---------- Wave scaling ----------
        // d = stage index 0..3 (difficulty tier, NOT which biome)
        // y = year within the decade, 1..10
        public const float BaseEnemyHp = 10f;
        public static readonly float[] StageHpMultiplier = { 1.00f, 2.60f, 6.80f, 17.50f };
        public const float YearHpStep      = 0.18f;
        public const float YearSpeedStep   = 0.03f;
        public const float StageSpeedStep  = 0.05f;
        public const float RewardBase      = 1.10f;
        public const float RewardYearStep  = 0.16f;
        public const float RewardStageMul  = 0.55f;
        public const int   CountBase       = 6;
        public const int   CountYearStep   = 2;
        public const int   CountStageStep  = 3;
        public const float GapBase         = 1.15f;
        public const float GapYearStep     = 0.05f;
        public const float GapStageStep    = 0.04f;
        public const float GapMinimum      = 0.42f;

        // ---------- Age-derived ----------
        public const int TowerCapacityBase = 2;
        public const int TowerCapacityPerAge = 8;       // capacity = Base + age / PerAge

        // ---------- Tower upgrades ----------
        // index 0 unused; levels are 1..5
        public static readonly float[] LevelDamageMultiplier = { 0f, 1.00f, 1.50f, 2.20f, 3.20f, 4.60f };
        public static readonly float[] LevelCostMultiplier   = { 0f, 1.00f, 0.75f, 1.20f, 1.80f, 2.60f };
        public static readonly float[] LevelRangeMultiplier  = { 0f, 1.00f, 1.06f, 1.12f, 1.18f, 1.25f };
        public const int MaxTowerLevel = 5;

        // ---------- Wish ----------
        // candles blown -> tier. Cost = candles * SecondsPerCandle
        public static readonly int[] WishCandleOptions = { 0, 1, 3, 5 };

        // ---------- Wind ----------
        public const float WindTelegraphSeconds = 3f;
        public const float WindDrainMultiplier  = 2f;

        // ---------- Cards ----------
        public const int CardsOfferedPerYear = 3;
        public const int RareUnlockAge       = 12;
        public const int EpicUnlockAge       = 20;

        // ---------- Adjacency ----------
        public const float AdjacencyRadius = 1.5f;      // world units, slot centre to slot centre

        // ---------- Meta ----------
        public const int MemoryPerAge        = 1;
        public const int MemoryPerStageVisit = 10;
        public const int MemoryPerWish       = 5;
        public const int HardYearsMaxTier    = 15;

        // ---------- Balance harness (not in spec §2; added for Phase 1) ----------
        // Seconds an enemy spends on the road after its spawn slot, used by BalanceSimulator for
        // "Duration = Count × Gap + walk". Reproduces the §8 Duration column.
        public const float SimWalkSeconds = 7f;
    }
}
