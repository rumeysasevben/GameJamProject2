using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TenCandles
{
    // The state machine. Nothing else.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] float intermissionDuration = 10f;
        [SerializeField] float earlyCallBonusFactor = 0.5f;
        [SerializeField] int finalYear = 10;

        public GameState State { get; private set; } = GameState.Boot;
        public int Year { get; private set; }
        public int FinalYear => finalYear;
        public float IntermissionLeft { get; private set; }
        public float EarlyCallBonus => IntermissionLeft * earlyCallBonusFactor;
        public bool IsOver => State == GameState.Victory || State == GameState.Defeat;

        void Awake()
        {
            Instance = this;
            // "21.5 s", not "21,5 s", whatever the player's system language is.
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            StatRegistry.Reset();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.UpgradeChosen += OnUpgradeChosen;
            GameEvents.TimeRanOut += OnTimeRanOut;
        }

        void OnDisable()
        {
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.UpgradeChosen -= OnUpgradeChosen;
            GameEvents.TimeRanOut -= OnTimeRanOut;
        }

        void Start() => Enter(GameState.Boot);

        void Update()
        {
            switch (State)
            {
                case GameState.Boot:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) StartGame();
                    break;

                case GameState.Intermission:
                    IntermissionLeft -= Time.deltaTime;
                    if (Input.GetKeyDown(KeyCode.Space)) CallWaveEarly();
                    else if (IntermissionLeft <= 0f) StartWave();
                    break;

                case GameState.Victory:
                case GameState.Defeat:
                    if (Input.GetKeyDown(KeyCode.R)) Restart();
                    break;
            }
        }

        public void StartGame()
        {
            if (State != GameState.Boot) return;
            Year = 1;
            BeginIntermission();
        }

        public void CallWaveEarly()
        {
            if (State != GameState.Intermission) return;
            float bonus = EarlyCallBonus;
            if (bonus > 0.05f) CandleClock.Instance.Add(bonus, "You called it early");
            StartWave();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void BeginIntermission()
        {
            IntermissionLeft = intermissionDuration;
            Enter(GameState.Intermission);
        }

        void StartWave()
        {
            Enter(GameState.Wave);
            WaveManager.Instance.StartWave(Year);
        }

        void OnWaveCleared(int year)
        {
            if (IsOver) return;

            if (year >= finalYear)
            {
                Enter(GameState.Victory);
                GameEvents.RaiseGameOver(true);
                return;
            }

            Enter(GameState.LevelUp);
            if (!LevelUpManager.Instance.Offer(year)) NextYear();
        }

        void OnUpgradeChosen(UpgradeCard card)
        {
            if (State == GameState.LevelUp) NextYear();
        }

        void NextYear()
        {
            Year++;
            BeginIntermission();
        }

        void OnTimeRanOut()
        {
            if (IsOver) return;
            Enter(GameState.Defeat);
            GameEvents.RaiseGameOver(false);
        }

        void Enter(GameState next)
        {
            State = next;
            var clock = CandleClock.Instance;
            if (clock != null) clock.IsDraining = next == GameState.Intermission || next == GameState.Wave;
            TimeControl.SetPaused(next == GameState.LevelUp || next == GameState.Defeat);
            GameEvents.RaiseStateChanged(next);
        }
    }
}
