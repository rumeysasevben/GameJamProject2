using TenCandles.Core;
using UnityEngine;

namespace TenCandles.Waves
{
    // Spec §2.1. Pure functions of (d = difficulty tier 0..3, y = year within the decade 1..10).
    // The §8 wave table is generated from these; BalanceSimulator checks them against it.
    public static class WaveGenerator
    {
        public static int Count(int d, int y) => Balance.CountBase + Balance.CountYearStep * y
                                                                   + Balance.CountStageStep * d;

        public static float Hp(int d, int y) => Balance.BaseEnemyHp
                                                * Balance.StageHpMultiplier[d]
                                                * (1f + Balance.YearHpStep * (y - 1));

        public static float Speed(int d, int y) => 1f + Balance.YearSpeedStep * (y - 1)
                                                      + Balance.StageSpeedStep * d;

        public static float Reward(int d, int y) => (Balance.RewardBase + Balance.RewardYearStep * y)
                                                    * (1f + Balance.RewardStageMul * d);

        public static float Gap(int d, int y) => Mathf.Max(Balance.GapMinimum,
                                                   Balance.GapBase - Balance.GapYearStep * y
                                                                   - Balance.GapStageStep * d);

        public static int TierOfAge(int age) => (age - 1) / Balance.YearsPerDecade;
        public static int YearOfAge(int age) => (age - 1) % Balance.YearsPerDecade + 1;
    }
}
