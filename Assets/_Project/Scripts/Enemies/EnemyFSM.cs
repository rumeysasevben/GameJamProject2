using System.Collections.Generic;

namespace TenCandles
{
    public enum EnemyStateId
    {
        Spawn,
        Walk,
        Stagger,
        Sprint,
        ReachEnd,
        Attack,
        BlowOut,
        Dying,
        Despawn
    }

    // Owns state transitions only. Damage lives on Enemy, visuals on EnemySkin.
    public class EnemyFSM
    {
        readonly Dictionary<EnemyStateId, EnemyState> states = new Dictionary<EnemyStateId, EnemyState>();

        public EnemyState Current { get; private set; }
        public EnemyStateId CurrentId { get; private set; }

        public EnemyFSM(Enemy enemy)
        {
            Add(EnemyStateId.Spawn, new SpawnState(), enemy);
            Add(EnemyStateId.Walk, new WalkState(), enemy);
            Add(EnemyStateId.Stagger, new StaggerState(), enemy);
            Add(EnemyStateId.Sprint, new SprintState(), enemy);
            Add(EnemyStateId.ReachEnd, new ReachEndState(), enemy);
            Add(EnemyStateId.Attack, new AttackState(), enemy);
            Add(EnemyStateId.BlowOut, new BlowOutState(), enemy);
            Add(EnemyStateId.Dying, new DyingState(), enemy);
            Add(EnemyStateId.Despawn, new DespawnState(), enemy);
        }

        void Add(EnemyStateId id, EnemyState state, Enemy enemy)
        {
            state.Bind(enemy, this);
            states[id] = state;
        }

        // Re-entering the current state is allowed; it restarts the state's timer.
        public void Change(EnemyStateId id)
        {
            Current?.Exit();
            CurrentId = id;
            Current = states[id];
            Current.ResetTimer();
            Current.Enter();
        }

        public void Tick(float dt) => Current?.Tick(dt);
    }
}
