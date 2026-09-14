using UnityEngine;

namespace TenCandles
{
    // Picks a target and shoots. Knows nothing about time or cost.
    public class Tower : MonoBehaviour
    {
        public TowerData Data { get; private set; }
        public TowerSpot Spot { get; private set; }
        public int Level { get; private set; } = 1;

        public float Range => Data.range * TowerData.RangeMultiplier(Level) * StatRegistry.RangeMultiplier;
        public float Damage => Data.damage * TowerData.DamageMultiplier(Level) * StatRegistry.DamageFor(Data.kind);
        public float FireInterval => 1f / (Data.fireRate * StatRegistry.FireRateFor(Data.kind));
        public float SplashRadius => Data.splashRadius * StatRegistry.SplashRadiusMultiplier;
        public float BurnDps => Data.burnDps * TowerData.DamageMultiplier(Level) * StatRegistry.DamageFor(Data.kind) * StatRegistry.BurnMultiplier;
        public float SlowPercent => Data.slowPercent > 0f ? Mathf.Clamp01(Data.slowPercent + StatRegistry.CandleSlowBonus) : 0f;
        public float Dps => Damage / FireInterval;
        public bool IsMaxLevel => Level >= TowerData.MaxLevel;

        SpriteRenderer body;
        Transform pips;
        float cooldown;
        float punchTime = -1f;

        public void Init(TowerData data, TowerSpot spot, Sprite pipSprite)
        {
            Data = data;
            Spot = spot;
            transform.position = spot.transform.position;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            body = bodyGo.AddComponent<SpriteRenderer>();
            body.sortingOrder = 5;

            pips = new GameObject("LevelPips").transform;
            pips.SetParent(transform, false);
            for (int i = 0; i < TowerData.MaxLevel; i++)
            {
                var pip = new GameObject("Pip" + i).AddComponent<SpriteRenderer>();
                pip.sprite = pipSprite;
                pip.sortingOrder = 6;
                pip.transform.SetParent(pips, false);
                pip.transform.localScale = Vector3.one * 0.14f;
                pip.transform.localPosition = new Vector3((i - 1) * 0.2f, -0.75f, 0f);
            }

            cooldown = 0.2f;
            SetLevel(1);
        }

        public void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 1, TowerData.MaxLevel);
            body.sprite = Data.SpriteForLevel(Level);
            body.color = Data.tint;
            for (int i = 0; i < pips.childCount; i++)
            {
                var pip = pips.GetChild(i).GetComponent<SpriteRenderer>();
                pip.color = i < Level ? new Color(1f, 0.85f, 0.3f) : new Color(0f, 0f, 0f, 0.35f);
            }
            Punch();
        }

        public void Punch() => punchTime = Time.time;

        void Update()
        {
            if (Data == null) return;
            AnimateBody();

            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            Enemy target = FindTarget();
            if (target == null) return;

            Fire(target);
            cooldown = FireInterval;
        }

        // "First" targeting: whoever is furthest along the road.
        Enemy FindTarget()
        {
            float r2 = Range * Range;
            Enemy best = null;
            float bestProgress = -1f;
            Vector3 pos = transform.position;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy e = list[i];
                if (!e.IsTargetable) continue;
                if ((e.transform.position - pos).sqrMagnitude > r2) continue;
                if (e.PathProgress > bestProgress)
                {
                    best = e;
                    bestProgress = e.PathProgress;
                }
            }
            return best;
        }

        void Fire(Enemy target)
        {
            bool crit = StatRegistry.CritChance > 0f && (float)Lifetime.LifetimeManager.CombatRandom.NextDouble() < StatRegistry.CritChance;
            var hit = new ProjectileHit
            {
                damage = Damage * (crit ? StatRegistry.CritDamageMultiplier : 1f),
                crit = crit,
                splashRadius = SplashRadius,
                burnDps = BurnDps,
                burnDuration = Data.burnDuration,
                slowPercent = SlowPercent,
                slowDuration = Data.slowDuration,
                chains = Data.kind == TowerKind.Confetti && StatRegistry.ConfettiChain ? 1 : 0,
                kind = Data.kind,
                level = Level
            };

            ProjectilePool.Instance.Launch(Data, transform.position + Vector3.up * LaunchHeight, target, hit);
            Face(target.transform.position);
            Punch();
            GameEvents.RaiseTowerFired(this);
        }

        void Face(Vector3 targetPos)
        {
            if (Mathf.Abs(targetPos.x - transform.position.x) > 0.05f) body.flipX = targetPos.x < transform.position.x;
        }

        // Shots leave from the top of the tower (the catapult cup), not its feet.
        float LaunchHeight => body.sprite != null ? Mathf.Max(0.25f, body.bounds.max.y - transform.position.y - 0.3f) : 0.25f;

        void AnimateBody()
        {
            // Level sprites already grow, so the extra scale per level stays small.
            float baseScale = Data.size * (1f + 0.05f * (Level - 1));
            float t = punchTime < 0f ? 1f : (Time.time - punchTime) / 0.18f;
            float punch = t < 1f ? Mathf.Sin(t * Mathf.PI) * 0.18f : 0f;
            body.transform.localScale = new Vector3(baseScale * (1f + punch), baseScale * (1f - punch * 0.5f), 1f);
        }
    }
}
