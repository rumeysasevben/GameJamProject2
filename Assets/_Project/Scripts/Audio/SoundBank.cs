using UnityEngine;

namespace TenCandles
{
    // Drop clips in here; any empty slot is simply silent.
    [CreateAssetMenu(menuName = "Ten Candles/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Music & ambience")]
        [Tooltip("Layer 0 plays from era 1; each era adds a layer.")]
        public AudioClip[] musicLayers;
        public AudioClip crowdAmbient;

        [Header("Towers (by TowerKind order: Confetti, Candle, Cake, Firework)")]
        public AudioClip[] towerFire = new AudioClip[4];
        public AudioClip build;
        public AudioClip upgrade;
        public AudioClip buildFailed;

        [Header("Enemies")]
        public AudioClip enemyHit;
        public AudioClip enemyPop;
        public AudioClip splash;
        public AudioClip bossDown;

        [Header("Candles")]
        public AudioClip candlePuff;
        public AudioClip crowdOhh;
        public AudioClip candleRelight;
        public AudioClip newCandle;

        [Header("Flow")]
        public AudioClip waveStart;
        public AudioClip waveCleared;
        public AudioClip levelUp;
        public AudioClip cardPicked;

        [Header("Wish")]
        [Tooltip("The hard beat as the candles are blown out.")]
        public AudioClip wishBlow;
        [Tooltip("The gift is granted.")]
        public AudioClip wishGranted;
        public AudioClip uiClick;
        public AudioClip victory;
        public AudioClip defeat;
    }
}
