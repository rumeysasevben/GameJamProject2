# Balance baseline — end of Phase 3 (Wish), after the gift correction

Measured after the Phase 3 correction, on top of commit `c07a389` plus the uncommitted Phase 3 work. No balance values were changed: Stay penalty, rewards, costs, and tier candle caps are all as in Phase 2.

What changed since the first Phase 3 measurement:

- **Gift tiers are separate pools** of 5 gifts each (spec §6.1). The ×1/×2/×3 value placeholder is gone.
- **No gift grants time or candle capacity** (§6 invariant). Second Wind and Endless Party were removed.
- **`MaxCandlesAdd` raises the cap without filling it** (§11.1). This also applies to the yearly cards The Eleventh Candle and Birthday Wish.
- **Card rarity `Legendary` is renamed `Epic`.** It's a name change only; the data is the same.

## Setup

- **Bot:** batch-mode AutoPlayer on map 0 (Lava Fields, two lanes), `-seed=12345`, cards on. One run per life per wish policy, 15 runs.
- **Wish policy:** `-bot.wish=0`, `1`, or `5`, meaning that many candles at every birthday (10, 20, 30).
  - The chosen option was legal at every birthday in every run.
  - The bot picks gifts by its card priority. It has no entry for `TowerCapacityAdd`, so it never takes Open House or Crowded Table.
- **Raw reports:** `Logs/autoplay_p3b_w{0,1,5}_<LIFE>.txt`. Every run logged 0 errors and exited with code 0.
- **Divergence between columns:** a gift draw now always uses `RunState.Rng` (5 candidates for 3 slots). The 1- and 5-candle runs therefore see different yearly card offers and wave shuffles after age 10 than the 0-candle run. Compare the columns as whole runs, not year by year.

## Comparison

"Time left" is at the end of the age-40 wave. Leaks include the age-40 wave.

| Life | Wish | Won | Final age | Time left | Total leaks | Where the leaks were | Time wasted at cap | Spent on wishes |
| :--- | ---: | :---: | ---: | ---: | ---: | :--- | ---: | ---: |
| AAA | 0 | yes | 40 | 294.0 s | 1 | 40 | 2 373 s | 0 s |
| AAA | 1 | yes | 40 | 300.0 s | 0 | — | 2 291 s | 90 s |
| AAA | 5 | yes | 40 | 300.0 s | 0 | — | 1 974 s | 450 s |
| AAS | 0 | yes | 40 | 256.9 s | 12 | 2 at 39, 10 at 40 | 2 200 s | 0 s |
| AAS | 1 | yes | 40 | 300.0 s | 0 | — | 2 160 s | 90 s |
| AAS | 5 | yes | 40 | 300.0 s | 0 | — | 1 840 s | 450 s |
| ASA | 0 | yes | 40 | 300.0 s | 1 | 30 | 2 336 s | 0 s |
| ASA | 1 | yes | 40 | 300.0 s | 0 | — | 2 290 s | 90 s |
| ASA | 5 | yes | 40 | 300.0 s | 0 | — | 1 971 s | 450 s |
| SAA | 0 | yes | 40 | 314.3 s | 3 | 40 | 2 342 s | 0 s |
| SAA | 1 | yes | 40 | 330.0 s | 0 | — | 2 261 s | 90 s |
| SAA | 5 | yes | 40 | 330.0 s | 0 | — | 1 943 s | 450 s |
| SAS | 0 | yes | 40 | 321.6 s | 3 | 40 | 2 337 s | 0 s |
| SAS | 1 | yes | 40 | 330.0 s | 0 | — | 2 259 s | 90 s |
| SAS | 5 | yes | 40 | 330.0 s | 0 | — | 1 938 s | 450 s |

### Candles at each birthday

Each cell is: wallet when the wish screen opens → wallet after the wish, then the cap at that moment.

| Life | Wish | At 10 | At 20 | At 30 |
| :--- | ---: | :--- | :--- | :--- |
| AAA / AAS | 0 | 245.5 → 245.5 s, cap 300 s | 360.0 → 360.0 s, cap 360 s | 360.0 → 360.0 s, cap 360 s |
| ASA | 0 | 245.5 → 245.5 s, cap 300 s | 360.0 → 360.0 s, cap 360 s | 348.5 → 348.5 s, cap 360 s |
| SAA / SAS | 0 | 245.5 → 245.5 s, cap 300 s | 390.0 → 390.0 s, cap 390 s | 390.0 → 390.0 s, cap 390 s |
| AAA / AAS / ASA | 1 | 245.5 → 215.5 s, cap 300 s | 330.0 → 300.0 s, cap 330 s | 300.0 → 270.0 s, cap 300 s |
| SAA / SAS | 1 | 245.5 → 215.5 s, cap 300 s | 360.0 → 330.0 s, cap 360 s | 330.0 → 300.0 s, cap 330 s |
| AAA / AAS / ASA | 5 | 245.5 → 95.5 s, cap 300 s | 330.0 → 180.0 s, cap 330 s | 300.0 → 150.0 s, cap 300 s |
| SAA / SAS | 5 | 245.5 → 95.5 s, cap 300 s | 360.0 → 210.0 s, cap 360 s | 330.0 → 180.0 s, cap 330 s |

Caps differ between columns because the Eleventh Candle and Birthday Wish cards arrive in different years once the card offers diverge.

### How low the wallet went after each wish

Year-end wallet only; the lowest point inside a wave is not logged.

| Life | Wish | Lowest, ages 11–20 | Lowest, ages 21–30 | Lowest, ages 31–39 | Wallet at 19 / 29 / 39 |
| :--- | ---: | ---: | ---: | ---: | :--- |
| AAA | 0 | 238.4 s | 328.4 s | 192.1 s | 359.8 / 328.4 / 300.0 s |
| AAA | 1 | 206.1 s | 298.3 s | 137.6 s | 328.7 / 298.9 / 300.0 s |
| AAA | 5 | **109.9 s** | 208.6 s | 105.9 s | 328.7 / 298.9 / 300.0 s |
| AAS | 0 | 238.4 s | 328.4 s | 111.0 s | 359.8 / 328.4 / 300.0 s |
| AAS | 1 | 206.1 s | 298.3 s | **65.4 s** | 328.7 / 298.9 / 300.0 s |
| AAS | 5 | 109.9 s | 208.6 s | 71.1 s | 328.7 / 298.9 / 300.0 s |
| SAA | 0 | 259.9 s | 358.5 s | 222.1 s | 389.8 / 358.5 / 330.0 s |
| SAA | 1 | 229.9 s | 328.5 s | 168.0 s | 360.0 / 328.9 / 330.0 s |
| SAA | 5 | 109.9 s | 238.5 s | 135.5 s | 358.6 / 328.7 / 330.0 s |

ASA tracks AAA and SAS tracks SAA to within 1 s.

### Gifts the bot took

The same gifts in every life:

- **1 candle:** Big Balloons (+35 % splash) at 10, Warm Glow (+25 % damage) at 20, Eager Hands (+20 % fire rate) at 30.
- **5 candles:** Grand Finale (+150 % Spike Mortar damage) at 10, Lucky Stars (+50 % crit chance) at 20, True Aim (ignore armour) at 30.
  - **True Aim was a dead gift.** The bot had already taken the Armor Breaker card at age 24, which has the same effect.

Raw DPS at age 39:

| Life | 0 candles | 1 candle | 5 candles |
| :--- | ---: | ---: | ---: |
| AAA | 519.7 | 700.0 | 809.8 |
| AAS | 597.3 | 804.5 | 887.3 |

## Is wishing more still strictly better?

**Wishing beats not wishing in every life. Five candles beat one candle in none.** On the scoreboard (won, time left, leaks) the 5-candle column exactly ties the 1-candle column in all five lives.

The tie is the real finding: **the cost isn't real, and it's an economy problem, not a gift problem.**

- **The 5-candle wish costs almost nothing.** Over a run the bot spends 450 s on wishes instead of 90 s. Wasted time at the cap drops by 317–321 s in every life, so most of the extra spend comes out of time that would have been thrown away anyway.
  - Net cost against the 0-candle run: about 51–90 s over 40 years for 5 candles, and 8–50 s for 1 candle.
  - This is approximate, because card offers diverge between columns.
- **The wallet fully recovers before the next birthday.** At ages 19, 29, and 39 the 1- and 5-candle runs hold the same wallet (328.7 / 298.9 / 300.0 s for the A-start lives). The 5-candle dip to ~110 s at age 11 is gone within the decade.
- **Gift power doesn't show up in the outcome either.**
  - The 5-candle column ends with 10–16 % more raw DPS than the 1-candle column (AAA 809.8 vs 700.0) and still ties it.
  - Its Legendary gift at 30 did nothing (True Aim on top of Armor Breaker), and it still ties.
  - The waves don't press hard enough for the bot to tell a Small gift from a Legendary one.
- **Even one candle erases the differences between lives.** Every life ends at 0 leaks, and the only spread left is Long Summer's +1 candle (300 vs 330 s).
  - Before any wish, the lives were separated by the age-40 wave: AAS 12 leaks, SAA and SAS 3, AAA 1.
  - A Small gift (+25–35 % of one stat) is enough to clear it.
  - AAS's 12 leaks turn into its lowest wallet of the run (65.4 s at 32 with 1 candle), but no leaks.

What the correction did fix:

- **Wishing no longer pays you back.** The old Second Wind refunded the 150 s at once, and time after a wish was *higher* (95.5 → 275.5 s). Now a wish is always a visible drop:
  - at age 10: 245.5 → 95.5 s,
  - lowest year-end wallet afterwards: 109.9 s,
  - no refill from the gift itself.
- **The candle cap holds.** Final caps are 10–11 candles, not the old 16–17, and §7's tier caps show through again.
- **Unlit candles cost the bot nothing.** The 0-candle runs give *exactly* the Phase 2 results (time left and leaks in all five lives). The only change is 90 s less wasted time in each: the three extra candles from Birthday Wish and The Eleventh Candle used to arrive lit, and that time went straight into overflow.

## Caveats

- One seed, one map, one bot. The bot's build plan never uses extra tower capacity, and it can't value gifts the way a player would.
- Final time left is mostly the final cap for any run that wins cleanly. Leaks, wasted time, and the lowest wallet are the numbers that move.
- Different wish columns draw different card offers (see Setup).
- The economy overflows because there is nothing to spend on yet (towers max out at level 3, and there are only 4 tower kinds). As agreed, this is expected to change in the content phases, so re-measure then.

## History: first Phase 3 measurement (superseded)

With the ×1/×2/×3 value placeholder and the three §11.2 Lifetime cards, the greedy bot blew 5 candles at every birthday:

- Every life ended at its full cap with 0 leaks: 480 s for A-start lives, 510 s for S-start lives.
- Caps sat at 16–19 candles, because Second Wind ×3 added 6 lit candles at age 10.
- The raw reports are `Logs/autoplay_phase3_*.txt`.
