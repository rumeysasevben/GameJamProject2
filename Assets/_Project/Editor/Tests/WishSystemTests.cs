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
    // Spec §6 (Phase 3). Window > General > Test Runner > EditMode.
    public class WishSystemTests
    {
        // CandleClock's spending rules without a scene: TrySpend refuses a spend that would put out the last candle.
        sealed class FakeWallet : IWishWallet
        {
            public float TimeRemaining { get; set; }
            public readonly List<(float seconds, string reason)> Spends = new List<(float, string)>();

            public FakeWallet(float time) => TimeRemaining = time;
            public bool CanAfford(float seconds) => TimeRemaining > seconds;

            public bool TrySpend(float seconds, string reason)
            {
                if (!CanAfford(seconds)) return false;
                TimeRemaining -= seconds;
                Spends.Add((seconds, reason));
                return true;
            }
        }

        readonly List<ScriptableObject> created = new List<ScriptableObject>();
        readonly List<(int candles, WishGift gift)> resolved = new List<(int, WishGift)>();
        readonly List<int> blown = new List<int>();

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

        void OnResolved(int candles, WishGift gift) => resolved.Add((candles, gift));
        void OnBlown(int candles) => blown.Add(candles);

        [SetUp]
        public void SetUp()
        {
            StatRegistry.Reset();
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

        [Test]
        public void FourCandleOptionsMatchSpecTable()
        {
            var wish = new WishSystem(BirthdayController.NewRun(0), Pool(), new FakeWallet(300f));
            WishOption[] options = wish.Options();

            CollectionAssert.AreEqual(new[] { 0, 1, 3, 5 }, options.Select(o => o.Candles));
            CollectionAssert.AreEqual(new[] { 0f, 30f, 90f, 150f }, options.Select(o => o.Cost));
            CollectionAssert.AreEqual(new[] { WishGiftTier.None, WishGiftTier.Small, WishGiftTier.Large, WishGiftTier.Legendary }, options.Select(o => o.Tier));
            Assert.IsTrue(options.All(o => o.Legal && o.BlockedReason.Length == 0));
        }

        // Acceptance: all four candle options work. Each spends its cost through TrySpend("wish") and grants a gift
        // from its own tier's pool, at the card's own value.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void EveryCandleOptionWorks(int candles)
        {
            RunState run = BirthdayController.NewRun(0);
            var wallet = new FakeWallet(300f);
            var wish = new WishSystem(run, Pool(), wallet);

            Assert.IsTrue(wish.Blow(candles));
            if (candles == 0)
            {
                Assert.IsEmpty(wallet.Spends, "blowing nothing is free");
                Assert.IsNull(wish.CurrentOffer);
                Assert.AreEqual(0, run.WishesMade);
                return;
            }

            WishGiftTier tier = WishSystem.TierFor(candles);
            CollectionAssert.AreEqual(new[] { (candles * Balance.SecondsPerCandle, "wish") }, wallet.Spends);
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

        // Acceptance: insufficient time disables the option, with a reason, and blowing it changes nothing.
        [Test]
        public void InsufficientTimeDisablesTheOption()
        {
            var wallet = new FakeWallet(100f);
            var wish = new WishSystem(BirthdayController.NewRun(0), Pool(), wallet);
            WishOption[] options = wish.Options();

            CollectionAssert.AreEqual(new[] { true, true, true, false }, options.Select(o => o.Legal));
            StringAssert.Contains("150", options[3].BlockedReason);
            Assert.IsFalse(wish.Blow(5));
            Assert.AreEqual(100f, wallet.TimeRemaining);
            Assert.IsNull(wish.CurrentOffer);
            Assert.IsEmpty(resolved);
            Assert.IsEmpty(blown);

            // §6: you need more than the cost. Spending the last second would end the run, and TrySpend refuses it (§3.2).
            Assert.IsFalse(new WishSystem(null, Pool(), new FakeWallet(90f)).IsLegal(3));
            Assert.IsTrue(new WishSystem(null, Pool(), new FakeWallet(90.01f)).IsLegal(3));
            // Nothing is ever needed to wish for nothing.
            Assert.IsTrue(new WishSystem(null, Pool(), new FakeWallet(0.5f)).IsLegal(0));
        }

        // Acceptance: WishResolved fires in every case, including 0 candles, exactly once per wish.
        [Test]
        public void WishResolvedFiresInEveryCaseIncludingZero()
        {
            foreach (int candles in Balance.WishCandleOptions)
            {
                resolved.Clear();
                var wish = new WishSystem(BirthdayController.NewRun(0), Pool(), new FakeWallet(300f));
                wish.Blow(candles);
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
        // tier, so every offer is the whole tier.
        [Test]
        public void WishGiftPersistsToTheEndOfTheRun()
        {
            var go = new GameObject("Lifetime");
            try
            {
                var lifetime = go.AddComponent<LifetimeManager>();
                lifetime.BeginRun(0);
                var wallet = new FakeWallet(300f);
                var three = new[] { "Lucky Candle", "Golden Luck", "Housewarming" };
                var wish = new WishSystem(lifetime.Run, Pool().Where(c => !three.Contains(c.title)), wallet);

                var wishes = new Queue<(int candles, string gift)>(new[] { (3, "Legacy Flame"), (5, "Lucky Stars"), (1, "Warm Glow") });
                var choices = new Queue<BirthdayChoice>(new[] { BirthdayChoice.Advance, BirthdayChoice.Advance, BirthdayChoice.Stay });
                for (; lifetime.Age <= Balance.TotalYears; lifetime.AdvanceAge())
                {
                    if (!LifetimeManager.IsBirthdayAge(lifetime.Age)) continue;
                    var (candles, wanted) = wishes.Dequeue();
                    wallet.TimeRemaining = 300f;
                    Assert.IsTrue(wish.Blow(candles), "age " + lifetime.Age);
                    Assert.AreEqual(wanted, wish.Choose(System.Array.FindIndex(wish.CurrentOffer, c => c.title == wanted))?.Card.title);
                    Assert.IsTrue(lifetime.ResolveBirthday(choices.Dequeue(), null), "age " + lifetime.Age);
                    Assert.AreEqual(1.45f + (lifetime.Age >= 30 ? 0.25f : 0f), StatRegistry.DamageMultiplier, 1e-4f, "Legacy Flame after the birthday at " + lifetime.Age);
                }

                Assert.AreEqual(1f + 0.45f + 0.25f, StatRegistry.DamageMultiplier, 1e-4f, "Legacy Flame and Warm Glow at age 40");
                Assert.AreEqual(0.5f, StatRegistry.CritChance, 1e-4f, "Lucky Stars from 20");
                Assert.AreEqual(1, StatRegistry.TowerCapacityBonus, "Established from the Stay at 30");
                Assert.AreEqual(3, lifetime.Run.WishesMade);
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
            var wallet = new FakeWallet(10000f);
            var wish = new WishSystem(BirthdayController.NewRun(3), Pool(), wallet);
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

            var wish = new WishSystem(BirthdayController.NewRun(0), pool, new FakeWallet(300f));
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

        // Gift draws use the run's seeded RNG: same seed, same gifts. With no more candidates than slots the RNG is
        // not touched.
        [Test]
        public void GiftDrawIsSeeded()
        {
            RunState c = BirthdayController.NewRun(7), d = BirthdayController.NewRun(7);
            var wc = new WishSystem(c, Pool(), new FakeWallet(300f));
            var wd = new WishSystem(d, Pool(), new FakeWallet(300f));
            wc.Blow(3);
            wd.Blow(3);
            Assert.AreEqual(Balance.WishGiftChoices, wc.CurrentOffer.Length);
            CollectionAssert.AreEqual(wc.CurrentOffer.Select(x => x.title), wd.CurrentOffer.Select(x => x.title), "same seed, same gifts");

            RunState a = BirthdayController.NewRun(12345), b = BirthdayController.NewRun(12345);
            UpgradeCard[] three = Pool().Where(x => x.giftTier != WishGiftTier.Small || x.title != "Warm Glow").ToArray();
            new WishSystem(a, three, new FakeWallet(300f)).Blow(1);
            Assert.AreEqual(b.Rng.Next(), a.Rng.Next(), "3 Small gifts for 3 slots: RNG untouched");
        }
    }
}
