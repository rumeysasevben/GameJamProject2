using UnityEngine;

namespace TenCandles
{
    [CreateAssetMenu(menuName = "Ten Candles/Era", fileName = "Era")]
    public class EraData : ScriptableObject
    {
        public string displayName = "Humble";
        public Color backgroundTint = Color.white;
        [Tooltip("Ambient embers rising per second.")]
        public float confettiRate;
        [Tooltip("How many music layers play in this era.")]
        public int musicLayers = 1;
        [Range(0f, 1f)] public float ambientVolume = 0.3f;
        [Tooltip("Show firework bursts in the background.")]
        public bool backgroundFireworks;
    }
}
