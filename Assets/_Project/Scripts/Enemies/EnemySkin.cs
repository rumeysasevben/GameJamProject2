using UnityEngine;

namespace TenCandles
{
    // Everything visual about an enemy: frame animation, hit flash, health bar, spawn/blow-out/death animations.
    public class EnemySkin : MonoBehaviour
    {
        const float FlashDuration = 0.06f;

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer shieldRing;
        [SerializeField] SpriteRenderer hpBack;
        [SerializeField] SpriteRenderer hpFill;

        static readonly Color SlowTint = new Color(0.55f, 0.75f, 1f);
        static readonly Color BurnTint = new Color(1f, 0.6f, 0.35f);
        static readonly Color HitTint = new Color(1f, 0.45f, 0.45f);

        Enemy enemy;
        EnemyData data;
        EnemyAnimation frames;
        float flashUntil;
        float animStart, animDuration;
        enum Anim { None, Spawn, BlowOut, Die, Attack }
        Anim anim;
        bool sprinting;
        float baseSize;
        float bob;
        float walkClock;
        float hurtStart = -1f;
        float jumpStart = -1f;

        bool Animated => frames != null && frames.walk != null && frames.walk.Length > 0;

        public void Build(Sprite square, Sprite ring)
        {
            body = NewRenderer("Body", 10);
            shieldRing = NewRenderer("Shield", 9); // behind the body, so it glows around it instead of tinting it
            shieldRing.sprite = ring;
            shieldRing.color = new Color(0.55f, 0.8f, 1f, 0.55f);
            hpBack = NewRenderer("HpBack", 12);
            hpBack.sprite = square;
            hpBack.color = new Color(0f, 0f, 0f, 0.6f);
            hpFill = NewRenderer("HpFill", 13);
            hpFill.sprite = square;
            hpFill.color = new Color(0.4f, 1f, 0.45f);
        }

        SpriteRenderer NewRenderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        void Awake() => enemy = GetComponent<Enemy>();

        public void Setup(EnemyData enemyData)
        {
            data = enemyData;
            frames = data.variants != null && data.variants.Length > 0 ? data.variants[Random.Range(0, data.variants.Length)] : null;
            body.sprite = Animated ? frames.walk[0] : data.sprite;
            body.color = data.tint;
            baseSize = data.size;
            body.transform.localScale = Vector3.one * baseSize;

            // Bars sit above the sprite's real height, not a guess from the size alone.
            float halfHeight = body.sprite != null ? body.sprite.bounds.extents.y * baseSize : baseSize * 0.5f;
            shieldRing.transform.localScale = Vector3.one * Mathf.Max(baseSize, halfHeight * 2f) * 1.6f;
            hpBack.transform.localPosition = new Vector3(0f, halfHeight + 0.12f, 0f);
            hpBack.transform.localScale = new Vector3(Mathf.Clamp(baseSize * 0.8f, 0.6f, 1.4f), 0.1f, 1f);
            hpFill.transform.localScale = hpBack.transform.localScale;
            hpFill.transform.localPosition = hpBack.transform.localPosition;
            transform.localScale = Vector3.one;
            flashUntil = 0f;
            hurtStart = -1f;
            jumpStart = -1f;
            sprinting = false;
            anim = Anim.None;
            bob = Random.value * 10f;
            walkClock = Random.value * 10f;
        }

        public void PlaySpawn(float duration) => StartAnim(Anim.Spawn, duration);
        public void PlayBlowOut(float duration) => StartAnim(Anim.BlowOut, duration);
        public void PlayDie(float duration) => StartAnim(Anim.Die, duration);
        public void PlayAttack(float duration) => StartAnim(Anim.Attack, duration);

        public void SetSprinting(bool value)
        {
            // A runner kicks off its sprint with a leap.
            if (value && !sprinting) jumpStart = Time.time;
            sprinting = value;
        }

        public void Flash()
        {
            flashUntil = Time.time + FlashDuration;
            // Don't restart the hurt clip on every hit, or rapid fire freezes it on frame one.
            if (hurtStart < 0f || Time.time - hurtStart > HurtLength) hurtStart = Time.time;
        }

        float HurtLength => Animated && frames.hurt != null ? frames.hurt.Length / Mathf.Max(1f, data.framesPerSecond * 1.5f) : 0f;

        public void FaceDirection(Vector3 dir)
        {
            if (Mathf.Abs(dir.x) > 0.01f) body.flipX = dir.x < 0f;
        }

        void StartAnim(Anim a, float duration)
        {
            anim = a;
            animStart = Time.time;
            animDuration = duration;
        }

        void LateUpdate()
        {
            if (data == null) return;

            float scale = 1f;
            float t = anim != Anim.None ? Mathf.Clamp01((Time.time - animStart) / animDuration) : 0f;
            if (anim == Anim.Spawn)
            {
                scale = EaseOutBack(t);
                if (t >= 1f) anim = Anim.None;
            }
            else if (anim == Anim.BlowOut)
            {
                scale = t < 0.5f ? 1f + t * 0.8f : Mathf.Lerp(1.4f, 0f, (t - 0.5f) * 2f);
            }
            transform.localScale = Vector3.one * scale;

            if (Animated) AnimateFrames(t);
            else
            {
                // Little walking bob so static shapes feel alive.
                bob += Time.deltaTime * (sprinting ? 18f : 9f) * Mathf.Max(0.2f, enemy.StateSpeedMultiplier);
                body.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bob)) * 0.06f, 0f);
            }

            Color c = data.tint;
            if (enemy.IsBurning) c = Color.Lerp(c, BurnTint, 0.45f);
            if (enemy.IsSlowed) c = Color.Lerp(c, SlowTint, 0.5f);
            if (sprinting) c = Color.Lerp(c, Color.red, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 30f)));
            if (Time.time < flashUntil) c = Animated ? Color.Lerp(c, HitTint, 0.7f) : Color.white;
            if (anim == Anim.Die) c.a *= 1f - Mathf.InverseLerp(0.7f, 1f, t);
            body.color = c;

            shieldRing.enabled = enemy.Shield > 0.01f;

            float pct = enemy.MaxHp > 0f ? Mathf.Clamp01(enemy.Hp / enemy.MaxHp) : 0f;
            bool showBar = enemy.IsAlive && pct < 0.999f;
            hpBack.enabled = hpFill.enabled = showBar;
            if (showBar)
            {
                Vector3 back = hpBack.transform.localScale;
                hpFill.transform.localScale = new Vector3(back.x * pct, back.y, 1f);
                hpFill.transform.localPosition = hpBack.transform.localPosition + new Vector3(-back.x * (1f - pct) * 0.5f, 0f, 0f);
                hpFill.color = Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.4f, 1f, 0.45f), pct);
            }
        }

        void AnimateFrames(float t)
        {
            // Clips tied to a state play once across that state's duration.
            Sprite[] timed = anim == Anim.Die ? frames.die : anim == Anim.Attack ? frames.attack : anim == Anim.Spawn ? frames.jump : null;
            if (EnemyAnimation.Has(timed))
            {
                body.sprite = timed[Mathf.Min(timed.Length - 1, Mathf.FloorToInt(t * timed.Length))];
                return;
            }

            float fps = data.framesPerSecond;
            if (jumpStart >= 0f && EnemyAnimation.Has(frames.jump))
            {
                int jumpFrame = Mathf.FloorToInt((Time.time - jumpStart) * fps * 1.5f);
                if (jumpFrame < frames.jump.Length)
                {
                    body.sprite = frames.jump[jumpFrame];
                    return;
                }
                jumpStart = -1f;
            }
            if (hurtStart >= 0f && frames.hurt != null && frames.hurt.Length > 0)
            {
                int hurtFrame = Mathf.FloorToInt((Time.time - hurtStart) * fps * 1.5f);
                if (hurtFrame < frames.hurt.Length)
                {
                    body.sprite = frames.hurt[hurtFrame];
                    return;
                }
                hurtStart = -1f;
            }

            Sprite[] loop = sprinting && frames.run != null && frames.run.Length > 0 ? frames.run : frames.walk;
            // Leg speed follows movement speed, so slowed enemies visibly trudge.
            float speed = Mathf.Clamp(enemy.StateSpeedMultiplier * (enemy.IsSlowed ? 0.6f : 1f), 0.3f, 2f);
            walkClock += Time.deltaTime * fps * speed;
            body.sprite = loop[Mathf.FloorToInt(walkClock) % loop.Length];
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
