using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TenCandles
{
    // Three gift cards after each year. Runs on unscaled time while the game is paused.
    public class LevelUpUI : MonoBehaviour
    {
        const int Slots = 3;

        class CardView
        {
            public Button button;
            public RectTransform content;
            public Image background;
            public Text rarity;
            public Text title;
            public Text description;
            public Text stacks;
            public CardHover hover;
            public bool legendary;
        }

        RectTransform panel;
        Text heading;
        readonly CardView[] views = new CardView[Slots];
        UpgradeCard[] offer;
        float openedAt;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            panel = UIFactory.Image(root, "LevelUp", skin.overlay).rectTransform;
            panel.Fill();

            heading = UIFactory.Label(panel, "Heading", "", 60, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            heading.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(1400f, 150f));

            for (int i = 0; i < Slots; i++)
            {
                int index = i;
                var v = new CardView();
                v.button = UIFactory.Button(panel, "Card" + i, skin.panelLight, () => Choose(index));
                var rt = v.button.GetComponent<RectTransform>();
                rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 440f, -40f), new Vector2(390f, 540f));
                v.background = (Image)v.button.targetGraphic;
                v.background.color = new Color(0f, 0f, 0f, 0f);

                // The button stays put for stable hover; only the visible card lifts.
                v.content = UIFactory.Panel(rt, "Content", skin.panelLight).rectTransform;
                v.content.Fill();
                v.content.GetComponent<Image>().raycastTarget = false;
                UIFactory.AddOutline(v.content.GetComponent<Image>(), skin.common, 5f);
                var shadow = v.content.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(0f, -10f);

                v.rarity = UIFactory.Label(v.content, "Rarity", "", 22, TextAnchor.MiddleCenter, skin.common, FontStyle.Bold);
                v.rarity.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(340f, 34f));

                v.title = UIFactory.Label(v.content, "Title", "", 40, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
                v.title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(350f, 120f));

                v.description = UIFactory.Label(v.content, "Description", "", 28, TextAnchor.UpperCenter, skin.text);
                v.description.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(330f, 220f));

                v.stacks = UIFactory.Label(v.content, "Stacks", "", 22, TextAnchor.MiddleCenter, skin.mutedText);
                v.stacks.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(340f, 34f));

                var key = UIFactory.Label(v.content, "Key", $"[{i + 1}]", 24, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
                key.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(100f, 34f));

                v.hover = v.button.gameObject.AddComponent<CardHover>();
                v.hover.target = v.content;
                views[i] = v;
            }

            panel.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEvents.LevelUpOffered += OnOffered;
            GameEvents.UpgradeChosen += OnChosen;
        }

        void OnDisable()
        {
            GameEvents.LevelUpOffered -= OnOffered;
            GameEvents.UpgradeChosen -= OnChosen;
        }

        // Close however the pick was made (click, key, or the autoplay bot).
        void OnChosen(UpgradeCard card)
        {
            offer = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        void OnOffered(UpgradeCard[] cards)
        {
            if (panel == null) return;
            offer = cards;
            openedAt = Time.unscaledTime;
            var gm = GameManager.Instance;
            heading.text = gm != null ? $"YOU SURVIVED AGE {gm.Age}!\n<size=34>" + (gm.Age + 2 < gm.FinalAge ? "Choose your gift. The next one comes in two years." : "Choose your last gift. Make it count.") + "</size>" : "LEVEL UP";

            var levelUp = LevelUpManager.Instance;
            for (int i = 0; i < Slots; i++)
            {
                var v = views[i];
                bool has = i < cards.Length;
                v.button.gameObject.SetActive(has);
                if (!has) continue;

                UpgradeCard card = cards[i];
                Color rarityColor = RarityColor(card.rarity);
                v.rarity.text = card.rarity.ToString().ToUpperInvariant();
                v.legendary = card.rarity == CardRarity.Legendary;
                v.rarity.color = rarityColor;
                v.content.GetComponent<Outline>().effectColor = rarityColor;
                v.title.text = card.title;
                v.description.text = card.description;
                int owned = levelUp != null ? levelUp.Stacks(card) : 0;
                v.stacks.text = card.maxStacks > 1 ? $"You own {owned} / {card.maxStacks}" : "One per game";
                v.hover.ResetLift();
            }

            panel.gameObject.SetActive(true);
        }

        Color RarityColor(CardRarity rarity) => rarity switch
        {
            CardRarity.Legendary => skin.legendary,
            CardRarity.Rare => skin.rare,
            _ => skin.common
        };

        void Choose(int index)
        {
            if (offer == null || Time.unscaledTime - openedAt < 0.35f) return; // no accidental double clicks
            offer = null;
            panel.gameObject.SetActive(false);
            LevelUpManager.Instance.Choose(index);
        }

        void Update()
        {
            if (offer == null || panel == null) return;

            for (int i = 0; i < offer.Length && i < Slots; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Choose(i);

            // Legendary cards shimmer so they read as special at a glance.
            float glow = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < offer.Length && i < Slots; i++)
                if (views[i].legendary)
                    views[i].content.GetComponent<Outline>().effectColor = Color.Lerp(skin.legendary, Color.white, glow * 0.6f);

            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.35f);
            for (int i = 0; i < Slots; i++)
            {
                float local = Mathf.Clamp01(t * 1.6f - i * 0.2f);
                views[i].button.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, 1f - (1f - local) * (1f - local));
            }
        }
    }

    // Lifts the card a few pixels on hover.
    public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform target;
        public float lift = 8f;
        bool over;

        public void OnPointerEnter(PointerEventData eventData) => over = true;
        public void OnPointerExit(PointerEventData eventData) => over = false;

        public void ResetLift()
        {
            over = false;
            if (target != null) target.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            if (target == null) return;
            float y = Mathf.Lerp(target.anchoredPosition.y, over ? lift : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 20f));
            target.anchoredPosition = new Vector2(0f, y);
        }
    }
}
