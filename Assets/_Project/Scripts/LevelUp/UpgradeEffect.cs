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
                case CardEffect.ExtraCandle:
                    for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(v)); i++) CandleClock.Instance.AddCandle();
                    break;
                case CardEffect.Crit: StatRegistry.CritChance += v; break;
                case CardEffect.Chain: StatRegistry.ConfettiChain = true; break;
                case CardEffect.SplashRadius: StatRegistry.SplashRadiusMultiplier += v; break;
                case CardEffect.Beeswax: StatRegistry.CandleSlowBonus += v; break;
                case CardEffect.FreeTower: StatRegistry.FreeBuilds += Mathf.Max(1, Mathf.RoundToInt(v)); break;
                case CardEffect.LastBreath: StatRegistry.LastBreathArmed = true; break;
                case CardEffect.KindDamage: StatRegistry.KindDamage[(int)card.kind] += v; break;
                case CardEffect.KindFireRate: StatRegistry.KindFireRate[(int)card.kind] += v; break;
                case CardEffect.BurnBoost: StatRegistry.BurnMultiplier += v; break;
                case CardEffect.UpgradeCost: StatRegistry.UpgradeCostMultiplier = Mathf.Max(0.2f, StatRegistry.UpgradeCostMultiplier - v); break;
                case CardEffect.InstantTime: CandleClock.Instance.Add(v, card.title); break;
                case CardEffect.HitSlow: StatRegistry.HitSlowPercent = Mathf.Max(StatRegistry.HitSlowPercent, v); break;
                case CardEffect.ArmorPierce: StatRegistry.IgnoreArmor = true; break;
                case CardEffect.Blessing:
                    StatRegistry.DamageMultiplier += v;
                    StatRegistry.FireRateMultiplier += v * 0.5f;
                    break;
                case CardEffect.SlowBurn: StatRegistry.DrainMultiplier = Mathf.Max(0.3f, StatRegistry.DrainMultiplier - v); break;
            }
        }
    }
}
