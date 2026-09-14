using System.Collections.Generic;
using System.Linq;
using System.Text;
using TenCandles.Lifetime;
using TenCandles.Waves;
using UnityEngine;

namespace TenCandles
{
    // Dev-only bot for balance checks. Plays like a sensible player and logs every year.
    // Not placed in the scene; the AutoTestRunner editor tool adds it.
    public class AutoPlayer : MonoBehaviour
    {
        public bool useCards = true;
        public float reserveSeconds = 40f;
        public bool avoidExtraCandles;
        // Birthday choices, one letter per birthday: "SAS" = Stay, Advance, Stay.
        public string life;
        public System.Action<bool, string> Finished;
        // Set by AutoTestRunner with -bot.shots; receives a file tag.
        public System.Action<string> Snapshot;

        const int BirthdayHoldFrames = 6;
        const int StageShotFrames = 30;
        int birthdayFrames, stageShotIn;
        bool laneShotTaken;

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
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.EnemyLeaked -= OnLeaked;
            GameEvents.LevelUpOffered -= OnOffered;
            GameEvents.GameOver -= OnGameOver;
            GameEvents.TowerBuilt -= OnTowerBuilt;
            GameEvents.EnemyKilled -= OnKilled;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void OnTowerBuilt(Tower t) => built.Add(t);

        GameState previousState, currentState;
        void OnStateChanged(GameState s)
        {
            previousState = currentState;
            currentState = s;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.State == GameState.Boot)
            {
                gm.StartGame();
                stageShotIn = StageShotFrames;
                return;
            }

            // Optional screenshots: every birthday screen, and the HUD shortly after each stage begins.
            if (Snapshot != null && stageShotIn > 0 && --stageShotIn == 0)
            {
                var s = gm.Lifetime.CurrentSlot;
                Snapshot($"stage{s.TierIndex}_{s.Stage.ToString().ToLowerInvariant()}");
            }

            if (gm.State == GameState.Birthday)
            {
                // Hold the screen for a few frames so it is drawn before it can be captured.
                if (Snapshot != null && ++birthdayFrames < BirthdayHoldFrames)
                {
                    if (birthdayFrames == BirthdayHoldFrames - 1) Snapshot($"birthday_{gm.Age}");
                    return;
                }
                birthdayFrames = 0;
                stageShotIn = StageShotFrames;
                var slot = gm.Lifetime.CurrentSlot;
                // -bot.life=SAS: the Stay / Advance sequence to play. Advance when not given or not legal.
                int birthday = gm.Lifetime.Run.Choices.Count;
                BirthdayChoice choice = life != null && birthday < life.Length && life[birthday] == 'S' ? BirthdayChoice.Stay : BirthdayChoice.Advance;
                var options = gm.Lifetime.Options;
                report.AppendLine($"    birthday at {gm.Age} in {slot.Stage}: stay {(options.StayLegal ? "legal" : "illegal (" + options.StayBlockedReason + ")")}, advance {(options.AdvanceLegal ? "legal" : "illegal (" + options.AdvanceBlockedReason + ")")}");
                if (!options.IsLegal(choice))
                {
                    report.AppendLine($"    wanted {choice}, which is illegal: advancing instead");
                    choice = BirthdayChoice.Advance;
                }
                gm.ChooseBirthday(choice);
                slot = gm.Lifetime.CurrentSlot;
                report.AppendLine($"    chose {choice}: {slot.Stage} (tier {slot.TierIndex}, ages {slot.AgeFrom}-{slot.AgeTo}, repeat {slot.IsRepeat}), candle cap {CandleClock.Instance.CandleCap}, masteries [{string.Join(", ", gm.Lifetime.Run.Masteries)}], passives [{string.Join(", ", BirthdayController.Passives(gm.Lifetime.Run).Select(p => p.Name))}]");
                return;
            }
            if (gm.State != GameState.Intermission && gm.State != GameState.Wave) return;

            // The wave where a second lane first opens, once most of it is on the roads.
            var waves = WaveManager.Instance;
            if (Snapshot != null && !laneShotTaken && gm.State == GameState.Wave && waves.LaneOpensThisYear(gm.Age)
                && waves.SpawnedPerLane.Sum() >= WaveGenerator.Count(WaveGenerator.TierOfAge(gm.Age), WaveGenerator.YearOfAge(gm.Age)) - 3)
            {
                laneShotTaken = true;
                Snapshot($"lanes_age{gm.Age}");
            }

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
                    // Over the age limit: skip new towers and keep upgrading.
                    if (build.AtTowerCapacity) continue;
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
            // Fixed pad order, so ties resolve the same way every run (same seed, same build).
            foreach (var spot in FindObjectsByType<TowerSpot>(FindObjectsSortMode.None).OrderBy(s => s.transform.position.x).ThenBy(s => s.transform.position.y))
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
                    // Keeps the candle bar at the bare stage caps, for checking them.
                    if (avoidExtraCandles && cards[i].effect == CardEffect.ExtraCandle) rank = CardPriority.Length;
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
            string build = string.Join(" ", towers.OrderBy(t => t.Data.kind).ThenBy(t => built.IndexOf(t)).Select(t => $"{Code(t.Data.kind)}{t.Level}"));
            var bm = BuildManager.Instance;
            var clock = CandleClock.Instance;
            report.AppendLine($"Age {year,2} cleared | time {clock.TimeRemaining,6:0.0}s | candles {clock.CandleCap,2} | towers {bm.TowersBuilt}/{bm.TowerCapacity} | lanes {string.Join("/", WaveManager.Instance.SpawnedPerLane)} | kills +{killIncome,5:0.0}s | leaks {leaksThisYear} | raw DPS {dps,6:0.0} | {build}");
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
                ? $"VICTORY with {CandleClock.Instance.TimeRemaining:0.0}s left, {leaksTotal} leaks, game time {Time.timeSinceLevelLoad:0}s, life {BirthdayController.Code(gm.Lifetime.Run.Choices)} = {BirthdayController.LifeName(gm.Lifetime.Run.Choices)}, stages [{string.Join(", ", gm.Lifetime.Run.Slots.Select(s => s.Stage))}], masteries [{string.Join(", ", gm.Lifetime.Run.Masteries)}], passives [{string.Join(", ", BirthdayController.Passives(gm.Lifetime.Run).Select(p => p.Name))}], seed {gm.Lifetime.Run.Seed}, kill reward ×{StatRegistry.KillRewardMultiplier:0.00}, fire rate ×{StatRegistry.FireRateMultiplier:0.00}"
                : $"DEFEAT at age {gm.Age} ({leaksTotal} leaks, {WaveManager.Instance.Remaining} guests still coming), game time {Time.timeSinceLevelLoad:0}s, life so far {BirthdayController.Code(gm.Lifetime.Run.Choices)}, seed {gm.Lifetime.Run.Seed}");
            if (!victory)
            {
                report.AppendLine($"    state before defeat: {previousState}, wave running {WaveManager.Instance.IsRunning}, alive counter {WaveManager.Instance.Alive}, timeScale {Time.timeScale}, dt {Time.deltaTime}, birthday hold frame {birthdayFrames}");
                foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                    report.AppendLine($"    on map: {e.Data.displayName}: alive {e.IsAlive}, targetable {e.IsTargetable}, progress {e.PathProgress:0.0}");
            }
            Finished?.Invoke(victory, report.ToString());
        }
    }
}
