using System.Collections;
using System.Collections.Generic;
using TenCandles.Core;
using TenCandles.Lifetime;
using TenCandles.Waves;
using UnityEngine;

namespace TenCandles
{
    // Runs one wave per age: stats from WaveGenerator (spec §2.1), the enemy mix, and detecting when it is cleared.
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [SerializeField] EnemySpawner spawner;

        [Header("Enemy types")]
        [SerializeField] EnemyData balloon;
        [SerializeField] EnemyData runner;
        [SerializeField] EnemyData giftBox;
        [SerializeField] EnemyData lantern;
        [SerializeField] EnemyData boss;

        [Tooltip("World units per second for speed 1.0. Tune to the road length.")]
        [SerializeField] float worldSpeedScale = 2.2f;
        [SerializeField] float year9BreakSeconds = 3f;

        public int CurrentAge { get; private set; }
        public bool IsRunning { get; private set; }
        public int RemainingToSpawn { get; private set; }
        public int Alive { get; private set; }
        public int Remaining => RemainingToSpawn + Alive;

        Coroutine spawning;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyGone;
            GameEvents.EnemyLeaked += OnEnemyLeaked;
            GameEvents.GameOver += OnGameOver;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyGone;
            GameEvents.EnemyLeaked -= OnEnemyLeaked;
            GameEvents.GameOver -= OnGameOver;
        }

        static bool IsBossYear(int yearInDecade) => yearInDecade >= Balance.YearsPerDecade;

        public void StartWave(int age)
        {
            CurrentAge = age;
            List<EnemyData> lineup = Compose(age);
            RemainingToSpawn = lineup.Count;
            Alive = 0;
            IsRunning = true;
            GameEvents.RaiseWaveStarted(age);
            spawning = StartCoroutine(Spawn(age, lineup));
        }

        IEnumerator Spawn(int age, List<EnemyData> lineup)
        {
            int d = WaveGenerator.TierOfAge(age), y = WaveGenerator.YearOfAge(age);
            var slot = LifetimeManager.Instance != null && LifetimeManager.Instance.Run != null ? LifetimeManager.Instance.Run.Slots[d] : null;
            var stats = new EnemyStats
            {
                hp = WaveGenerator.Hp(d, y, slot != null && slot.IsRepeat),
                speed = WaveGenerator.Speed(d, y) * worldSpeedScale,
                reward = WaveGenerator.Reward(d, y)
            };
            float gap = WaveGenerator.Gap(d, y);
            int breakAt = y == Balance.YearsPerDecade - 1 ? lineup.Count / 2 : -1;
            float[] shares = LaneShares = WaveGenerator.LaneShares(spawner.LaneCount, d, y);
            var regularPerLane = new int[shares.Length];
            SpawnedPerLane = new int[shares.Length];

            for (int i = 0; i < lineup.Count; i++)
            {
                if (i == breakAt) yield return new WaitForSeconds(year9BreakSeconds);

                RemainingToSpawn--;
                Alive++;
                // Regular enemies follow the lane shares (the new lane eases in); the boss always takes the first lane.
                int lane = lineup[i] == boss ? 0 : WaveGenerator.PickLane(shares, regularPerLane);
                if (lineup[i] != boss) regularPerLane[lane]++;
                SpawnedPerLane[lane]++;
                spawner.Spawn(lineup[i], stats, lane);

                if (i < lineup.Count - 1) yield return new WaitForSeconds(gap);
            }
            spawning = null;
            CheckCleared();
        }

        // Spec §7 / §12.2: min(map entrances, the tier's lane allowance). Tier-indexed, so Stay does not delay it.
        public int ActiveLanes(int age) => WaveGenerator.ActiveLanes(spawner.LaneCount, WaveGenerator.TierOfAge(age));

        // The current wave: planned share per lane, and how many enemies each lane has been sent so far.
        public float[] LaneShares { get; private set; } = { 1f };
        public int[] SpawnedPerLane { get; private set; } = new int[1];

        // True for the first age at which a map's extra lane is used.
        public bool LaneOpensThisYear(int age) => age > 1 && ActiveLanes(age) > ActiveLanes(age - 1);

        // The mix follows the year within the decade, so every decade opens gently again.
        List<(EnemyData data, float weight)> Weights(int yearInDecade)
        {
            var weights = new List<(EnemyData data, float weight)>();
            if (yearInDecade <= 2) weights.Add((balloon, 1f));
            else if (yearInDecade <= 4) { weights.Add((balloon, 0.65f)); weights.Add((runner, 0.35f)); }
            else if (yearInDecade <= 6) { weights.Add((balloon, 0.45f)); weights.Add((runner, 0.3f)); weights.Add((giftBox, 0.25f)); }
            else if (yearInDecade <= 8) { weights.Add((balloon, 0.25f)); weights.Add((runner, 0.25f)); weights.Add((giftBox, 0.35f)); weights.Add((lantern, 0.15f)); }
            else { weights.Add((balloon, 0.3f)); weights.Add((runner, 0.25f)); weights.Add((giftBox, 0.3f)); weights.Add((lantern, 0.15f)); }
            return weights;
        }

        // Which enemy types an age brings, for the "coming next" preview.
        public List<EnemyData> UpcomingTypes(int age)
        {
            int y = WaveGenerator.YearOfAge(age);
            var list = new List<EnemyData>();
            foreach (var w in Weights(y))
                if (w.data != null && !list.Contains(w.data)) list.Add(w.data);
            if (IsBossYear(y) && boss != null) list.Add(boss);
            return list;
        }

        // Mix per the GDD table. Proportional (not random) counts keep balance stable between runs.
        List<EnemyData> Compose(int age)
        {
            int d = WaveGenerator.TierOfAge(age), y = WaveGenerator.YearOfAge(age);
            int count = WaveGenerator.Count(d, y);
            var weights = Weights(y);

            bool hasBoss = IsBossYear(y) && boss != null;
            int regular = hasBoss ? count - 1 : count;

            var lineup = new List<EnemyData>(count);
            float total = 0f;
            foreach (var w in weights) total += w.weight;
            int assigned = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                int n = i == weights.Count - 1 ? regular - assigned : Mathf.RoundToInt(regular * weights[i].weight / total);
                n = Mathf.Clamp(n, 0, regular - assigned);
                for (int k = 0; k < n; k++) lineup.Add(weights[i].data ?? balloon);
                assigned += n;
            }

            // Shuffle with the run's seeded RNG, but never open a wave with a tank or support unit.
            System.Random rng = LifetimeManager.RunRandom;
            for (int i = lineup.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (lineup[i], lineup[j]) = (lineup[j], lineup[i]);
            }
            int firstBalloon = lineup.IndexOf(balloon);
            if (firstBalloon > 0) (lineup[0], lineup[firstBalloon]) = (lineup[firstBalloon], lineup[0]);

            if (hasBoss) lineup.Insert(lineup.Count / 2, boss);
            return lineup;
        }

        void OnEnemyGone(Enemy enemy, float reward) => OnEnemyLeaked(enemy);

        void OnEnemyLeaked(Enemy enemy)
        {
            if (!IsRunning) return;
            Alive = Mathf.Max(0, Alive - 1);
            CheckCleared();
        }

        void CheckCleared()
        {
            if (!IsRunning || spawning != null || RemainingToSpawn > 0 || Alive > 0) return;
            IsRunning = false;
            GameEvents.RaiseWaveCleared(CurrentAge);
        }

        void OnGameOver(bool victory)
        {
            if (spawning != null) StopCoroutine(spawning);
            spawning = null;
            IsRunning = false;
        }
    }
}
