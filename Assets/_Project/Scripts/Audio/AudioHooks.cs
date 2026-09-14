using TenCandles.Core;
using UnityEngine;

namespace TenCandles
{
    // Maps game events to sounds. Listens only.
    public class AudioHooks : MonoBehaviour
    {
        [SerializeField] SoundBank bank;
        [SerializeField] float towerFireVolume = 0.35f;
        [SerializeField] float hitVolume = 0.3f;

        static AudioManager Audio => AudioManager.Instance;

        void OnEnable()
        {
            if (bank == null) return;
            GameEvents.TowerFired += OnTowerFired;
            GameEvents.TowerBuilt += OnTowerBuilt;
            GameEvents.TowerUpgraded += OnTowerUpgraded;
            GameEvents.BuildFailed += OnBuildFailed;
            GameEvents.EnemyDamaged += OnEnemyDamaged;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.ProjectileImpact += OnImpact;
            GameEvents.CandleExtinguished += OnCandleOut;
            GameEvents.CandleRestored += OnCandleRestored;
            GameEvents.CandleCountChanged += OnCandleCountChanged;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.LevelUpOffered += OnLevelUpOffered;
            GameEvents.UpgradeChosen += OnUpgradeChosen;
            GameEvents.EraChanged += OnEraChanged;
            GameEvents.GameOver += OnGameOver;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.TowerFired -= OnTowerFired;
            GameEvents.TowerBuilt -= OnTowerBuilt;
            GameEvents.TowerUpgraded -= OnTowerUpgraded;
            GameEvents.BuildFailed -= OnBuildFailed;
            GameEvents.EnemyDamaged -= OnEnemyDamaged;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.ProjectileImpact -= OnImpact;
            GameEvents.CandleExtinguished -= OnCandleOut;
            GameEvents.CandleRestored -= OnCandleRestored;
            GameEvents.CandleCountChanged -= OnCandleCountChanged;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.LevelUpOffered -= OnLevelUpOffered;
            GameEvents.UpgradeChosen -= OnUpgradeChosen;
            GameEvents.EraChanged -= OnEraChanged;
            GameEvents.GameOver -= OnGameOver;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void Play(AudioClip clip, float volume = 1f, float pitchVariance = 0.1f)
        {
            if (Audio != null && bank != null) Audio.PlaySfx(clip, volume, pitchVariance);
        }

        void OnStateChanged(GameState state)
        {
            if (state != GameState.Intermission || Audio == null) return;
            Audio.PlayMusic(bank.musicLayers);
            var era = ThemeManager.Instance != null ? ThemeManager.Instance.CurrentEra : null;
            Audio.SetMusicLayers(era != null ? era.musicLayers : 1);
        }

        void OnTowerFired(Tower t)
        {
            if (bank == null || bank.towerFire == null) return;
            int i = (int)t.Data.kind;
            if (i < bank.towerFire.Length) Play(bank.towerFire[i], towerFireVolume);
        }

        void OnTowerBuilt(Tower t) => Play(bank.build);
        void OnTowerUpgraded(Tower t, int level) => Play(bank.upgrade);
        void OnBuildFailed(string reason) => Play(bank.buildFailed, 0.6f, 0f);
        void OnEnemyDamaged(Enemy e, float amount, bool crit) => Play(bank.enemyHit, hitVolume);

        void OnEnemyKilled(Enemy e, float reward)
        {
            Play(e.Data.isBoss && bank.bossDown != null ? bank.bossDown : bank.enemyPop, 0.6f);
        }

        void OnImpact(Vector3 pos, float radius, TowerKind kind)
        {
            if (radius > 0f) Play(bank.splash, 0.5f);
        }

        void OnCandleOut(int index)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsOver) return;
            Play(bank.candlePuff);
            Play(bank.crowdOhh, 0.6f);
        }

        void OnCandleRestored(int index) => Play(bank.candleRelight, 0.7f);
        int lastCandleCount = Balance.BaseCandleCount;

        // Only a gained candle gets the fanfare; a stage lowering the cap does not.
        void OnCandleCountChanged(int count)
        {
            if (count > lastCandleCount) Play(bank.newCandle, 1f, 0f);
            lastCandleCount = count;
        }
        void OnWaveStarted(int year) => Play(bank.waveStart, 0.8f, 0f);
        void OnWaveCleared(int year) => Play(bank.waveCleared, 1f, 0.03f);
        void OnLevelUpOffered(UpgradeCard[] cards) => Play(bank.levelUp, 0.9f, 0f);
        void OnUpgradeChosen(UpgradeCard card) => Play(bank.cardPicked, 0.9f, 0f);

        void OnEraChanged(int era)
        {
            var data = ThemeManager.Instance != null ? ThemeManager.Instance.CurrentEra : null;
            if (Audio == null || bank == null || data == null) return;
            Audio.SetMusicLayers(data.musicLayers);
            Audio.PlayAmbient(bank.crowdAmbient, data.ambientVolume);
        }

        void OnGameOver(bool victory)
        {
            if (Audio == null || bank == null) return;
            if (victory)
            {
                Audio.SetMusicLayers(bank.musicLayers != null ? bank.musicLayers.Length : 0);
                Play(bank.victory, 1f, 0f);
            }
            else
            {
                Audio.StopMusic(true);
                Play(bank.defeat, 1f, 0f);
            }
        }
    }
}
