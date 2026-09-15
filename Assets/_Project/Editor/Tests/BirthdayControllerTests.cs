using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenCandles.Core;
using TenCandles.Lifetime;
using TenCandles.Waves;

namespace TenCandles.EditorTools.Tests
{
    // Spec §5 (Phase 2). Window > General > Test Runner > EditMode.
    public class BirthdayControllerTests
    {
        const BirthdayChoice S = BirthdayChoice.Stay;
        const BirthdayChoice A = BirthdayChoice.Advance;

        // The unlock column of the §9.2 tower table. Fixture data, like BalanceSimulator's §8 table.
        static readonly TowerUnlock[] SpecCatalogue =
        {
            new TowerUnlock("confetti_cannon", UnlockSource.Starting, DecadeStage.Childhood),
            new TowerUnlock("candle_tower", UnlockSource.Starting, DecadeStage.Childhood),
            new TowerUnlock("cake_catapult", UnlockSource.Starting, DecadeStage.Childhood),
            new TowerUnlock("firework_launcher", UnlockSource.Starting, DecadeStage.Childhood),
            new TowerUnlock("whistle", UnlockSource.StageVisit, DecadeStage.Childhood),
            new TowerUnlock("balloon_trap", UnlockSource.StageMastery, DecadeStage.Childhood),
            new TowerUnlock("music_box", UnlockSource.StageVisit, DecadeStage.Youth),
            new TowerUnlock("searchlight", UnlockSource.StageMastery, DecadeStage.Youth),
            new TowerUnlock("camera_flash", UnlockSource.StageVisit, DecadeStage.Adulthood),
            new TowerUnlock("champagne", UnlockSource.StageMastery, DecadeStage.Adulthood),
            new TowerUnlock("memory_lantern", UnlockSource.StageVisit, DecadeStage.OldAge),
            new TowerUnlock("hourglass", UnlockSource.StageVisit, DecadeStage.OldAge),        // both Old Age towers are visit towers
        };

        // §5.2, row by row.
        static readonly (string code, DecadeStage[] stages, string name, string[] masteries, string[] visits)[] SpecLives =
        {
            ("AAA", new[] { DecadeStage.Childhood, DecadeStage.Youth, DecadeStage.Adulthood, DecadeStage.OldAge }, "Full Life",
                new string[0], new[] { "whistle", "music_box", "camera_flash", "memory_lantern", "hourglass" }),
            ("AAS", new[] { DecadeStage.Childhood, DecadeStage.Youth, DecadeStage.Adulthood, DecadeStage.Adulthood }, "Late Bloomer",
                new[] { "champagne" }, new[] { "whistle", "music_box", "camera_flash" }),
            ("ASA", new[] { DecadeStage.Childhood, DecadeStage.Youth, DecadeStage.Youth, DecadeStage.Adulthood }, "Long Youth",
                new[] { "searchlight" }, new[] { "whistle", "music_box", "camera_flash" }),
            ("SAA", new[] { DecadeStage.Childhood, DecadeStage.Childhood, DecadeStage.Youth, DecadeStage.Adulthood }, "Slow Childhood",
                new[] { "balloon_trap" }, new[] { "whistle", "music_box", "camera_flash" }),
            ("SAS", new[] { DecadeStage.Childhood, DecadeStage.Childhood, DecadeStage.Youth, DecadeStage.Youth }, "Never Grew Up",
                new[] { "balloon_trap", "searchlight" }, new[] { "whistle", "music_box" }),
        };

        // Plays birthdays at ages 10, 20, 30 through the real rules. Null if any choice was illegal.
        static RunState Live(IEnumerable<BirthdayChoice> choices)
        {
            RunState run = BirthdayController.NewRun(0);
            foreach (BirthdayChoice choice in choices)
            {
                run.CurrentAge += Balance.YearsPerDecade - 1;          // the birthday age: 10, 20, 30
                if (!BirthdayController.Evaluate(run).IsLegal(choice)) return null;
                BirthdayController.Resolve(run, choice, SpecCatalogue);
                run.CurrentAge++;                                        // first age of the new decade
            }
            return run;
        }

        static IEnumerable<BirthdayChoice[]> AllSequences()
        {
            int birthdays = Balance.DecadesPerRun - 1;
            for (int bits = 0; bits < 1 << birthdays; bits++)
                yield return Enumerable.Range(0, birthdays).Select(i => (bits >> (birthdays - 1 - i) & 1) == 1 ? S : A).ToArray();
        }

        static string[] VisitTowers(RunState run) => SpecCatalogue
            .Where(t => t.Source == UnlockSource.StageVisit && BirthdayController.IsUnlocked(run, t))
            .Select(t => t.Id).ToArray();

        [Test]
        public void ExactlyFiveLegalChoiceSequences()
        {
            var legal = AllSequences().Where(seq => Live(seq) != null).Select(BirthdayController.Code).ToList();

            Assert.AreEqual(8, AllSequences().Count(), "3 birthdays give 2^3 sequences");
            Assert.AreEqual(5, legal.Count, "legal: " + string.Join(", ", legal));
            CollectionAssert.AreEquivalent(SpecLives.Select(l => l.code), legal);
        }

        [Test]
        public void EveryLegalLifeMatchesSpecTable()
        {
            foreach (var life in SpecLives)
            {
                RunState run = Live(life.code.Select(c => c == 'S' ? S : A));
                Assert.IsNotNull(run, life.code + " should be legal");
                Assert.AreEqual(Balance.TotalYears - Balance.YearsPerDecade + 1, run.CurrentAge, life.code);

                CollectionAssert.AreEqual(life.stages, run.Slots.Select(s => s.Stage), life.code + " composition");
                Assert.AreEqual(life.name, BirthdayController.LifeName(run.Choices), life.code + " name");
                CollectionAssert.AreEquivalent(life.masteries, run.Masteries, life.code + " masteries");
                CollectionAssert.AreEquivalent(life.visits, VisitTowers(run), life.code + " visit towers");

                for (int tier = 0; tier < run.Slots.Length; tier++)
                {
                    Assert.AreEqual(tier, run.Slots[tier].TierIndex, life.code + " tier index");
                    Assert.AreEqual(tier > 0 && run.Slots[tier].Stage == run.Slots[tier - 1].Stage, run.Slots[tier].IsRepeat, life.code + " IsRepeat at tier " + tier);
                }
            }
        }

        [Test]
        public void OnlyFullLifeReachesOldAge()
        {
            var reaching = SpecLives.Where(l => Live(l.code.Select(c => c == 'S' ? S : A)).VisitedStages.Contains(DecadeStage.OldAge)).Select(l => l.code);
            CollectionAssert.AreEqual(new[] { "AAA" }, reaching);
        }

        // After these choices, one more Stay would make a third decade in the same stage (S S *, A S S).
        [TestCase("S", "Childhood")]
        [TestCase("AS", "Youth")]
        public void ThirdDecadeInOneStageIsIllegalWithReason(string played, string stage)
        {
            Assert.IsNull(Live((played + "S").Select(c => c == 'S' ? S : A)), played + "S should be illegal");

            RunState run = Live(played.Select(c => c == 'S' ? S : A));
            run.CurrentAge += Balance.YearsPerDecade - 1;               // the next birthday

            BirthdayOptions options = BirthdayController.Evaluate(run);
            Assert.IsFalse(options.StayLegal);
            StringAssert.Contains(stage, options.StayBlockedReason);
            Assert.IsTrue(options.AdvanceLegal);
            Assert.IsEmpty(options.AdvanceBlockedReason);
            Assert.Throws<System.InvalidOperationException>(() => BirthdayController.Resolve(run, S, SpecCatalogue));
        }

        [Test]
        public void AdvanceFromOldAgeIsIllegalWithReason()
        {
            // Unreachable in a 4-slot run (§5.1), so build the state by hand.
            RunState run = BirthdayController.NewRun(0);
            run.Slots[1] = BirthdayController.MakeSlot(1, DecadeStage.OldAge, false);
            run.CurrentAge = 20;

            BirthdayOptions options = BirthdayController.Evaluate(run);
            Assert.IsFalse(options.AdvanceLegal);
            StringAssert.Contains("Old Age", options.AdvanceBlockedReason);
            Assert.IsTrue(options.StayLegal);
        }

        [Test]
        public void MasteriesPersistForTheRestOfTheRun()
        {
            RunState run = BirthdayController.NewRun(0);
            var balloonTrap = SpecCatalogue.First(t => t.Id == "balloon_trap");
            var searchlight = SpecCatalogue.First(t => t.Id == "searchlight");
            Assert.IsFalse(BirthdayController.IsUnlocked(run, balloonTrap));

            // Walk every age of an S A S life. Once earned at 10, balloon_trap must stay unlocked at every later age,
            // across the later Advance and Stay birthdays.
            var choices = new Queue<BirthdayChoice>(new[] { S, A, S });
            bool earned = false;
            for (run.CurrentAge = 1; run.CurrentAge <= Balance.TotalYears; run.CurrentAge++)
            {
                Assert.AreEqual(earned, BirthdayController.IsUnlocked(run, balloonTrap), $"balloon_trap at age {run.CurrentAge}");
                if (!LifetimeManager.IsBirthdayAge(run.CurrentAge)) continue;
                BirthdayController.Resolve(run, choices.Dequeue(), SpecCatalogue);
                earned = true;
            }
            Assert.AreEqual(0, choices.Count);
            Assert.IsTrue(BirthdayController.IsUnlocked(run, balloonTrap));
            Assert.IsTrue(BirthdayController.IsUnlocked(run, searchlight));
            Assert.IsFalse(BirthdayController.IsUnlocked(run, SpecCatalogue.First(t => t.Id == "champagne")));
        }

        [Test]
        public void StayHpMultiplierOnlyInRepeatDecades()
        {
            RunState run = Live(new[] { S, A, S });
            for (int age = 1; age <= Balance.TotalYears; age++)
            {
                int d = WaveGenerator.TierOfAge(age), y = WaveGenerator.YearOfAge(age);
                float expected = WaveGenerator.Hp(d, y) * (run.Slots[d].IsRepeat ? Balance.StayHpMultiplier : 1f);
                Assert.AreEqual(expected, WaveGenerator.Hp(d, y, run.Slots[d].IsRepeat), 1e-4f, "age " + age);
            }
            Assert.IsTrue(run.Slots[1].IsRepeat && run.Slots[3].IsRepeat && !run.Slots[2].IsRepeat);
        }

        // Correction (a): ageing follows the tier, so "Never Grew Up" gets the same 10/10/9/7 as a Full Life.
        [Test]
        public void CandleCapAndLanesFollowTierNotStage()
        {
            foreach (var life in SpecLives)
            {
                RunState run = Live(life.code.Select(c => c == 'S' ? S : A));
                CollectionAssert.AreEqual(new[] { 10, 10, 9, 7 }, run.Slots.Select(s => LifetimeManager.CandleCapFor(s.TierIndex)), life.code);
                CollectionAssert.AreEqual(new[] { 1, 2, 2, 2 }, run.Slots.Select(s => LifetimeManager.ActiveLanes(2, s.TierIndex)), life.code);
                CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, run.Slots.Select(s => LifetimeManager.ActiveLanes(1, s.TierIndex)), life.code);
            }
            Assert.AreEqual(3, LifetimeManager.TowerCapacityFor(1));
            Assert.AreEqual(8, LifetimeManager.TowerCapacityFor(Balance.TotalYears));
        }

        // Answer 2: Old Age can never be stayed in, so no Old Age mastery tower may exist.
        [Test]
        public void EveryMasteryTowerIsReachable()
        {
            foreach (var t in SpecCatalogue.Where(t => t.Source == UnlockSource.StageMastery))
                Assert.IsTrue(SpecLives.Any(l => Live(l.code.Select(c => c == 'S' ? S : A)).Masteries.Contains(t.Id)), t.Id + " is unreachable");
            foreach (var t in SpecCatalogue.Where(t => t.Source == UnlockSource.StageVisit))
                Assert.IsTrue(SpecLives.Any(l => VisitTowers(Live(l.code.Select(c => c == 'S' ? S : A))).Contains(t.Id)), t.Id + " is unreachable");
        }

        // Answer 1: §5.1 passive table, and each legal life's passives follow its Stays.
        [Test]
        public void MasteryPassivesMatchSpec()
        {
            Assert.AreEqual(("Long Summer", CardEffect.ExtraCandle, 1f), Passive(DecadeStage.Childhood));
            Assert.AreEqual(("Boundless Energy", CardEffect.FireRate, 0.15f), Passive(DecadeStage.Youth));
            Assert.AreEqual(("Established", CardEffect.TowerCapacityAdd, 1f), Passive(DecadeStage.Adulthood));
            Assert.IsNull(BirthdayController.PassiveFor(DecadeStage.OldAge));

            var expected = new Dictionary<string, string[]>
            {
                ["AAA"] = new string[0],
                ["AAS"] = new[] { "Established" },
                ["ASA"] = new[] { "Boundless Energy" },
                ["SAA"] = new[] { "Long Summer" },
                ["SAS"] = new[] { "Long Summer", "Boundless Energy" },
            };
            foreach (var life in SpecLives)
                CollectionAssert.AreEqual(expected[life.code], BirthdayController.Passives(Live(life.code.Select(c => c == 'S' ? S : A))).Select(p => p.Name), life.code);
        }

        static (string, CardEffect, float) Passive(DecadeStage stage)
        {
            MasteryPassive p = BirthdayController.PassiveFor(stage);
            return (p.Name, p.Effect, (float)System.Math.Round(p.Value, 4));
        }

        // The passives go through the card pipeline into StatRegistry and stay there.
        [Test]
        public void PassivesWriteStatRegistry()
        {
            StatRegistry.Reset();
            try
            {
                var energy = BirthdayController.PassiveFor(DecadeStage.Youth);
                var established = BirthdayController.PassiveFor(DecadeStage.Adulthood);
                UpgradeEffect.Apply(energy.Effect, energy.Value, default, energy.Name);
                UpgradeEffect.Apply(established.Effect, established.Value, default, established.Name);
                Assert.AreEqual(1.15f, StatRegistry.FireRateMultiplier, 1e-4f);
                Assert.AreEqual(1, StatRegistry.TowerCapacityBonus);
                Assert.AreEqual(1f, StatRegistry.KillRewardMultiplier, 1e-4f, "Savings is gone");
            }
            finally
            {
                StatRegistry.Reset();
            }
        }

        // Established: +1 on top of the age-derived capacity at every later age, including 40.
        [Test]
        public void EstablishedRaisesTowerCapacityForTheRestOfTheRun()
        {
            var go = new UnityEngine.GameObject("Lifetime");
            StatRegistry.Reset();
            try
            {
                var lifetime = go.AddComponent<LifetimeManager>();
                lifetime.BeginRun(0);
                foreach (BirthdayChoice choice in new[] { A, A, S })
                {
                    lifetime.Run.CurrentAge += Balance.YearsPerDecade - 1;
                    Assert.IsTrue(lifetime.ResolveBirthday(choice, SpecCatalogue));
                    lifetime.AdvanceAge();
                }
                for (; lifetime.Run.CurrentAge <= Balance.TotalYears; lifetime.AdvanceAge())
                    Assert.AreEqual(LifetimeManager.TowerCapacityFor(lifetime.Age) + 1, lifetime.TowerCapacity, "age " + lifetime.Age);
                Assert.AreEqual(9, LifetimeManager.TowerCapacityFor(Balance.TotalYears) + StatRegistry.TowerCapacityBonus);
            }
            finally
            {
                StatRegistry.Reset();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // Answer 3: the new lane eases in from 25 % to 50 % in the decade it opens, then holds 50 %.
        [Test]
        public void NewLaneEasesIn()
        {
            CollectionAssert.AreEqual(new[] { 1f }, WaveGenerator.LaneShares(2, 0, 5), "tier 0 is single-lane");
            CollectionAssert.AreEqual(new[] { 1f }, WaveGenerator.LaneShares(1, 2, 5), "single-entrance map");

            Assert.AreEqual(Balance.NewLaneShareStart, WaveGenerator.LaneShares(2, 1, 1)[1], 1e-4f, "age 11");
            Assert.AreEqual(Balance.NewLaneShareEnd, WaveGenerator.LaneShares(2, 1, 10)[1], 1e-4f, "age 20");
            float previous = 0f;
            for (int y = 1; y <= Balance.YearsPerDecade; y++)
            {
                float[] shares = WaveGenerator.LaneShares(2, 1, y);
                Assert.AreEqual(1f, shares.Sum(), 1e-4f);
                Assert.Greater(shares[1], previous - 1e-5f, "ramps up at year " + y);
                previous = shares[1];
            }
            for (int d = 2; d < Balance.DecadesPerRun; d++)
                for (int y = 1; y <= Balance.YearsPerDecade; y++)
                    Assert.AreEqual(Balance.NewLaneShareEnd, WaveGenerator.LaneShares(2, d, y)[1], 1e-4f, $"tier {d} year {y}");
        }

        // Dealing is deterministic and tracks the share: age 11 (11 enemies at 25 %) sends 3 down the new lane.
        [TestCase(1, 1, 11, 3)]
        [TestCase(1, 10, 30, 15)]
        [TestCase(3, 10, 34, 17)]
        public void DealingFollowsShares(int d, int y, int enemies, int expectedOnNewLane)
        {
            float[] shares = WaveGenerator.LaneShares(2, d, y);
            var dealt = new int[shares.Length];
            for (int i = 0; i < enemies; i++) dealt[WaveGenerator.PickLane(shares, dealt)]++;
            Assert.AreEqual(expectedOnNewLane, dealt[1], $"{dealt[0]}/{dealt[1]}");
        }

        // Answer 7: a run's randomness comes from its seed alone.
        [Test]
        public void SameSeedSameRandomSequence()
        {
            RunState a = BirthdayController.NewRun(12345), b = BirthdayController.NewRun(12345), c = BirthdayController.NewRun(54321);
            var seqA = Enumerable.Range(0, 50).Select(_ => a.Rng.Next()).ToArray();
            CollectionAssert.AreEqual(seqA, Enumerable.Range(0, 50).Select(_ => b.Rng.Next()).ToArray());
            CollectionAssert.AreNotEqual(seqA, Enumerable.Range(0, 50).Select(_ => c.Rng.Next()).ToArray());
        }

        // Crit rolls use their own seeded stream: reproducible, and rolling them never moves the card/wave stream.
        [Test]
        public void CombatRollsDoNotShiftCardStream()
        {
            RunState a = BirthdayController.NewRun(12345), b = BirthdayController.NewRun(12345);
            for (int i = 0; i < 1000; i++) a.CombatRng.NextDouble();
            CollectionAssert.AreEqual(Enumerable.Range(0, 50).Select(_ => a.Rng.Next()).ToArray(), Enumerable.Range(0, 50).Select(_ => b.Rng.Next()).ToArray());

            RunState c = BirthdayController.NewRun(12345), d = BirthdayController.NewRun(12345);
            CollectionAssert.AreEqual(Enumerable.Range(0, 50).Select(_ => c.CombatRng.Next()).ToArray(), Enumerable.Range(0, 50).Select(_ => d.CombatRng.Next()).ToArray());
        }
    }
}
