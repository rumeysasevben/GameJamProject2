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
        public readonly float Cost;
        public readonly WishGiftTier Tier;
        public readonly bool Legal;
        // Empty when the option is legal.
        public readonly string BlockedReason;

        public WishOption(int candles, float cost, WishGiftTier tier, bool legal, string reason)
        {
            Candles = candles;
            Cost = cost;
            Tier = tier;
            Legal = legal;
            BlockedReason = reason ?? "";
        }
    }

    // The part of CandleClock a wish needs, so the rules run without a scene.
    public interface IWishWallet
    {
        float TimeRemaining { get; }
        bool CanAfford(float seconds);
        bool TrySpend(float seconds, string reason);
    }

    // Spec §6. Shown at each birthday before Stay / Advance: blow out 0, 1, 3 or 5 candles, then pick one of the
    // gifts from that tier's Lifetime pool. GameManager drives it; WishResolved fires once per wish, including 0 candles.
    public sealed class WishSystem
    {
        readonly RunState run;
        readonly List<UpgradeCard> pool = new List<UpgradeCard>();
        readonly IWishWallet wallet;
        readonly Dictionary<UpgradeCard, int> stacks = new Dictionary<UpgradeCard, int>();

        // Candles blown for the wish in progress, and the gifts on offer. Null offer: waiting for a candle count.
        public int PendingCandles { get; private set; }
        public UpgradeCard[] CurrentOffer { get; private set; }
        public WishGiftTier PendingTier => TierFor(PendingCandles);

        public WishSystem(RunState run, IEnumerable<UpgradeCard> cards, IWishWallet wallet)
        {
            this.run = run;
            this.wallet = wallet;
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

        public static float CostFor(int candles) => candles * Balance.SecondsPerCandle;

        public static WishGiftTier TierFor(int candles)
        {
            int index = Array.IndexOf(Balance.WishCandleOptions, candles);
            return index < 0 ? WishGiftTier.None : (WishGiftTier)index;
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

        // Gifts of one tier that can still be wished for.
        public List<UpgradeCard> Available(WishGiftTier tier)
        {
            var list = new List<UpgradeCard>();
            foreach (var card in pool)
                if (card.giftTier == tier && Stacks(card) < card.maxStacks) list.Add(card);
            return list;
        }

        // One entry per Balance.WishCandleOptions. Blowing no candles is always possible.
        public WishOption[] Options()
        {
            var options = new WishOption[Balance.WishCandleOptions.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int candles = Balance.WishCandleOptions[i];
                float cost = CostFor(candles);
                WishGiftTier tier = TierFor(candles);
                string reason = "";
                if (candles > 0 && Available(tier).Count == 0)
                    reason = $"There are no {tier.ToString().ToLowerInvariant()} gifts left to wish for.";
                // CanAfford, like TrySpend, refuses a spend that would put out the last candle (§3.2).
                else if (candles > 0 && (wallet == null || !wallet.CanAfford(cost)))
                    reason = $"You need more than {cost:0} s to blow out {candles} candle{(candles == 1 ? "" : "s")}.";
                options[i] = new WishOption(candles, cost, tier, reason.Length == 0, reason);
            }
            return options;
        }

        public bool IsLegal(int candles)
        {
            foreach (var option in Options())
                if (option.Candles == candles) return option.Legal;
            return false;
        }

        // Step 1. 0 candles resolves the wish at once. Otherwise spends the candles and draws the offer from that
        // tier's pool. Returns false (and changes nothing) for an illegal count.
        public bool Blow(int candles)
        {
            if (CurrentOffer != null || !IsLegal(candles)) return false;
            if (candles == 0)
            {
                Resolve(0, null);
                return true;
            }

            if (!wallet.TrySpend(CostFor(candles), "wish")) return false;
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
            if (run != null) run.WishesMade++;
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

        // Up to WishGiftChoices distinct gifts of one tier, drawn with the run's seeded RNG. With no more candidates
        // than slots, all are offered in pool order and the RNG is not touched.
        UpgradeCard[] Draw(WishGiftTier tier)
        {
            List<UpgradeCard> candidates = Available(tier);
            if (candidates.Count <= Balance.WishGiftChoices) return candidates.ToArray();

            System.Random rng = run != null && run.Rng != null ? run.Rng : LifetimeManager.RunRandom;
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
