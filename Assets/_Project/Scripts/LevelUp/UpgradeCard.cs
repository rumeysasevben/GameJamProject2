using UnityEngine;

namespace TenCandles
{
    public enum CardRarity
    {
        Common,
        Rare,
        Legendary
    }

    // Values are appended, never reordered: card assets store them as numbers.
    public enum CardEffect
    {
        Damage,         // Bonfire
        FireRate,       // Quick Hands
        Range,          // Long Fuse
        KillBonus,      // Tip Jar
        BuildCost,      // Bulk Buy
        WaveEndBonus,   // Anniversary
        LeakShield,     // Sturdy Door
        ExtraCandle,    // The Eleventh Candle (value = candles)
        Crit,           // Critical Celebration
        Chain,          // Chain Reaction
        SplashRadius,   // Big Bang
        Beeswax,        // Beeswax
        FreeTower,      // Free Gift
        LastBreath,     // Last Breath
        KindDamage,     // extra damage for one tower kind
        KindFireRate,   // faster shots for one tower kind
        BurnBoost,      // Hot Coals
        UpgradeCost,    // Master Builder
        InstantTime,    // Spare Wax
        HitSlow,        // Cold Snap
        ArmorPierce,    // Armor Breaker
        Blessing,       // Farum's Blessing: damage and fire rate
        SlowBurn        // Slow-Burning Wax: candles drain slower
    }

    [CreateAssetMenu(menuName = "Ten Candles/Upgrade Card", fileName = "Card")]
    public class UpgradeCard : ScriptableObject
    {
        public string title = "Card";
        [TextArea] public string description;
        public CardRarity rarity;
        public CardEffect effect;
        public float value;
        [Tooltip("Tower kind for KindDamage and KindFireRate.")]
        public TowerKind kind;
        public int maxStacks = 1;
        public Sprite icon;
    }
}
