using UnityEngine;

namespace TenCandles
{
    // Writes a chosen card into StatRegistry (or CandleClock for time effects).
    public static class UpgradeEffect
    {
        public static void Apply(UpgradeCard card)
        {
            float v = card.value;
            switch (card.effect)
            {
                case CardEffect.Damage: StatRegistry.DamageMultiplier += v; break;
                case CardEffect.FireRate: StatRegistry.FireRateMultiplier += v; break;
                case CardEffect.Range: StatRegistry.RangeMultiplier += v; break;
                case CardEffect.KillBonus: StatRegistry.KillBonus += v; break;
                case CardEffect.BuildCost: StatRegistry.BuildCostMultiplier = Mathf.Max(0.2f, StatRegistry.BuildCostMultiplier - v); break;
                case CardEffect.WaveEndBonus: StatRegistry.WaveEndBonus += v; break;
                case CardEffect.LeakShield: StatRegistry.LeakPenaltyReduction += v; break;
                case CardEffect.ExtraCandle: CandleClock.Instance.AddCandle(); break;
                case CardEffect.Crit: StatRegistry.CritChance += v; break;
                case CardEffect.Chain: StatRegistry.ConfettiChain = true; break;
                case CardEffect.SplashRadius: StatRegistry.SplashRadiusMultiplier += v; break;
                case CardEffect.Beeswax: StatRegistry.CandleSlowBonus += v; break;
                case CardEffect.FreeTower: StatRegistry.FreeBuilds += Mathf.Max(1, Mathf.RoundToInt(v)); break;
                case CardEffect.LastBreath: StatRegistry.LastBreathArmed = true; break;
            }
        }
    }
}
