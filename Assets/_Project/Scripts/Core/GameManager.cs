using System.Globalization;
using TenCandles.Core;
using TenCandles.Lifetime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TenCandles
{
    // The state machine. Nothing else.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public LifetimeManager Lifetime { get; private set; }
        // 1..40. The age whose wave is next or in progress.
        public int Age => Lifetime != null ? Lifetime.Age : 0;
        public int FinalAge => Balance.TotalYears;
        public float IntermissionLeft { get; private set; }
        public float EarlyCallBonus => IntermissionLeft * Balance.EarlyCallBonusMul;
        public bool IsOver => State == GameState.Victory || State == GameState.Defeat;

        // Fixes the run seed (card draws, wave shuffles) for balance testing. Null: a new seed every run.
        public static int? SeedOverride;

        // "Play again" skips the main menu; "Main menu" does not.
        static bool skipMenuOnce;
        int startedFrame = -1;

        void Awake()
        {
            Instance = this;
            // "21.5 s", not "21,5 s", whatever the player's system language is.
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            StatRegistry.Reset();
            // Added at runtime so the existing scene needs no rebuild.
            Lifetime = GetComponent<LifetimeManager>();
            if (Lifetime == null) Lifetime = gameObject.AddComponent<LifetimeManager>();
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
            GameEvents.WishResolved += OnWishResolved;
        }

        void OnDisable()
        {
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.UpgradeChosen -= OnUpgradeChosen;
            GameEvents.TimeRanOut -= OnTimeRanOut;
            GameEvents.WishResolved -= OnWishResolved;
        }

        void Start()
        {
            Enter(GameState.Boot);
            if (skipMenuOnce)
            {
                skipMenuOnce = false;
                StartGame();
            }
        }

        void Update()
        {
            if (TimeControl.IsMenuPaused) return;

            // Boot is the main menu; MainMenuUI starts the game.
            switch (State)
            {
                case GameState.Intermission:
                    IntermissionLeft -= Time.deltaTime;
                    if (Input.GetKeyDown(KeyCode.Space) && Time.frameCount != startedFrame) CallWaveEarly();
                    else if (IntermissionLeft <= 0f) StartWave();
                    break;

                case GameState.Victory:
                case GameState.Defeat:
                    if (Input.GetKeyDown(KeyCode.R)) Restart();
                    else if (Input.GetKeyDown(KeyCode.M)) BackToMenu();
                    break;
            }
        }

        public void StartGame()
        {
            if (State != GameState.Boot) return;
            // The SPACE that pressed "Play" must not also call the first wave early.
            startedFrame = Time.frameCount;
            Lifetime.BeginRun(SeedOverride ?? System.Environment.TickCount);
            BeginIntermission();
        }

        public void CallWaveEarly()
        {
            if (State != GameState.Intermission) return;
            float bonus = EarlyCallBonus;
            if (bonus > 0.05f) CandleClock.Instance.Add(bonus, "You called it early");
            StartWave();
        }

        // Stay or Advance (spec §5). An illegal choice is ignored; the birthday screen shows why it is disabled.
        public void ChooseBirthday(BirthdayChoice choice)
        {
            if (State != GameState.Birthday) return;
            var build = BuildManager.Instance;
            if (!Lifetime.ResolveBirthday(choice, build != null ? build.Catalogue : null)) return;
            // The key that closed the birthday screen must not also call the next wave early.
            startedFrame = Time.frameCount;
            NextYear();
        }

        public void Restart()
        {
            skipMenuOnce = true;
            Reload();
        }

        public void BackToMenu()
        {
            skipMenuOnce = false;
            Reload();
        }

        static void Reload()
        {
            TimeControl.SetMenuPaused(false);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void BeginIntermission()
        {
            IntermissionLeft = Balance.IntermissionLength;
            Enter(GameState.Intermission);
        }

        void StartWave()
        {
            Enter(GameState.Wave);
            WaveManager.Instance.StartWave(Age);
        }

        void OnWaveCleared(int age)
        {
            if (IsOver) return;

            if (age >= FinalAge)
            {
                Enter(GameState.Victory);
                GameEvents.RaiseGameOver(true);
                return;
            }

            // Gifts only come every few years; other years go straight on.
            var levelUp = LevelUpManager.Instance;
            if (levelUp == null || !levelUp.IsOfferYear(age))
            {
                AfterCardPick();
                return;
            }
            Enter(GameState.LevelUp);
            if (!levelUp.Offer(age)) AfterCardPick();
        }

        void OnUpgradeChosen(UpgradeCard card)
        {
            if (State == GameState.LevelUp) AfterCardPick();
        }

        // Ages 10, 20 and 30 end a decade with a wish and then a birthday; every other age moves straight on.
        void AfterCardPick()
        {
            if (!LifetimeManager.IsBirthdayAge(Age))
            {
                NextYear();
                return;
            }
            Enter(GameState.Wish);
        }

        // Wish step 1 (§6): 0, 1, 3 or 5 candles. An illegal count is ignored; the wish screen shows why.
        public void BlowWishCandles(int candles)
        {
            if (State == GameState.Wish) Lifetime.Wish.Blow(candles);
        }

        // Wish step 2: the gift, from the offer drawn by BlowWishCandles.
        public void ChooseWishGift(int index)
        {
            if (State == GameState.Wish) Lifetime.Wish.Choose(index);
        }

        void OnWishResolved(int candles, WishGift gift)
        {
            if (State == GameState.Wish) OfferBirthday();
        }

        void OfferBirthday()
        {
            Enter(GameState.Birthday);
            BirthdayOptions options = Lifetime.OfferBirthday();
            GameEvents.RaiseBirthdayOffered(options.StayLegal, options.AdvanceLegal);
        }

        void NextYear()
        {
            Lifetime.AdvanceAge();
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
            TimeControl.SetPaused(next == GameState.LevelUp || next == GameState.Wish || next == GameState.Birthday || next == GameState.Defeat);
            GameEvents.RaiseStateChanged(next);
        }
    }
}
