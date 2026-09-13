using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Card pool, the draw, and applying the pick.
    public class LevelUpManager : MonoBehaviour
    {
        public static LevelUpManager Instance { get; private set; }

        [SerializeField] UpgradeCard[] pool;
        [SerializeField] int cardsPerOffer = 3;
        [Tooltip("Rare cards join the pool once this year is cleared.")]
        [SerializeField] int rareFromYear = 4;
        [SerializeField] float rareWeight = 0.7f;

        readonly Dictionary<UpgradeCard, int> stacks = new Dictionary<UpgradeCard, int>();

        public UpgradeCard[] CurrentOffer { get; private set; }
        public int CardsTaken { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int Stacks(UpgradeCard card) => stacks.TryGetValue(card, out int n) ? n : 0;

        // Returns false when nothing is left to offer, so the game can skip the screen.
        public bool Offer(int clearedYear)
        {
            var candidates = new List<UpgradeCard>();
            foreach (var card in pool)
            {
                if (card == null || Stacks(card) >= card.maxStacks) continue;
                if (card.rarity == CardRarity.Rare && clearedYear < rareFromYear) continue;
                candidates.Add(card);
            }

            var picks = new List<UpgradeCard>();
            while (picks.Count < cardsPerOffer && candidates.Count > 0)
            {
                float total = 0f;
                foreach (var c in candidates) total += Weight(c);
                float roll = Random.value * total;
                int chosen = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    roll -= Weight(candidates[i]);
                    if (roll <= 0f)
                    {
                        chosen = i;
                        break;
                    }
                }
                picks.Add(candidates[chosen]);
                candidates.RemoveAt(chosen);
            }

            if (picks.Count == 0)
            {
                CurrentOffer = null;
                return false;
            }

            CurrentOffer = picks.ToArray();
            GameEvents.RaiseLevelUpOffered(CurrentOffer);
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

        float Weight(UpgradeCard card) => card.rarity == CardRarity.Rare ? rareWeight : 1f;
    }
}
