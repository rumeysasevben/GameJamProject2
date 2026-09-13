using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TenCandles.EditorTools
{
    // Tools > Ten Candles > Build Game Scene.
    // Data assets are created once and never overwritten (your tuning survives a rebuild).
    // The exception is art: sprites, names and effect settings from Art/Craftpix are re-applied on every build.
    // The scene itself is regenerated each time.
    public static class SceneBuilder
    {
        const string Root = "Assets/_Project";
        const string DataFolder = Root + "/Data";
        const string ScenePath = Root + "/Scenes/Game.unity";

        [MenuItem("Tools/Ten Candles/Build Game Scene")]
        public static void BuildMenu()
        {
            if (!EditorUtility.DisplayDialog("Ten Candles",
                    "Regenerate Game.unity? Data assets in Assets/_Project/Data keep their stats; art is re-applied.", "Build", "Cancel"))
                return;
            Build();
            EditorUtility.DisplayDialog("Ten Candles", "Game scene built. Press Play.", "OK");
        }

        // Unity -batchmode -executeMethod TenCandles.EditorTools.SceneBuilder.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[TenCandles] Build finished");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            if (!CraftpixArt.Available) throw new InvalidOperationException($"Art is missing: expected {CraftpixArt.Folder}/Maps");

            Directory.CreateDirectory(DataFolder + "/Towers");
            Directory.CreateDirectory(DataFolder + "/Enemies");
            Directory.CreateDirectory(DataFolder + "/Cards");
            Directory.CreateDirectory(DataFolder + "/Eras");
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();

            // Open the fresh scene first: NewScene unloads unreferenced assets, which would
            // silently turn freshly created ScriptableObjects into missing references.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var art = PlaceholderArt.CreateAll();
            CraftpixArt.EnsureImportSettings();
            var data = CreateData(art);
            ApplyArt(data);
            ApplyBalance(data);
            data.maps = CraftpixArt.CreateMaps(DataFolder);
            AssetDatabase.SaveAssets();
            CreateScene(scene, art, data);
            WebGLSettings.Apply();
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- data

        class DataSet
        {
            public TowerData[] towers;
            public EnemyData balloon, runner, giftBox, lantern, boss;
            public UpgradeCard[] cards;
            public EraData[] eras;
            public UISkin skin;
            public SoundBank sounds;
            public MapData[] maps;
        }

        static DataSet CreateData(PlaceholderArt.Set art)
        {
            var d = new DataSet();

            d.towers = new[]
            {
                Asset<TowerData>("Towers/ConfettiCannon", t =>
                {
                    t.displayName = "Confetti Cannon"; t.kind = TowerKind.Confetti;
                    t.description = "Cheap and quick. Hits one guest at a time.";
                    t.baseCost = 20; t.damage = 3.5f; t.fireRate = 1.8f; t.range = 2.5f;
                    t.icon = art.cannon; t.tint = new Color(1f, 0.55f, 0.72f); t.size = 0.9f;
                    t.projectileSprite = art.square; t.projectileColor = new Color(1f, 0.85f, 0.4f); t.projectileSize = 0.16f; t.projectileSpeed = 11f;
                }),
                Asset<TowerData>("Towers/CandleTower", t =>
                {
                    t.displayName = "Candle Tower"; t.kind = TowerKind.Candle;
                    t.description = "Weak hits, but burns and slows. Keeps guests inside a Firework's blast.";
                    t.baseCost = 30; t.damage = 2f; t.fireRate = 1f; t.range = 2.2f;
                    t.burnDps = 3f; t.burnDuration = 2f; t.slowPercent = 0.25f; t.slowDuration = 1.5f;
                    t.icon = art.candleTower; t.tint = new Color(1f, 0.93f, 0.8f); t.size = 0.95f;
                    t.projectileSprite = art.soft; t.projectileColor = new Color(1f, 0.6f, 0.2f); t.projectileSize = 0.4f; t.projectileSpeed = 8f;
                }),
                Asset<TowerData>("Towers/CakeCatapult", t =>
                {
                    t.displayName = "Cake Catapult"; t.kind = TowerKind.Cake;
                    t.description = "Lobs cake that splashes a small group.";
                    t.baseCost = 40; t.damage = 10f; t.fireRate = 0.6f; t.range = 3f; t.splashRadius = 1.2f;
                    t.icon = art.cake; t.tint = new Color(1f, 0.75f, 0.85f); t.size = 0.95f;
                    t.projectileSprite = art.cake; t.projectileColor = Color.white; t.projectileSize = 0.35f; t.projectileSpeed = 6f; t.lobbed = true;
                }),
                Asset<TowerData>("Towers/Firework", t =>
                {
                    t.displayName = "Firework"; t.kind = TowerKind.Firework;
                    t.description = "Slow, long reach, huge blast. The late-game backbone.";
                    t.baseCost = 55; t.damage = 26f; t.fireRate = 0.3f; t.range = 4.2f; t.splashRadius = 2f;
                    t.icon = art.rocket; t.tint = new Color(1f, 0.45f, 0.4f); t.size = 0.95f;
                    t.projectileSprite = art.rocket; t.projectileColor = new Color(1f, 0.5f, 0.45f); t.projectileSize = 0.35f; t.projectileSpeed = 7f;
                }),
            };

            d.balloon = Asset<EnemyData>("Enemies/Balloon", e =>
            {
                e.displayName = "Balloon"; e.sprite = art.balloon; e.tint = new Color(1f, 0.38f, 0.42f); e.size = 0.6f;
            });
            d.runner = Asset<EnemyData>("Enemies/Runner", e =>
            {
                e.displayName = "Runner"; e.hpMultiplier = 0.55f; e.speedMultiplier = 1.7f; e.canSprint = true;
                e.sprite = art.balloon; e.tint = new Color(0.5f, 1f, 0.5f); e.size = 0.45f;
            });
            d.giftBox = Asset<EnemyData>("Enemies/GiftBox", e =>
            {
                e.displayName = "Gift Box"; e.hpMultiplier = 2.4f; e.speedMultiplier = 0.65f; e.armor = 2f;
                e.sprite = art.gift; e.tint = new Color(0.72f, 0.5f, 1f); e.size = 0.62f;
            });
            d.lantern = Asset<EnemyData>("Enemies/Lantern", e =>
            {
                e.displayName = "Lantern"; e.hpMultiplier = 1.2f; e.speedMultiplier = 0.9f; e.auraRadius = 2f; e.auraShieldPercent = 0.25f;
                e.sprite = art.lantern; e.tint = new Color(1f, 0.72f, 0.3f); e.size = 0.6f;
            });
            d.boss = Asset<EnemyData>("Enemies/Boss", e =>
            {
                e.displayName = "Party Pooper"; e.hpMultiplier = 14f; e.speedMultiplier = 0.55f; e.killBonusTime = 25f; e.isBoss = true;
                e.sprite = art.balloon; e.tint = new Color(0.95f, 0.3f, 0.85f); e.size = 1.4f;
            });

            d.cards = new[]
            {
                Card("Bonfire", CardRarity.Common, CardEffect.Damage, 0.15f, 4, "All towers deal +15% damage."),
                Card("Quick Hands", CardRarity.Common, CardEffect.FireRate, 0.12f, 3, "All towers fire 12% faster."),
                Card("Long Fuse", CardRarity.Common, CardEffect.Range, 0.15f, 2, "All towers reach 15% further."),
                Card("Tip Jar", CardRarity.Common, CardEffect.KillBonus, 0.4f, 3, "Every defeat gives +0.4 s more."),
                Card("Bulk Buy", CardRarity.Common, CardEffect.BuildCost, 0.2f, 2, "New towers cost 20% less."),
                Card("Anniversary", CardRarity.Common, CardEffect.WaveEndBonus, 12f, 3, "+12 s at the end of every year."),
                Card("Sturdy Door", CardRarity.Common, CardEffect.LeakShield, 15f, 1, "A leak costs 15 s instead of a whole candle."),
                Card("The Eleventh Candle", CardRarity.Rare, CardEffect.ExtraCandle, 30f, 2, "Put one more candle on the cake.\nBreak the ten."),
                Card("Critical Celebration", CardRarity.Rare, CardEffect.Crit, 0.12f, 2, "Shots get a 12% chance to deal 2.5Ã— damage."),
                Card("Chain Reaction", CardRarity.Rare, CardEffect.Chain, 1f, 1, "Stone Thrower shots bounce to a second enemy."),
                Card("Big Bang", CardRarity.Rare, CardEffect.SplashRadius, 0.35f, 2, "Splash radius +35%."),
                Card("Beeswax", CardRarity.Rare, CardEffect.Beeswax, 0.2f, 1, "Fire Brazier slows by 45% instead of 25%."),
                Card("Free Gift", CardRarity.Rare, CardEffect.FreeTower, 1f, 2, "Your next tower costs nothing."),
                Card("Last Breath", CardRarity.Rare, CardEffect.LastBreath, 45f, 1, "When only one candle is left, get +45 s. Once."),
            };

            d.eras = new[]
            {
                Era("Era1_Humble", "Humble Beginnings", new Color(0.3f, 0.27f, 0.4f), 0f, 1, 0.15f, false),
                Era("Era2_WarmingUp", "Warming Up", new Color(0.38f, 0.29f, 0.46f), 0f, 2, 0.25f, false),
                Era("Era3_Crowded", "A Crowded Room", new Color(0.44f, 0.31f, 0.5f), 2.5f, 2, 0.4f, false),
                Era("Era4_Night", "Into the Night", new Color(0.2f, 0.15f, 0.33f), 4f, 3, 0.55f, false),
                Era("Era5_Finale", "The Big Ten", new Color(0.33f, 0.24f, 0.46f), 7f, 4, 0.7f, true),
            };

            d.skin = Asset<UISkin>("UISkin", s =>
            {
                s.candle = art.candle; s.flame = art.flame; s.soft = art.soft; s.vignette = art.vignette;
            });
            d.sounds = Asset<SoundBank>("SoundBank", s => { });

            return d;
        }

        // Art, names and flavour text for the Craftpix look. Stats are never touched here.
        static void ApplyArt(DataSet d)
        {
            foreach (var t in d.towers)
            {
                switch (t.kind)
                {
                    case TowerKind.Confetti:
                        Tower(t, "Stone Thrower", "Cheap and quick. You hit one monster at a time.", "stone");
                        t.projectileSprite = CraftpixArt.Sprite("Projectiles/iron_ball"); t.projectileSize = 0.8f; t.projectileSpins = true;
                        t.levelProjectiles = new[] { Shot("iron_ball", 0.32f, spin: true), Shot("iron_ball", 0.4f, spin: true), Shot("chained_rock", 0.55f, spin: true) };
                        t.impactFrames = CraftpixArt.Numbered("FX/shards"); t.impactSecondaryFrames = null; t.impactScale = 0.8f;
                        break;
                    case TowerKind.Candle:
                        Tower(t, "Fire Brazier", "Weak hits, but you burn and slow monsters. Keep them inside your Spike Mortar's blast.", "fire");
                        t.projectileSprite = CraftpixArt.Sprite("Projectiles/fireball"); t.projectileSize = 0.32f; t.projectileSpins = true;
                        t.levelProjectiles = new[] { Shot("flame", 0.3f, face: true), Shot("flame", 0.38f, face: true), Shot("fireball", 0.55f, spin: true) };
                        t.impactFrames = CraftpixArt.Numbered("FX/embers"); t.impactSecondaryFrames = null; t.impactScale = 0.8f;
                        break;
                    case TowerKind.Cake:
                        Tower(t, "Catapult", "You lob rocks that splash a small group.", "wood");
                        t.projectileSprite = CraftpixArt.Sprite("Projectiles/rock"); t.projectileSize = 0.75f;
                        t.levelProjectiles = new[] { Shot("rock", 0.45f, spin: true), Shot("boulder", 0.6f, spin: true), Shot("boulder", 0.75f, spin: true) };
                        t.impactFrames = CraftpixArt.Numbered("FX/rock_break"); t.impactSecondaryFrames = CraftpixArt.Numbered("FX/dust"); t.impactScale = 0.9f;
                        break;
                    case TowerKind.Firework:
                        Tower(t, "Spike Mortar", "Slow, long reach, huge blast. Your late-game backbone.", "spike");
                        t.projectileSprite = CraftpixArt.Sprite("Projectiles/spiked_ball"); t.projectileSize = 0.45f; t.lobbed = true;
                        t.levelProjectiles = new[] { Shot("spiked_ball", 0.5f, spin: true), Shot("spiked_ball", 0.58f, spin: true), Shot("spiked_ball", 0.66f, spin: true) };
                        t.impactFrames = CraftpixArt.Numbered("FX/explosion"); t.impactSecondaryFrames = CraftpixArt.Numbered("FX/smoke_ring"); t.impactScale = 1.3f;
                        break;
                }
                EditorUtility.SetDirty(t);
            }

            // Ten enemies, spread over the five roles; each spawn picks one look at random.
            Enemy(d.balloon, "Goblin", 1.25f, 1, 4, 10);
            Enemy(d.runner, "Imp", 1.1f, 3, 5);
            Enemy(d.giftBox, "Brute", 1.35f, 2, 7, 8);
            Enemy(d.lantern, "Shield Bearer", 1.3f, 6);
            Enemy(d.boss, "Orc Warlord", 2f, 9);

            // Birthday party pieces: the candle on the HUD and the ring of signs for building.
            d.skin.candle = CraftpixArt.Sprite("Party/candle_wax");
            d.skin.flame = CraftpixArt.Sprite("Party/candle_flame");
            d.skin.flameColor = Color.white;
            d.skin.buildRing = CraftpixArt.Sprite("UI/build_ring");
            // Black Ops One is wide, so every size comes down a little to fit the panels.
            d.skin.font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/Fonts/BlackOpsOne-Regular.ttf");
            d.skin.fontScale = d.skin.font != null ? 0.88f : 1f;
            // Charcoal panels and cream candles.
            d.skin.ResetColours();
            EditorUtility.SetDirty(d.skin);

            // Painted maps: keep them bright early and let the night fall later.
            Color[] tints = { Color.white, new Color(0.96f, 0.94f, 0.92f), new Color(0.9f, 0.86f, 0.88f), new Color(0.62f, 0.62f, 0.66f), new Color(0.8f, 0.76f, 0.72f) };
            for (int i = 0; i < d.eras.Length && i < tints.Length; i++)
            {
                d.eras[i].backgroundTint = tints[i];
                EditorUtility.SetDirty(d.eras[i]);
            }

            // Gifts speak to the player.
            foreach (var c in d.cards)
            {
                string pct = $"{c.value * 100f:0}%";
                string text = CardText(c, pct);
                if (text == null) continue;
                c.description = text;
                EditorUtility.SetDirty(c);
            }
        }

        static string CardText(UpgradeCard c, string pct)
        {
            switch (c.effect)
            {
                case CardEffect.Damage: return $"Your towers deal {pct} more damage.";
                case CardEffect.FireRate: return $"Your towers fire {pct} faster.";
                case CardEffect.Range: return $"Your towers reach {pct} further.";
                case CardEffect.KillBonus: return $"You get +{c.value:0.#} s more for every monster you defeat.";
                case CardEffect.BuildCost: return $"New towers cost you {pct} less.";
                case CardEffect.WaveEndBonus: return $"You get +{c.value:0} s at the end of every year.";
                case CardEffect.LeakShield: return $"A monster that reaches the cake costs you {c.value:0} s instead of a whole candle.";
                case CardEffect.ExtraCandle: return $"You put one more candle on {Story.HeroPossessive} cake.\nBreak the ten.";
                case CardEffect.Crit: return $"Your shots get a {pct} chance to deal 2.5× damage.";
                case CardEffect.Chain: return "Your Stone Thrower shots bounce to a second monster.";
                case CardEffect.SplashRadius: return $"Your splash damage reaches {pct} wider.";
                case CardEffect.Beeswax: return "Your Fire Brazier slows monsters by 45% instead of 25%.";
                case CardEffect.FreeTower: return "Your next tower costs you nothing.";
                case CardEffect.LastBreath: return $"When you are down to your last candle, you get +{c.value:0} s. Once.";
                default: return null;
            }
        }

        // Stats are tuned here, not in CreateData, so every rebuild brings existing assets up to date.
        // Towers hit harder and faster than the original GDD numbers; enemies have a little less health and armour.
        static void ApplyBalance(DataSet d)
        {
            foreach (var t in d.towers)
            {
                switch (t.kind)
                {
                    case TowerKind.Confetti: t.damage = 5f; t.fireRate = 2.3f; t.range = 2.7f; break;
                    case TowerKind.Candle: t.damage = 3f; t.fireRate = 1.4f; t.range = 2.5f; t.burnDps = 4.5f; break;
                    case TowerKind.Cake: t.damage = 15f; t.fireRate = 0.8f; t.range = 3.2f; t.splashRadius = 1.3f; break;
                    case TowerKind.Firework: t.damage = 36f; t.fireRate = 0.4f; t.range = 4.4f; t.splashRadius = 2f; break;
                }
                EditorUtility.SetDirty(t);
            }

            d.runner.hpMultiplier = 0.5f;
            d.giftBox.hpMultiplier = 2f; d.giftBox.armor = 1f;
            d.lantern.auraShieldPercent = 0.2f;
            d.boss.hpMultiplier = 11f;
            foreach (var e in new[] { d.balloon, d.runner, d.giftBox, d.lantern, d.boss }) EditorUtility.SetDirty(e);
        }

        static ProjectileLook Shot(string sprite, float size, bool spin = false, bool face = false) =>
            new ProjectileLook { sprite = CraftpixArt.Sprite("Projectiles/" + sprite), size = size, spin = spin, faceTravel = face };

        static void Tower(TowerData t, string name, string description, string set)
        {
            t.displayName = name;
            t.description = description;
            t.levelSprites = CraftpixArt.TowerLevels(set);
            t.icon = t.levelSprites[0];
            t.tint = Color.white;
            t.size = 1.15f;
            t.projectileColor = Color.white;
        }

        static void Enemy(EnemyData e, string name, float size, params int[] looks)
        {
            e.displayName = name;
            e.size = size;
            e.tint = Color.white;
            e.framesPerSecond = 16f;
            e.variants = Array.ConvertAll(looks, CraftpixArt.Enemy);
            e.sprite = e.variants[0].walk.Length > 0 ? e.variants[0].walk[0] : e.sprite;
            EditorUtility.SetDirty(e);
        }

        static UpgradeCard Card(string title, CardRarity rarity, CardEffect effect, float value, int max, string description)
        {
            return Asset<UpgradeCard>("Cards/" + title.Replace(" ", ""), c =>
            {
                c.title = title; c.rarity = rarity; c.effect = effect; c.value = value; c.maxStacks = max; c.description = description;
            });
        }

        static EraData Era(string file, string name, Color tint, float confetti, int layers, float ambient, bool fireworks)
        {
            return Asset<EraData>("Eras/" + file, e =>
            {
                e.displayName = name; e.backgroundTint = tint; e.confettiRate = confetti;
                e.musicLayers = layers; e.ambientVolume = ambient; e.backgroundFireworks = fireworks;
            });
        }

        static T Asset<T>(string relativePath, Action<T> init) where T : ScriptableObject
        {
            string path = $"{DataFolder}/{relativePath}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------------------------------------------------------------- scene

        static void CreateScene(UnityEngine.SceneManagement.Scene scene, PlaceholderArt.Set art, DataSet data)
        {
            MapData firstMap = data.maps[0];

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = MapData.WorldHeight * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = firstMap.cameraColor;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraShake>();

            // World. MapLoader fills in the background sprite, waypoints and pads at runtime.
            var background = Sprite("Background", null, firstMap.background, data.eras[0].backgroundTint, -100);
            var path = new GameObject("Path").AddComponent<PathRoute>();
            var spots = new GameObject("TowerSpots");

            var filler = Sprite("Filler", null, firstMap.filler, Color.white, -110);
            filler.drawMode = SpriteDrawMode.Tiled;
            filler.size = new Vector2(60f, 30f);
            var props = new GameObject("MapProps");

            var party = new GameObject("Party");
            var cake = party.AddComponent<CakeView>();
            Set(cake, "cakeSprite", CraftpixArt.Sprite("Party/birthday_cake"));
            Set(cake, "waxSprite", CraftpixArt.Sprite("Party/candle_wax"));
            Set(cake, "flameSprite", CraftpixArt.Sprite("Party/candle_flame"));
            Set(cake, "glowSprite", art.soft);

            // The birthday wizard, standing behind the cake.
            var hostGo = new GameObject("Farum");
            hostGo.transform.SetParent(party.transform, false);
            var host = hostGo.AddComponent<BirthdayHost>();
            Set(host, "sprite", CraftpixArt.Sprite("Party/farum"));

            var maps = new GameObject("Map").AddComponent<MapLoader>();
            SetArray(maps, "maps", data.maps);
            Set(maps, "background", background);
            Set(maps, "filler", filler);
            Set(maps, "propsRoot", props.transform);
            Set(maps, "route", path);
            Set(maps, "spotsRoot", spots.transform);
            Set(maps, "party", party.transform);
            Set(maps, "highlightSprite", art.pad);

            var range = Sprite("RangeIndicator", null, art.ring, new Color(1f, 0.95f, 0.7f, 0.55f), 3);
            range.enabled = false;

            // Systems
            var systems = new GameObject("Systems");
            systems.AddComponent<GameManager>();
            systems.AddComponent<CandleClock>();
            systems.AddComponent<TimeControl>();
            systems.AddComponent<TimeRules>();

            var waves = systems.AddComponent<WaveManager>();
            var spawner = Child<EnemySpawner>(systems, "Enemies");
            Set(spawner, "route", path);
            Set(spawner, "squareSprite", art.square);
            // A soft blue glow reads as a magic shield on the painted enemies; the thin ring did not.
            Set(spawner, "ringSprite", art.soft);
            Set(waves, "spawner", spawner);
            Set(waves, "balloon", data.balloon);
            Set(waves, "runner", data.runner);
            Set(waves, "giftBox", data.giftBox);
            Set(waves, "lantern", data.lantern);
            Set(waves, "boss", data.boss);

            var build = Child<BuildManager>(systems, "Towers");
            SetArray(build, "towers", data.towers);
            Set(build, "spotsRoot", spots.transform);
            Set(build, "rangeIndicator", range);
            Set(build, "pipSprite", art.circle);
            Child<ProjectilePool>(systems, "Projectiles");

            var levelUp = systems.AddComponent<LevelUpManager>();
            SetArray(levelUp, "pool", data.cards);

            var theme = systems.AddComponent<ThemeManager>();
            SetArray(theme, "eras", data.eras);
            Set(theme, "background", background);
            SetArray(theme, "decorGroups", new UnityEngine.Object[0]);

            var fx = Child<FXManager>(systems, "FX");
            Set(fx, "squareSprite", art.square);
            Set(fx, "softSprite", art.soft);
            Set(fx, "partyPoint", party.transform);
            SetArray(fx, "deathFrames", CraftpixArt.Numbered("FX/smoke_ring"));
            SetArray(fx, "bossDeathFrames", CraftpixArt.Numbered("FX/explosion"));
            SetArray(fx, "buildFrames", CraftpixArt.Numbered("FX/dust"));

            var hooks = systems.AddComponent<AudioHooks>();
            Set(hooks, "bank", data.sounds);

            new GameObject("AudioManager").AddComponent<AudioManager>();

            var hud = new GameObject("HUD").AddComponent<HUD>();
            Set(hud, "skin", data.skin);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ---------------------------------------------------------------- helpers

        static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static T Child<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
