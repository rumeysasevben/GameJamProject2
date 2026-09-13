using UnityEngine;

namespace TenCandles
{
    public struct ProjectileHit
    {
        public float damage;
        public bool crit;
        public float splashRadius;
        public float burnDps;
        public float burnDuration;
        public float slowPercent;
        public float slowDuration;
        public int chains;
        public TowerKind kind;
        public int level;
    }

    public class Projectile : MonoBehaviour
    {
        const float ChainRange = 2.2f;

        SpriteRenderer sr;
        Enemy target;
        Vector3 start, lastTargetPos;
        ProjectileHit hit;
        TowerData data;
        float traveled, totalDistance;
        bool spins, facesTravel;
        System.Action<Projectile> release;

        public void Build()
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 20;
        }

        public void Launch(TowerData towerData, Vector3 from, Enemy enemy, ProjectileHit projectileHit, System.Action<Projectile> onRelease)
        {
            data = towerData;
            target = enemy;
            hit = projectileHit;
            release = onRelease;
            start = from;
            lastTargetPos = enemy.transform.position;
            transform.position = from;
            transform.rotation = Quaternion.identity;
            traveled = 0f;
            totalDistance = Mathf.Max(0.1f, Vector3.Distance(from, lastTargetPos));

            float critScale = hit.crit ? 1.4f : 1f;
            sr.color = hit.crit ? new Color(1f, 0.95f, 0.4f) : data.projectileColor;
            ProjectileLook look = data.ProjectileForLevel(hit.level);
            if (look != null)
            {
                sr.sprite = look.sprite;
                float width = Mathf.Max(0.01f, look.sprite.bounds.size.x);
                transform.localScale = Vector3.one * (look.size / width) * critScale;
                spins = look.spin || data.lobbed;
                facesTravel = look.faceTravel;
            }
            else
            {
                sr.sprite = data.projectileSprite;
                transform.localScale = Vector3.one * data.projectileSize * critScale;
                spins = data.projectileSpins || data.lobbed;
                facesTravel = false;
            }
        }

        void Update()
        {
            if (target != null && target.IsTargetable) lastTargetPos = target.transform.position;

            float step = data.projectileSpeed * Time.deltaTime;
            if (data.lobbed)
            {
                // Lobbed shots fly a fixed arc toward the target's current spot.
                totalDistance = Mathf.Max(totalDistance, traveled + 0.01f);
                traveled += step;
                float t = Mathf.Clamp01(traveled / totalDistance);
                Vector3 flat = Vector3.Lerp(start, lastTargetPos, t);
                float height = Mathf.Sin(t * Mathf.PI) * Mathf.Min(1.6f, totalDistance * 0.35f);
                Orient(flat + Vector3.up * height, 540f);
                if (t >= 1f) Impact();
                return;
            }

            Vector3 pos = Vector3.MoveTowards(transform.position, lastTargetPos, step);
            Orient(pos, -720f);
            if ((pos - lastTargetPos).sqrMagnitude < 0.01f) Impact();
        }

        void Orient(Vector3 next, float spinSpeed)
        {
            Vector3 delta = next - transform.position;
            transform.position = next;
            if (facesTravel)
            {
                // The sprite's bottom leads: a flame ball with its trail behind.
                if (delta.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f);
            }
            else if (spins) transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }

        void Impact()
        {
            if (hit.splashRadius > 0f)
            {
                float r2 = hit.splashRadius * hit.splashRadius;
                var list = Enemy.Active;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (i >= list.Count) continue;
                    Enemy e = list[i];
                    if ((e.transform.position - lastTargetPos).sqrMagnitude <= r2) Apply(e, Enemy.DamageSource.Splash);
                }
            }
            else if (target != null && target.IsTargetable)
            {
                Enemy struck = target;
                Apply(struck, Enemy.DamageSource.Hit);
                if (hit.chains > 0) Chain(struck);
            }

            GameEvents.RaiseProjectileImpact(lastTargetPos, hit.splashRadius, hit.kind);
            target = null;
            release?.Invoke(this);
        }

        void Apply(Enemy e, Enemy.DamageSource source)
        {
            if (hit.slowPercent > 0f) e.ApplySlow(hit.slowPercent, hit.slowDuration);
            if (hit.burnDps > 0f) e.ApplyBurn(hit.burnDps, hit.burnDuration);
            e.Damage(hit.damage, hit.crit, source);
        }

        // "Chain Reaction": the confetti shot jumps to the nearest other enemy.
        void Chain(Enemy from)
        {
            Enemy next = null;
            float best = ChainRange * ChainRange;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy e = list[i];
                if (e == from || !e.IsTargetable) continue;
                float d = (e.transform.position - lastTargetPos).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    next = e;
                }
            }
            if (next == null) return;

            ProjectileHit chained = hit;
            chained.chains--;
            ProjectilePool.Instance.Launch(data, lastTargetPos, next, chained);
        }
    }
}
