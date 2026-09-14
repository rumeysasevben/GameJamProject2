using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Card pool, the draw, and applying the pick.
    // Gifts come every few years, never repeat inside an offer, never re-show what the last offer had,
    // and each card can only be taken as often as its maxStacks allows.
    public class LevelUpManager : MonoBehaviour
    {
        public static LevelUpManager Instance { get; private set; }

        [SerializeField] UpgradeCard[] pool;
        [SerializeField] int cardsPerOffer = 3;
        [Tooltip("A gift is offered after year 1 and then every this many years (1, 3, 5, 7, 9 with 2).")]
        [SerializeField] int offerEveryYears = 2;

        [Header("Rarity")]
        [SerializeField] int rareFromYear = 3;
        [SerializeField] float rareWeight = 0.55f;
        [SerializeField] int legendaryFromYear = 5;
        [SerializeField] float legendaryWeight = 0.18f;
        [Tooltip("Every offer from this year on holds at least one card of Rare or better.")]
        [SerializeField] int guaranteedRareFromYear = 5;

        readonly Dictionary<UpgradeCard, int> stacks = new Dictionary<UpgradeCard, int>();
        readonly HashSet<UpgradeCard> lastOffer = new HashSet<UpgradeCard>();

        public UpgradeCard[] CurrentOffer { get; private set; }
        public int CardsTaken { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int Stacks(UpgradeCard card) => stacks.TryGetValue(card, out int n) ? n : 0;

        public bool IsOfferYear(int clearedYear) => offerEveryYears <= 1 || (clearedYear - 1) % offerEveryYears == 0;

        // Returns false when there is no gift this year (or nothing left), so the game skips the screen.
        public bool Offer(int clearedYear)
        {
            if (!IsOfferYear(clearedYear))
            {
                CurrentOffer = null;
                return false;
            }

            var fresh = new List<UpgradeCard>();
            var seenLastTime = new List<UpgradeCard>();
            foreach (var card in pool)
            {
                if (card == null || Stacks(card) >= card.maxStacks || !Unlocked(card, clearedYear)) continue;
                if (lastOffer.Contains(card)) seenLastTime.Add(card);
                else fresh.Add(card);
            }

            var picks = new List<UpgradeCard>();
            if (clearedYear >= guaranteedRareFromYear) Draw(fresh, picks, c => c.rarity != CardRarity.Common);
            while (picks.Count < cardsPerOffer && Draw(fresh, picks, null)) { }
            // Only when the pool runs dry do last offer's cards come back.
            while (picks.Count < cardsPerOffer && Draw(seenLastTime, picks, null)) { }

            lastOffer.Clear();
            foreach (var card in picks) lastOffer.Add(card);

            if (picks.Count == 0)
            {
                CurrentOffer = null;
                return false;
            }

            CurrentOffer = picks.ToArray();
            GameEvents.RaiseLevelUpOffered(CurrentOffer);
            return true;
        }

        bool Unlocked(UpgradeCard card, int year) => card.rarity switch
        {
            CardRarity.Rare => year >= rareFromYear,
            CardRarity.Legendary => year >= legendaryFromYear,
            _ => true
        };

        // Weighted pick from candidates (optionally filtered); moves it into picks.
        bool Draw(List<UpgradeCard> candidates, List<UpgradeCard> picks, System.Predicate<UpgradeCard> filter)
        {
            float total = 0f;
            foreach (var c in candidates)
                if (filter == null || filter(c)) total += Weight(c);
            if (total <= 0f) return false;

            float roll = Random.value * total;
            int chosen = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (filter != null && !filter(candidates[i])) continue;
                chosen = i; // the last match also absorbs float rounding at the end of the roll
                roll -= Weight(candidates[i]);
                if (roll <= 0f) break;
            }
            picks.Add(candidates[chosen]);
            candidates.RemoveAt(chosen);
            return true;
        }

        public void Choose(int index)
        {
            if (CurrentOffer == null || index < 0 || index >= CurrentOffer.Length) return;

            UpgradeCard card = CurrentOffer[index];
            CurrentOffer = null;
            stacks[card] = Stacks(card) + 1;
            CardsTaken++;
            UpgradeEffect.Apply(card);
            GameEvents.RaiseUpgradeChosen(card);
        }

        float Weight(UpgradeCard card) => card.rarity switch
        {
            CardRarity.Rare => rareWeight,
            CardRarity.Legendary => legendaryWeight,
            _ => 1f
        };
    }
}
