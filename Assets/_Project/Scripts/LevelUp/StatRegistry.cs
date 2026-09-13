namespace TenCandles
{
    // The single home of every global multiplier. Towers read their stats through here.
    public static class StatRegistry
    {
        public const float CritDamageMultiplier = 2.5f;

        public static float DamageMultiplier;
        public static float FireRateMultiplier;
        public static float RangeMultiplier;
        public static float SplashRadiusMultiplier;
        public static float BuildCostMultiplier;
        public static float KillBonus;
        public static float WaveEndBonus;
        public static float LeakPenaltyReduction;
        public static float CritChance;
        public static float CandleSlowBonus;
        public static bool ConfettiChain;
        public static bool LastBreathArmed;
        public static int FreeBuilds;

        static StatRegistry() => Reset();

        public static void Reset()
        {
            DamageMultiplier = 1f;
            FireRateMultiplier = 1f;
            RangeMultiplier = 1f;
            SplashRadiusMultiplier = 1f;
            BuildCostMultiplier = 1f;
            KillBonus = 0f;
            WaveEndBonus = 0f;
            LeakPenaltyReduction = 0f;
            CritChance = 0f;
            CandleSlowBonus = 0f;
            ConfettiChain = false;
            LastBreathArmed = false;
            FreeBuilds = 0;
        }
    }
}
