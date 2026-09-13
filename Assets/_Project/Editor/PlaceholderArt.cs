using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TenCandles.EditorTools
{
    // Generates simple white/greyscale PNG sprites so the game is playable before real art arrives.
    // Existing files are never overwritten, so replacing a PNG by hand is safe.
    public static class PlaceholderArt
    {
        public const string Folder = "Assets/_Project/Art/Placeholder";

        public class Set
        {
            public Sprite circle, square, ring, soft, pad, vignette, floor;
            public Sprite candle, flame, triangle;
            public Sprite balloon, gift, lantern;
            public Sprite cannon, candleTower, cake, rocket;
        }

        public static Set CreateAll()
        {
            Directory.CreateDirectory(Folder);
            var s = new Set
            {
                circle = Make("circle", 128, 128, 128, Center, (x, y) => Disk(x, y, 0.5f, 0.5f, 0.48f)),
                square = Make("square", 8, 8, 8, Center, (x, y) => Color.white),
                ring = Make("ring", 256, 256, 256, Center, (x, y) =>
                {
                    float d = Dist(x, y, 0.5f, 0.5f);
                    float rim = d > 0.475f && d < 0.495f ? 1f : 0f;
                    return new Color(1f, 1f, 1f, d < 0.495f ? Mathf.Max(rim, 0.1f) : 0f);
                }),
                soft = Make("soft", 128, 128, 128, Center, (x, y) =>
                {
                    float d = Mathf.Clamp01(Dist(x, y, 0.5f, 0.5f) * 2f);
                    return new Color(1f, 1f, 1f, (1f - d) * (1f - d));
                }),
                pad = Make("pad", 128, 128, 128, Center, (x, y) =>
                {
                    float d = Dist(x, y, 0.5f, 0.5f);
                    if (d > 0.48f) return Color.clear;
                    return d > 0.42f ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                }),
                vignette = Make("vignette", 256, 256, 256, Center, (x, y) =>
                {
                    float d = Mathf.Clamp01(Dist(x, y, 0.5f, 0.5f) / 0.707f);
                    return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d)));
                }),
                floor = Make("floor", 256, 256, 64, Center, (x, y) =>
                {
                    bool check = (Mathf.FloorToInt(x * 4f) + Mathf.FloorToInt(y * 4f)) % 2 == 0;
                    float v = check ? 1f : 0.9f;
                    return new Color(v, v, v, 1f);
                }, wrap: true),
                candle = Make("candle", 64, 256, 256, new Vector2(0.5f, 0f), (x, y) =>
                {
                    if (x < 0.12f || x > 0.88f || y > 0.97f) return Color.clear;
                    float stripe = Mathf.Repeat(y * 6f + x * 1.5f, 1f) < 0.3f ? 0.86f : 1f;
                    float shade = Mathf.Lerp(0.85f, 1f, 1f - Mathf.Abs(x - 0.45f) * 2f);
                    float v = stripe * shade;
                    return new Color(v, v, v, 1f);
                }),
                flame = Make("flame", 64, 96, 96, new Vector2(0.5f, 0f), Flame),
                triangle = Make("triangle", 128, 128, 128, Center, (x, y) =>
                    Tri(x, y, new Vector2(0.5f, 0.95f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.05f)) ? Color.white : Color.clear),
                balloon = Make("balloon", 128, 160, 128, new Vector2(0.5f, 0.45f), (x, y) =>
                {
                    Color c = Disk(x, (y - 0.12f) * 1.25f + 0.12f, 0.5f, 0.62f, 0.4f);
                    if (c.a > 0f)
                    {
                        float hl = Dist(x, y, 0.36f, 0.78f) < 0.08f ? 1f : 0.85f;
                        return new Color(hl, hl, hl, c.a);
                    }
                    if (Tri(x, y, new Vector2(0.5f, 0.2f), new Vector2(0.42f, 0.12f), new Vector2(0.58f, 0.12f))) return new Color(0.75f, 0.75f, 0.75f, 1f);
                    if (Mathf.Abs(x - 0.5f - Mathf.Sin(y * 30f) * 0.02f) < 0.012f && y < 0.12f) return new Color(0.9f, 0.9f, 0.9f, 0.9f);
                    return Color.clear;
                }),
                gift = Make("gift", 128, 128, 128, Center, (x, y) =>
                {
                    bool box = x > 0.1f && x < 0.9f && y > 0.08f && y < 0.72f;
                    bool lid = x > 0.05f && x < 0.95f && y >= 0.66f && y < 0.8f;
                    bool bow = Dist(x, y, 0.38f, 0.86f) < 0.09f || Dist(x, y, 0.62f, 0.86f) < 0.09f;
                    bool ribbon = Mathf.Abs(x - 0.5f) < 0.07f && y < 0.8f;
                    if (bow || (ribbon && (box || lid))) return Color.white;
                    if (lid) return new Color(0.82f, 0.82f, 0.82f, 1f);
                    if (box) return new Color(0.68f, 0.68f, 0.68f, 1f);
                    return Color.clear;
                }),
                lantern = Make("lantern", 128, 160, 128, Center, (x, y) =>
                {
                    if (x > 0.38f && x < 0.62f && y > 0.86f && y < 0.94f) return new Color(0.3f, 0.3f, 0.3f, 1f);
                    Color body = Disk((x - 0.5f) * 1.2f + 0.5f, y, 0.5f, 0.5f, 0.36f);
                    if (body.a <= 0f) return Color.clear;
                    float rib = Mathf.Abs(Mathf.Sin((x - 0.5f) * 20f)) < 0.2f ? 0.8f : 1f;
                    return new Color(rib, rib, rib, body.a);
                }),
                cannon = Make("cannon", 128, 128, 128, Center, (x, y) =>
                {
                    Vector2 p = Rotate(new Vector2(x - 0.5f, y - 0.45f), -35f);
                    if (p.x > -0.05f && p.x < 0.42f && Mathf.Abs(p.y) < 0.12f) return new Color(0.85f, 0.85f, 0.85f, 1f);
                    if (Mathf.Abs(p.x - 0.42f) < 0.03f && Mathf.Abs(p.y) < 0.15f) return Color.white;
                    Color wheel = Disk(x, y, 0.42f, 0.32f, 0.2f);
                    if (wheel.a > 0f) return new Color(0.6f, 0.6f, 0.6f, wheel.a);
                    return Color.clear;
                }),
                candleTower = Make("candle_tower", 96, 160, 128, new Vector2(0.5f, 0.3f), (x, y) =>
                {
                    Color f = Flame((x - 0.5f) * 2.2f + 0.5f, (y - 0.62f) * 2.6f);
                    if (f.a > 0f && y > 0.62f) return f;
                    if (x > 0.34f && x < 0.66f && y > 0.12f && y < 0.64f)
                    {
                        float stripe = Mathf.Repeat(y * 8f + x * 2f, 1f) < 0.3f ? 0.8f : 1f;
                        return new Color(stripe, stripe, stripe, 1f);
                    }
                    if (x > 0.18f && x < 0.82f && y > 0.04f && y < 0.14f) return new Color(0.7f, 0.7f, 0.7f, 1f);
                    return Color.clear;
                }),
                cake = Make("cake", 128, 128, 128, Center, (x, y) =>
                {
                    if (x > 0.08f && x < 0.92f && y > 0.08f && y < 0.42f) return y > 0.34f ? Color.white : new Color(0.8f, 0.72f, 0.72f, 1f);
                    if (x > 0.22f && x < 0.78f && y >= 0.42f && y < 0.68f) return y > 0.61f ? Color.white : new Color(0.85f, 0.78f, 0.78f, 1f);
                    if (Mathf.Abs(x - 0.5f) < 0.03f && y >= 0.68f && y < 0.82f) return new Color(0.9f, 0.9f, 1f, 1f);
                    Color f = Disk(x, y, 0.5f, 0.87f, 0.05f);
                    if (f.a > 0f) return new Color(1f, 0.8f, 0.3f, f.a);
                    return Color.clear;
                }),
                rocket = Make("rocket", 96, 160, 128, Center, (x, y) =>
                {
                    if (Tri(x, y, new Vector2(0.5f, 0.97f), new Vector2(0.3f, 0.75f), new Vector2(0.7f, 0.75f))) return Color.white;
                    if (x > 0.32f && x < 0.68f && y > 0.25f && y <= 0.75f) return Mathf.Abs(y - 0.5f) < 0.05f ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
                    if (Tri(x, y, new Vector2(0.32f, 0.45f), new Vector2(0.12f, 0.15f), new Vector2(0.32f, 0.25f))) return new Color(0.65f, 0.65f, 0.65f, 1f);
                    if (Tri(x, y, new Vector2(0.68f, 0.45f), new Vector2(0.68f, 0.25f), new Vector2(0.88f, 0.15f))) return new Color(0.65f, 0.65f, 0.65f, 1f);
                    if (Mathf.Abs(x - 0.5f) < 0.02f && y < 0.25f) return new Color(0.5f, 0.5f, 0.5f, 1f);
                    return Color.clear;
                })
            };
            return s;
        }

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        static Color Flame(float x, float y)
        {
            if (y < 0f || y > 1f) return Color.clear;
            // Teardrop: round bottom, pointed top.
            float radius = y < 0.35f ? Mathf.Sqrt(Mathf.Max(0f, 0.35f * 0.35f - (0.35f - y) * (0.35f - y))) : 0.35f * Mathf.Pow(1f - (y - 0.35f) / 0.65f, 0.8f);
            float dx = Mathf.Abs(x - 0.5f);
            if (dx > radius) return Color.clear;
            float core = radius > 0f ? dx / radius : 1f;
            Color outer = new Color(1f, 0.55f, 0.15f);
            Color inner = new Color(1f, 0.97f, 0.75f);
            float heat = Mathf.Clamp01(core * 0.8f + y * 0.5f);
            return Color.Lerp(inner, outer, heat);
        }

        static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        static Color Disk(float x, float y, float cx, float cy, float r) => Dist(x, y, cx, cy) <= r ? Color.white : Color.clear;

        static bool Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
            var p = new Vector2(x, y);
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // Paints with 4x supersampling for smooth edges, writes the PNG once, and imports it as a sprite.
        static Sprite Make(string name, int w, int h, float ppu, Vector2 pivot, Func<float, float, Color> paint, bool wrap = false)
        {
            string path = $"{Folder}/{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color[w * h];
                for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        Color c = paint((px + 0.25f + sx * 0.5f) / w, (py + 0.25f + sy * 0.5f) / h);
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                    sum /= 4f;
                    pixels[py * w + px] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
                }
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = wrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
