using System;
using System.Collections.Generic;
using TenCandles.Core;

namespace TenCandles.Lifetime
{
    public enum WishGiftTier { None, Small, Large, Legendary }

    // What a wish granted: a Lifetime card from its tier's own pool, applied at the card's value.
    public sealed class WishGift
    {
        public readonly UpgradeCard Card;
        public readonly WishGiftTier Tier;

        public WishGift(UpgradeCard card, WishGiftTier tier)
        {
            Card = card;
            Tier = tier;
        }
    }

    public readonly struct WishOption
    {
        public readonly int Candles;
        public readonly WishGiftTier Tier;
        // The candle cap right after this wish, and the lowest it would reach at any tier left in the run.
        public readonly int CapAfter;
        public readonly int LowestCapAfter;
        public readonly bool Legal;
        // Empty when the option is legal.
        public readonly string BlockedReason;

        public WishOption(int candles, WishGiftTier tier, int capAfter, int lowestCapAfter, bool legal, string reason)
        {
            Candles = candles;
            Tier = tier;
            CapAfter = capAfter;
            LowestCapAfter = lowestCapAfter;
            Legal = legal;
            BlockedReason = reason ?? "";
        }
    }

    // Spec §6. Shown at each birthday before Stay / Advance: blow out 0, 1, 3 or 5 candles, then pick one of the
    // gifts from that tier's Lifetime pool. Each candle blown comes off the candle cap for the rest of the run.
    // GameManager drives it; WishResolved fires once per wish, including 0 candles.
    public sealed class WishSystem
    {
        readonly RunState run;
        readonly List<UpgradeCard> pool = new List<UpgradeCard>();
        // Pushes the new cap to the clock (LifetimeManager.RefreshCandleCap). Null in tests.
        readonly Action capChanged;
        readonly Dictionary<UpgradeCard, int> stacks = new Dictionary<UpgradeCard, int>();

        // Candles blown for the wish in progress, and the gifts on offer. Null offer: waiting for a candle count.
        public int PendingCandles { get; private set; }
        public UpgradeCard[] CurrentOffer { get; private set; }
        public WishGiftTier PendingTier => TierFor(PendingCandles);

        public WishSystem(RunState run, IEnumerable<UpgradeCard> cards, Action capChanged = null)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.capChanged = capChanged;
            if (cards == null) return;

            foreach (var card in cards)
            {
                if (card == null || card.rarity != CardRarity.Lifetime) continue;
                // §6 invariant: a wish trades lifespan for power, so a gift that gives time back is never offered.
                if (GrantsTime(card.effect))
                    UnityEngine.Debug.LogWarning($"Wish: '{card.title}' ({card.effect}) grants time or candles and is left out of the gift pool (§6).");
                else if (card.giftTier == WishGiftTier.None)
                    UnityEngine.Debug.LogWarning($"Wish: '{card.title}' has no gift tier and is left out of the gift pool (§6).");
                else pool.Add(card);
            }
        }

        public static WishGiftTier TierFor(int candles)
        {
            int index = Array.IndexOf(Balance.WishCandleOptions, candles);
            return index < 0 ? WishGiftTier.None : (WishGiftTier)index;
        }

        // The cap at `tier` once `candlesWished` candles are gone, before the WishMinCandleCap safety floor.
        static int RawCap(int tier, int candlesWished) => LifetimeManager.CandleCapFor(tier) + StatRegistry.BonusCandles - candlesWished;

        // The lowest the cap would be, from the current tier to the end of the run, after blowing `candles` more.
        // Tier caps shrink with age (10, 10, 9, 7), so a wish that is safe today can break the floor in Old Age.
        public int LowestCapAfter(int candles)
        {
            int lowest = int.MaxValue;
            for (int tier = run.CurrentTier; tier < Balance.DecadesPerRun; tier++)
                lowest = Math.Min(lowest, RawCap(tier, run.CandlesWished + candles));
            return lowest;
        }

        // §6: effects that put seconds in the wallet, raise the candle cap, or slow the loss of time.
        // No wish gift may use one.
        public static bool GrantsTime(CardEffect effect) => effect switch
        {
            CardEffect.ExtraCandle or CardEffect.InstantTime or CardEffect.KillBonus or CardEffect.KillRewardMul or
                CardEffect.WaveEndBonus or CardEffect.LastBreath or CardEffect.SlowBurn or CardEffect.LeakShield => true,
            _ => false
        };

        public int Stacks(UpgradeCard card) => stacks.TryGetValue(card, out int n) ? n : 0;

        // Gifts of one tier that can still be wished for. Same exclusion rule as the yearly card draw: not at
        // maxStacks, and the effect is not already maxed by a card (True Aim after Armor Breaker).
        public List<UpgradeCard> Available(WishGiftTier tier)
        {
            var list = new List<UpgradeCard>();
            foreach (var card in pool)
                if (card.giftTier == tier && UpgradeEffect.CanOffer(card, Stacks(card))) list.Add(card);
            return list;
        }

        // One entry per Balance.WishCandleOptions. Blowing no candles is always possible.
        public WishOption[] Options()
        {
            var options = new WishOption[Balance.WishCandleOptions.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int candles = Balance.WishCandleOptions[i];
                WishGiftTier tier = TierFor(candles);
                int capAfter = RawCap(run.CurrentTier, run.CandlesWished + candles);
                int lowest = LowestCapAfter(candles);
                string reason = "";
                if (candles > 0 && Available(tier).Count == 0)
                    reason = $"There are no {tier.ToString().ToLowerInvariant()} gifts left to wish for.";
                // A wish can never end the run: the cap stays at WishMinCandleCap or more, now and in every later decade.
                else if (candles > 0 && lowest < Balance.WishMinCandleCap)
                    reason = $"That would leave only {Math.Max(0, lowest)} candle{(lowest == 1 ? "" : "s")} later in life. You must keep at least {Balance.WishMinCandleCap}.";
                options[i] = new WishOption(candles, tier, capAfter, lowest, reason.Length == 0, reason);
            }
            return options;
        }

        public bool IsLegal(int candles)
        {
            foreach (var option in Options())
                if (option.Candles == candles) return option.Legal;
            return false;
        }

        // Step 1. 0 candles resolves the wish at once. Otherwise takes the candles off the cap for good and draws
        // the offer from that tier's pool. Returns false (and changes nothing) for an illegal count.
        public bool Blow(int candles)
        {
            if (CurrentOffer != null || !IsLegal(candles)) return false;
            if (candles == 0)
            {
                Resolve(0, null);
                return true;
            }

            run.CandlesWished += candles;
            capChanged?.Invoke();
            PendingCandles = candles;
            CurrentOffer = Draw(PendingTier);
            GameEvents.RaiseWishCandlesBlown(candles);
            return true;
        }

        // Step 2. Applies the chosen gift for the rest of the run and resolves the wish.
        public WishGift Choose(int index)
        {
            if (CurrentOffer == null || index < 0 || index >= CurrentOffer.Length) return null;

            UpgradeCard card = CurrentOffer[index];
            var gift = new WishGift(card, PendingTier);
            stacks[card] = Stacks(card) + 1;
            run.WishesMade++;
            // Same pipeline as cards and passives: StatRegistry, never reset during a run.
            UpgradeEffect.Apply(card);
            Resolve(PendingCandles, gift);
            return gift;
        }

        void Resolve(int candles, WishGift gift)
        {
            PendingCandles = 0;
            CurrentOffer = null;
            GameEvents.RaiseWishResolved(candles, gift);
        }

        // Up to WishGiftChoices distinct gifts of one tier, drawn with the run's wish stream (RunState.WishRng), never
        // the card and wave stream. With no more candidates than slots, all are offered in pool order.
        UpgradeCard[] Draw(WishGiftTier tier)
        {
            List<UpgradeCard> candidates = Available(tier);
            if (candidates.Count <= Balance.WishGiftChoices) return candidates.ToArray();

            System.Random rng = run.WishRng ?? LifetimeManager.RunRandom;
            var picks = new List<UpgradeCard>();
            while (picks.Count < Balance.WishGiftChoices)
            {
                int i = rng.Next(candidates.Count);
                picks.Add(candidates[i]);
                candidates.RemoveAt(i);
            }
            return picks.ToArray();
        }
    }
}
