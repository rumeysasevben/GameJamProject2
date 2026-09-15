using System.Collections.Generic;
using TenCandles.Core;
using TenCandles.Lifetime;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Shown at ages 10, 20 and 30, before the birthday (spec §6). Pick how many candles to blow out, watch them go,
    // then pick the gift. The game is paused here, so every effect runs on unscaled time inside this canvas.
    public class WishUI : MonoBehaviour
    {
        const float InputDelay = 0.5f;
        const float CandleStagger = 0.14f;
        const float GiftsAfter = 0.5f;          // seconds after the last candle goes out
        const float OutroSeconds = 0.9f;
        const int MaxParticles = 160;
        const int MaxCandleIcons = 5;

        class OptionView
        {
            public WishOption option;
            public Button button;
            public Image background;
            public Text title, cost, tier, body;
            public Image[] wax = new Image[MaxCandleIcons];
            public Image[] flames = new Image[MaxCandleIcons];
        }

        class GiftView
        {
            public Button button;
            public RectTransform content;
            public Text tier, title, body;
            public CardHover hover;
        }

        class Particle
        {
            public Image image;
            public Vector2 velocity;
            public float life, age, gravity, grow;
            public Color color;
        }

        enum Step { Hidden, Candles, Blowing, Gifts, Outro }

        CanvasGroup group;
        RectTransform panel, shakeRoot, particleRoot;
        Text title, subtitle, wallet;
        Image flash;
        readonly OptionView[] options = new OptionView[4];
        readonly GiftView[] gifts = new GiftView[Balance.WishGiftChoices];
        readonly List<Particle> particles = new List<Particle>();
        UISkin skin;

        Step step;
        float stepAt, shake, flashAlpha;
        OptionView blowing;
        int candlesPuffed;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            panel = UIFactory.Image(root, "Wish", skin.overlay).rectTransform;
            panel.Fill();
            group = panel.gameObject.AddComponent<CanvasGroup>();

            shakeRoot = UIFactory.Rect("Shake", panel);
            shakeRoot.Fill();

            title = UIFactory.Label(shakeRoot, "Title", "MAKE A WISH", 100, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 380f), new Vector2(1700f, 150f));
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.8f), 5f);

            subtitle = UIFactory.Label(shakeRoot, "Subtitle", "", 40, TextAnchor.MiddleCenter, skin.text);
            subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 280f), new Vector2(1700f, 60f));

            wallet = UIFactory.Label(shakeRoot, "Wallet", "", 30, TextAnchor.MiddleCenter, skin.mutedText);
            wallet.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 215f), new Vector2(1600f, 50f));

            for (int i = 0; i < options.Length; i++) options[i] = BuildOption(i);
            for (int i = 0; i < gifts.Length; i++) gifts[i] = BuildGift(i);

            particleRoot = UIFactory.Rect("Particles", panel);
            particleRoot.Fill();
            for (int i = 0; i < MaxParticles; i++)
            {
                var img = UIFactory.Image(particleRoot, "Particle", Color.clear, skin.soft);
                img.raycastTarget = false;
                img.gameObject.SetActive(false);
                particles.Add(new Particle { image = img });
            }

            flash = UIFactory.Image(panel, "Flash", new Color(1f, 0.93f, 0.75f, 0f));
            flash.rectTransform.Fill();
            flash.raycastTarget = false;

            panel.gameObject.SetActive(false);
        }

        OptionView BuildOption(int i)
        {
            var v = new OptionView();
            v.button = UIFactory.Button(shakeRoot, "Option" + i, skin.panelLight, () => Blow(v));
            v.button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 430f, -120f), new Vector2(400f, 520f));
            v.background = (Image)v.button.targetGraphic;
            UIFactory.AddOutline(v.background, skin.accent, 4f);
            var rt = v.button.transform;

            // A little row of cake candles: the ones this option blows out.
            for (int c = 0; c < MaxCandleIcons; c++)
            {
                float x = (c - (MaxCandleIcons - 1) * 0.5f) * 52f;
                v.wax[c] = UIFactory.Image(rt, "Wax" + c, skin.waxColors != null && skin.waxColors.Length > 0 ? skin.waxColors[0] : Color.white, skin.candle);
                v.wax[c].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(x, -170f), new Vector2(30f, 90f));
                v.wax[c].preserveAspect = true;
                v.wax[c].raycastTarget = false;
                v.flames[c] = UIFactory.Image(rt, "Flame" + c, skin.flameColor, skin.flame);
                v.flames[c].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(x, -84f), new Vector2(30f, 44f));
                v.flames[c].preserveAspect = true;
                v.flames[c].raycastTarget = false;
            }

            v.title = UIFactory.Label(rt, "Title", "", 44, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            v.title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(380f, 60f));
            v.cost = UIFactory.Label(rt, "Cost", "", 34, TextAnchor.MiddleCenter, skin.danger, FontStyle.Bold);
            v.cost.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(380f, 46f));
            v.tier = UIFactory.Label(rt, "Tier", "", 28, TextAnchor.MiddleCenter, skin.epic, FontStyle.Bold);
            v.tier.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -298f), new Vector2(380f, 40f));
            v.body = UIFactory.Label(rt, "Body", "", 24, TextAnchor.UpperCenter, skin.text);
            v.body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(360f, 130f));

            var key = UIFactory.Label(rt, "Key", $"[{i + 1}]", 24, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            key.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(100f, 34f));
            return v;
        }

        GiftView BuildGift(int i)
        {
            var v = new GiftView();
            int index = i;
            v.button = UIFactory.Button(shakeRoot, "Gift" + i, new Color(0f, 0f, 0f, 0f), () => ChooseGift(index));
            var rt = v.button.GetComponent<RectTransform>();
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 440f, -120f), new Vector2(390f, 520f));

            v.content = UIFactory.Panel(rt, "Content", skin.panelLight).rectTransform;
            v.content.Fill();
            v.content.GetComponent<Image>().raycastTarget = false;
            UIFactory.AddOutline(v.content.GetComponent<Image>(), skin.epic, 5f);

            v.tier = UIFactory.Label(v.content, "Tier", "", 24, TextAnchor.MiddleCenter, skin.epic, FontStyle.Bold);
            v.tier.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(360f, 36f));
            v.title = UIFactory.Label(v.content, "Title", "", 42, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
            v.title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(360f, 110f));
            v.body = UIFactory.Label(v.content, "Body", "", 28, TextAnchor.UpperCenter, skin.text);
            v.body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(340f, 220f));
            var lasts = UIFactory.Label(v.content, "Lasts", "Yours for the rest of this life", 22, TextAnchor.MiddleCenter, skin.good);
            lasts.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(360f, 34f));
            var key = UIFactory.Label(v.content, "Key", $"[{i + 1}]", 24, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            key.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(100f, 34f));

            v.hover = v.button.gameObject.AddComponent<CardHover>();
            v.hover.target = v.content;
            return v;
        }

        void OnEnable()
        {
            GameEvents.StateChanged += OnState;
            GameEvents.WishCandlesBlown += OnBlown;
            GameEvents.WishResolved += OnResolved;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnState;
            GameEvents.WishCandlesBlown -= OnBlown;
            GameEvents.WishResolved -= OnResolved;
        }

        static WishSystem Wish => LifetimeManager.Instance != null ? LifetimeManager.Instance.Wish : null;

        void OnState(GameState state)
        {
            if (panel == null) return;
            if (state == GameState.Wish) Open();
            // The normal exit is to the birthday, over which the outro plays (GameManager hears WishResolved first,
            // so the state can change before OnResolved runs). Leaving any other way closes at once.
            else if (state != GameState.Birthday && step != Step.Hidden) Close();
        }

        void Open()
        {
            var gm = GameManager.Instance;
            var wish = Wish;
            if (gm == null || wish == null) return;

            title.text = "MAKE A WISH";
            subtitle.text = $"{Story.Hero} turns {gm.Age}. Blow out candles and wish for a gift that lasts a lifetime.";
            RefreshOptions(wish);
            foreach (var g in gifts) g.button.gameObject.SetActive(false);
            foreach (var o in options) o.button.gameObject.SetActive(true);

            group.alpha = 1f;
            group.blocksRaycasts = true;
            shakeRoot.localScale = Vector3.one;
            flashAlpha = 0f;
            blowing = null;
            SetStep(Step.Candles);
            panel.gameObject.SetActive(true);
        }

        void RefreshOptions(WishSystem wish)
        {
            var clock = CandleClock.Instance;
            wallet.text = clock != null ? $"You have {clock.TimeRemaining:0} s  ·  {clock.LitCandles} of {clock.CandleCap} candles lit" : "";

            WishOption[] rules = wish.Options();
            for (int i = 0; i < options.Length && i < rules.Length; i++)
            {
                OptionView v = options[i];
                WishOption o = rules[i];
                v.option = o;
                v.title.text = o.Candles == 0 ? "NO CANDLES" : $"{o.Candles} CANDLE{(o.Candles == 1 ? "" : "S")}";
                v.cost.text = o.Candles == 0 ? "Free" : $"-{o.Cost:0} s";
                v.cost.color = o.Candles == 0 ? skin.mutedText : skin.danger;
                v.tier.text = o.Tier == WishGiftTier.None ? "No gift" : $"{o.Tier.ToString().ToUpperInvariant()} GIFT";
                v.tier.color = o.Tier == WishGiftTier.None ? skin.mutedText : TierColor(o.Tier);
                v.body.text = o.Legal
                    ? (o.Candles == 0 ? "Keep every candle burning. No gift this time." : GiftCountText(Mathf.Min(Balance.WishGiftChoices, wish.Available(o.Tier).Count)))
                    : $"<color=#{Hex(skin.danger)}>{o.BlockedReason}</color>";

                v.button.interactable = o.Legal;
                v.background.color = o.Legal ? skin.panelLight : skin.disabled;
                v.background.GetComponent<Outline>().enabled = o.Legal;
                for (int c = 0; c < MaxCandleIcons; c++)
                {
                    bool shown = c < o.Candles;
                    v.wax[c].gameObject.SetActive(shown);
                    v.flames[c].gameObject.SetActive(shown);
                    v.flames[c].transform.localScale = Vector3.one;
                    v.flames[c].color = skin.flameColor;
                    // The last wish greyed the candles it blew out.
                    v.wax[c].color = skin.waxColors != null && skin.waxColors.Length > 0 ? skin.waxColors[0] : Color.white;
                }
            }
        }

        Color TierColor(WishGiftTier tier) => tier switch
        {
            WishGiftTier.Legendary => skin.epic,
            WishGiftTier.Large => skin.rare,
            _ => skin.common
        };

        void SetStep(Step next)
        {
            step = next;
            stepAt = Time.unscaledTime;
        }

        bool Ready => Time.unscaledTime - stepAt >= InputDelay; // no accidental double clicks

        void Blow(OptionView v)
        {
            if (step != Step.Candles || !Ready || !v.option.Legal) return;
            GameManager.Instance.BlowWishCandles(v.option.Candles);
        }

        void ChooseGift(int index)
        {
            if (step != Step.Gifts || !Ready) return;
            GameManager.Instance.ChooseWishGift(index);
        }

        // The candles really were spent: now the moment.
        void OnBlown(int candles)
        {
            if (panel == null || !panel.gameObject.activeSelf) return;
            blowing = null;
            foreach (var o in options)
                if (o.option.Candles == candles) blowing = o;
            candlesPuffed = 0;
            title.text = "BLOW!";
            subtitle.text = $"{candles} candle{(candles == 1 ? "" : "s")} out. {WishSystem.CostFor(candles):0} seconds of {Story.HeroPossessive} life, traded for a wish.";
            var clock = CandleClock.Instance;
            if (clock != null) wallet.text = $"You have {clock.TimeRemaining:0} s left  ·  {clock.LitCandles} of {clock.CandleCap} candles lit";
            foreach (var o in options)
                if (o != blowing) o.button.gameObject.SetActive(false);
            SetStep(Step.Blowing);
        }

        void ShowGifts()
        {
            var wish = Wish;
            if (wish == null || wish.CurrentOffer == null) return;
            if (blowing != null) blowing.button.gameObject.SetActive(false);

            WishGiftTier tier = wish.PendingTier;
            title.text = $"{tier.ToString().ToUpperInvariant()} WISH";
            subtitle.text = "Choose your gift. It stays with you for the rest of this life.";
            UpgradeCard[] offer = wish.CurrentOffer;
            for (int i = 0; i < gifts.Length; i++)
            {
                GiftView g = gifts[i];
                bool has = i < offer.Length;
                g.button.gameObject.SetActive(has);
                if (!has) continue;
                g.tier.text = $"LIFETIME  ·  {tier.ToString().ToUpperInvariant()}";
                g.tier.color = TierColor(tier);
                g.content.GetComponent<Outline>().effectColor = TierColor(tier);
                g.title.text = offer[i].title;
                g.body.text = offer[i].description;
                g.hover.ResetLift();
                g.button.transform.localScale = Vector3.zero;
            }
            // Gifts sit where the candle row was; keep them centred for 1 or 2 gifts too.
            for (int i = 0; i < offer.Length && i < gifts.Length; i++)
                gifts[i].button.GetComponent<RectTransform>().anchoredPosition = new Vector2((i - (offer.Length - 1) * 0.5f) * 440f, -120f);
            SetStep(Step.Gifts);
        }

        void OnResolved(int candles, WishGift gift)
        {
            if (panel == null || !panel.gameObject.activeSelf) return;
            if (gift != null)
            {
                title.text = "WISH GRANTED";
                subtitle.text = $"{gift.Card.title}: {gift.Card.description}";
                flashAlpha = 0.7f;
                shake = 0.6f;
                for (int i = 0; i < gifts.Length; i++)
                    if (gifts[i].button.gameObject.activeSelf && gifts[i].title.text == gift.Card.title)
                        Burst(Local(gifts[i].button.transform), 70, skin.epic, 900f);
                    else gifts[i].button.gameObject.SetActive(false);
            }
            else
            {
                title.text = "NO WISH";
                subtitle.text = "You keep every candle burning.";
                foreach (var o in options) o.button.gameObject.SetActive(false);
            }
            // The birthday screen underneath takes clicks as soon as its own input delay is over.
            group.blocksRaycasts = false;
            SetStep(Step.Outro);
        }

        void Close()
        {
            SetStep(Step.Hidden);
            foreach (var p in particles) p.image.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (panel == null || !panel.gameObject.activeSelf || TimeControl.IsMenuPaused) return;
            float t = Time.unscaledTime - stepAt;
            float dt = Time.unscaledDeltaTime;

            switch (step)
            {
                case Step.Candles:
                    for (int i = 0; i < options.Length; i++)
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Blow(options[i]);
                    FlickerFlames(options);
                    title.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(t / 0.35f));
                    break;

                case Step.Blowing:
                    AnimateBlowOut(t);
                    break;

                case Step.Gifts:
                {
                    var wish = Wish;
                    int count = wish != null && wish.CurrentOffer != null ? wish.CurrentOffer.Length : 0;
                    for (int i = 0; i < count && i < gifts.Length; i++)
                    {
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i)) ChooseGift(i);
                        float local = Mathf.Clamp01(t * 3f - i * 0.25f);
                        gifts[i].button.transform.localScale = Vector3.one * (1f - (1f - local) * (1f - local));
                        var outline = gifts[i].content.GetComponent<Outline>();
                        outline.effectColor = Color.Lerp(TierColor(wish.PendingTier), Color.white, (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)) * 0.5f);
                    }
                    break;
                }

                case Step.Outro:
                    group.alpha = 1f - Mathf.Clamp01((t - OutroSeconds * 0.5f) / (OutroSeconds * 0.5f));
                    if (t >= OutroSeconds) Close();
                    break;
            }

            // Hard beat: panel shake and a warm flash, decaying on real time.
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.8f);
            float power = shake * shake * 40f;
            shakeRoot.anchoredPosition = new Vector2(Mathf.PerlinNoise(Time.unscaledTime * 30f, 0.2f) - 0.5f, Mathf.PerlinNoise(0.8f, Time.unscaledTime * 30f) - 0.5f) * 2f * power;
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.6f);
            flash.color = new Color(1f, 0.93f, 0.75f, flashAlpha);
            UpdateParticles(dt);
        }

        // One candle at a time: the flame flares, then snuffs out in a puff of smoke and sparks.
        void AnimateBlowOut(float t)
        {
            if (blowing == null)
            {
                if (t >= GiftsAfter) ShowGifts();
                return;
            }

            int count = blowing.option.Candles;
            for (int c = 0; c < count; c++)
            {
                float local = (t - c * CandleStagger) / 0.22f;
                Image f = blowing.flames[c];
                if (local < 0f) continue;
                f.transform.localScale = Vector3.one * (local < 0.35f ? Mathf.Lerp(1f, 1.6f, local / 0.35f) : Mathf.Lerp(1.6f, 0f, (local - 0.35f) / 0.65f));
                if (local >= 1f && c >= candlesPuffed)
                {
                    candlesPuffed = c + 1;
                    Vector2 top = Local(f.transform);
                    Smoke(top, 9);
                    Burst(top, 16, skin.epic, 520f);
                    blowing.wax[c].color = skin.waxOut;
                    shake = Mathf.Min(1f, shake + 0.25f);
                    if (candlesPuffed == count)
                    {
                        // The last one: the big beat.
                        flashAlpha = 0.45f + 0.06f * count;
                        shake = 1f;
                        Burst(Local(blowing.button.transform), 30 + 12 * count, skin.accent, 1100f);
                    }
                }
            }
            if (candlesPuffed == count && t >= (count - 1) * CandleStagger + 0.22f + GiftsAfter) ShowGifts();
        }

        void FlickerFlames(OptionView[] views)
        {
            foreach (var v in views)
                for (int c = 0; c < MaxCandleIcons; c++)
                {
                    if (!v.flames[c].gameObject.activeSelf) continue;
                    float n = Mathf.PerlinNoise(Time.unscaledTime * 6f + c * 1.7f, v.GetHashCode() * 0.001f);
                    v.flames[c].transform.localScale = new Vector3(0.9f + 0.2f * n, 0.85f + 0.3f * n, 1f);
                }
        }

        Vector2 Local(Transform t) => particleRoot.InverseTransformPoint(t.position);

        void Smoke(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
                Spawn(at, new Vector2(Random.Range(-40f, 40f), Random.Range(60f, 160f)), Random.Range(0.8f, 1.4f),
                    new Color(0.8f, 0.8f, 0.85f, 0.7f), Random.Range(22f, 40f), -30f, 60f);
        }

        void Burst(Vector2 at, int count, Color color, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(speed * 0.3f, speed);
                Color c = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));
                Spawn(at, v, Random.Range(0.5f, 1.1f), c, Random.Range(8f, 18f), 900f, -4f);
            }
        }

        void Spawn(Vector2 at, Vector2 velocity, float life, Color color, float size, float gravity, float grow)
        {
            foreach (var p in particles)
            {
                if (p.image.gameObject.activeSelf) continue;
                p.image.gameObject.SetActive(true);
                p.image.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(size, size));
                p.velocity = velocity;
                p.life = life;
                p.age = 0f;
                p.gravity = gravity;
                p.grow = grow;
                p.color = color;
                return;
            }
        }

        void UpdateParticles(float dt)
        {
            foreach (var p in particles)
            {
                if (!p.image.gameObject.activeSelf) continue;
                p.age += dt;
                if (p.age >= p.life)
                {
                    p.image.gameObject.SetActive(false);
                    continue;
                }
                p.velocity *= 1f - Mathf.Min(1f, dt * 1.5f);
                p.velocity.y -= p.gravity * dt;
                var rt = p.image.rectTransform;
                rt.anchoredPosition += p.velocity * dt;
                rt.sizeDelta += Vector2.one * p.grow * dt;
                float fade = 1f - p.age / p.life;
                p.image.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * fade);
            }
        }

        static string GiftCountText(int gifts) => gifts == 1 ? "One gift left to wish for." : $"Pick one of {gifts} gifts.";

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
