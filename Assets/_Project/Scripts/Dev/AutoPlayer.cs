using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TenCandles
{
    // Dev-only bot for balance checks. Plays like a sensible player and logs every year.
    // Not placed in the scene; the AutoTestRunner editor tool adds it.
    public class AutoPlayer : MonoBehaviour
    {
        public bool useCards = true;
        public float reserveSeconds = 40f;
        public System.Action<bool, string> Finished;

        readonly StringBuilder report = new StringBuilder();
        int leaksThisYear, leaksTotal;
        float thinkTimer;

        static readonly CardEffect[] CardPriority =
        {
            CardEffect.Blessing, CardEffect.ExtraCandle, CardEffect.SlowBurn, CardEffect.Damage, CardEffect.FireRate,
            CardEffect.KindDamage, CardEffect.KindFireRate, CardEffect.SplashRadius, CardEffect.Crit, CardEffect.ArmorPierce, CardEffect.HitSlow,
            CardEffect.WaveEndBonus, CardEffect.LeakShield, CardEffect.FreeTower, CardEffect.Range, CardEffect.KillBonus,
            CardEffect.LastBreath, CardEffect.UpgradeCost, CardEffect.InstantTime, CardEffect.Chain, CardEffect.BuildCost, CardEffect.BurnBoost, CardEffect.Beeswax
        };

        // Rough plan in the spirit of the GDD target build: (kind, nth tower of that kind, level).
        static readonly (TowerKind kind, int index, int level)[] Plan =
        {
            (TowerKind.Confetti, 0, 1), (TowerKind.Confetti, 1, 1), (TowerKind.Cake, 0, 1), (TowerKind.Confetti, 0, 2),
            (TowerKind.Cake, 1, 1), (TowerKind.Confetti, 1, 2), (TowerKind.Firework, 0, 1), (TowerKind.Cake, 0, 2),
            (TowerKind.Cake, 1, 2), (TowerKind.Firework, 0, 2), (TowerKind.Firework, 0, 3), (TowerKind.Candle, 0, 1),
            (TowerKind.Firework, 1, 1), (TowerKind.Firework, 1, 2), (TowerKind.Cake, 2, 1), (TowerKind.Cake, 2, 2),
            (TowerKind.Firework, 1, 3), (TowerKind.Cake, 0, 3), (TowerKind.Cake, 1, 3), (TowerKind.Firework, 2, 1),
            (TowerKind.Firework, 2, 2), (TowerKind.Firework, 2, 3), (TowerKind.Cake, 2, 3), (TowerKind.Confetti, 0, 3),
        };

        readonly List<Tower> built = new List<Tower>();

        void OnEnable()
        {
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.EnemyLeaked += OnLeaked;
            GameEvents.LevelUpOffered += OnOffered;
            GameEvents.GameOver += OnGameOver;
            GameEvents.TowerBuilt += OnTowerBuilt;
            GameEvents.EnemyKilled += OnKilled;
        }

        void OnDisable()
        {
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.EnemyLeaked -= OnLeaked;
            GameEvents.LevelUpOffered -= OnOffered;
            GameEvents.GameOver -= OnGameOver;
            GameEvents.TowerBuilt -= OnTowerBuilt;
            GameEvents.EnemyKilled -= OnKilled;
        }

        void OnTowerBuilt(Tower t) => built.Add(t);

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.State == GameState.Boot)
            {
                gm.StartGame();
                return;
            }
            if (gm.State != GameState.Intermission && gm.State != GameState.Wave) return;

            thinkTimer -= Time.deltaTime;
            if (thinkTimer > 0f) return;
            thinkTimer = 0.25f;

            bool acted = TryNextStep();
            if (gm.State == GameState.Intermission && !acted && gm.IntermissionLeft < 9f) gm.CallWaveEarly();
        }

        bool TryNextStep()
        {
            var build = BuildManager.Instance;
            var clock = CandleClock.Instance;

            foreach (var step in Plan)
            {
                var ofKind = built.Where(t => t.Data.kind == step.kind).ToList();
                if (step.index < ofKind.Count && ofKind[step.index].Level >= step.level) continue;

                if (step.index >= ofKind.Count)
                {
                    TowerData data = build.Towers.First(t => t.kind == step.kind);
                    float cost = build.BuildCost(data);
                    if (cost > 0f && clock.TimeRemaining - cost < reserveSeconds) return false;
                    TowerSpot spot = BestSpot(data);
                    // Maps have fewer pads than the plan has towers: skip what cannot be placed.
                    if (spot == null) continue;
                    return build.TryBuild(data, spot);
                }

                Tower tower = ofKind[step.index];
                if (tower.Level + 1 < step.level) return false;
                float upgradeCost = build.UpgradeCost(tower);
                if (clock.TimeRemaining - upgradeCost < reserveSeconds) return false;
                return build.TryUpgrade(tower);
            }
            return false;
        }

        // The empty pad that covers the most road within the tower's range.
        TowerSpot BestSpot(TowerData data)
        {
            var spawner = FindAnyObjectByType<EnemySpawner>();
            var routes = new List<PathRoute> { spawner.Route };
            if (spawner.HasSecondRoute) routes.Add(spawner.SecondRoute);
            TowerSpot best = null;
            int bestScore = -1;
            foreach (var spot in FindObjectsByType<TowerSpot>(FindObjectsSortMode.None))
            {
                if (!spot.IsEmpty) continue;
                int score = 0;
                foreach (var route in routes)
                    for (int i = 0; i < route.Count - 1; i++)
                    {
                        Vector3 a = route[i], b = route[i + 1];
                        int samples = Mathf.CeilToInt(Vector3.Distance(a, b) / 0.25f);
                        for (int s = 0; s <= samples; s++)
                            if (Vector3.Distance(Vector3.Lerp(a, b, s / (float)samples), spot.transform.position) <= data.range) score++;
                    }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = spot;
                }
            }
            return best;
        }

        void OnOffered(UpgradeCard[] cards)
        {
            int pick = 0;
            if (useCards)
            {
                int bestRank = int.MaxValue;
                for (int i = 0; i < cards.Length; i++)
                {
                    int rank = System.Array.IndexOf(CardPriority, cards[i].effect);
                    if (rank >= 0 && rank < bestRank)
                    {
                        bestRank = rank;
                        pick = i;
                    }
                }
                report.AppendLine($"    card: {cards[pick].title}");
                LevelUpManager.Instance.Choose(pick);
            }
            else
            {
                // "No cards" run: skip the level-up without applying anything.
                GameEvents.RaiseUpgradeChosen(cards[0]);
            }
        }

        void OnLeaked(Enemy e)
        {
            leaksThisYear++;
            leaksTotal++;
        }

        void OnWaveCleared(int year)
        {
            var towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
            float dps = towers.Sum(t => t.Dps);
            string build = string.Join(" ", towers.OrderBy(t => t.Data.kind).Select(t => $"{Code(t.Data.kind)}{t.Level}"));
            report.AppendLine($"Year {year,2} cleared | time {CandleClock.Instance.TimeRemaining,6:0.0}s | kills +{killIncome,5:0.0}s | leaks {leaksThisYear} | raw DPS {dps,6:0.0} | {build}");
            leaksThisYear = 0;
            killIncome = 0f;
        }

        float killIncome;

        void OnKilled(Enemy e, float reward) => killIncome += reward + StatRegistry.KillBonus + e.Data.killBonusTime;

        static string Code(TowerKind kind) => kind switch
        {
            TowerKind.Confetti => "Conf",
            TowerKind.Candle => "Cndl",
            TowerKind.Cake => "Cake",
            _ => "Fire"
        };

        void OnGameOver(bool victory)
        {
            var gm = GameManager.Instance;
            report.AppendLine(victory
                ? $"VICTORY with {CandleClock.Instance.TimeRemaining:0.0}s left, {leaksTotal} leaks, game time {Time.timeSinceLevelLoad:0}s"
                : $"DEFEAT in year {gm.Year} ({leaksTotal} leaks, {WaveManager.Instance.Remaining} guests still coming), game time {Time.timeSinceLevelLoad:0}s");
            Finished?.Invoke(victory, report.ToString());
        }
    }
}
