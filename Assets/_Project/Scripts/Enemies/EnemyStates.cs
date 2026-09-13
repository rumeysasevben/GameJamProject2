namespace TenCandles
{
    public abstract class EnemyState
    {
        protected Enemy Enemy { get; private set; }
        protected EnemyFSM Fsm { get; private set; }
        protected float Elapsed { get; private set; }

        public virtual bool TakesDamage => true;
        public virtual bool CanStagger => false;
        public virtual bool IsLeaving => false;

        public void Bind(Enemy enemy, EnemyFSM fsm)
        {
            Enemy = enemy;
            Fsm = fsm;
        }

        public void ResetTimer() => Elapsed = 0f;

        public virtual void Enter() { }
        public virtual void Exit() { }

        public void Tick(float dt)
        {
            Elapsed += dt;
            OnTick(dt);
        }

        protected virtual void OnTick(float dt) { }

        // Shared by every state that walks: move, and hand over to ReachEnd at the last waypoint.
        protected bool Walk(float dt)
        {
            if (!Enemy.MoveAlongPath(dt)) return false;
            Fsm.Change(EnemyStateId.ReachEnd);
            return true;
        }
    }

    // 0.3 s pop-in, can't be hurt.
    public class SpawnState : EnemyState
    {
        public const float Duration = 0.3f;
        public override bool TakesDamage => false;

        public override void Enter() => Enemy.Skin.PlaySpawn(Duration);

        protected override void OnTick(float dt)
        {
            if (Elapsed >= Duration) Fsm.Change(EnemyStateId.Walk);
        }
    }

    public class WalkState : EnemyState
    {
        public override bool CanStagger => true;

        public override void Enter() => Enemy.StateSpeedMultiplier = 1f;

        protected override void OnTick(float dt) => Walk(dt);
    }

    // Hit reaction: 60 % speed for 0.12 s.
    public class StaggerState : EnemyState
    {
        public const float Duration = 0.12f;
        public const float SpeedMultiplier = 0.6f;
        public override bool CanStagger => true;

        public override void Enter() => Enemy.StateSpeedMultiplier = SpeedMultiplier;

        protected override void OnTick(float dt)
        {
            if (Walk(dt)) return;
            if (Elapsed >= Duration) Fsm.Change(EnemyStateId.Walk);
        }
    }

    // Runner only, once per life.
    public class SprintState : EnemyState
    {
        public override void Enter()
        {
            Enemy.StateSpeedMultiplier = 1f + Enemy.Data.sprintSpeedBonus;
            Enemy.Skin.SetSprinting(true);
        }

        public override void Exit() => Enemy.Skin.SetSprinting(false);

        protected override void OnTick(float dt)
        {
            if (Walk(dt)) return;
            if (Elapsed >= Enemy.Data.sprintDuration) Fsm.Change(EnemyStateId.Walk);
        }
    }

    public class ReachEndState : EnemyState
    {
        public override bool TakesDamage => false;
        public override bool IsLeaving => true;

        public override void Enter() => Enemy.StateSpeedMultiplier = 0f;

        protected override void OnTick(float dt) => Fsm.Change(EnemyStateId.Attack);
    }

    // Swings at the cake before blowing a candle out. Already out of reach, so balance is unchanged.
    public class AttackState : EnemyState
    {
        public const float Duration = 0.45f;
        public override bool TakesDamage => false;
        public override bool IsLeaving => true;

        public override void Enter()
        {
            Enemy.StateSpeedMultiplier = 0f;
            Enemy.Skin.PlayAttack(Duration);
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed >= Duration) Fsm.Change(EnemyStateId.BlowOut);
        }
    }

    // Blows out a candle: the leak happens here.
    public class BlowOutState : EnemyState
    {
        public const float Duration = 0.4f;
        public override bool TakesDamage => false;
        public override bool IsLeaving => true;

        public override void Enter()
        {
            Enemy.Skin.PlayBlowOut(Duration);
            GameEvents.RaiseEnemyLeaked(Enemy);
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed >= Duration) Fsm.Change(EnemyStateId.Despawn);
        }
    }

    // Killed: plays the death clip in place, then goes back to the pool.
    public class DyingState : EnemyState
    {
        public const float Duration = 0.6f;
        public override bool TakesDamage => false;
        public override bool IsLeaving => true;

        public override void Enter()
        {
            Enemy.StateSpeedMultiplier = 0f;
            Enemy.Skin.PlayDie(Duration);
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed >= Duration) Fsm.Change(EnemyStateId.Despawn);
        }
    }

    public class DespawnState : EnemyState
    {
        public override bool TakesDamage => false;
        public override bool IsLeaving => true;

        public override void Enter() => Enemy.ReturnToPool();
    }
}
