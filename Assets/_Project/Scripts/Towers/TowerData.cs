using UnityEngine;

namespace TenCandles
{
    public enum TowerKind
    {
        Confetti,
        Candle,
        Cake,
        Firework
    }

    // How a shot looks at one tower level.
    [System.Serializable]
    public class ProjectileLook
    {
        public Sprite sprite;
        [Tooltip("Width in world units.")]
        public float size = 0.3f;
        [Tooltip("Tumbles in flight.")]
        public bool spin;
        [Tooltip("Points along its flight, sprite bottom first (flames with a trail).")]
        public bool faceTravel;
    }

    [CreateAssetMenu(menuName = "Ten Candles/Tower", fileName = "Tower")]
    public class TowerData : ScriptableObject
    {
        public const int MaxLevel = 3;
        static readonly float[] LevelDamage = { 1f, 1.6f, 2.56f };
        static readonly float[] LevelCost = { 1f, 0.75f, 1.5f };
        static readonly float[] LevelRange = { 1f, 1.1f, 1.2f };

        public string displayName = "Tower";
        public TowerKind kind;
        [TextArea] public string description;

        [Header("Stats")]
        public float baseCost = 20f;
        public float damage = 3.5f;
        public float fireRate = 1.8f;
        public float range = 2.5f;
        public float splashRadius;

        [Header("Candle Tower effects")]
        public float burnDps;
        public float burnDuration = 2f;
        [Range(0f, 1f)] public float slowPercent;
        public float slowDuration = 1.5f;

        [Header("Projectile")]
        public Sprite projectileSprite;
        public Color projectileColor = Color.white;
        public float projectileSize = 0.22f;
        public float projectileSpeed = 9f;
        [Tooltip("Arcs through the air instead of flying straight.")]
        public bool lobbed;
        [Tooltip("Straight shots tumble as they fly. Lobbed shots always spin.")]
        public bool projectileSpins;
        [Tooltip("Optional look per level (L1, L2, L3). Empty slots fall back to the level below, then to the fields above.")]
        public ProjectileLook[] levelProjectiles;

        [Header("Impact effect")]
        public Sprite[] impactFrames;
        [Tooltip("Plays together with impactFrames, a little larger (smoke, dust).")]
        public Sprite[] impactSecondaryFrames;
        [Tooltip("World scale of the effect. Splash towers multiply this by their splash radius.")]
        public float impactScale = 1f;

        [Header("Skin")]
        public Sprite icon;
        [Tooltip("One sprite per level (L1, L2, L3). Empty slots fall back to the previous level.")]
        public Sprite[] levelSprites = new Sprite[MaxLevel];
        public Color tint = Color.white;
        public float size = 0.85f;

        public static float DamageMultiplier(int level) => LevelDamage[Mathf.Clamp(level, 1, MaxLevel) - 1];
        public static float RangeMultiplier(int level) => LevelRange[Mathf.Clamp(level, 1, MaxLevel) - 1];

        // Cost of reaching `level` from the level below it.
        public float UpgradeCost(int level) => baseCost * LevelCost[Mathf.Clamp(level, 1, MaxLevel) - 1];

        public ProjectileLook ProjectileForLevel(int level)
        {
            for (int i = Mathf.Clamp(level, 1, MaxLevel) - 1; i >= 0; i--)
                if (levelProjectiles != null && i < levelProjectiles.Length && levelProjectiles[i] != null && levelProjectiles[i].sprite != null)
                    return levelProjectiles[i];
            return null;
        }

        public Sprite SpriteForLevel(int level)
        {
            for (int i = Mathf.Clamp(level, 1, MaxLevel) - 1; i >= 0; i--)
                if (levelSprites != null && i < levelSprites.Length && levelSprites[i] != null) return levelSprites[i];
            return icon;
        }
    }
}
