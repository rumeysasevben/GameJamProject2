using UnityEngine;

namespace TenCandles
{
    // Frame sequences for one look of an enemy. Frames play in order; walk and run loop.
    [System.Serializable]
    public class EnemyAnimation
    {
        public Sprite[] walk;
        public Sprite[] run;
        public Sprite[] hurt;
        public Sprite[] die;
        public Sprite[] idle;
        public Sprite[] attack;
        public Sprite[] jump;

        public static bool Has(Sprite[] clip) => clip != null && clip.Length > 0 && clip[0] != null;
    }

    [CreateAssetMenu(menuName = "Ten Candles/Enemy", fileName = "Enemy")]
    public class EnemyData : ScriptableObject
    {
        public string displayName = "Balloon";

        [Header("Stats (multipliers on the year formula)")]
        public float hpMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float rewardMultiplier = 1f;
        [Tooltip("Flat damage removed from every hit. Burn ignores it.")]
        public float armor;
        [Tooltip("Extra seconds on death (boss).")]
        public float killBonusTime;

        [Header("Runner sprint")]
        public bool canSprint;
        [Range(0f, 1f)] public float sprintHpThreshold = 0.3f;
        public float sprintSpeedBonus = 0.4f;
        public float sprintDuration = 2f;

        [Header("Lantern shield aura")]
        public float auraRadius;
        [Range(0f, 1f)] public float auraShieldPercent = 0.25f;

        [Header("Skin")]
        [Tooltip("Static sprite, used when there are no animation variants.")]
        public Sprite sprite;
        [Tooltip("One is picked at random for every spawn.")]
        public EnemyAnimation[] variants;
        public float framesPerSecond = 16f;
        public Color tint = Color.white;
        public float size = 0.6f;
        public bool isBoss;
    }
}
