# Balance baseline — Phase 4 structural corrections (before any Phase 4 content)

Measured on top of commit `172ad24` plus the four corrections. No balance values were changed. Content is the same as Phase 3: 4 towers (max level 3), 5 enemies, the same cards and gifts.

What changed since `baseline_phase3.md`:

1. **A wish spends candle cap, not time.**
   - Blowing N candles lowers the cap by N for the rest of the run.
   - Running cap = `max(3, TierCandleCap[tier] + bonus candles − CandlesWished)`.
   - An option is greyed out if it would leave the cap below 3 at the current tier **or any later tier** (Old Age's tier cap is 7).
2. **A gift never duplicates a maxed effect.** Gifts and yearly cards share one rule, `UpgradeEffect.CanOffer`: True Aim is not offered after Armor Breaker, and Armor Breaker is not offered after True Aim.
3. **Wish gifts draw from their own stream, `RunState.WishRng`.**
4. **The bot can spend extra tower slots.**
   - It takes capacity gifts first. Only Open House and Crowded Table carry the effect; no yearly card does.
   - Its build plan has an open-ended tail past its 9 base towers.
   - `-bot.wish=N` now means "the most legal candles up to N".

## Setup

- **Bot:** batch-mode AutoPlayer, map 0 (Lava Fields, two lanes), `-seed=12345`, cards on. One run per life per wish column, 15 runs. All won; every run logged 0 errors and exited with code 0.
- **Raw reports:** `Logs/autoplay_p4fix_w{0,1,5}_<LIFE>.txt`.
  - The 5-candle column was re-run after a bot fix. The first pass ranked capacity below damage and fire rate, so the bot passed on Open House twice and capacity went untested again.
  - The superseded reports are in `Logs/p4fix_prebotfix/`. The 0- and 1-candle columns never see a capacity gift, so those runs are unaffected.
- **The comparison is controlled now.** All 15 runs take the **identical 20 yearly cards** in the same order. In Phase 3, the 0- and 5-candle AAA runs diverged from the 6th card on. Any difference between columns comes from the wish itself: the gift, the lower cap, and the play they cause.

## What each column actually wished

| Column | At 10 | At 20 | At 30 | Candles wished | Gifts taken |
| :--- | :--- | :--- | :--- | ---: | :--- |
| 0 | 0 | 0 | 0 | 0 | — |
| 1 | 1 | 1 | 1 | 3 | Warm Glow, Eager Hands, Big Balloons |
| "5" | **3** (5 illegal) | **3** (5 illegal) | **1** (3 and 5 illegal) | 7 | Open House, Legacy Flame, Warm Glow |

**The Legendary tier was never reachable in this seed.** The table below shows the check at each birthday for the A-start lives (bonus candles: Birthday Wish +2 at 11, The Eleventh Candle +1 at 29). Candles are shown before the wish.

| Birthday | Old Age cap | Bonus candles | Already wished | Room above the floor of 3 |
| :--- | ---: | ---: | ---: | ---: |
| 10 | 7 | 0 | 0 | 4 |
| 20 | 7 | 2 | 3 | 3 |
| 30 | 7 | 3 | 6 | 1 |

A 5-candle wish needs 5 candles of room. With no bonus candles, a whole run can wish away at most 4. The S-start lives have one more (Long Summer) and got the same wishes.

## Comparison

"Time left" is at the end of the age-40 wave. Leaks include age 40. "Low 31–39" is the lowest year-end wallet in the last decade.

| Life | Wish | Time left | Final cap | Leaks | Where | Wasted at cap | Low 11–20 | Low 31–39 | Raw DPS at 39 | Towers at 40 |
| :--- | :--- | ---: | ---: | ---: | :--- | ---: | ---: | ---: | ---: | ---: |
| AAA | 0 | 294.0 s | 10 | 1 | 40 | 2 373 s | 238.4 s | 192.1 s | 519.7 | 7 / 8 |
| AAA | 1 | 210.0 s | 7 | 0 | — | 2 418 s | 208.5 s | 103.8 s | 700.0 | 7 / 8 |
| AAA | 3+3+1 | 90.0 s | 3 | 0 | — | 2 407 s | **45.6 s** | 89.2 s | 833.4 | 8 / 9 |
| AAS | 0 | 256.9 s | 10 | 12 | 2 at 39, 10 at 40 | 2 200 s | 238.4 s | 111.0 s | 597.3 | 8 / 9 |
| AAS | 1 | 210.0 s | 7 | 0 | — | 2 287 s | 208.5 s | 78.0 s | 804.5 | 8 / 9 |
| AAS | 3+3+1 | 84.5 s | 3 | 1 | 40 | 2 403 s | 45.6 s | 66.2 s | 833.4 | 8 / **10** |
| ASA | 0 | 300.0 s | 10 | 1 | 30 | 2 336 s | 238.4 s | 191.9 s | 579.9 | 7 / 8 |
| ASA | 1 | 210.0 s | 7 | 0 | — | 2 417 s | 208.5 s | 104.9 s | 770.2 | 7 / 8 |
| ASA | 3+3+1 | 90.0 s | 3 | 0 | — | 2 408 s | 45.6 s | 89.2 s | 929.9 | 8 / 9 |
| SAA | 0 | 314.3 s | 11 | 3 | 40 | 2 342 s | 259.9 s | 222.1 s | 519.7 | 7 / 8 |
| SAA | 1 | 240.0 s | 8 | 0 | — | 2 387 s | 238.4 s | 133.7 s | 700.0 | 7 / 8 |
| SAA | 3+3+1 | 120.0 s | 4 | 0 | — | 2 317 s | 45.6 s | 63.2 s | 876.1 | 8 / 9 |
| SAS | 0 | 321.6 s | 11 | 3 | 40 | 2 337 s | 259.9 s | 222.0 s | 579.9 | 7 / 8 |
| SAS | 1 | 240.0 s | 8 | 0 | — | 2 383 s | 238.4 s | 133.6 s | 770.2 | 7 / 8 |
| SAS | 3+3+1 | 114.6 s | 4 | 1 | 40 | 2 314 s | 45.6 s | 62.9 s | 977.5 | 8 / 9 |

The towers column is read from the report line printed after the age-40 wave, so capacity is age 40's.

**Control check:** the 0-candle column reproduces `baseline_phase3.md` exactly (time left and leaks in all five lives), as it should. A run that never wishes never touches the gift draw.

## Readings

**The wish now has a price you can see, and it is permanent.**

- Time left at 40 steps down with the candles wished: 300 → 210 → 90 s for A-start lives, 330 → 240 → 120 s for S-start lives.
- Every winning run ends at its cap, so the final wallet is the final cap. The old cost vanished into overflow; this one doesn't.
- In Phase 3 the 1- and 5-candle columns ended at the same 300 s. Here the three columns separate in every life.

**Wishing more is still not punished, because the game is too loose.**

- The 7-candle column fights the whole Old Age decade on a 3–4 candle cap (90–120 s wallet) and still leaks 0–1 enemies.
- Its extra power shows up clearly: raw DPS at 39 of 833–978, against 700–804 for 1 candle and 520–597 for none.
- Waste at the cap barely moves (2 314–2 418 s in every column). Kill income still overflows even a 90 s wallet, so a small cap costs almost nothing while waves stay this easy. This matches the plan: don't tune now, re-measure with Phase 4 content.

**The first measurable risk of wishing is early.**

- Open House at 10 lifts capacity to 5 at age 11, a year when the second lane opens, and the bot spends on its 5th tower at once.
- Its year-end wallet falls to **45.6 s** at age 11 in all five lives, against 208–260 s in the other columns.
- This is the lowest wallet recorded in any baseline so far.

**Capacity is measured for the first time, partly.**

- Open House was taken at 10 in all five lives, and the extra slot was filled: 8 towers against 7.
- **AAS reached capacity 10 (Established + Open House) but fielded only 8.** Once the cap fell to 3 candles, the wallet topped out at 90 s. The bot's next step, the 9th base tower (Spike Mortar, 55 s), would leave less than its fixed 40 s reserve, so its build plan stalled.
  - The same stall left one Cake at level 2 in A-start lives: the 60 s upgrade leaves 30 s.
  - This is a bot limit (a fixed reserve, and a plan that stops at the first unaffordable step), not a capacity bug. It is also the first sign of the intended tension: a low cap limits what a big tower count can buy.
- The open-ended capacity tail (tower 10+) was never reached in this seed.
- Crowded Table (Legendary) was never offered, since 5 candles were never legal.

**A dead gift was not offered.** The Armor Breaker yearly card is taken at 24 in every run. No True Aim appeared in any 3-candle offer, and True Aim sits in the Legendary tier, which was never blown. The rule is covered by `WishSystemTests.AGiftNeverDuplicatesAMaxedEffect` rather than by this baseline.

## Open questions this raises (not decided here)

1. **Legendary reachability.** Under the look-ahead floor, a 5-candle wish needs bonus candles first, and in this seed it was never legal. If Legendary should be reachable without them, the options are:
   - check only the cap right after the wish (the floor then absorbs later tier drops, and some lost candles come back in Old Age);
   - lower `WishMinCandleCap`;
   - change `TierCandleCap[3]`.
   All of these are design or balance calls.
2. **The §9.3 cost constraint assumes a 210 s wallet** (`min(TierCandleCap) × 30`). With wishes the wallet can be 90 s. That is fine for today's 3-level towers (the priciest step is 82.5 s). At the spec's 5 levels, a `firework_launcher` L5 step (143 s) or `memory_lantern` L5 (195 s) would be unbuyable on a 3-candle cap. §9.3 should either use `WishMinCandleCap × 30` or accept that heavy wishing locks out top upgrades.

## Caveats

- One seed, one map, one bot with a fixed 40 s reserve.
- The bot fields fewer towers than its capacity whenever the cap is small (see above), so late-decade capacity numbers undercount what a player could build.
- Year-end wallets only; the low point inside a wave isn't logged.
