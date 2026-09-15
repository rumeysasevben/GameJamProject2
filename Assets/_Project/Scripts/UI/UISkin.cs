using UnityEngine;
using UnityEngine.Serialization;

namespace TenCandles
{
    // Fonts, sprites and colours for the HUD. Swap art here without touching code.
    [CreateAssetMenu(menuName = "Ten Candles/UI Skin", fileName = "UISkin")]
    public class UISkin : ScriptableObject
    {
        [Header("Font (empty = Unity's built-in font)")]
        public Font font;
        [Tooltip("Multiplies every font size. Wide display fonts need less than 1.")]
        [Range(0.5f, 1.5f)] public float fontScale = 1f;

        [Header("Sprites")]
        public Sprite panel;
        public Sprite candle;
        public Sprite flame;
        public Sprite soft;
        public Sprite vignette;
        [Tooltip("Ring of four sign boards shown around an empty pad.")]
        public Sprite buildRing;
        [Tooltip("Main menu art: the birthday host and the cake.")]
        public Sprite hero;
        public Sprite cake;

        // Charcoal panels with warm candle-light text: grey-black, never purple.
        [Header("Colours")]
        public Color panelColor = new Color(0.07f, 0.07f, 0.075f, 0.88f);
        public Color panelLight = new Color(0.18f, 0.175f, 0.17f, 0.95f);
        [Tooltip("Full-screen dimmer behind the start, level-up and end screens.")]
        public Color overlay = new Color(0.03f, 0.03f, 0.035f, 0.8f);
        public Color text = new Color(1f, 0.96f, 0.9f);
        public Color mutedText = new Color(0.74f, 0.72f, 0.68f);
        public Color accent = new Color(1f, 0.8f, 0.3f);
        public Color danger = new Color(1f, 0.42f, 0.42f);
        public Color good = new Color(0.55f, 1f, 0.6f);
        public Color common = new Color(0.78f, 0.77f, 0.74f);
        public Color rare = new Color(0.4f, 0.75f, 1f);
        [FormerlySerializedAs("legendary")] public Color epic = new Color(1f, 0.74f, 0.2f);
        public Color disabled = new Color(0.27f, 0.27f, 0.27f, 0.95f);
        public Color flameColor = Color.white;
        public Color waxOut = new Color(0.45f, 0.44f, 0.42f);
        [Tooltip("Candles cycle through these. One entry = every candle the same colour.")]
        public Color[] waxColors = { new Color(1f, 0.96f, 0.88f) };

        // Resets the colours above to their defaults, so existing skin assets pick up palette changes.
        public void ResetColours()
        {
            var defaults = CreateInstance<UISkin>();
            panelColor = defaults.panelColor;
            panelLight = defaults.panelLight;
            overlay = defaults.overlay;
            text = defaults.text;
            mutedText = defaults.mutedText;
            accent = defaults.accent;
            danger = defaults.danger;
            good = defaults.good;
            common = defaults.common;
            rare = defaults.rare;
            epic = defaults.epic;
            disabled = defaults.disabled;
            flameColor = defaults.flameColor;
            waxOut = defaults.waxOut;
            waxColors = defaults.waxColors;
            DestroyImmediate(defaults);
        }
    }
}
