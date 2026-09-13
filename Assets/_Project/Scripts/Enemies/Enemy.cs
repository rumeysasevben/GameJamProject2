using System;
using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    public struct EnemyStats
    {
        public float hp;
        public float speed;
        public float reward;
    }

    // Health, status effects and path movement. State transitions live in EnemyFSM.
    [RequireComponent(typeof(EnemySkin))]
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> Active = new List<Enemy>();

        const float AuraInterval = 0.5f;

        public EnemyData Data { get; private set; }
        public EnemySkin Skin { get; private set; }
        public float MaxHp { get; private set; }
        public float Hp { get; private set; }
        public float Shield { get; private set; }
        public float Reward { get; private set; }
        public float PathProgress { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsSlowed => Time.time < slowUntil;
        public bool IsBurning => Time.time < burnUntil;

        public bool IsTargetable => IsAlive && fsm.Current.TakesDamage;
        public float StateSpeedMultiplier { get; set; } = 1f;

        EnemyFSM fsm;
        PathRoute route;
        Action<Enemy> release;
        float baseSpeed;
        int nextWaypoint;
        float slowPercent, slowUntil;
        float burnDps, burnUntil;
        bool hasSprinted, shieldGranted;
        float auraTimer;

        void Awake()
        {
            Skin = GetComponent<EnemySkin>();
            fsm = new EnemyFSM(this);
        }

        public void Init(EnemyData data, EnemyStats stats, PathRoute path, Action<Enemy> onRelease)
        {
            Data = data;
            route = path;
            release = onRelease;

            MaxHp = stats.hp * data.hpMultiplier;
            Hp = MaxHp;
            Shield = 0f;
            baseSpeed = stats.speed * data.speedMultiplier;
            Reward = stats.reward * data.rewardMultiplier;

            PathProgress = 0f;
            nextWaypoint = 1;
            transform.position = route.StartPoint;
            slowUntil = burnUntil = 0f;
            hasSprinted = shieldGranted = false;
            auraTimer = 0f;
            StateSpeedMultiplier = 0f;
            IsAlive = true;

            Skin.Setup(data);
            Active.Add(this);
            fsm.Change(EnemyStateId.Spawn);
            GameEvents.RaiseEnemySpawned(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            fsm.Tick(dt);
            if (!IsAlive) return;

            if (IsBurning) Damage(burnDps * dt, false, DamageSource.Burn);
            if (IsAlive && Data.auraRadius > 0f) TickAura(dt);
        }

        public float CurrentSpeed
        {
            get
            {
                float slow = IsSlowed ? 1f - slowPercent : 1f;
                return baseSpeed * StateSpeedMultiplier * slow;
            }
        }

        // Returns true once the last waypoint is reached.
        public bool MoveAlongPath(float dt)
        {
            float step = CurrentSpeed * dt;
            while (step > 0f && nextWaypoint < route.Count)
            {
                Vector3 target = route[nextWaypoint];
                Vector3 pos = transform.position;
                float dist = Vector3.Distance(pos, target);
                if (dist > step)
                {
                    transform.position = Vector3.MoveTowards(pos, target, step);
                    PathProgress += step;
                    Skin.FaceDirection(target - pos);
                    return false;
                }
                transform.position = target;
                PathProgress += dist;
                step -= dist;
                nextWaypoint++;
            }
            return nextWaypoint >= route.Count;
        }

        public enum DamageSource { Hit, Splash, Burn }

        public void Damage(float amount, bool crit, DamageSource source)
        {
            if (!IsTargetable || amount <= 0f) return;

            if (source != DamageSource.Burn) amount = Mathf.Max(amount - Data.armor, amount > 0f ? 0.5f : 0f);

            float absorbed = Mathf.Min(Shield, amount);
            Shield -= absorbed;
            float dealt = amount - absorbed;
            Hp -= dealt;

            if (source != DamageSource.Burn)
            {
                Skin.Flash();
                GameEvents.RaiseEnemyDamaged(this, amount, crit);
            }

            if (Hp <= 0f)
            {
                Die();
                return;
            }

            if (Data.canSprint && !hasSprinted && Hp < MaxHp * Data.sprintHpThreshold)
            {
                hasSprinted = true;
                fsm.Change(EnemyStateId.Sprint);
            }
            else if (source != DamageSource.Burn && fsm.Current.CanStagger)
            {
                fsm.Change(EnemyStateId.Stagger);
            }
        }

        public void ApplySlow(float percent, float duration)
        {
            if (!IsTargetable) return;
            if (!IsSlowed || percent >= slowPercent) slowPercent = percent;
            slowUntil = Mathf.Max(slowUntil, Time.time + duration);
        }

        public void ApplyBurn(float dps, float duration)
        {
            if (!IsTargetable) return;
            burnDps = IsBurning ? Mathf.Max(burnDps, dps) : dps;
            burnUntil = Mathf.Max(burnUntil, Time.time + duration);
        }

        public void GiveShield(float amount)
        {
            if (shieldGranted) return;
            shieldGranted = true;
            Shield = amount;
        }

        void TickAura(float dt)
        {
            auraTimer -= dt;
            if (auraTimer > 0f) return;
            auraTimer = AuraInterval;

            float r2 = Data.auraRadius * Data.auraRadius;
            for (int i = 0; i < Active.Count; i++)
            {
                Enemy other = Active[i];
                if (other == this || !other.IsTargetable) continue;
                if ((other.transform.position - transform.position).sqrMagnitude > r2) continue;
                other.GiveShield(other.MaxHp * Data.auraShieldPercent);
            }
        }

        void Die()
        {
            IsAlive = false;
            Hp = 0f;
            Active.Remove(this);
            GameEvents.RaiseEnemyKilled(this, Reward);
            fsm.Change(EnemyStateId.Dying);
        }

        // Called by DespawnState.
        public void ReturnToPool()
        {
            IsAlive = false;
            Active.Remove(this);
            release?.Invoke(this);
        }

        void OnDisable() => Active.Remove(this);
    }
}
