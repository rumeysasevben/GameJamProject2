using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenCandles.Core;
using TenCandles.Lifetime;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TenCandles.EditorTools.Tests
{
    // Spec §6 (Phase 3, with the candle-cap cost). Window > General > Test Runner > EditMode.
    public class WishSystemTests
    {
        readonly List<ScriptableObject> created = new List<ScriptableObject>();
        readonly List<(int candles, WishGift gift)> resolved = new List<(int, WishGift)>();
        readonly List<int> blown = new List<int>();
        int capChanges;

        UpgradeCard Card(string title, CardRarity rarity, CardEffect effect, float value, WishGiftTier tier = WishGiftTier.None)
        {
            var card = ScriptableObject.CreateInstance<UpgradeCard>();
            card.title = title;
            card.rarity = rarity;
            card.effect = effect;
            card.value = value;
            card.maxStacks = 1;
            card.giftTier = tier;
            created.Add(card);
            return card;
        }

        UpgradeCard Gift(string title, WishGiftTier tier, CardEffect effect, float value) => Card(title, CardRarity.Lifetime, effect, value, tier);

        // Four gifts per tier (one more than an offer, so the draw must choose), plus a common card the wish must ignore.
        UpgradeCard[] Pool() => new[]
        {
            Card("Bonfire", CardRarity.Common, CardEffect.Damage, 0.15f),
            Gift("Warm Glow", WishGiftTier.Small, CardEffect.Damage, 0.25f),
            Gift("Eager Hands", WishGiftTier.Small, CardEffect.FireRate, 0.2f),
            Gift("Clear Sight", WishGiftTier.Small, CardEffect.Range, 0.25f),
            Gift("Lucky Candle", WishGiftTier.Small, CardEffect.Crit, 0.12f),
            Gift("Legacy Flame", WishGiftTier.Large, CardEffect.Damage, 0.45f),
            Gift("Open House", WishGiftTier.Large, CardEffect.TowerCapacityAdd, 1f),
            Gift("Restless Hands", WishGiftTier.Large, CardEffect.FireRate, 0.4f),
            Gift("Golden Luck", WishGiftTier.Large, CardEffect.Crit, 0.25f),
            Gift("True Aim", WishGiftTier.Legendary, CardEffect.ArmorPierce, 1f),
            Gift("Crowded Table", WishGiftTier.Legendary, CardEffect.TowerCapacityAdd, 2f),
            Gift("Lucky Stars", WishGiftTier.Legendary, CardEffect.Crit, 0.5f),
            Gift("Housewarming", WishGiftTier.Legendary, CardEffect.FreeTower, 3f),
        };

        WishSystem Wish(RunState run, IEnumerable<UpgradeCard> pool = null) => new WishSystem(run, pool ?? Pool(), () => capChanges++);

        static RunState RunAtAge(int age, int candlesWished = 0)
        {
            RunState run = BirthdayController.NewRun(0);
            run.CurrentAge = age;
            run.CandlesWished = candlesWished;
            return run;
        }

        void OnResolved(int candles, WishGift gift) => resolved.Add((candles, gift));
        void OnBlown(int candles) => blown.Add(candles);

        [SetUp]
        public void SetUp()
        {
            StatRegistry.Reset();
            capChanges = 0;
            GameEvents.WishResolved += OnResolved;
            GameEvents.WishCandlesBlown += OnBlown;
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.WishResolved -= OnResolved;
            GameEvents.WishCandlesBlown -= OnBlown;
            StatRegistry.Reset();
            foreach (var so in created) Object.DestroyImmediate(so);
            created.Clear();
            resolved.Clear();
            blown.Clear();
        }

        // At age 10 with one bonus candle every option is legal. Each takes its candles off the cap: 11 -> 10 / 8 / 6.
        [Test]
        public void FourCandleOptionsMatchSpecTable()
        {
            StatRegistry.BonusCandles = 1;
            WishOption[] options = Wish(RunAtAge(10)).Options();

            CollectionAssert.AreEqual(new[] { 0, 1, 3, 5 }, options.Select(o => o.Candles));
            CollectionAssert.AreEqual(new[] { WishGiftTier.None, WishGiftTier.Small, WishGiftTier.Large, WishGiftTier.Legendary }, options.Select(o => o.Tier));
            CollectionAssert.AreEqual(new[] { 11, 10, 8, 6 }, options.Select(o => o.CapAfter));
            CollectionAssert.AreEqual(new[] { 8, 7, 5, 3 }, options.Select(o => o.LowestCapAfter), "Old Age's 7 + 1 bonus, less the candles");
            Assert.IsTrue(options.All(o => o.Legal && o.BlockedReason.Length == 0));
        }

        // Acceptance: all four candle options work. Each lowers the cap for good (never spends current time) and grants
        // a gift from its own tier's pool, at the card's own value.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void EveryCandleOptionWorks(int candles)
        {
            StatRegistry.BonusCandles = 1;
            RunState run = RunAtAge(10);
            var wish = Wish(run);

            Assert.IsTrue(wish.Blow(candles));
            Assert.AreEqual(candles, run.CandlesWished);
            if (candles == 0)
            {
                Assert.AreEqual(0, capChanges, "blowing nothing leaves the cap alone");
                Assert.IsNull(wish.CurrentOffer);
                Assert.AreEqual(0, run.WishesMade);
                return;
            }

            WishGiftTier tier = WishSystem.TierFor(candles);
            Assert.AreEqual(1, capChanges);
            Assert.AreEqual(11 - candles, LifetimeManager.RunningCandleCap(run.CurrentTier, StatRegistry.BonusCandles, run.CandlesWished));
            CollectionAssert.AreEqual(new[] { candles }, blown);
            Assert.AreEqual(Balance.WishGiftChoices, wish.CurrentOffer.Length);
            Assert.IsTrue(wish.CurrentOffer.All(c => c.rarity == CardRarity.Lifetime && c.giftTier == tier), "gifts come from the blown tier's pool only");
            CollectionAssert.AllItemsAreUnique(wish.CurrentOffer);

            UpgradeCard card = wish.CurrentOffer[0];
            WishGift gift = wish.Choose(0);
            Assert.AreEqual(tier, gift.Tier);
            Assert.AreSame(card, gift.Card);
            Assert.AreEqual(1, run.WishesMade);
            Assert.IsNull(wish.CurrentOffer);
        }

        // §6: a wish may never leave the cap below WishMinCandleCap (3), at this tier or any later one. Tier caps shrink
        // with age, so the check looks ahead to Old Age's 7. An illegal option is disabled with a reason and changes nothing.
        [Test]
        public void AWishCanNeverBreakTheCandleFloor()
        {
            RunState run = RunAtAge(10);
            var wish = Wish(run);
            WishOption[] options = wish.Options();

            CollectionAssert.AreEqual(new[] { true, true, true, false }, options.Select(o => o.Legal), "7 - 5 = 2 in Old Age");
            Assert.AreEqual(5, options[3].CapAfter, "the cap right after would still be 5; the later tiers decide");
            StringAssert.Contains("at least 3", options[3].BlockedReason);
            Assert.IsFalse(wish.Blow(5));
            Assert.AreEqual(0, run.CandlesWished);
            Assert.AreEqual(0, capChanges);
            Assert.IsNull(wish.CurrentOffer);
            Assert.IsEmpty(resolved);
            Assert.IsEmpty(blown);

            // Earlier wishes count: 3 already gone leaves room for exactly one more candle.
            CollectionAssert.AreEqual(new[] { true, true, false, false }, Wish(RunAtAge(20, 3)).Options().Select(o => o.Legal));
            CollectionAssert.AreEqual(new[] { true, false, false, false }, Wish(RunAtAge(30, 4)).Options().Select(o => o.Legal));
            // Bonus candles (cards, Long Summer) make room.
            StatRegistry.BonusCandles = 3;
            CollectionAssert.AreEqual(new[] { true, true, true, false }, Wish(RunAtAge(30, 4)).Options().Select(o => o.Legal), "7 + 3 - 4 = 6: room for 3, not 5");
            // Wishing for nothing is always possible.
            StatRegistry.BonusCandles = 0;
            Assert.IsTrue(Wish(RunAtAge(30, 4)).IsLegal(0));
            // The formula's own floor, for a cap that falls for any other reason.
            Assert.AreEqual(Balance.WishMinCandleCap, LifetimeManager.RunningCandleCap(3, 0, 6));
        }

        // The cost lands on the real clock: the cap drops by N for the rest of the run, time above the new cap is clamped
        // off (§3.2), and each later tier's cap is N lower too. A later MaxCandlesAdd still adds on top.
        [Test]
        public void BlownCandlesComeOffTheCapForTheRestOfTheRun()
        {
            var clockGo = new GameObject("Clock");
            var lifetimeGo = new GameObject("Lifetime");
            try
            {
                var clock = clockGo.AddComponent<CandleClock>();
                typeof(CandleClock).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(clock, null);
                var lifetime = lifetimeGo.AddComponent<LifetimeManager>();
                lifetime.BeginRun(0);
                for (int age = 1; age < 10; age++) lifetime.AdvanceAge();
                var wish = new WishSystem(lifetime.Run, Pool(), lifetime.RefreshCandleCap);

                Assert.AreEqual(10, clock.CandleCap);
                Assert.AreEqual(300f, clock.TimeRemaining);
                Assert.IsTrue(wish.Blow(3));
                Assert.AreEqual(7, clock.CandleCap);
                Assert.AreEqual(210f, clock.TimeRemaining, 1e-4f, "time above the new cap is clamped off");
                wish.Choose(0);

                var caps = new List<int> { clock.CandleCap };
                for (int birthday = 0; birthday < 3; birthday++)
                {
                    Assert.IsTrue(lifetime.ResolveBirthday(BirthdayChoice.Advance, null));
                    for (int i = 0; i < Balance.YearsPerDecade; i++) lifetime.AdvanceAge();
                    caps.Add(clock.CandleCap);
                }
                CollectionAssert.AreEqual(new[] { 7, 7, 6, 4 }, caps, "tier caps 10 / 10 / 9 / 7, each 3 lower");

                UpgradeEffect.Apply(CardEffect.ExtraCandle, 1f);
                Assert.AreEqual(5, clock.CandleCap, "a bonus candle still adds on top");
            }
            finally
            {
                typeof(CandleClock).GetProperty("Instance").SetValue(null, null);
                typeof(LifetimeManager).GetProperty("Instance").SetValue(null, null);
                Object.DestroyImmediate(clockGo);
                Object.DestroyImmediate(lifetimeGo);
            }
        }

        // Acceptance: WishResolved fires in every case, including 0 candles, exactly once per wish.
        [Test]
        public void WishResolvedFiresInEveryCaseIncludingZero()
        {
            StatRegistry.BonusCandles = 1;
            foreach (int candles in Balance.WishCandleOptions)
            {
                resolved.Clear();
                var wish = Wish(RunAtAge(10));
                Assert.IsTrue(wish.Blow(candles), candles + " candles");
                if (candles > 0)
                {
                    Assert.IsEmpty(resolved, "not before the gift is picked");
                    wish.Choose(1);
                }

                Assert.AreEqual(1, resolved.Count, candles + " candles");
                Assert.AreEqual(candles, resolved[0].candles);
                Assert.AreEqual(candles == 0, resolved[0].gift == null, candles + " candles");
            }
        }

        // Acceptance: a wish gift persists to the end of the run. Walk a whole A A S life through the real birthday
        // code: Legacy Flame (Large) at 10, Lucky Stars (Legendary) at 20, Warm Glow (Small) at 30. Three gifts per
        // tier, so every offer is the whole tier. Nine candles in all need six bonus candles to stay above the floor.
        [Test]
        public void WishGiftPersistsToTheEndOfTheRun()
        {
            var go = new GameObject("Lifetime");
            try
            {
                var lifetime = go.AddComponent<LifetimeManager>();
                lifetime.BeginRun(0);
                StatRegistry.BonusCandles = 6;
                var three = new[] { "Lucky Candle", "Golden Luck", "Housewarming" };
                var wish = Wish(lifetime.Run, Pool().Where(c => !three.Contains(c.title)));

                var wishes = new Queue<(int candles, string gift)>(new[] { (3, "Legacy Flame"), (5, "Lucky Stars"), (1, "Warm Glow") });
                var choices = new Queue<BirthdayChoice>(new[] { BirthdayChoice.Advance, BirthdayChoice.Advance, BirthdayChoice.Stay });
                for (; lifetime.Age <= Balance.TotalYears; lifetime.AdvanceAge())
                {
                    if (!LifetimeManager.IsBirthdayAge(lifetime.Age)) continue;
                    var (candles, wanted) = wishes.Dequeue();
                    Assert.IsTrue(wish.Blow(candles), "age " + lifetime.Age);
                    Assert.AreEqual(wanted, wish.Choose(System.Array.FindIndex(wish.CurrentOffer, c => c.title == wanted))?.Card.title);
                    Assert.IsTrue(lifetime.ResolveBirthday(choices.Dequeue(), null), "age " + lifetime.Age);
                    Assert.AreEqual(1.45f + (lifetime.Age >= 30 ? 0.25f : 0f), StatRegistry.DamageMultiplier, 1e-4f, "Legacy Flame after the birthday at " + lifetime.Age);
                }

                Assert.AreEqual(1f + 0.45f + 0.25f, StatRegistry.DamageMultiplier, 1e-4f, "Legacy Flame and Warm Glow at age 40");
                Assert.AreEqual(0.5f, StatRegistry.CritChance, 1e-4f, "Lucky Stars from 20");
                Assert.AreEqual(1, StatRegistry.TowerCapacityBonus, "Established from the Stay at 30");
                Assert.AreEqual(3, lifetime.Run.WishesMade);
                Assert.AreEqual(9, lifetime.Run.CandlesWished);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // §6: tiers are separate pools. Taken gifts (maxStacks 1) never come back, and an empty tier disables only
        // its own option.
        [Test]
        public void TiersAreSeparatePoolsThatEmptyOnTheirOwn()
        {
            StatRegistry.BonusCandles = 100;
            var wish = Wish(RunAtAge(10));
            var taken = new List<UpgradeCard>();
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(wish.Blow(1), "wish " + i);
                Assert.IsTrue(wish.CurrentOffer.All(c => c.giftTier == WishGiftTier.Small));
                CollectionAssert.IsEmpty(wish.CurrentOffer.Intersect(taken), "wish " + i);
                Assert.AreEqual(Mathf.Min(3, 4 - i), wish.CurrentOffer.Length);
                taken.Add(wish.Choose(0).Card);
            }

            WishOption[] options = wish.Options();
            CollectionAssert.AreEqual(new[] { true, false, true, true }, options.Select(o => o.Legal));
            StringAssert.Contains("no small gifts left", options[1].BlockedReason);
            Assert.AreEqual(4, wish.Available(WishGiftTier.Large).Count);
        }

        // Correction 2: a gift never duplicates an effect the player already has at max. True Aim (ignore armour) is
        // not offered after the Armor Breaker card, and the yearly draw applies the same rule the other way round.
        [Test]
        public void AGiftNeverDuplicatesAMaxedEffect()
        {
            StatRegistry.BonusCandles = 1;
            var wish = Wish(RunAtAge(10));
            Assert.IsTrue(wish.Available(WishGiftTier.Legendary).Any(c => c.title == "True Aim"), "offered while armour still counts");

            UpgradeCard armorBreaker = Card("Armor Breaker", CardRarity.Rare, CardEffect.ArmorPierce, 1f);
            UpgradeEffect.Apply(armorBreaker);
            CollectionAssert.AreEquivalent(new[] { "Crowded Table", "Lucky Stars", "Housewarming" }, wish.Available(WishGiftTier.Legendary).Select(c => c.title));
            Assert.IsTrue(wish.Blow(5));
            Assert.IsFalse(wish.CurrentOffer.Any(c => c.title == "True Aim"));

            // The shared rule, for every switch-like or floored effect.
            Assert.IsFalse(UpgradeEffect.CanOffer(armorBreaker, 0), "same effect, already on");
            StatRegistry.HitSlowPercent = 0.3f;
            Assert.IsTrue(UpgradeEffect.IsMaxed(CardEffect.HitSlow, 0.2f));
            Assert.IsFalse(UpgradeEffect.IsMaxed(CardEffect.HitSlow, 0.4f), "a stronger slow still changes something");
            StatRegistry.BuildCostMultiplier = Balance.MinCostMultiplier;
            Assert.IsTrue(UpgradeEffect.IsMaxed(CardEffect.BuildCost, 0.2f));
            Assert.IsFalse(UpgradeEffect.IsMaxed(CardEffect.Damage, 0.2f), "stacking multipliers are never maxed");

            // Yearly draw: once True Aim is taken, Armor Breaker never shows up in an offer.
            var go = new GameObject("LevelUp");
            try
            {
                StatRegistry.Reset();
                var levelUp = go.AddComponent<LevelUpManager>();
                UpgradeCard[] pool = { armorBreaker, Card("Quick Hands", CardRarity.Common, CardEffect.FireRate, 0.12f),
                    Card("Long Fuse", CardRarity.Common, CardEffect.Range, 0.15f), Card("Bonfire", CardRarity.Common, CardEffect.Damage, 0.15f) };
                typeof(LevelUpManager).GetField("pool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(levelUp, pool);
                bool seenBefore = false;
                for (int year = 5; year < 20; year += 2)
                    seenBefore |= levelUp.Offer(year) && levelUp.CurrentOffer.Contains(armorBreaker);
                Assert.IsTrue(seenBefore, "Armor Breaker is offered before True Aim");

                UpgradeEffect.Apply(Pool().First(c => c.title == "True Aim"));
                for (int year = 21; year < Balance.TotalYears; year += 2)
                    if (levelUp.Offer(year))
                        CollectionAssert.DoesNotContain(levelUp.CurrentOffer, armorBreaker, "year " + year);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // §6 invariant: no wish gift may grant time or candle capacity. A Lifetime card that would is left out of
        // the pool (with a warning), and so is one with no tier.
        [Test]
        public void TimeGrantingOrUntieredCardsNeverBecomeGifts()
        {
            UpgradeCard[] pool = Pool().Concat(new[]
            {
                Gift("Second Wind", WishGiftTier.Large, CardEffect.ExtraCandle, 2f),
                Gift("Endless Party", WishGiftTier.Large, CardEffect.KillBonus, 1.2f),
                Gift("No Tier", WishGiftTier.None, CardEffect.Damage, 0.3f),
            }).ToArray();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Second Wind"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Endless Party"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No Tier"));

            var wish = Wish(RunAtAge(10), pool);
            CollectionAssert.AreEquivalent(new[] { "Legacy Flame", "Open House", "Restless Hands", "Golden Luck" }, wish.Available(WishGiftTier.Large).Select(c => c.title));

            foreach (CardEffect e in new[] { CardEffect.ExtraCandle, CardEffect.InstantTime, CardEffect.KillBonus, CardEffect.KillRewardMul,
                         CardEffect.WaveEndBonus, CardEffect.LastBreath, CardEffect.SlowBurn, CardEffect.LeakShield })
                Assert.IsTrue(WishSystem.GrantsTime(e), e.ToString());
        }

        // The shipped gift data (§6 table): five per tier, no time or candles, one per run each.
        [Test]
        public void ShippedGiftsAreFivePerTierAndNeverGrantTime()
        {
            UpgradeCard[] gifts = AssetDatabase.FindAssets("t:UpgradeCard", new[] { "Assets/_Project/Data/Cards" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<UpgradeCard>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(c => c != null && c.rarity == CardRarity.Lifetime).ToArray();

            foreach (WishGiftTier tier in new[] { WishGiftTier.Small, WishGiftTier.Large, WishGiftTier.Legendary })
                Assert.AreEqual(5, gifts.Count(c => c.giftTier == tier), tier.ToString());
            foreach (var gift in gifts)
            {
                Assert.AreNotEqual(WishGiftTier.None, gift.giftTier, gift.title);
                Assert.IsFalse(WishSystem.GrantsTime(gift.effect), $"{gift.title} ({gift.effect}) grants time");
            }
        }

        // §11.1: MaxCandlesAdd raises the cap but never fills it. The new candle is unlit until the seconds are earned.
        [Test]
        public void MaxCandlesAddLeavesTheNewCandlesUnlit()
        {
            var go = new GameObject("Clock");
            try
            {
                var clock = go.AddComponent<CandleClock>();
                typeof(CandleClock).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(clock, null);
                float before = clock.TimeRemaining;
                int lit = clock.LitCandles;

                UpgradeEffect.Apply(CardEffect.ExtraCandle, 2f);

                Assert.AreEqual(2, StatRegistry.BonusCandles);
                Assert.AreEqual(Balance.BaseCandleCount + 2, clock.CandleCap);
                Assert.AreEqual((Balance.BaseCandleCount + 2) * Balance.SecondsPerCandle, clock.MaxTime);
                Assert.AreEqual(before, clock.TimeRemaining, "raising the cap adds no time");
                Assert.AreEqual(lit, clock.LitCandles, "the new candles start unlit");
                Assert.AreEqual(0f, clock.CandleFill(clock.CandleCap - 1));

                clock.Add(2 * Balance.SecondsPerCandle);
                Assert.AreEqual(clock.MaxTime, clock.TimeRemaining, 1e-4f, "earned seconds can now fill them");
            }
            finally
            {
                typeof(CandleClock).GetProperty("Instance").SetValue(null, null);
                Object.DestroyImmediate(go);
            }
        }

        // Lifetime cards are wishes only: the yearly card offer never shows one (§11.3).
        [Test]
        public void LifetimeCardsAreNeverInCardOffers()
        {
            var go = new GameObject("LevelUp");
            try
            {
                var levelUp = go.AddComponent<LevelUpManager>();
                UpgradeCard[] pool = Pool();
                typeof(LevelUpManager).GetField("pool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(levelUp, pool);
                for (int year = 1; year < Balance.TotalYears; year += 2)
                    if (levelUp.Offer(year))
                        Assert.IsTrue(levelUp.CurrentOffer.All(c => c.rarity != CardRarity.Lifetime), "year " + year);
                Assert.AreEqual(12, levelUp.Pool.Count(c => c.rarity == CardRarity.Lifetime));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Correction 3: gift draws use their own seeded stream, RunState.WishRng. Same seed, same gifts; and a wish
        // never advances RunState.Rng, so card offers and wave shuffles are the same whatever the player wishes for.
        [Test]
        public void GiftDrawIsSeededOnItsOwnStream()
        {
            StatRegistry.BonusCandles = 1;
            RunState c = BirthdayController.NewRun(7), d = BirthdayController.NewRun(7);
            c.CurrentAge = d.CurrentAge = 10;
            var wc = Wish(c);
            var wd = Wish(d);
            wc.Blow(3);
            wd.Blow(3);
            Assert.AreEqual(Balance.WishGiftChoices, wc.CurrentOffer.Length);
            CollectionAssert.AreEqual(wc.CurrentOffer.Select(x => x.title), wd.CurrentOffer.Select(x => x.title), "same seed, same gifts");

            RunState a = BirthdayController.NewRun(12345), b = BirthdayController.NewRun(12345);
            a.CurrentAge = b.CurrentAge = 10;
            Assert.IsTrue(Wish(a).Blow(5), "4 Legendary gifts for 3 slots: a real draw");
            Assert.AreEqual(b.Rng.Next(), a.Rng.Next(), "the card and wave stream is untouched");
            Assert.AreEqual(b.CombatRng.Next(), a.CombatRng.Next(), "so is the combat stream");
            Assert.AreNotEqual(b.WishRng.Next(), a.WishRng.Next(), "the draw used the wish stream");
        }
    }
}
