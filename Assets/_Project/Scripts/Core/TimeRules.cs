using TenCandles.Core;
using UnityEngine;

namespace TenCandles
{
    // Turns game events into time gains and losses. All of them go through CandleClock.
    public class TimeRules : MonoBehaviour
    {
        [SerializeField] float leakHitStop = 0.6f;
        [SerializeField, Range(0f, 1f)] float leakHitStopScale = 0.1f;
        [SerializeField] float lastBreathBonus = 45f;

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.EnemyLeaked += OnEnemyLeaked;
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.TimeChanged += OnTimeChanged;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.EnemyLeaked -= OnEnemyLeaked;
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.TimeChanged -= OnTimeChanged;
        }

        static CandleClock Clock => CandleClock.Instance;

        void OnEnemyKilled(Enemy enemy, float reward)
        {
            if (Clock == null) return;
            float bonus = enemy.Data.killBonusTime;
            Clock.Add(reward + StatRegistry.KillBonus + bonus, bonus > 0f ? "You beat the boss!" : null);
        }

        void OnEnemyLeaked(Enemy enemy)
        {
            if (Clock == null) return;
            float penalty = Mathf.Max(0f, Balance.LeakPenalty - StatRegistry.LeakPenaltyReduction);
            Clock.Drain(penalty, "You lost a candle");
            TimeControl.HitStop(leakHitStop, leakHitStopScale);
        }

        void OnWaveCleared(int year)
        {
            if (Clock == null || StatRegistry.WaveEndBonus <= 0f) return;
            Clock.Add(StatRegistry.WaveEndBonus, "Anniversary");
        }

        void OnTimeChanged(float current, float max)
        {
            if (!StatRegistry.LastBreathArmed || current <= 0f || current > Balance.SecondsPerCandle) return;
            StatRegistry.LastBreathArmed = false;
            Clock.Add(lastBreathBonus, "Last Breath");
        }
    }
}
