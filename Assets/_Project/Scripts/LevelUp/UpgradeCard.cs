using UnityEngine;

namespace TenCandles
{
    public enum CardRarity
    {
        Common,
        Rare
    }

    public enum CardEffect
    {
        Damage,         // Bonfire
        FireRate,       // Quick Hands
        Range,          // Long Fuse
        KillBonus,      // Tip Jar
        BuildCost,      // Bulk Buy
        WaveEndBonus,   // Anniversary
        LeakShield,     // Sturdy Door
        ExtraCandle,    // The Eleventh Candle
        Crit,           // Critical Celebration
        Chain,          // Chain Reaction
        SplashRadius,   // Big Bang
        Beeswax,        // Beeswax
        FreeTower,      // Free Gift
        LastBreath      // Last Breath
    }

    [CreateAssetMenu(menuName = "Ten Candles/Upgrade Card", fileName = "Card")]
    public class UpgradeCard : ScriptableObject
    {
        public string title = "Card";
        [TextArea] public string description;
        public CardRarity rarity;
        public CardEffect effect;
        public float value;
        public int maxStacks = 1;
        public Sprite icon;
    }
}
