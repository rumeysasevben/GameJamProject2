# Balance baseline — end of Phase 2

Measured at commit `c07a389` (Phase 2) before any Phase 3 change. No balance values were changed.

> **AAS numbers predate the Established passive.** This baseline ran with the original Adulthood passive, Savings (+20 % kill reward). Savings has since been replaced by Established (+1 tower capacity), so the AAS row and the Savings findings below describe the old passive. The other four lives never take the Adulthood passive.

- **Setup:** batch-mode AutoPlayer bot, map 0 (Lava Fields, two lanes), `-seed=12345`, cards on, one run per life.
- **Command:** `-executeMethod TenCandles.EditorTools.AutoTestRunner.RunFromCommandLine -map=0 -seed=12345 -bot.life=<LIFE> -tag=baseline_<LIFE>`
- **Raw reports:** `Logs/autoplay_baseline_<LIFE>.txt`.
- **Errors:** all five runs logged 0 errors and exited with code 0.
- **Reproducibility:** AAA and AAS are byte-identical to earlier same-seed runs, so the baseline is reproducible.

## Comparison

| Life | Name | Won | Final age | Time left at end | Final cap | Short of full | Total leaks | Candles at 10 | Candles at 20 | Candles at 30 |
| :--- | :--- | :---: | ---: | ---: | ---: | ---: | ---: | :--- | :--- | :--- |
| AAA | Full Life | yes | 40 | 294.0 s | 300 s (10) | 6.0 s | 1 | 8.1 / 10 | 12.0 / 12 | 12.0 / 12 |
| AAS | Late Bloomer | yes | 40 | **232.1 s** | 300 s (10) | **67.9 s** | **12** | 8.1 / 10 | 12.0 / 12 | 12.0 / 12 |
| ASA | Long Youth | yes | 40 | 300.0 s | 300 s (10) | **0.0 s** | 1 | 8.1 / 10 | 12.0 / 12 | 11.4 / 12 |
| SAA | Slow Childhood | yes | 40 | 314.3 s | 330 s (11) | 15.7 s | 3 | 8.1 / 10 | 12.9 / 13 | 13.0 / 13 |
| SAS | Never Grew Up | yes | 40 | **321.6 s** | 330 s (11) | 8.4 s | 3 | 8.1 / 10 | 12.9 / 13 | 13.0 / 13 |

How to read the columns:
- **Candles at N** = candles in hand when the birthday screen opens (time ÷ 30 s) / candle cap at that moment. No time drains between the wave end and the birthday.
- The caps sit above the §2 tier caps (10/10/9/7) because they include bonus candles:
  - +2 from the Birthday Wish card (age 11);
  - +1 from The Eleventh Candle card (age 29);
  - +1 from Long Summer in SAA and SAS.
- **Short of full** = final cap − time left: how far the last wave pushed the wallet below full.

## Leaks by decade

| Life | Ages 1–10 | 11–20 | 21–30 | 31–39 | Age 40 | Final-decade passive / HP |
| :--- | ---: | ---: | ---: | ---: | ---: | :--- |
| AAA | 0 | 0 | 0 | 0 | 1 | none, HP ×1.00 |
| AAS | 0 | 0 | 0 | 2 (ages 37, 39) | **10** | Savings, HP ×1.30 |
| ASA | 0 | 0 | 1 (age 30) | 0 | 0 | Boundless Energy, HP ×1.00 |
| SAA | 0 | 0 | 0 | 0 | 3 | Long Summer, HP ×1.00 |
| SAS | 0 | 0 | 0 | 0 | 3 | Long Summer + Boundless Energy, HP ×1.30 |

The bot report has no line for the age-40 wave, because that wave ends in victory. Age-40 leaks are therefore the total minus the per-year lines.

## Findings

- **Hardest: AAS (Late Bloomer).**
  - 12 leaks: 10 in the age-40 wave, 2 more at ages 37 and 39. Every other life leaked at most 3 in total.
  - It ends 67.9 s below a full wallet. The next worst life ends 15.7 s below.
  - It is the only life whose last decade has both the Stay +30 % HP penalty and no damage boost: Savings raises kill reward, and the wallet was already full in 7 of the 9 recorded Old Age–tier years.
  - SAS has the same +30 % HP in its last decade but has +15 % fire rate (raw DPS 579.9 vs 519.7), and leaked 3.
- **Easiest: ASA (Long Youth).**
  - It ends on a completely full wallet (300.0 / 300 s) with 1 leak.
  - It is the only life with no leak at age 40.
  - It pays its Stay +30 % HP in tier 2, where the waves are soft, and carries +15 % fire rate into tier 3 at normal HP.
  - SAS has the most seconds left (321.6 s), but only because Long Summer adds a candle. Measured against its own cap, it is 8.4 s short and leaked 3.
- **Stay costs almost nothing before age 40.**
  - In decades 2 and 3, Stay decades leaked 0–1 and ended at or near a full wallet.
  - Birthday holdings are identical or better for Stay lives (SAA/SAS 12.9–13.0 candles vs 12.0).
- **Masteries are empty in every life.** The mastery and visit towers (§9.2) come with the content phases. In this baseline, Stay's reward is only the passive.

## Caveats

- **The bot saturates the wallet.** Every life is at full cap for 3–7 years of each decade after the first. So "time left at end" mostly measures the cap, and differences show up mainly in the age-40 wave. A harder map or seed would separate the lives more.
- **Scope:** one seed, one map, one bot strategy. This baseline is meant for before/after comparisons at seed 12345, not as a verdict on human difficulty.
