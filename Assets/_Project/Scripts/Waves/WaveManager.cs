using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Year counter, formula-driven waves, and detecting when a wave is cleared.
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

        [Header("count(n) = base + perYear * n")]
        [SerializeField] int countBase = 5;
        [SerializeField] int countPerYear = 2;

        [Header("hp(n) = base * (1 + linear(n-1)) * exp^(n-1)")]
        [SerializeField] float hpBase = 10.5f;
        [SerializeField] float hpLinear = 0.42f;
        [Tooltip("GDD value is 1.13. Autoplay (Sept 2026): 10.5 / 0.42 / 1.12 with reward 2.6 + 0.25n, gifts every 2 years and second roads: wins with cards (60-145 s left), loses in year 10 without.")]
        [SerializeField] float hpExponent = 1.12f;

        [Header("speed(n) = base + perYear(n-1), times world scale")]
        [SerializeField] float speedBase = 1f;
        [SerializeField] float speedPerYear = 0.04f;
        [Tooltip("World units per second for speed 1.0. Tune to the road length.")]
        [SerializeField] float worldSpeedScale = 2.2f;

        [Header("reward(n) = base + perYear * n")]
        [Tooltip("GDD value is 1.3, which the autoplay bot could not win even with cards. 2.1 keeps a careful player out of time trouble.")]
        [SerializeField] float rewardBase = 2.6f;
        [SerializeField] float rewardPerYear = 0.25f;

        [Header("gap(n) = max(min, base - perYear * n)")]
        [SerializeField] float gapBase = 1.2f;
        [SerializeField] float gapPerYear = 0.06f;
        [SerializeField] float gapMin = 0.55f;
        [SerializeField] float year9BreakSeconds = 3f;

        public int CurrentYear { get; private set; }
        public bool IsRunning { get; private set; }
        public int RemainingToSpawn { get; private set; }
        public int Alive { get; private set; }
        public int Remaining => RemainingToSpawn + Alive;

        public int Count(int n) => countBase + countPerYear * n;
        public float Hp(int n) => hpBase * (1f + hpLinear * (n - 1)) * Mathf.Pow(hpExponent, n - 1);
        public float Speed(int n) => speedBase + speedPerYear * (n - 1);
        public float Reward(int n) => rewardBase + rewardPerYear * n;
        public float Gap(int n) => Mathf.Max(gapMin, gapBase - gapPerYear * n);

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

        public void StartWave(int year)
        {
            CurrentYear = year;
            List<EnemyData> lineup = Compose(year);
            RemainingToSpawn = lineup.Count;
            Alive = 0;
            IsRunning = true;
            GameEvents.RaiseWaveStarted(year);
            spawning = StartCoroutine(Spawn(year, lineup));
        }

        IEnumerator Spawn(int year, List<EnemyData> lineup)
        {
            var stats = new EnemyStats
            {
                hp = Hp(year),
                speed = Speed(year) * worldSpeedScale,
                reward = Reward(year)
            };
            float gap = Gap(year);
            int breakAt = year == 9 ? lineup.Count / 2 : -1;

            for (int i = 0; i < lineup.Count; i++)
            {
                if (i == breakAt) yield return new WaitForSeconds(year9BreakSeconds);

                RemainingToSpawn--;
                Alive++;
                spawner.Spawn(lineup[i], stats, TakesSecondRoad(year, i, lineup[i]));

                if (i < lineup.Count - 1) yield return new WaitForSeconds(gap);
            }
            spawning = null;
            CheckCleared();
        }

        // Maps with two entrances open the second road part-way through the game:
        // a third of each wave uses it at first, half of it two years later. The boss always takes the main road.
        public bool SecondRoadActive(int year)
        {
            var maps = MapLoader.Instance;
            return spawner.HasSecondRoute && maps != null && year >= maps.SecondRouteFromYear;
        }

        public bool SecondRoadOpensThisYear(int year)
        {
            var maps = MapLoader.Instance;
            return spawner.HasSecondRoute && maps != null && year == maps.SecondRouteFromYear;
        }

        bool TakesSecondRoad(int year, int index, EnemyData data)
        {
            if (data == boss || !SecondRoadActive(year)) return false;
            int every = year < MapLoader.Instance.SecondRouteFromYear + 2 ? 3 : 2;
            return index % every == every - 1;
        }

        List<(EnemyData data, float weight)> Weights(int year)
        {
            var weights = new List<(EnemyData data, float weight)>();
            if (year <= 2) weights.Add((balloon, 1f));
            else if (year <= 4) { weights.Add((balloon, 0.65f)); weights.Add((runner, 0.35f)); }
            else if (year <= 6) { weights.Add((balloon, 0.45f)); weights.Add((runner, 0.3f)); weights.Add((giftBox, 0.25f)); }
            else if (year <= 8) { weights.Add((balloon, 0.25f)); weights.Add((runner, 0.25f)); weights.Add((giftBox, 0.35f)); weights.Add((lantern, 0.15f)); }
            else { weights.Add((balloon, 0.3f)); weights.Add((runner, 0.25f)); weights.Add((giftBox, 0.3f)); weights.Add((lantern, 0.15f)); }
            return weights;
        }

        // Which enemy types a year brings, for the "coming next" preview.
        public List<EnemyData> UpcomingTypes(int year)
        {
            var list = new List<EnemyData>();
            foreach (var w in Weights(year))
                if (w.data != null && !list.Contains(w.data)) list.Add(w.data);
            if (year >= 10 && boss != null) list.Add(boss);
            return list;
        }

        // Mix per the GDD table. Proportional (not random) counts keep balance stable between runs.
        List<EnemyData> Compose(int year)
        {
            int count = Count(year);
            var weights = Weights(year);

            bool hasBoss = year >= 10 && boss != null;
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

            // Shuffle, but never open a wave with a tank or support unit.
            for (int i = lineup.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
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
            GameEvents.RaiseWaveCleared(CurrentYear);
        }

        void OnGameOver(bool victory)
        {
            if (spawning != null) StopCoroutine(spawning);
            spawning = null;
            IsRunning = false;
        }
    }
}
