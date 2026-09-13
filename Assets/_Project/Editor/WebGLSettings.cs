using UnityEditor;
using UnityEngine;

namespace TenCandles.EditorTools
{
    // The GDD's WebGL checklist, applied in one click.
    public static class WebGLSettings
    {
        public const int CanvasWidth = 1280;
        public const int CanvasHeight = 720;

        [MenuItem("Tools/Ten Candles/Apply WebGL Settings")]
        public static void Apply()
        {
            PlayerSettings.productName = "Ten Candles";
            PlayerSettings.companyName = "Rumeysa Sevben";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.defaultWebScreenWidth = CanvasWidth;
            PlayerSettings.defaultWebScreenHeight = CanvasHeight;
            PlayerSettings.runInBackground = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            EditorUserBuildSettings.development = false;
            Debug.Log($"[TenCandles] WebGL settings applied (Gzip, {CanvasWidth}x{CanvasHeight}, stripping Medium).");
        }
    }
}
