using System.Collections;
using UnityEngine;

namespace TenCandles
{
    // Particles, flipbooks, shake and hit pause, driven purely by game events.
    // The look is candle-light in a dark fantasy world: dust and smoke for fighting, warm embers for celebrating.
    public class FXManager : MonoBehaviour
    {
        public static FXManager Instance { get; private set; }

        [SerializeField] Sprite squareSprite;
        [SerializeField] Sprite softSprite;
        [SerializeField] Transform partyPoint;
        [SerializeField] Vector2 playArea = new Vector2(16f, 10f);

        [Header("Flipbooks")]
        [Tooltip("Puff of smoke where an enemy falls.")]
        [SerializeField] Sprite[] deathFrames;
        [Tooltip("The boss goes out with a bang.")]
        [SerializeField] Sprite[] bossDeathFrames;
        [Tooltip("Dust kicked up when a tower is built.")]
        [SerializeField] Sprite[] buildFrames;

        [Header("Shake")]
        [SerializeField] float smallShake = 0.08f;
        [SerializeField] float mediumShake = 0.2f;
        [SerializeField] float bigShake = 0.55f;
        [SerializeField] float splashHitPause = 0.03f;

        [Header("Finale")]
        [SerializeField] float finaleSeconds = 4f;

        ParticleSystem embers, smoke, dust, sparks, ambient;
        ObjectPool<Flipbook> flipbooks;

        static readonly Color[] EmberColors =
        {
            new Color(1f, 0.84f, 0.4f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.72f, 0.28f),
            new Color(1f, 0.94f, 0.7f), new Color(0.95f, 0.42f, 0.14f)
        };

        static readonly Color DustColor = new Color(0.52f, 0.45f, 0.38f, 0.85f);
        static readonly Color SmokeColor = new Color(0.32f, 0.3f, 0.3f, 0.75f);

        public float FinaleSeconds => finaleSeconds;

        void Awake()
        {
            Instance = this;
            flipbooks = new ObjectPool<Flipbook>(CreateFlipbook, 12);
            Material baseMat = DefaultSpriteMaterial();
            embers = CreateSystem("Embers", baseMat, softSprite, 40, gravity: -0.25f, life: (0.9f, 1.8f), size: (0.05f, 0.12f), speed: (1.5f, 4.5f), multicolor: true);
            smoke = CreateSystem("Smoke", baseMat, softSprite, 30, gravity: -0.15f, life: (0.6f, 1.1f), size: (0.4f, 0.9f), speed: (0.3f, 1.2f), multicolor: false);
            dust = CreateSystem("Dust", baseMat, softSprite, 29, gravity: 0.2f, life: (0.4f, 0.8f), size: (0.15f, 0.35f), speed: (0.6f, 2.2f), multicolor: false);
            sparks = CreateSystem("Sparks", baseMat, softSprite, 45, gravity: 0.4f, life: (0.6f, 1.2f), size: (0.07f, 0.16f), speed: (3f, 7f), multicolor: true);
            ambient = CreateSystem("AmbientEmbers", baseMat, softSprite, 2, gravity: -0.04f, life: (6f, 9f), size: (0.04f, 0.09f), speed: (0f, 0.35f), multicolor: true);

            // Embers and ash drift up from the bottom of the map as the years pass.
            var shape = ambient.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(playArea.x * 1.3f, 0.1f, 0f);
            ambient.transform.position = new Vector3(0f, -playArea.y * 0.5f - 1f, 0f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.EnemyLeaked += OnEnemyLeaked;
            GameEvents.ProjectileImpact += OnImpact;
            GameEvents.TowerBuilt += OnTowerBuilt;
            GameEvents.TowerUpgraded += OnTowerUpgraded;
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.CandleExtinguished += OnCandleOut;
            GameEvents.EraChanged += OnEraChanged;
            GameEvents.GameOver += OnGameOver;
            GameEvents.UpgradeChosen += OnUpgradeChosen;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.EnemyLeaked -= OnEnemyLeaked;
            GameEvents.ProjectileImpact -= OnImpact;
            GameEvents.TowerBuilt -= OnTowerBuilt;
            GameEvents.TowerUpgraded -= OnTowerUpgraded;
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.CandleExtinguished -= OnCandleOut;
            GameEvents.EraChanged -= OnEraChanged;
            GameEvents.GameOver -= OnGameOver;
            GameEvents.UpgradeChosen -= OnUpgradeChosen;
        }

        void OnEnemyKilled(Enemy enemy, float reward)
        {
            Vector3 p = enemy.transform.position;
            // The death clip plays on the enemy itself; the smoke rises behind it as it fades.
            PlayFlipbook(deathFrames, p + Vector3.down * 0.2f, enemy.Data.size * 0.9f, 14f);
            Emit(dust, p + Vector3.down * 0.3f, 8, DustColor);
            if (enemy.Data.isBoss)
            {
                PlayFlipbook(bossDeathFrames, p, enemy.Data.size * 1.6f, 12f);
                Emit(sparks, p, 50);
                Emit(smoke, p, 30, SmokeColor);
                CameraShake.Add(bigShake);
                TimeControl.HitStop(0.12f);
            }
        }

        void OnEnemyLeaked(Enemy enemy)
        {
            Emit(smoke, enemy.transform.position, 16, SmokeColor);
        }

        void OnImpact(Vector3 pos, float radius, TowerKind kind)
        {
            TowerData data = TowerOfKind(kind);
            if (data != null)
            {
                float scale = data.impactScale * (radius > 0f ? radius : 1f);
                PlayFlipbook(data.impactSecondaryFrames, pos, scale * 1.3f);
                PlayFlipbook(data.impactFrames, pos, scale);
            }

            bool fire = kind == TowerKind.Candle;
            if (radius > 0f)
            {
                if (kind == TowerKind.Firework) Emit(sparks, pos, 22);
                Emit(dust, pos, 12, DustColor);
                CameraShake.Add(kind == TowerKind.Firework ? mediumShake : smallShake * 1.5f);
                if (splashHitPause > 0f) TimeControl.HitStop(splashHitPause);
            }
            else
            {
                if (fire) Emit(embers, pos, 5);
                else Emit(dust, pos, 3, DustColor);
                CameraShake.Add(smallShake * 0.25f);
            }
        }

        void OnTowerBuilt(Tower tower)
        {
            Vector3 feet = tower.transform.position + Vector3.down * 0.3f;
            PlayFlipbook(buildFrames, feet, 1.6f, 14f);
            Emit(dust, feet, 14, DustColor);
            CameraShake.Add(smallShake);
        }

        void OnTowerUpgraded(Tower tower, int level)
        {
            Emit(embers, tower.transform.position + Vector3.up * 0.5f, 20);
            Emit(dust, tower.transform.position + Vector3.down * 0.3f, 10, DustColor);
        }

        // A year survived: the cake's candles flare up.
        void OnWaveCleared(int year)
        {
            Vector3 p = partyPoint != null ? partyPoint.position : Vector3.zero;
            Emit(embers, p + Vector3.up * 0.6f, 50);
            Emit(sparks, p + Vector3.up * 0.8f, 20);
        }

        void OnCandleOut(int index) => CameraShake.Add(bigShake * 0.6f);

        void OnUpgradeChosen(UpgradeCard card)
        {
            if (card.effect == CardEffect.ExtraCandle) ScreenEmbers(200);
        }

        void OnEraChanged(int era)
        {
            var theme = ThemeManager.Instance;
            var data = theme != null ? theme.CurrentEra : null;
            var emission = ambient.emission;
            emission.rateOverTime = data != null ? data.confettiRate : 0f;
            StopAllCoroutines();
            if (data != null && data.backgroundFireworks) StartCoroutine(BackgroundFireworks());
        }

        void OnGameOver(bool victory)
        {
            StopAllCoroutines();
            if (victory) StartCoroutine(Finale());
        }

        // Embers rising across the whole field.
        public void ScreenEmbers(int count)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(Random.Range(-playArea.x, playArea.x) * 0.5f, Random.Range(-playArea.y, 0f) * 0.5f, 0f);
                Emit(embers, p, count / 6);
            }
        }

        IEnumerator BackgroundFireworks()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(1.5f, 3f));
                Emit(sparks, RandomSkyPoint(), 30);
            }
        }

        IEnumerator Finale()
        {
            float end = Time.unscaledTime + finaleSeconds;
            while (Time.unscaledTime < end)
            {
                Emit(sparks, RandomSkyPoint(), 45);
                if (Random.value < 0.4f) Emit(embers, RandomSkyPoint(), 40);
                CameraShake.Add(smallShake);
                yield return new WaitForSecondsRealtime(Random.Range(0.15f, 0.35f));
            }
        }
        Vector3 RandomSkyPoint() => new Vector3(Random.Range(-playArea.x, playArea.x) * 0.4f, Random.Range(0f, playArea.y * 0.4f), 0f);

        // A small wisp of smoke, e.g. a candle on the cake going out.
        public void Puff(Vector3 pos) => Emit(smoke, pos, 6, new Color(0.85f, 0.85f, 0.9f, 0.7f));

        public void PlayFlipbook(Sprite[] frames, Vector3 pos, float scale, float fps = 16f)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null) return;
            flipbooks.Get().Play(frames, pos, scale, fps, flipbooks.Release);
        }

        static TowerData TowerOfKind(TowerKind kind)
        {
            var build = BuildManager.Instance;
            if (build == null || build.Towers == null) return null;
            foreach (var t in build.Towers)
                if (t != null && t.kind == kind) return t;
            return null;
        }

        Flipbook CreateFlipbook()
        {
            var go = new GameObject("Flipbook");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var f = go.AddComponent<Flipbook>();
            f.Build(24);
            return f;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, int count, Color? color = null)
        {
            if (ps == null || count <= 0) return;
            var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true };
            if (color.HasValue) ep.startColor = color.Value;
            ps.Emit(ep, count);
        }

        ParticleSystem CreateSystem(string name, Material baseMat, Sprite sprite, int order, float gravity,
            (float min, float max) life, (float min, float max) size, (float min, float max) speed, bool multicolor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.min, life.max);
            main.startSize = new ParticleSystem.MinMaxCurve(size.min, size.max);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.min, speed.max);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (multicolor)
            {
                var gradient = new Gradient();
                var keys = new GradientColorKey[EmberColors.Length > 8 ? 8 : EmberColors.Length];
                for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(EmberColors[i], i / (float)(keys.Length - 1));
                gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
            }

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.5f;

            var fadeOut = ps.colorOverLifetime;
            fadeOut.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fadeOut.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(baseMat);
            if (sprite != null) mat.mainTexture = sprite.texture;
            renderer.sharedMaterial = mat;
            renderer.sortingOrder = order;

            ps.Play();
            return ps;
        }

        // The pipeline's default sprite material works with the 2D renderer and is always in the build.
        static Material DefaultSpriteMaterial()
        {
            var probe = new GameObject("MaterialProbe").AddComponent<SpriteRenderer>();
            Material mat = probe.sharedMaterial;
            Destroy(probe.gameObject);
            return mat;
        }
    }
}
