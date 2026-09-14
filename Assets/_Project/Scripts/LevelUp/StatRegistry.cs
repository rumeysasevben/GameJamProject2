namespace TenCandles
{
    // The single home of every global multiplier. Towers read their stats through here.
    public static class StatRegistry
    {
        public const float CritDamageMultiplier = 2.5f;
        const int TowerKinds = 4;

        public static float DamageMultiplier;
        public static float FireRateMultiplier;
        public static float RangeMultiplier;
        public static float SplashRadiusMultiplier;
        public static float BuildCostMultiplier;
        public static float UpgradeCostMultiplier;
        public static float KillBonus;
        public static float WaveEndBonus;
        public static float LeakPenaltyReduction;
        public static float CritChance;
        public static float CandleSlowBonus;
        public static float BurnMultiplier;
        public static float DrainMultiplier;
        public static float HitSlowPercent;
        public static bool ConfettiChain;
        public static bool LastBreathArmed;
        public static bool IgnoreArmor;
        public static int FreeBuilds;

        // Per tower kind, indexed by (int)TowerKind.
        public static readonly float[] KindDamage = new float[TowerKinds];
        public static readonly float[] KindFireRate = new float[TowerKinds];

        static StatRegistry() => Reset();

        public static float DamageFor(TowerKind kind) => DamageMultiplier * KindDamage[(int)kind];
        public static float FireRateFor(TowerKind kind) => FireRateMultiplier * KindFireRate[(int)kind];

        public static void Reset()
        {
            DamageMultiplier = 1f;
            FireRateMultiplier = 1f;
            RangeMultiplier = 1f;
            SplashRadiusMultiplier = 1f;
            BuildCostMultiplier = 1f;
            UpgradeCostMultiplier = 1f;
            KillBonus = 0f;
            WaveEndBonus = 0f;
            LeakPenaltyReduction = 0f;
            CritChance = 0f;
            CandleSlowBonus = 0f;
            BurnMultiplier = 1f;
            DrainMultiplier = 1f;
            HitSlowPercent = 0f;
            ConfettiChain = false;
            LastBreathArmed = false;
            IgnoreArmor = false;
            FreeBuilds = 0;
            for (int i = 0; i < TowerKinds; i++)
            {
                KindDamage[i] = 1f;
                KindFireRate[i] = 1f;
            }
        }
    }
}
