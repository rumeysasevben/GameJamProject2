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

        // §5.1: a Stay decade (a repeat of the previous slot's stage) multiplies enemy HP.
        public static float Hp(int d, int y, bool isRepeatDecade) => Hp(d, y) * (isRepeatDecade ? Balance.StayHpMultiplier : 1f);

        public static float Speed(int d, int y) => 1f + Balance.YearSpeedStep * (y - 1)
                                                      + Balance.StageSpeedStep * d;

        public static float Reward(int d, int y) => (Balance.RewardBase + Balance.RewardYearStep * y)
                                                    * (1f + Balance.RewardStageMul * d);

        public static float Gap(int d, int y) => Mathf.Max(Balance.GapMinimum,
                                                   Balance.GapBase - Balance.GapYearStep * y
                                                                   - Balance.GapStageStep * d);

        // §7 / §12.2: min(map entrances, the tier's lane allowance).
        public static int ActiveLanes(int mapEntrances, int d)
            => Mathf.Max(1, Mathf.Min(mapEntrances, Balance.TierLaneAllowance[Mathf.Clamp(d, 0, Balance.TierLaneAllowance.Length - 1)]));

        // Share of a wave's regular enemies per active lane (sums to 1). Lane 0 is the main road.
        // A lane that first opened in this decade ramps from NewLaneShareStart (year 1) to NewLaneShareEnd (year 10);
        // an older extra lane holds NewLaneShareEnd. Shares are relative to the main road, so two lanes end at 50 / 50.
        public static float[] LaneShares(int mapEntrances, int d, int y)
        {
            int lanes = ActiveLanes(mapEntrances, d);
            var weights = new float[lanes];
            float total = 0f;
            for (int lane = 0; lane < lanes; lane++)
            {
                float share = Balance.NewLaneShareEnd;
                if (lane > 0 && (d == 0 || ActiveLanes(mapEntrances, d - 1) <= lane))
                    share = Mathf.Lerp(Balance.NewLaneShareStart, Balance.NewLaneShareEnd, (y - 1) / (float)(Balance.YearsPerDecade - 1));
                weights[lane] = lane == 0 ? 1f : share / (1f - share);
                total += weights[lane];
            }
            for (int lane = 0; lane < lanes; lane++) weights[lane] /= total;
            return weights;
        }

        // Deterministic dealing: the n-th regular enemy goes to the lane furthest behind its share.
        public static int PickLane(float[] shares, int[] dealtPerLane)
        {
            int dealt = 0;
            foreach (int n in dealtPerLane) dealt += n;
            int best = 0;
            float bestDeficit = float.MinValue;
            for (int lane = 0; lane < shares.Length; lane++)
            {
                float deficit = shares[lane] * (dealt + 1) - dealtPerLane[lane];
                if (deficit > bestDeficit + 1e-5f)
                {
                    bestDeficit = deficit;
                    best = lane;
                }
            }
            return best;
        }

        public static int TierOfAge(int age) => (age - 1) / Balance.YearsPerDecade;
        public static int YearOfAge(int age) => (age - 1) % Balance.YearsPerDecade + 1;
    }
}
