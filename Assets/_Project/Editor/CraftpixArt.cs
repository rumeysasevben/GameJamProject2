using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TenCandles.EditorTools
{
    // The Craftpix tower-defense art in Assets/_Project/Art/Craftpix.
    //   Maps/       map1-4.png backgrounds (1920x1080) and pad1-4.png tower pads
    //   Enemies/    E01-E10/{walk,run,hurt,die}_00-09.png, downscaled to 40%
    //   Towers/     {stone,fire,wood,spike}_1-3.png, base + crossbar + arm baked per level
    //   Projectiles/, FX/ flipbook frames named <effect>_<n>.png
    //   Maps/Tiles/T1-4/ the separate layer pieces of each background (road, river, bridge, decor, ground)
    //   Party/      birthday cake and the candle, split into wax and flame
    //   UI/         build ring with the baked prices painted out
    public static class CraftpixArt
    {
        public const string Folder = "Assets/_Project/Art/Craftpix";
        const float SpritePixelsPerUnit = 100f;
        static readonly Vector2 TowerPivot = new Vector2(0.45f, 0.25f);

        public static bool Available => AssetDatabase.IsValidFolder(Folder + "/Maps");

        // Import settings are applied by the postprocessor below; this catches files that were imported before it existed.
        public static void EnsureImportSettings()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && Configure(importer, path))
                    importer.SaveAndReimport();
            }
        }

        // Returns true when something had to change.
        public static bool Configure(TextureImporter importer, string path)
        {
            bool isMap = path.Contains("/Maps/");
            bool isTower = path.Contains("/Towers/");
            // Farum is 16 px pixel art blown up 8x: keep the pixels crisp and stand on the feet.
            bool isPixelHero = path.EndsWith("/Party/farum.png");
            float ppu = isMap ? MapData.PixelsPerUnit : SpritePixelsPerUnit;
            Vector2 pivot = isTower ? TowerPivot : isPixelHero ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            FilterMode filter = isPixelHero ? FilterMode.Point : FilterMode.Bilinear;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool ok = importer.textureType == TextureImporterType.Sprite
                      && importer.spriteImportMode == SpriteImportMode.Single
                      && Mathf.Approximately(importer.spritePixelsPerUnit, ppu)
                      && !importer.mipmapEnabled
                      && importer.filterMode == filter
                      && settings.spriteAlignment == (int)SpriteAlignment.Custom
                      && settings.spritePivot == pivot
                      && (!isMap || importer.maxTextureSize >= 2048);
            if (ok) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = filter;
            if (isMap) importer.maxTextureSize = 2048;
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            return true;
        }

        public static Sprite Sprite(string relativePath)
        {
            string path = $"{Folder}/{relativePath}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning("[TenCandles] Missing sprite " + path);
            return sprite;
        }

        // "FX/explosion" -> explosion_1, explosion_2, ... until a file is missing.
        public static Sprite[] Numbered(string prefix)
        {
            var list = new List<Sprite>();
            for (int i = 1; File.Exists($"{Folder}/{prefix}_{i}.png"); i++) list.Add(Sprite($"{prefix}_{i}"));
            return list.ToArray();
        }

        public static Sprite[] TowerLevels(string set) => new[] { Sprite($"Towers/{set}_1"), Sprite($"Towers/{set}_2"), Sprite($"Towers/{set}_3") };

        public static EnemyAnimation Enemy(int id)
        {
            string dir = $"Enemies/E{id:D2}";
            return new EnemyAnimation
            {
                walk = Clip(dir, "walk"), run = Clip(dir, "run"), hurt = Clip(dir, "hurt"), die = Clip(dir, "die"),
                idle = Clip(dir, "idle"), attack = Clip(dir, "attack"), jump = Clip(dir, "jump")
            };
        }

        static Sprite[] Clip(string dir, string clip)
        {
            var list = new List<Sprite>();
            for (int i = 0; File.Exists($"{Folder}/{dir}/{clip}_{i:D2}.png"); i++) list.Add(Sprite($"{dir}/{clip}_{i:D2}"));
            return list.ToArray();
        }

        // Layouts traced from the four backgrounds. Pixel coordinates, top-left origin.
        // Keep pads out of the HUD: left 250 px, top 160 px, right 250 px.
        public static MapData[] CreateMaps(string dataFolder)
        {
            Directory.CreateDirectory(dataFolder + "/Maps");
            return new[]
            {
                Map(dataFolder, "Map1_Lava", "Lava Fields", 1, new Color(0.13f, 0.08f, 0.06f),
                    new[] { V(-60, 490), V(380, 490), V(530, 535), V(640, 560), V(820, 555), V(980, 555), V(1090, 610), V(1150, 720), V(1200, 840), V(1290, 910), V(1450, 925), V(1600, 935) },
                    new[] { V(420, 350), V(360, 645), V(920, 725), V(1190, 460), V(1380, 770) },
                    new[] { V(610, 285), V(930, 425), V(1590, 790) },
                    new[] { V(1385, -60), V(1385, 250), V(1380, 400), V(1340, 490), V(1260, 560), V(1150, 600), V(1090, 610), V(1150, 720), V(1200, 840), V(1290, 910), V(1450, 925), V(1600, 935) }),
                Map(dataFolder, "Map2_Mine", "Crystal Mine", 2, new Color(0.12f, 0.07f, 0.04f),
                    new[] { V(-60, 370), V(90, 420), V(150, 560), V(250, 690), V(500, 715), V(1000, 715), V(1250, 745), V(1345, 840), V(1370, 1005) },
                    new[] { V(345, 580), V(325, 860), V(810, 860), V(1160, 860), V(1225, 540), V(1155, 210) },
                    new[] { V(1560, 740) },
                    new[] { V(1360, -60), V(1360, 120), V(1330, 230), V(1230, 300), V(1100, 350), V(1010, 450), V(960, 580), V(1000, 715), V(1250, 745), V(1345, 840), V(1370, 1005) }),
                Map(dataFolder, "Map3_Snow", "Frozen Pass", 3, new Color(0.2f, 0.3f, 0.4f),
                    new[] { V(-60, 550), V(300, 555), V(560, 615), V(700, 720), V(730, 860), V(850, 945), V(1150, 960), V(1300, 1005) },
                    new[] { V(755, 545), V(1145, 570), V(500, 790), V(1385, 915) },
                    new[] { V(400, 420), V(560, 1000) },
                    new[] { V(1980, 500), V(1700, 470), V(1450, 440), V(1260, 400), V(1120, 320), V(1000, 420), V(900, 560), V(780, 660), V(700, 720), V(730, 860), V(850, 945), V(1150, 960), V(1300, 1005) }),
                Map(dataFolder, "Map4_Swamp", "Dead Swamp", 4, new Color(0.08f, 0.1f, 0.05f),
                    new[] { V(30, 1150), V(60, 900), V(140, 720), V(300, 640), V(1100, 640), V(1250, 625), V(1480, 640), V(1600, 700), V(1630, 850), V(1630, 1000) },
                    new[] { V(800, 460), V(360, 770), V(940, 770), V(1420, 770) },
                    new[] { V(650, 780), V(1440, 470), V(470, 520) },
                    new[] { V(650, -60), V(660, 100), V(730, 200), V(870, 290), V(960, 380), V(1030, 500), V(1100, 640), V(1250, 625), V(1480, 640), V(1600, 700), V(1630, 850), V(1630, 1000) }),
                RandomMap(dataFolder, "Map5_RandomLava", "Random Lava Fields", 1, new Color(0.13f, 0.08f, 0.06f)),
                RandomMap(dataFolder, "Map6_RandomMine", "Random Crystal Mine", 2, new Color(0.12f, 0.07f, 0.04f)),
                RandomMap(dataFolder, "Map7_RandomSnow", "Random Frozen Pass", 3, new Color(0.2f, 0.3f, 0.4f)),
                RandomMap(dataFolder, "Map8_RandomSwamp", "Random Dead Swamp", 4, new Color(0.08f, 0.1f, 0.05f)),
            };
        }

        // A theme for MapGenerator, built from the separate layer pieces of one background.
        static MapData RandomMap(string dataFolder, string file, string name, int theme, Color cameraColor)
        {
            string path = $"{dataFolder}/Maps/{file}.asset";
            var map = AssetDatabase.LoadAssetAtPath<MapData>(path);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<MapData>();
                map.displayName = name;
                map.cameraColor = cameraColor;
                map.route = new Vector2[0];
                map.paintedSpots = new Vector2[0];
                map.extraSpots = new Vector2[0];
                AssetDatabase.CreateAsset(map, path);
            }

            string dir = $"Maps/Tiles/T{theme}";
            map.generated = true;
            map.background = Sprite($"{dir}/main_bg");
            map.padSprite = Sprite($"{dir}/dot");
            map.filler = Sprite($"{dir}/land");

            var decor = new List<Sprite>();
            foreach (string file2 in Directory.GetFiles($"{Folder}/{dir}", "*.png"))
            {
                string n = Path.GetFileNameWithoutExtension(file2);
                if (n.StartsWith("decor") || n.StartsWith("stone") || n.StartsWith("tree") || n.StartsWith("bush") || n == "lake")
                    decor.Add(Sprite($"{dir}/{n}"));
            }

            map.tiles = new RoadTiles
            {
                horizontal = Sprite($"{dir}/road_6"),
                vertical = Sprite($"{dir}/road_5"),
                bottomRight = Sprite($"{dir}/road_1"),
                leftBottom = Sprite($"{dir}/road_2"),
                topRight = Sprite($"{dir}/road_3"),
                topLeft = Sprite($"{dir}/road_4"),
                riverVertical = File.Exists($"{Folder}/{dir}/river_6.png") ? Sprite($"{dir}/river_6") : null,
                bridge = File.Exists($"{Folder}/{dir}/bridge.png") ? Sprite($"{dir}/bridge") : null,
                decor = decor.ToArray()
            };
            EditorUtility.SetDirty(map);
            return map;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static MapData Map(string dataFolder, string file, string name, int image, Color cameraColor, Vector2[] route, Vector2[] painted, Vector2[] extra, Vector2[] secondRoute)
        {
            string path = $"{dataFolder}/Maps/{file}.asset";
            var map = AssetDatabase.LoadAssetAtPath<MapData>(path);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<MapData>();
                map.displayName = name;
                map.cameraColor = cameraColor;
                AssetDatabase.CreateAsset(map, path);
            }
            // Layouts are traced from the art, not tuned by hand, so they are refreshed on every build.
            map.route = route;
            map.secondRoute = secondRoute;
            map.secondRouteFromYear = 4;
            map.paintedSpots = painted;
            map.extraSpots = extra;
            map.background = Sprite($"Maps/map{image}");
            map.padSprite = Sprite($"Maps/pad{image}");
            map.filler = Sprite($"Maps/Tiles/T{image}/land");
            EditorUtility.SetDirty(map);
            return map;
        }
    }

    class CraftpixArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CraftpixArt.Folder)) return;
            CraftpixArt.Configure((TextureImporter)assetImporter, assetPath);
        }
    }
}
