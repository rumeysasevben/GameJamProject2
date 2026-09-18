using TenCandles.Core;
using TenCandles.Lifetime;
using UnityEngine;

namespace TenCandles
{
    // Writes a chosen card, or a permanent stat entry such as a mastery passive, into StatRegistry (or CandleClock for time effects).
    public static class UpgradeEffect
    {
        public static void Apply(UpgradeCard card) => Apply(card.effect, card.value, card.kind, card.title);

        public static void Apply(CardEffect effect, float v, TowerKind kind = default, string title = null)
        {
            switch (effect)
            {
                case CardEffect.Damage: StatRegistry.DamageMultiplier += v; break;
                case CardEffect.FireRate: StatRegistry.FireRateMultiplier += v; break;
                case CardEffect.Range: StatRegistry.RangeMultiplier += v; break;
                case CardEffect.KillBonus: StatRegistry.KillBonus += v; break;
                case CardEffect.BuildCost: StatRegistry.BuildCostMultiplier = Mathf.Max(Balance.MinCostMultiplier, StatRegistry.BuildCostMultiplier - v); break;
                case CardEffect.WaveEndBonus: StatRegistry.WaveEndBonus += v; break;
                case CardEffect.LeakShield: StatRegistry.LeakPenaltyReduction += v; break;
                case CardEffect.ExtraCandle:
                    int candles = Mathf.Max(1, Mathf.RoundToInt(v));
                    StatRegistry.BonusCandles += candles;
                    // In a run the cap is tier cap + bonus - candles wished (§6); outside one, just add to the bar.
                    var lifetime = LifetimeManager.Instance;
                    if (lifetime != null && lifetime.Run != null) lifetime.RefreshCandleCap();
                    else for (int i = 0; i < candles; i++) CandleClock.Instance.AddCandle();
                    break;
                case CardEffect.Crit: StatRegistry.CritChance += v; break;
                case CardEffect.Chain: StatRegistry.ConfettiChain = true; break;
                case CardEffect.SplashRadius: StatRegistry.SplashRadiusMultiplier += v; break;
                case CardEffect.Beeswax: StatRegistry.CandleSlowBonus += v; break;
                case CardEffect.FreeTower: StatRegistry.FreeBuilds += Mathf.Max(1, Mathf.RoundToInt(v)); break;
                case CardEffect.LastBreath: StatRegistry.LastBreathArmed = true; break;
                case CardEffect.KindDamage: StatRegistry.KindDamage[(int)kind] += v; break;
                case CardEffect.KindFireRate: StatRegistry.KindFireRate[(int)kind] += v; break;
                case CardEffect.BurnBoost: StatRegistry.BurnMultiplier += v; break;
                case CardEffect.UpgradeCost: StatRegistry.UpgradeCostMultiplier = Mathf.Max(Balance.MinCostMultiplier, StatRegistry.UpgradeCostMultiplier - v); break;
                case CardEffect.InstantTime: CandleClock.Instance.Add(v, title); break;
                case CardEffect.HitSlow: StatRegistry.HitSlowPercent = Mathf.Max(StatRegistry.HitSlowPercent, v); break;
                case CardEffect.ArmorPierce: StatRegistry.IgnoreArmor = true; break;
                case CardEffect.Blessing:
                    StatRegistry.DamageMultiplier += v;
                    StatRegistry.FireRateMultiplier += v * 0.5f;
                    break;
                case CardEffect.SlowBurn: StatRegistry.DrainMultiplier = Mathf.Max(Balance.MinDrainMultiplier, StatRegistry.DrainMultiplier - v); break;
                case CardEffect.KillRewardMul: StatRegistry.KillRewardMultiplier += v; break;
                case CardEffect.TowerCapacityAdd: StatRegistry.TowerCapacityBonus += Mathf.RoundToInt(v); break;
            }
        }

        // True when applying the effect again would change nothing: a switch that is already on (Armor Breaker and
        // True Aim both set IgnoreArmor), or a multiplier already at its floor.
        public static bool IsMaxed(CardEffect effect, float v) => effect switch
        {
            CardEffect.ArmorPierce => StatRegistry.IgnoreArmor,
            CardEffect.Chain => StatRegistry.ConfettiChain,
            CardEffect.LastBreath => StatRegistry.LastBreathArmed,
            CardEffect.HitSlow => StatRegistry.HitSlowPercent >= v,
            CardEffect.BuildCost => StatRegistry.BuildCostMultiplier <= Balance.MinCostMultiplier,
            CardEffect.UpgradeCost => StatRegistry.UpgradeCostMultiplier <= Balance.MinCostMultiplier,
            CardEffect.SlowBurn => StatRegistry.DrainMultiplier <= Balance.MinDrainMultiplier,
            _ => false
        };

        // The one exclusion rule for yearly card offers and wish gifts (§11.3, §6): the card itself is below maxStacks,
        // and its effect is not already maxed by another card.
        public static bool CanOffer(UpgradeCard card, int stacks) => card != null && stacks < card.maxStacks && !IsMaxed(card.effect, card.value);
    }
}
