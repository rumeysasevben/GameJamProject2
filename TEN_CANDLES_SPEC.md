# TEN CANDLES — Implementation Spec v2 ("Lifetime")

**Engine:** Unity 6.3 LTS · 2D URP · C\# · **Root namespace:** `TenCandles` **Target:** Steam (Windows first), plus WebGL demo build **This document is the single source of truth for gameplay behaviour and balance.**

---

## 0\. Agent working rules

Read this section before writing any code.

1. **Implement one Phase at a time** (§15). Do not begin Phase N+1 until every acceptance criterion of Phase N passes. State which Phase you are on before editing files.  
2. **Run the Asset Inventory step (§14.1) before implementing any tower or enemy.** Never invent a tower or enemy that is not in §9 / §10.  
3. **Never hardcode a gameplay number.** Every number in §2 lives in `Balance.cs` and is referenced from there. If a value you need is not in §2, add it to `Balance.cs` and note it in your summary — do not inline it.  
4. **Data over code.** Adding a new tower or enemy must require creating a `.asset` file and nothing else. If a design needs a code branch per tower, model it as an effect/ability component referenced by the ScriptableObject instead.  
5. **Do not refactor outside the current Phase's scope.** If you find a problem elsewhere, list it in your summary; do not fix it silently.  
6. **`CandleClock` is the only writer of `timeRemaining`.** Any other class that needs to change it calls a `CandleClock` method. Enforce with a private setter.  
7. **Systems communicate through `GameEvents` (§5.3).** UI, audio and VFX subscribe; they never call gameplay code and gameplay never calls them directly.  
8. **No new third-party packages** without asking. DOTween is already in the project and may be used.  
9. **WebGL constraint:** no `System.Threading`, no reflection-heavy serialization, pool anything that spawns more than \~20 instances per wave.  
10. When a spec value and existing code disagree, **the spec wins** — change the code and say so.

### 0.1 Conventions

| Thing | Convention | Example |
| :---- | :---- | :---- |
| Namespace | `TenCandles.<Area>` | `TenCandles.Waves` |
| ScriptableObject id fields | `snake_case` string | `confetti_cannon` |
| Asset filenames | `<Type>_<id>.asset` | `Tower_confetti_cannon.asset` |
| Prefabs | `PascalCase` | `ConfettiCannon.prefab` |
| Enum members | `PascalCase` | `DecadeStage.Childhood` |
| Private fields | `_camelCase` | `_timeRemaining` |
| Serialized private | `[SerializeField] private` | — |

### 0.2 Folder layout

Assets/\_Project/

  Scripts/

    Core/        GameManager, GameState, CandleClock, GameEvents, Balance, RunState

    Lifetime/    LifetimeManager, DecadeStage, BirthdayController, WishSystem

    Waves/       WaveManager, WaveGenerator, EnemySpawner

    Path/        PathRoute, PathVariantSet, Waypoint

    Towers/      BuildManager, TowerSlot, Tower, TowerData, TowerAbility, Projectile, AdjacencyResolver

    Enemies/     Enemy, EnemyData, EnemyFsm, states/, EnemyAbility, EnemySkin

    Cards/       CardManager, UpgradeCard, UpgradeEffectType, StatRegistry

    Meta/        LegacyStore, LegacyNode, HardYears, RunScorer, SaveSystem

    Theme/       StageThemeManager, StageThemeData, WindController

    UI/          CandleBarUI, BuildPanelUI, BirthdayUI, WishUI, CardUI, HudUI, EndScreenUI

    FX/          FxManager, ObjectPool, CameraShake, HitFlash, DamagePopup

    Audio/       AudioManager, AudioHooks

    Editor/      DataAssetGenerator, AssetInventoryTool, BalanceSimulator

  Data/

    Towers/  Enemies/  Cards/  Waves/  Stages/  Legacy/

  Art/         (see §14)

  Audio/  Prefabs/  Scenes/

---

## 1\. What changes from v1

v1 was a single 10-wave run. v2 makes a run **a lifetime**.

|  | v1 | v2 |
| :---- | :---- | :---- |
| Run length | 10 waves, \~5 min | **40 waves, \~22 min** |
| Structure | flat | **4 decades × 10 years** |
| Player choice | upgrade card each wave | \+ **Stay/Advance at ages 10, 20, 30** and **Wish** |
| Towers | 4 | **12** |
| Enemies | 4 \+ boss | **13 \+ 4 stage bosses** |
| Cards | 14 | **30+** |
| Between runs | none | **Legacy tree, stage unlocks** |
| After winning | none | **Hard Years (15 tiers), Legend mode, leaderboard** |

The v1 core — `timeRemaining` as the single resource, 10 candles as its visualisation, event-driven systems, `PathRoute` waypoints, tower/enemy ScriptableObjects — is unchanged and is reused.

### 1.1 Core loop (unchanged from v1, restated)

`timeRemaining` is simultaneously the clock, the currency and the health pool. It drains continuously. Killing enemies adds to it. Building and upgrading towers subtracts from it. An enemy reaching the end subtracts a full candle. It hits zero → the run ends.

---

## 2\. Balance.cs — authoritative constants

Create `Assets/_Project/Scripts/Core/Balance.cs` exactly as below. Every other number in this spec is derived from these.

namespace TenCandles.Core

{

    public static class Balance

    {

        // \---------- Candle clock \----------

        public const float SecondsPerCandle   \= 30f;

        public const int   BaseCandleCount    \= 10;

        public const float StartTime          \= 300f;   // \= 10 candles

        public const float DrainRatePerSecond \= 1f;

        public const float LeakPenalty        \= 30f;    // one full candle

        public const float IntermissionLength \= 9f;

        public const float EarlyCallBonusMul  \= 0.5f;   // × remaining intermission

        // \---------- Lifetime \----------

        public const int YearsPerDecade      \= 10;

        public const int DecadesPerRun       \= 4;

        public const int TotalYears          \= 40;      // YearsPerDecade \* DecadesPerRun

        public const int MaxVisitsPerStage   \= 2;       // legality rule for Stay

        public const float StayHpMultiplier  \= 1.30f;   // \+30% enemy HP when Stay chosen

        // Candle cap per tier index 0..3 (the run's 1st..4th decade, NOT the biome).

        // Indexed by RunState.CurrentTier so Stay cannot dodge ageing.

        public static readonly int\[\] TierCandleCap \= { 10, 10, 9, 7 };

        // Lanes the map may use per tier index 0..3. Active lanes \= min(map entrance count, TierLaneAllowance\[CurrentTier\]).

        public static readonly int\[\] TierLaneAllowance \= { 1, 2, 2, 2 };

        // In the decade a lane first opens, its share of each wave ramps from Start (year 1) to End (year 10); End after that (§12.2).

        public const float NewLaneShareStart \= 0.25f;

        public const float NewLaneShareEnd   \= 0.50f;

        // Mastery passives, granted by Stay with the mastery tower and kept for the rest of the run (§5.1).

        public const int   LongSummerCandles       \= 1;      // Childhood: MaxCandlesAdd

        public const float BoundlessEnergyFireRate \= 0.15f;  // Youth: TowerFireRateMul

        public const float SavingsKillReward       \= 0.20f;  // Adulthood: KillRewardMul

        // \---------- Wave scaling \----------

        // d \= stage index 0..3 (difficulty tier, NOT which biome)

        // y \= year within the decade, 1..10

        public const float BaseEnemyHp \= 10f;

        public static readonly float\[\] StageHpMultiplier \= { 1.00f, 2.60f, 6.80f, 17.50f };

        public const float YearHpStep      \= 0.18f;

        public const float YearSpeedStep   \= 0.03f;

        public const float StageSpeedStep  \= 0.05f;

        public const float RewardBase      \= 1.10f;

        public const float RewardYearStep  \= 0.16f;

        public const float RewardStageMul  \= 0.55f;

        public const int   CountBase       \= 6;

        public const int   CountYearStep   \= 2;

        public const int   CountStageStep  \= 3;

        public const float GapBase         \= 1.15f;

        public const float GapYearStep     \= 0.05f;

        public const float GapStageStep    \= 0.04f;

        public const float GapMinimum      \= 0.42f;

        // \---------- Age-derived \----------

        public const int TowerCapacityBase \= 3;       // invariant: capacity \>= 2 × active lanes (§7)

        public const int TowerCapacityPerAge \= 8;       // capacity \= Base \+ age / PerAge

        // \---------- Tower upgrades \----------

        // index 0 unused; levels are 1..5

        public static readonly float\[\] LevelDamageMultiplier \= { 0f, 1.00f, 1.50f, 2.20f, 3.20f, 4.60f };

        public static readonly float\[\] LevelCostMultiplier   \= { 0f, 1.00f, 0.75f, 1.20f, 1.80f, 2.60f };

        public static readonly float\[\] LevelRangeMultiplier  \= { 0f, 1.00f, 1.06f, 1.12f, 1.18f, 1.25f };

        public const int MaxTowerLevel \= 5;

        // \---------- Wish \----------

        // candles blown \-\> tier. Cost \= candles \* SecondsPerCandle

        public static readonly int\[\] WishCandleOptions \= { 0, 1, 3, 5 };

        // \---------- Wind \----------

        public const float WindTelegraphSeconds \= 3f;

        public const float WindDrainMultiplier  \= 2f;

        // \---------- Cards \----------

        public const int CardsOfferedPerYear \= 3;

        public const int RareUnlockAge       \= 12;

        public const int EpicUnlockAge       \= 20;

        // \---------- Adjacency \----------

        public const float AdjacencyRadius \= 1.5f;      // world units, slot centre to slot centre

        // \---------- Meta \----------

        public const int MemoryPerAge        \= 1;

        public const int MemoryPerStageVisit \= 10;

        public const int MemoryPerWish       \= 5;

        public const int HardYearsMaxTier    \= 15;

        // \---------- Balance harness \----------

        // Seconds on the road after an enemy's spawn slot: Duration \= Count × Gap \+ SimWalkSeconds (§8).

        public const float SimWalkSeconds \= 7f;

    }

}

### 2.1 Derived formulas

Implement in `WaveGenerator` as pure static functions. They must match exactly — the tables in §8 were generated from them.

int   Count (int d, int y) \=\> Balance.CountBase \+ Balance.CountYearStep \* y

                                                \+ Balance.CountStageStep \* d;

float Hp    (int d, int y) \=\> Balance.BaseEnemyHp

                            \* Balance.StageHpMultiplier\[d\]

                            \* (1f \+ Balance.YearHpStep \* (y \- 1));

float Speed (int d, int y) \=\> 1f \+ Balance.YearSpeedStep \* (y \- 1\)

                                 \+ Balance.StageSpeedStep \* d;

float Reward(int d, int y) \=\> (Balance.RewardBase \+ Balance.RewardYearStep \* y)

                            \* (1f \+ Balance.RewardStageMul \* d);

float Gap   (int d, int y) \=\> Mathf.Max(Balance.GapMinimum,

                              Balance.GapBase \- Balance.GapYearStep \* y

                                              \- Balance.GapStageStep \* d);

> `d` is the **difficulty tier** (0-based slot index in the run), not the biome identity. If the player chooses Stay, the next decade reuses the same biome but `d` still increments, and enemy HP is additionally multiplied by `Balance.StayHpMultiplier`. Everything that represents ageing — `TierCandleCap`, `TierLaneAllowance` and the wave formulas — is indexed by `d`, never by stage.

---

## 3\. CandleClock

`Assets/_Project/Scripts/Core/CandleClock.cs`

### 3.1 Responsibilities

Owns `timeRemaining`. Nothing else may write it.

public sealed class CandleClock : MonoBehaviour

{

    public float TimeRemaining { get; private set; }

    public float MaxTime       { get; private set; }   // \= CandleCap \* SecondsPerCandle

    public int   CandleCap     { get; private set; }   // current cap in candles

    public bool  TrySpend(float seconds, string reason);   // false if insufficient; does not go negative

    public void  Add(float seconds, string reason);        // clamped to MaxTime; surplus is discarded

    public void  ApplyLeak();                              // subtracts Balance.LeakPenalty

    public void  SetCandleCap(int candles);                // stage change or card effect

    public void  SetDrainMultiplier(float m);              // wind, pause (0), Level-Up screen (0)

    public int   FullCandles  \=\> Mathf.FloorToInt(TimeRemaining / Balance.SecondsPerCandle);

    public float PartialFill  \=\> (TimeRemaining % Balance.SecondsPerCandle) / Balance.SecondsPerCandle;

}

### 3.2 Invariants (assert these in a test)

- `0 <= TimeRemaining <= MaxTime` at all times.  
- `MaxTime == CandleCap * Balance.SecondsPerCandle`.  
- `TrySpend` returns false and changes nothing if `seconds > TimeRemaining`.  
- `Add` never raises `TimeRemaining` above `MaxTime`; the discarded amount is reported via `GameEvents.TimeOverflow(float wasted)` for telemetry and UI feedback.  
- Lowering `CandleCap` clamps `TimeRemaining` down to the new `MaxTime` immediately.

### 3.3 Candle visualisation

`CandleBarUI` renders `CandleCap` candle objects. **Do not hardcode 10** — the cap changes per stage (§2) and the `EleventhCandle` card can raise it.

for i in 0 .. CandleCap-1:

    if      i \<  FullCandles : fill \= 1

    else if i \== FullCandles : fill \= PartialFill

    else                     : fill \= 0

Implementation notes carried over from v1, still required:

- Candle sprite pivot **bottom-centre**; melt by scaling `localScale.y`.  
- The flame is **not** a child of the scaled candle transform — it is a sibling repositioned each frame to the candle's current top, otherwise it squashes.  
- Lerp the visual height toward the target, **except** on a leak, where it must snap so the hit reads.  
- On a candle going out, raise `GameEvents.CandleExtinguished(index)` once. UI, audio, FX and the screen-darkening all subscribe to that single event.

---

## 4\. Run structure — Lifetime

### 4.1 Types

public enum DecadeStage { Childhood \= 0, Youth \= 1, Adulthood \= 2, OldAge \= 3 }

public enum BirthdayChoice { Stay, Advance }

\[Serializable\]

public sealed class DecadeSlot

{

    public int          TierIndex;     // d, 0..3 — drives difficulty

    public DecadeStage  Stage;         // which biome/content set

    public bool         IsRepeat;      // true if the previous slot had the same Stage

    public int          AgeFrom;       // TierIndex\*10 \+ 1

    public int          AgeTo;         // TierIndex\*10 \+ 10

}

public sealed class RunState

{

    public DecadeSlot\[\]        Slots \= new DecadeSlot\[4\];

    public List\<BirthdayChoice\> Choices \= new();      // length 0..3

    public int   CurrentAge;                           // 1..40

    public int   CurrentTier \=\> (CurrentAge \- 1\) / 10;

    public int   YearInDecade \=\> ((CurrentAge \- 1\) % 10\) \+ 1;

    public HashSet\<DecadeStage\> VisitedStages \= new();

    public List\<string\> Masteries \= new();             // tower ids granted by Stay

    public int   WishesMade;

    public int   HardYearsTier;

    public int   Seed;

    \[NonSerialized\] public System.Random Rng;         // new System.Random(Seed); the only source of run randomness (§13.3)

}

### 4.2 Game state machine

`GameManager` owns this. Exactly one state is active.

Boot → Intermission → Wave → CardPick → Intermission → ... 

                              ↘ (at age 10/20/30, after CardPick) → Wish → Birthday → Intermission

                              ↘ (at age 40\) → Victory

any → Defeat   (TimeRemaining \<= 0\)

| State | Drain multiplier | Input allowed |
| :---- | :---- | :---- |
| `Boot` | 0 | none |
| `Intermission` | 1 | build, upgrade, early-call |
| `Wave` | 1 (×2 during wind) | build, upgrade |
| `CardPick` | 0 | pick 1 of 3 |
| `Wish` | 0 | pick candle count, then a gift |
| `Birthday` | 0 | Stay or Advance |
| `Victory` / `Defeat` | 0 | none |

> Time **does** drain during Intermission. Waiting is not free — this is deliberate and is what makes early-calling a real decision.

### 4.3 Run construction

Slot 0 is always `{ TierIndex = 0, Stage = Childhood }`. Each birthday appends one slot:

DecadeStage Next(DecadeStage s, BirthdayChoice c)

    \=\> c \== BirthdayChoice.Stay ? s : (DecadeStage)Mathf.Min(3, (int)s \+ 1);

---

## 5\. Birthday: Stay / Advance

### 5.1 Rules

Triggered after the year-10 card pick of each decade, i.e. at ages 10, 20, 30\. Not at 40\.

| Choice | Effect | Cost | Reward |
| :---- | :---- | :---- | :---- |
| **Stay** | Next decade reuses the same `DecadeStage`; `TierIndex` still increments | Enemy HP × `Balance.StayHpMultiplier` (1.30) for that decade | Grants that stage's **Mastery tower** (§9) and its **mastery passive** (below), both permanently for the run |
| **Advance** | Next decade uses `Stage + 1` | — | Unlocks that stage's **Visit tower(s)** (§9) on the first visit; reward rate rises via `d`. Old Age has two visit towers |

**Mastery passives.** Birthdays happen at ages 10, 20 and 30 only, so Stay can never target Old Age: there are exactly three passives. A passive is a permanent stat entry applied through the card pipeline (`UpgradeEffect` → `StatRegistry`), granted together with the mastery tower and kept for the rest of the run. It is not a card: it takes no card slot and never counts toward `maxStacks`.

| Stage | Passive | Effect | Value (`Balance`) |
| :---- | :---- | :---- | ----: |
| Childhood | **Long Summer** | `MaxCandlesAdd` | \+1 (`LongSummerCandles`) |
| Youth | **Boundless Energy** | `TowerFireRateMul` | \+0.15 (`BoundlessEnergyFireRate`) |
| Adulthood | **Savings** | `KillRewardMul` | \+0.20 (`SavingsKillReward`) |

A Long Summer candle is a bonus candle: it sits on top of `TierCandleCap` at every later tier, like a card-granted candle.

**Old Age payoff.** Old Age is the only stage with no Stay option and only `A A A` reaches it, so entering it grants **both** of its towers (`memory_lantern` and `hourglass`) as visit towers. This gives the one life with no masteries a concrete reward, and no tower in §9.2 is unreachable.

**Legality:** `Stay` is disabled when it would produce a third consecutive slot of the same stage (`Balance.MaxVisitsPerStage`). `Advance` is disabled when the current stage is already `OldAge` (unreachable in a 4-slot run, but assert it).

Neither choice changes the ageing curve: the candle cap (`TierCandleCap`) and lane allowance (`TierLaneAllowance`) follow the tier, so a Stay decade has the same cap and lanes as an Advance decade at the same age.

### 5.2 The five legal lives

Enumerate these in a test. `S` \= Stay, `A` \= Advance.

| Choices | Composition | Name | Masteries | Passives | Visit towers |
| :---- | :---- | :---- | :---- | :---- | :---- |
| `A A A` | Childhood · Youth · Adulthood · OldAge | **Full Life** | none | none | whistle, music\_box, camera\_flash, memory\_lantern, hourglass |
| `A A S` | Childhood · Youth · Adulthood × 2 | **Late Bloomer** | champagne | Savings | whistle, music\_box, camera\_flash |
| `A S A` | Childhood · Youth × 2 · Adulthood | **Long Youth** | searchlight | Boundless Energy | whistle, music\_box, camera\_flash |
| `S A A` | Childhood × 2 · Youth · Adulthood | **Slow Childhood** | balloon\_trap | Long Summer | whistle, music\_box, camera\_flash |
| `S A S` | Childhood × 2 · Youth × 2 | **Never Grew Up** | balloon\_trap, searchlight | Long Summer, Boundless Energy | whistle, music\_box |

`A A A` is the only path that reaches `OldAge`. Combinations `S S *` and `A S S` are illegal by the max-visits rule.

### 5.3 GameEvents

`Assets/_Project/Scripts/Core/GameEvents.cs` — static class. **Convention (matches the codebase):** each event is a `public static event Action<…> Name` with no `On` prefix, raised only through a matching `RaiseName(…)` method. Subscribers name their handlers `OnName`. Do not rename existing events to add a prefix.

Implemented:

| Event | Arguments | Notes |
| :---- | :---- | :---- |
| `TimeChanged` | (float current, float max) | |
| `TimeAdjusted` | (float delta, string reason) | every labelled gain or loss, for popups |
| `CandleExtinguished` | (int index) | |
| `CandleRestored` | (int index) | |
| `CandleCountChanged` | (int newCap) | the candle cap changed (tier change or card) |
| `TimeRanOut` | () | |
| `StateChanged` | (GameState state) | |
| `WaveStarted` | (int age) | tier and year in decade derive from age |
| `WaveCleared` | (int age) | |
| `EraChanged` | (int era) | v1 visual eras; replaced by `StageChanged` in Phase 6 |
| `GameOver` | (bool victory) | age reached is `GameManager.Age` |
| `BirthdayOffered` | (bool stayLegal, bool advanceLegal) | reasons are on `LifetimeManager.Options` |
| `BirthdayChosen` | (BirthdayChoice choice, DecadeStage newStage) | |
| `StageChanged` | (DecadeStage stage, int tier) | |
| `EnemySpawned` | (Enemy e) | |
| `EnemyDamaged` | (Enemy e, float amount, bool crit) | |
| `EnemyKilled` | (Enemy e, float reward) | reward already includes `KillRewardMul` |
| `EnemyLeaked` | (Enemy e) | |
| `TowerBuilt` | (Tower t) | |
| `TowerUpgraded` | (Tower t, int newLevel) | |
| `TowerFired` | (Tower t) | |
| `ProjectileImpact` | (Vector3 position, float splashRadius, TowerKind kind) | |
| `BuildFailed` | (string reason) | |
| `LevelUpOffered` | (UpgradeCard\[\] cards) | the card offer (§11.3) |
| `UpgradeChosen` | (UpgradeCard card) | |

Planned, added by the phase that needs them, same convention:

| Event | Arguments | Phase |
| :---- | :---- | :---- |
| `TimeOverflow` | (float wasted) | 3 |
| `WishResolved` | (int candlesBlown, WishGift gift) | 3 |
| `WindWarning` | (float seconds) | 6 |
| `WindStarted` | (float duration) | 6 |
| `WindEnded` | () | 6 |

---

## 6\. Wish

Shown at each birthday, **before** the Stay/Advance screen.

| Candles blown | Cost | Gift tier | Choices offered |
| ----: | ----: | :---- | ----: |
| 0 | 0 s | none | — |
| 1 | 30 s | Small | 3 |
| 3 | 90 s | Large | 3 |
| 5 | 150 s | Legendary | 3 |

Rules:

- An option is selectable only if `CandleClock.TimeRemaining >= cost`.  
- The gift is a `UpgradeCard` drawn from the `Lifetime` rarity pool (§11), which is **only** reachable through wishes.  
- The spend goes through `CandleClock.TrySpend(cost, "wish")`.  
- `GameEvents.WishResolved` fires in all cases, including 0 candles.

Design intent: this is the signature moment of the game. Blowing out candles must be spectacular — VFX, audio, a hard beat — because the player is trading lifespan for power.

---

## 7\. Age-derived stats

int TowerCapacity(int age) \=\> Balance.TowerCapacityBase \+ age / Balance.TowerCapacityPerAge;

int ActiveLanes(int mapEntrances, int tier) \=\> Mathf.Min(mapEntrances, Balance.TierLaneAllowance\[tier\]);

| Age | Tower capacity | Candle cap (by tier) | Lane allowance (by tier) |
| ----: | ----: | ----: | ----: |
| 1–7 | 3 | 10 | 1 |
| 8–15 | 4 | 10 | 1 / 2 |
| 16–23 | 5 | 10 / 9 | 2 |
| 24–31 | 6 | 9 / 7 | 2 |
| 32–39 | 7 | 7 | 2 |
| 40 | 8 | 7 | 2 |

**Invariant:** tower capacity must always be ≥ 2 × active lanes, so every open lane can be covered by at least two towers. The tightest point is ages 11–15 (capacity 4, two lanes). Checked by `BalanceSimulator` (§17, check 7) against the lane allowance, i.e. the worst case of a map with enough entrances.

Card rarity gate by age: `Common` always; `Rare` from `Balance.RareUnlockAge` (12); `Epic` from `Balance.EpicUnlockAge` (20); `Lifetime` only from wishes.

> The intended tension: capacity rises while the candle cap falls. In the fourth decade the player fields 8 towers on a 210-second wallet. Power up, fragility up.

---

## 8\. Wave table

Generated from §2.1. Use this table as the regression fixture for `BalanceSimulator`.

| Tier | Year | Age | Count | Unit HP | Wave HP | Duration s | Required DPS | Reward/kill | Wave income |
| ----: | ----: | ----: | ----: | ----: | ----: | ----: | ----: | ----: | ----: |
| 0 | 1 | 1 | 8 | 10 | 80 | 16 | 5 | 1.26 | 10 |
| 0 | 5 | 5 | 16 | 17 | 275 | 21 | 13 | 1.90 | 30 |
| 0 | 10 | 10 | 26 | 26 | 681 | 24 | 29 | 2.70 | 70 |
| 1 | 1 | 11 | 11 | 26 | 286 | 19 | 15 | 1.95 | 21 |
| 1 | 5 | 15 | 19 | 45 | 850 | 23 | 36 | 2.95 | 56 |
| 1 | 10 | 20 | 29 | 68 | 1 975 | 25 | 80 | 4.19 | 121 |
| 2 | 1 | 21 | 14 | 68 | 952 | 21 | 45 | 2.65 | 37 |
| 2 | 5 | 25 | 22 | 117 | 2 573 | 25 | 103 | 3.99 | 88 |
| 2 | 10 | 30 | 32 | 178 | 5 701 | 25 | 226 | 5.67 | 181 |
| 3 | 1 | 31 | 17 | 175 | 2 975 | 24 | 126 | 3.34 | 57 |
| 3 | 5 | 35 | 25 | 301 | 7 525 | 26 | 284 | 5.04 | 126 |
| 3 | 10 | 40 | 35 | 458 | 16 048 | 26 | **628** | 7.16 | 250 |

**Run totals:** 860 enemies · 942 s of waves · \~1302 s including intermissions (**\~22 min**) · 3 464 s total kill income.

Two properties this curve must keep — verify them in `BalanceSimulator`:

1. **Sawtooth.** Required DPS drops at each decade boundary (29 → 15, 80 → 45, 226 → 126\) and then climbs past the previous peak. The dip is the player's room to learn a new biome's enemies.  
2. **Continuity of unit HP.** `Hp(d,10) ≈ Hp(d+1,1)` (26→26, 68→68, 178→175). Individual enemies never get suddenly weaker; only the wave's shape resets.

### 8.1 Wave composition

`WaveGenerator` picks the enemy mix from the active stage's `StageThemeData.enemyPool`, weighted:

| Year in decade | Composition rule |
| :---- | :---- |
| 1–2 | Basic type only |
| 3–4 | Basic \+ one stage-specific type |
| 5–6 | Basic \+ two stage-specific types |
| 7–9 | Full stage pool, weights shift toward the harder types |
| 10 | Full pool \+ **stage boss** entering at 60 % of the wave |

---

## 9\. Towers

### 9.1 TowerData schema

\[CreateAssetMenu(menuName \= "TenCandles/Tower")\]

public sealed class TowerData : ScriptableObject

{

    public string        id;                 // snake\_case, unique, matches art folder

    public string        displayName;

    \[TextArea\] public string description;

    public TowerRole     role;

    public UnlockSource  unlock;             // Starting | StageVisit | StageMastery

    public DecadeStage   unlockStage;        // ignored when unlock \== Starting

    \[Header("Combat")\]

    public float baseCost;                   // seconds

    public float damage;

    public float fireRate;                   // shots per second

    public float range;                      // world units

    public float splashRadius;               // 0 \= single target

    public int   pierce;                     // 0 \= no pierce

    public int   chainTargets;               // 0 \= no chain

    \[Header("Visual")\]

    public Sprite\[\]       levelSprites;      // length 5, index 0 \= level 1

    public GameObject     projectilePrefab;

    public RuntimeAnimatorController animator; // nullable

    \[Header("Ability")\]

    public TowerAbility   ability;           // nullable ScriptableObject, see §9.4

}

public enum TowerRole { SingleDps, Slow, Splash, HeavyAoe, Control, Support, Pierce, Chain, Economy, Summon }

public enum UnlockSource { Starting, StageVisit, StageMastery }

### 9.2 Tower table

These are **first-pass values**. They are internally consistent (see §9.3) but must be tuned against playtest. Create one `.asset` per row in `Assets/_Project/Data/Towers/`.

| id | Display | Role | Cost | Dmg | Rate | Range | Splash | Special | Unlock |
| :---- | :---- | :---- | ----: | ----: | ----: | ----: | ----: | :---- | :---- |
| `confetti_cannon` | Confetti Cannon | SingleDps | 20 | 4.0 | 1.80 | 2.5 | — | — | Starting |
| `candle_tower` | Candle Tower | Slow | 30 | 2.0 | 1.00 | 2.2 | — | burn 3 dmg/s for 3 s; slow 25 % | Starting |
| `cake_catapult` | Cake Catapult | Splash | 40 | 11.0 | 0.60 | 3.0 | 1.2 | — | Starting |
| `firework_launcher` | Firework Launcher | HeavyAoe | 55 | 28.0 | 0.30 | 4.2 | 2.0 | — | Starting |
| `whistle` | Whistle | Support | 35 | 0 | — | 1.8 | — | adjacent towers \+25 % fire rate | Childhood visit |
| `balloon_trap` | Balloon Trap | Control | 45 | 0 | 0.17 | 2.0 | 1.0 | holds all in radius 1.5 s, 6 s cooldown | Childhood **mastery** |
| `music_box` | Music Box | Splash | 50 | 6.0 | 2.00 | 1.8 | 1.8 | damages everything in radius, no projectile | Youth visit |
| `searchlight` | Searchlight | Pierce | 60 | 45.0 | 0.45 | 6.0 | — | pierces 3 enemies in a line | Youth **mastery** |
| `camera_flash` | Camera Flash | Control | 55 | 10.0 | 0.50 | 2.6 | 1.5 | freezes hit enemies 0.8 s | Adulthood visit |
| `champagne` | Champagne | Chain | 65 | 18.0 | 0.70 | 3.2 | — | chains to 3 targets, −25 % dmg per hop | Adulthood **mastery** |
| `memory_lantern` | Memory Lantern | Summon | 75 | 22.0 | 0.60 | 3.0 | — | killed enemy becomes an ally ghost for 4 s | OldAge visit |
| `hourglass` | Hourglass | Economy | 50 | 5.0 | 1.00 | 3.5 | — | kills inside its range grant \+0.5 s extra | OldAge visit (granted together with `memory_lantern`, §5.1) |

### 9.3 Upgrade maths and the hard constraint

`effectiveDamage(level) = damage * Balance.LevelDamageMultiplier[level] * StatRegistry.DamageMultiplier` `upgradeCost(level)    = baseCost * Balance.LevelCostMultiplier[level] * StatRegistry.CostMultiplier`

> **Hard constraint — enforce with an editor validation:** no single upgrade step may cost more than the smallest stage candle cap (`min(TierCandleCap) * SecondsPerCandle` \= **210 s**). The most expensive case is `memory_lantern` L5 \= `75 × 2.60 = 195 s`. Any tower whose `baseCost × 2.60 > 210` is invalid content — the player could never afford it. `DataAssetGenerator` must fail the build on violation.

Sanity target: at age 40 the player fields 8 towers and needs 628 DPS, i.e. \~80 DPS per tower. A level-5 `firework_launcher` at 4.60× with \~3 targets in splash reaches roughly that with card multipliers. Verify with `BalanceSimulator`.

### 9.4 TowerAbility

Abilities are ScriptableObjects, not code branches on `Tower`.

public abstract class TowerAbility : ScriptableObject

{

    public abstract void OnBuilt   (Tower self);

    public abstract void OnFire    (Tower self, Enemy target);

    public abstract void OnKill    (Tower self, Enemy victim);

    public abstract void OnTick    (Tower self, float dt);

    public abstract void OnNeighboursChanged(Tower self, IReadOnlyList\<Tower\> neighbours);

}

Concrete subclasses needed: `BurnSlowAbility`, `SupportAuraAbility`, `HoldAbility`, `PierceAbility`, `FreezeAbility`, `ChainAbility`, `SummonAbility`, `EconomyAbility`.

### 9.5 Adjacency synergy

`AdjacencyResolver` recomputes on every build/sell. Two towers are neighbours if their slot centres are within `Balance.AdjacencyRadius`.

| Pair | Bonus |
| :---- | :---- |
| `candle_tower` \+ `firework_launcher` | Firework splash radius \+30 % |
| `whistle` \+ any | That tower fire rate \+25 % (the Whistle's own ability) |
| `confetti_cannon` \+ `confetti_cannon` | Both \+15 % fire rate |
| `cake_catapult` \+ `music_box` | Catapult splash \+20 % |
| `searchlight` \+ `camera_flash` | Searchlight damage \+25 % against frozen targets |
| `hourglass` \+ any | That tower's kills grant \+0.5 s (the Hourglass's own ability) |

Implement as a data table (`AdjacencySet` ScriptableObject), not a switch statement.

---

## 10\. Enemies

### 10.1 EnemyData schema

\[CreateAssetMenu(menuName \= "TenCandles/Enemy")\]

public sealed class EnemyData : ScriptableObject

{

    public string      id;

    public string      displayName;

    public EnemyRole   role;

    public DecadeStage firstAppearsIn;

    \[Header("Stats — multipliers on the wave's generated values")\]

    public float hpMultiplier     \= 1f;

    public float speedMultiplier  \= 1f;

    public float rewardMultiplier \= 1f;

    public float flatArmor        \= 0f;    // subtracted from each incoming hit, min 1 damage

    \[Header("Visual")\]

    public Sprite          idleSprite;

    public RuntimeAnimatorController animator;  // nullable

    public GameObject      deathVfx;

    public float           spriteScale \= 1f;

    \[Header("Ability")\]

    public EnemyAbility    ability;              // nullable

    public bool            isBoss;

}

public enum EnemyRole { Basic, Fast, Armored, Swarm, Support, Immune, Skipper, Healer, Stealth, Thief, Evasive, MiniBoss, Boss }

### 10.2 Enemy table

| id | Display | Role | HP× | Spd× | Armor | Rwd× | Ability | First seen |
| :---- | :---- | :---- | ----: | ----: | ----: | ----: | :---- | :---- |
| `balloon` | Balloon | Basic | 1.00 | 1.00 | 0 | 1.00 | — | Childhood |
| `runner` | Runner | Fast | 0.55 | 1.70 | 0 | 0.80 | one-shot Sprint at HP \< 30 %: \+40 % speed for 2 s | Childhood |
| `gift_box` | Gift Box | Armored | 2.40 | 0.65 | 2 | 1.60 | flat armor | Childhood |
| `confetti_swarm` | Confetti Swarm | Swarm | 0.80 | 1.10 | 0 | 0.50 | on death spawns 3 children at 0.3× HP | Childhood |
| `lantern` | Lantern | Support | 1.20 | 0.90 | 0 | 1.40 | \+25 % max HP shield to enemies within 2.0 | Youth |
| `gust` | Gust | Support | 1.00 | 1.20 | 0 | 1.20 | \+30 % speed to enemies within 2.0 | Youth |
| `wet_candle` | Wet Candle | Immune | 1.60 | 0.85 | 0 | 1.30 | immune to burn and all DoT | Youth |
| `floater` | Floater | Skipper | 0.90 | 1.00 | 0 | 1.30 | skips one path segment mid-route (untargetable while airborne) | Youth |
| `mender` | Mender | Healer | 1.30 | 0.80 | 0 | 1.50 | heals 4 % max HP/s to allies within 2.5 | Adulthood |
| `hush` | Hush | Stealth | 1.10 | 1.05 | 0 | 1.40 | untargetable until inside a tower's range | Adulthood |
| `cake_thief` | Cake Thief | Thief | 1.40 | 1.15 | 0 | 1.80 | on reaching the end steals 12 s directly instead of a candle | Adulthood |
| `shade` | Shade | Evasive | 1.50 | 0.95 | 0 | 1.70 | 30 % chance to fully evade each incoming hit | OldAge |
| `giant_gift` | Giant Gift | MiniBoss | 6.00 | 0.55 | 4 | 4.00 | spawns once mid-decade from tier 2 | OldAge |

Plus **four stage bosses** (`boss_childhood`, `boss_youth`, `boss_adulthood`, `boss_oldage`), `hpMultiplier = 14`, `speedMultiplier = 0.55`, `rewardMultiplier = 8`, entering at 60 % of every year-10 wave. Each carries the signature ability of its stage.

> **Design rule to preserve:** every enemy must punish one tower role. `wet_candle` punishes burn, `shade` punishes single-target, `mender` punishes low burst, `hush` punishes long range, `gift_box` punishes fast/weak. If one build clears everything, the enemy set has failed — treat that as a bug, not a balance tweak.

### 10.3 Enemy FSM

`EnemyFsm` with one class per state in `Enemies/states/`.

Spawn → Walk → ReachEnd → BlowOut → Despawn

         │▲

         ││ on hit, 0.12 s

         ▼│

      Stagger

Runner only:  Walk ──(HP \< 30 %, once)──► Sprint ──► Walk

Floater only: Walk ──(at segment marker)──► Airborne ──► Walk

| State | Behaviour |
| :---- | :---- |
| `Spawn` | 0.3 s scale-in, invulnerable, not targetable |
| `Walk` | follows `PathRoute` waypoints at `speed × speedMultiplier × statusModifiers` |
| `Stagger` | speed × 0.6, hit flash, 0.12 s, interruptible |
| `Sprint` | Runner only, one-shot, \+40 % speed, 2 s |
| `Airborne` | Floater only, untargetable, lerps across one segment |
| `ReachEnd` | stops, fires within one frame |
| `BlowOut` | raises `GameEvents.EnemyLeaked`; `CandleClock.ApplyLeak()` or thief variant |
| `Despawn` | returns to pool |

---

## 11\. Upgrade cards

### 11.1 Schema

\[CreateAssetMenu(menuName \= "TenCandles/Card")\]

public sealed class UpgradeCard : ScriptableObject

{

    public string           id;

    public string           displayName;

    \[TextArea\] public string description;

    public CardRarity       rarity;

    public UpgradeEffectType effect;

    public float            value;

    public string           targetTowerId;   // empty \= all towers

    public int              maxStacks \= 1;

    public Sprite           icon;

    public bool             isCurse;         // pairs a drawback with the effect

    public UpgradeEffectType curseEffect;

    public float            curseValue;

}

public enum CardRarity { Common, Rare, Epic, Lifetime }

public enum UpgradeEffectType

{

    TowerDamageMul, TowerFireRateMul, TowerRangeMul, TowerCostMul,

    SplashRadiusMul, CritChanceAdd, CritMultiplierAdd, ChainTargetsAdd, PierceAdd,

    KillRewardAdd, KillRewardMul, WaveClearBonusAdd, LeakPenaltyAdd, MaxCandlesAdd,

    SlowStrengthAdd, BurnDamageMul, FreezeDurationAdd,

    TowerCapacityAdd, FreeTowerGrant, AdjacencyBonusMul,

    ReviveOnce, DrainRateMul

}

All effects funnel into `StatRegistry`, a single struct of global multipliers that `Tower` reads when computing its stats. No card writes to a `Tower` directly.

### 11.2 Card pool (starter set — expand toward 40+)

| id | Rarity | Effect | Value | Max |
| :---- | :---- | :---- | ----: | ----: |
| `festival_fire` | Common | TowerDamageMul | \+0.15 | 4 |
| `quick_hands` | Common | TowerFireRateMul | \+0.12 | 3 |
| `long_wick` | Common | TowerRangeMul | \+0.15 | 2 |
| `tip_jar` | Common | KillRewardAdd | \+0.4 s | 3 |
| `bulk_buy` | Common | TowerCostMul | −0.20 | 2 |
| `anniversary` | Common | WaveClearBonusAdd | \+12 s | 3 |
| `sturdy_door` | Common | LeakPenaltyAdd | −15 s | 1 |
| `steady_wax` | Common | DrainRateMul | −0.10 | 2 |
| `extra_hands` | Rare | TowerCapacityAdd | \+1 | 2 |
| `eleventh_candle` | Rare | MaxCandlesAdd | \+1 | 2 |
| `critical_party` | Rare | CritChanceAdd / CritMultiplierAdd | \+0.12 / ×2.5 | 2 |
| `ricochet` | Rare | ChainTargetsAdd (`confetti_cannon`) | \+1 | 1 |
| `big_bang` | Rare | SplashRadiusMul | \+0.35 | 2 |
| `beeswax` | Rare | SlowStrengthAdd (`candle_tower`) | \+0.20 | 1 |
| `free_gift` | Rare | FreeTowerGrant | 1 | 2 |
| `piercing_light` | Rare | PierceAdd (`searchlight`) | \+2 | 1 |
| `last_breath` | Epic | ReviveOnce | \+45 s | 1 |
| `neighbourhood` | Epic | AdjacencyBonusMul | \+0.50 | 2 |
| `bonfire` | Epic | BurnDamageMul | \+1.00 | 2 |
| `long_exposure` | Epic | FreezeDurationAdd (`camera_flash`) | \+0.6 s | 1 |
| `borrowed_time` | Epic **curse** | TowerDamageMul \+0.60 / MaxCandlesAdd −1 | — | 1 |
| `reckless_youth` | Epic **curse** | TowerFireRateMul \+0.40 / LeakPenaltyAdd \+15 s | — | 1 |
| `second_wind` | Lifetime | MaxCandlesAdd | \+2 | 1 |
| `legacy_flame` | Lifetime | TowerDamageMul | \+0.45 | 1 |
| `endless_party` | Lifetime | KillRewardAdd | \+1.2 s | 1 |

### 11.3 Draw rules

- Offer `Balance.CardsOfferedPerYear` (3) distinct cards **every 2 years**: after the waves of ages 1, 3, 5 … 39, i.e. **20 offers per run**. Not every wave: 40 picks against a pool of ~25 cards averaging 2 stacks would leave the last third of the run with no real choice.  
- Exclude cards already at `maxStacks`.  
- Exclude cards whose `targetTowerId` the player does not own.  
- Filter by rarity gate on `RunState.CurrentAge` (§7).  
- `Lifetime` cards are never offered here — wishes only.  
- Draw must use the run's seeded RNG (`RunState.Rng`, §13.3), never `UnityEngine.Random`. `KillRewardMul` multiplies the reward reported by `EnemyKilled`.

---

## 12\. Environment mechanics

### 12.1 Wind

`WindController`, enabled per stage via `StageThemeData.windEnabled`.

1. Schedule a gust at a random point in each wave (never in years 1–2 of the run).  
2. Raise `WindWarning(Balance.WindTelegraphSeconds)`; UI shows a clear 3-second telegraph.  
3. Call `CandleClock.SetDrainMultiplier(Balance.WindDrainMultiplier)` for the gust duration (4–8 s scaled by tier).  
4. Restore to 1 and raise `WindEnded`.

### 12.2 Path variants

Each `StageThemeData` holds 3 `PathRoute` prefabs. One is chosen per decade from the run seed. Reuse the existing MapPainter editor tool to author them.

**Multi-lane maps.** A map may have more than one entrance, each with its own route ending at the same cake. The number of lanes in use is `ActiveLanes = min(map entrance count, Balance.TierLaneAllowance[RunState.CurrentTier])` (§7): tier 0 is always single-lane, and the second lane opens at tier 1. It is indexed by tier, not stage, so Stay does not delay it. Regular enemies are dealt across the active lanes by share, deterministically (each goes to the lane furthest behind its share); stage bosses always take the first lane. **Ease-in:** in the decade a lane first opens (tier 1 for the second lane), its share ramps linearly from `Balance.NewLaneShareStart` (25 %, year 1) to `Balance.NewLaneShareEnd` (50 %, year 10), and stays at 50 % from then on. This softens the system's tightest point (age 11: capacity 4, two lanes), where the new lane gets 3 of 11 enemies. A map's own "lane opens at year N" setting is not used — the allowance table replaces it. Every route stays drawn, so the player can see a lane before it opens.

### 12.3 Memory fragments

2 % of enemy deaths drop a pickup with a 6-second lifetime. Collecting it grants one of: \+5 s, \+20 % damage for 8 s, instant tower reload. Purely for a reward beat inside the wave — all other rewards land at wave end.

---

## 13\. Meta progression

### 13.1 Legacy

memories \= ageReached \* MemoryPerAge

         \+ visitedStages.Count \* MemoryPerStageVisit

         \+ wishesMade \* MemoryPerWish

Spent on a persistent node tree (`LegacyNode` ScriptableObjects): extra starting towers, new cards into the pool, starting-time bonus, and **stage unlocks**.

**Stage unlock rule:** a stage becomes permanently available once the player has completed a full decade in the preceding stage. A first run therefore offers only `Childhood` and `Youth`, and lasts \~10 minutes. This is deliberate — Steam refunds are decided inside the first 15 minutes, so the first run must not be a 22-minute commitment.

### 13.2 Hard Years

Unlocked after the first `Full Life` victory. 15 tiers, cumulative, one modifier each: enemy HP \+8 % per tier, drain \+5 %, one fewer tower slot at tiers 4/9/14, wish costs \+1 candle at tier 6, masteries disabled at tier 11, and so on. Store the chosen tier in `RunState.HardYearsTier` and apply as multipliers in `WaveGenerator` and `Balance` lookups.

### 13.3 Legend mode, seeds and leaderboard

- Unlocked after first reaching age 40\. The run continues past 40; `d` keeps incrementing with `StageHpMultiplier` extrapolated as `17.50 * 2.57^(d-3)`. Score \= age reached.  
- **All run randomness comes from `RunState.Seed`: the seeded `System.Random` `RunState.Rng`, plus `RunState.CombatRng` for combat rolls.** Never call `UnityEngine.Random` in gameplay code. Card draws and wave shuffles use it since Phase 2. Combat rolls (crits) use a second stream, `RunState.CombatRng`, seeded from the same `Seed`, so the number of shots fired never shifts card offers or wave order. Daily seed \= `yyyyMMdd` hashed.  
- Leaderboard submission sends `{ seed, score, choices[], wishes[], purchases[], checksum }`, not just the score. Server-side validation replays the choice log for plausibility. Client-side scores are forgeable; this only raises the bar.

---

## 14\. Asset integration contract

**Rumeysa is adding new tower and enemy art to the project. Follow this section whenever art is added or changed.**

- **Unassigned art** — a folder with no spec row. **Do not invent a design for it.** List it under "Unassigned art" in your summary and ask before using it.  
2. Write the manifest to `Assets/_Project/Data/asset_manifest.md` and update it whenever art changes.

`Editor/AssetInventoryTool.cs` should implement this as a menu item: `Tools/Ten Candles/Asset Inventory`.

### 14.2 Folder and naming convention

Assets/\_Project/Art/

  Towers/\<tower\_id\>/

      level1.png  level2.png  level3.png  level4.png  level5.png

      base.png                 \# optional shared base/platform

      muzzle.png               \# optional

  Enemies/\<enemy\_id\>/

      idle.png                 \# required

      walk.png                 \# optional sheet

      death.png                \# optional sheet

  Projectiles/\<name\>.png

  Candles/   candle.png  flame.png  cake.png

  Decor/\<stage\_id\>/…

  UI/

  \_Placeholder/  placeholder\_tower.png  placeholder\_enemy.png

Rules:

- **The folder name is the id.** If an asset pack ships with different filenames, rename the files to match this convention — never rename the spec `id`, because the `id` is referenced by save data and the Legacy tree.  
- A tower with fewer than 5 level sprites: repeat the last available sprite for the missing levels and log it.  
- Lowercase, `snake_case`, ASCII only. No spaces.

### 14.3 Import settings — apply to every sprite

| Setting | Value | Why |
| :---- | :---- | :---- |
| Texture Type | Sprite (2D and UI) | — |
| Sprite Mode | Single, or Multiple for sheets | — |
| **Pixels Per Unit** | **32** | Project-wide. A mismatched PPU is the most common cause of "the new enemy is enormous" |
| Filter Mode | Point (no filter) for pixel art; Bilinear otherwise | Keep consistent within a pack |
| Compression | None | Small 2D project; avoids artefacts |
| Generate Mip Maps | Off | 2D orthographic |
| Wrap Mode | Clamp | — |
| Max Size | 2048 | — |
| **Pivot** | Towers & enemies: **Bottom Centre**. Projectiles & pickups: **Centre**. Candle: **Bottom Centre** | Ground alignment and the candle melt both depend on this |

Write these as an `AssetPostprocessor` (`Editor/SpriteImportPostprocessor.cs`) keyed on the `Art/` path so new drops are configured automatically rather than by hand.

### 14.4 Sprite atlases

Create one `SpriteAtlas` per category (`Towers`, `Enemies`, `Projectiles`, `UI`, `Decor`). WebGL draw calls matter: 25+ enemies plus projectiles on screen in tier 3 will tank without atlasing.

### 14.5 Wiring new art into data

`Editor/DataAssetGenerator.cs`, menu item `Tools/Ten Candles/Generate Data Assets`:

1. Reads the §9.2 and §10.2 tables (keep them as a CSV in `Assets/_Project/Data/tables/` so they are editable without touching code).  
2. Creates or updates one `.asset` per row, preserving any hand-tuned values already set in the inspector unless `--force` is passed.  
3. Resolves sprites by the §14.2 convention.  
4. **Fails the run** if any tower violates the §9.3 cost constraint, if an `id` is duplicated, or if a required sprite is missing with no placeholder.

### 14.6 Scaling new art

Enemies must read at a consistent size regardless of source resolution. Normalise with `EnemyData.spriteScale` so that a `balloon` occupies roughly **0.8 world units** tall and `giant_gift` roughly **1.6**. Do not rescale the source PNGs; set the multiplier in data.

### 14.7 When a new pack arrives

Report, don't guess:

- Which spec ids the pack now covers.  
- Which ids are still on placeholders.  
- Any art that suggests a mechanic not in this spec — describe it and **ask** rather than adding a tower.

---

## 15\. Implementation phases

Each phase must leave the game playable end to end. Never leave two systems half-finished at once.

### Phase 1 — Lifetime skeleton

Extend the run to 40 waves across 4 tiers. `RunState`, `DecadeSlot`, `LifetimeManager`. Birthday screen exists but only says "Happy Birthday" and advances the stage. Per-tier candle cap. Per-tier lane allowance on multi-lane maps. Age-derived tower capacity.

**Acceptance:** a full 40-wave run is playable start to finish; the candle bar shows 10/10/9/7 by tier; tower capacity reaches 8 at age 40; a multi-lane map uses one lane in tier 0 and two from tier 1; `BalanceSimulator` reproduces the §8 table within ±2 %.

### Phase 2 — Stay / Advance

`BirthdayController`, legality rules, `StayHpMultiplier`, mastery and visit tower grants, mastery passives (§5.1), both Old Age towers as visit towers. **Seeded run RNG** (pulled forward from Phase 9 so balance tuning is reproducible): `RunState.Rng` drives card draws and wave shuffles; `RunState.CombatRng` drives crit rolls.

**Acceptance:** all five lives in §5.2 are reachable; illegal options are disabled with a reason shown; a unit test enumerates exactly five legal choice sequences; masteries and their passives persist for the rest of the run; the same seed produces the same card offers and wave shuffles.

### Phase 3 — Wish

`WishSystem`, `Lifetime` rarity pool, the blow-out moment with full VFX and audio.

**Acceptance:** all four candle options work; insufficient time disables the option; `WishResolved` fires in every case including 0; a wish gift persists to the end of the run.

### Phase 4 — Content pass 1

Towers 5–8, enemies 5–9, their abilities, the §14 asset pipeline.

**Acceptance:** each new tower is usable and its role is distinguishable in play; `Tools/Ten Candles/Asset Inventory` reports zero unassigned art; no placeholder remains for an implemented id.

### Phase 5 — Legacy

`LegacyStore`, save system, stage unlock rule, memories formula.

**Acceptance:** a first run offers only Childhood and Youth and finishes in \~10 minutes; memories persist across sessions; unlocking Adulthood makes it selectable in the next run.

### Phase 6 — Environment

Path variants, wind, adjacency synergy, memory fragments.

**Acceptance:** the same stage played twice uses different layouts from the same seed; wind is telegraphed 3 s ahead and doubles drain; adjacency bonuses appear in the tower's inspector tooltip.

### Phase 7 — Content pass 2

Remaining towers and enemies, card pool to 40+, four stage bosses.

**Acceptance:** every §9.2 and §10.2 row exists as data and is reachable in play; no build clears every enemy type (§10.2 design rule).

### Phase 8 — Hard Years

15 tiers with cumulative modifiers.

**Acceptance:** tier selection persists; each tier's modifier is visible before the run; tier 15 is winnable by the developer at least once.

### Phase 9 — Legend, seeds, leaderboard

Legend mode, daily seed, score submission. (The seeded run RNG moved to Phase 2.)

**Acceptance:** the same seed produces an identical run twice (waves, cards, paths); no `UnityEngine.Random` call remains in gameplay code; a submitted score round-trips with its choice log.

---

## 16\. Out of scope

Do not build these unless asked:

- Multiplayer or co-op  
- Tower selling or repositioning  
- Mid-wave save/resume (save between decades only)  
- Mobile touch controls  
- Procedurally generated paths (hand-authored variants only)  
- A settings/options screen before Phase 5 (boot straight into the game until then)  
- Any analytics or telemetry upload beyond the leaderboard payload

---

## 17\. Balance harness

Port the generator formulas to a standalone script (`Editor/BalanceSimulator.cs` or a `Tools/balance.py`) that prints the §8 table. It must be runnable without entering Play mode.

Checks it must perform:

1. §8 table reproduced within ±2 %.  
2. `Hp(d,10)` within 5 % of `Hp(d+1,1)` for d \= 0,1,2 — HP continuity.  
3. Required DPS dips at each decade boundary and later exceeds the prior peak — the sawtooth.  
4. No tower violates the 210 s single-step cost cap.  
5. Total run duration between 19 and 25 minutes.  
6. Total kill income exceeds total drain by 2.5–3.5× (the tower budget).  
7. Tower capacity is ≥ 2 × `TierLaneAllowance[tier]` at every age 1–40 (the multi-lane invariant, §7).

When tuning, turn these knobs in this order:

| \# | Knob | Controls |
| ----: | :---- | :---- |
| 1 | `StageHpMultiplier` | the whole difficulty curve |
| 2 | `RewardStageMul` (0.55) | how loose the late economy feels |
| 3 | `SecondsPerCandle` (30) | total run length |
| 4 | `LeakPenalty` (30) | error tolerance |
| 5 | `StayHpMultiplier` (1.30) | Stay vs Advance balance |
| 6 | Tower costs | build diversity |

---

## 18\. Open questions

Flag these to Rumeysa rather than deciding alone:

1. Does `Stay` reuse the same path variant, or reroll it? (Spec assumes reroll.)  
2. Should masteries carry across runs via Legacy, or stay run-local? (Spec assumes run-local.)  
3. Should `OldAge` be reachable through a Legacy unlock on non-`A A A` paths? (Spec says no — its rarity is the point.)  
4. Controller support: Phase 5 or after Phase 9?

   and when unity is close , you can make test

---

*Ten Candles · Implementation Spec v2 · September 2026*  
