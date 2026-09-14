using System;
using TenCandles.Lifetime;
using UnityEngine;

namespace TenCandles
{
    // Systems talk through these events instead of referencing each other.
    // UI, audio and FX only listen; they never touch game logic.
    public static class GameEvents
    {
        // Time
        public static event Action<float, float> TimeChanged;      // current, max
        public static event Action<float, string> TimeAdjusted;   // delta, reason shown to the player
        public static event Action<int> CandleExtinguished;        // candle index
        public static event Action<int> CandleRestored;            // candle index
        public static event Action<int> CandleCountChanged;        // new candle count
        public static event Action TimeRanOut;

        // Flow
        public static event Action<GameState> StateChanged;
        public static event Action<int> WaveStarted;               // age
        public static event Action<int> WaveCleared;               // age
        public static event Action<int> EraChanged;                // era index
        public static event Action<bool> GameOver;                 // victory

        // Lifetime
        public static event Action<bool, bool> BirthdayOffered;                 // stayLegal, advanceLegal
        public static event Action<BirthdayChoice, DecadeStage> BirthdayChosen; // choice, new stage
        public static event Action<DecadeStage, int> StageChanged;              // stage, tier

        // Enemies
        public static event Action<Enemy> EnemySpawned;
        public static event Action<Enemy, float, bool> EnemyDamaged; // enemy, amount, crit
        public static event Action<Enemy, float> EnemyKilled;        // enemy, reward
        public static event Action<Enemy> EnemyLeaked;

        // Towers
        public static event Action<Tower> TowerBuilt;
        public static event Action<Tower, int> TowerUpgraded;      // tower, new level
        public static event Action<Tower> TowerFired;
        public static event Action<Vector3, float, TowerKind> ProjectileImpact; // position, splash radius, kind
        public static event Action<string> BuildFailed;

        // Level up
        public static event Action<UpgradeCard[]> LevelUpOffered;
        public static event Action<UpgradeCard> UpgradeChosen;

        public static void RaiseTimeChanged(float current, float max) => TimeChanged?.Invoke(current, max);
        public static void RaiseTimeAdjusted(float delta, string reason) => TimeAdjusted?.Invoke(delta, reason);
        public static void RaiseCandleExtinguished(int index) => CandleExtinguished?.Invoke(index);
        public static void RaiseCandleRestored(int index) => CandleRestored?.Invoke(index);
        public static void RaiseCandleCountChanged(int count) => CandleCountChanged?.Invoke(count);
        public static void RaiseTimeRanOut() => TimeRanOut?.Invoke();

        public static void RaiseStateChanged(GameState state) => StateChanged?.Invoke(state);
        public static void RaiseWaveStarted(int year) => WaveStarted?.Invoke(year);
        public static void RaiseWaveCleared(int year) => WaveCleared?.Invoke(year);
        public static void RaiseEraChanged(int era) => EraChanged?.Invoke(era);
        public static void RaiseGameOver(bool victory) => GameOver?.Invoke(victory);

        public static void RaiseBirthdayOffered(bool stayLegal, bool advanceLegal) => BirthdayOffered?.Invoke(stayLegal, advanceLegal);
        public static void RaiseBirthdayChosen(BirthdayChoice choice, DecadeStage stage) => BirthdayChosen?.Invoke(choice, stage);
        public static void RaiseStageChanged(DecadeStage stage, int tier) => StageChanged?.Invoke(stage, tier);

        public static void RaiseEnemySpawned(Enemy e) => EnemySpawned?.Invoke(e);
        public static void RaiseEnemyDamaged(Enemy e, float amount, bool crit) => EnemyDamaged?.Invoke(e, amount, crit);
        public static void RaiseEnemyKilled(Enemy e, float reward) => EnemyKilled?.Invoke(e, reward);
        public static void RaiseEnemyLeaked(Enemy e) => EnemyLeaked?.Invoke(e);

        public static void RaiseTowerBuilt(Tower t) => TowerBuilt?.Invoke(t);
        public static void RaiseTowerUpgraded(Tower t, int level) => TowerUpgraded?.Invoke(t, level);
        public static void RaiseTowerFired(Tower t) => TowerFired?.Invoke(t);
        public static void RaiseProjectileImpact(Vector3 pos, float radius, TowerKind kind) => ProjectileImpact?.Invoke(pos, radius, kind);
        public static void RaiseBuildFailed(string reason) => BuildFailed?.Invoke(reason);

        public static void RaiseLevelUpOffered(UpgradeCard[] cards) => LevelUpOffered?.Invoke(cards);
        public static void RaiseUpgradeChosen(UpgradeCard card) => UpgradeChosen?.Invoke(card);
    }
}
