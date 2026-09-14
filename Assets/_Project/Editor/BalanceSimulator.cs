using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TenCandles.Core;
using TenCandles.Lifetime;
using TenCandles.Waves;
using UnityEditor;
using UnityEngine;

namespace TenCandles.EditorTools
{
    // Spec §17 balance harness. Prints the §8 wave table from WaveGenerator and runs the checks, without Play mode.
    // Menu: Tools > Ten Candles > Balance Simulator.
    // Batch: -executeMethod TenCandles.EditorTools.BalanceSimulator.RunFromCommandLine (writes Logs/balance_report.txt, exit 1 on failure)
    public static class BalanceSimulator
    {
        const float TableTolerance = 0.02f;
        const float ContinuityTolerance = 0.05f;
        const float MinRunMinutes = 19f, MaxRunMinutes = 25f;
        const float MinIncomeRatio = 2.5f, MaxIncomeRatio = 3.5f;

        // §8 regression fixture, copied verbatim from the spec (rounded as printed there).
        //                     tier year age count unitHp waveHp duration dps  reward income
        static readonly float[][] Fixture =
        {
            new[] { 0f,  1f,  1f,  8f,  10f,    80f, 16f,   5f, 1.26f,  10f },
            new[] { 0f,  5f,  5f, 16f,  17f,   275f, 21f,  13f, 1.90f,  30f },
            new[] { 0f, 10f, 10f, 26f,  26f,   681f, 24f,  29f, 2.70f,  70f },
            new[] { 1f,  1f, 11f, 11f,  26f,   286f, 19f,  15f, 1.95f,  21f },
            new[] { 1f,  5f, 15f, 19f,  45f,   850f, 23f,  36f, 2.95f,  56f },
            new[] { 1f, 10f, 20f, 29f,  68f,  1975f, 25f,  80f, 4.19f, 121f },
            new[] { 2f,  1f, 21f, 14f,  68f,   952f, 21f,  45f, 2.65f,  37f },
            new[] { 2f,  5f, 25f, 22f, 117f,  2573f, 25f, 103f, 3.99f,  88f },
            new[] { 2f, 10f, 30f, 32f, 178f,  5701f, 25f, 226f, 5.67f, 181f },
            new[] { 3f,  1f, 31f, 17f, 175f,  2975f, 24f, 126f, 3.34f,  57f },
            new[] { 3f,  5f, 35f, 25f, 301f,  7525f, 26f, 284f, 5.04f, 126f },
            new[] { 3f, 10f, 40f, 35f, 458f, 16048f, 26f, 628f, 7.16f, 250f },
        };
        static readonly string[] Columns = { "tier", "year", "age", "count", "unit HP", "wave HP", "duration", "req DPS", "reward", "income" };
        // Precision each column is printed with in §8 (integers, or 2 decimals for reward).
        static readonly float[] PrintedStep = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0.01f, 1f };

        const float FixtureEnemies = 860f, FixtureWaveSeconds = 942f, FixtureRunSeconds = 1302f, FixtureIncome = 3464f;

        public struct Row
        {
            public int Tier, Year, Age, Count;
            public float UnitHp, WaveHp, Duration, RequiredDps, Reward, Income;
        }

        public static Row Compute(int d, int y)
        {
            int count = WaveGenerator.Count(d, y);
            float hp = WaveGenerator.Hp(d, y);
            float duration = count * WaveGenerator.Gap(d, y) + Balance.SimWalkSeconds;
            float reward = WaveGenerator.Reward(d, y);
            return new Row
            {
                Tier = d, Year = y, Age = d * Balance.YearsPerDecade + y, Count = count,
                UnitHp = hp, WaveHp = count * hp, Duration = duration, RequiredDps = count * hp / duration,
                Reward = reward, Income = count * reward
            };
        }

        [MenuItem("Tools/Ten Candles/Balance Simulator")]
        static void RunFromMenu()
        {
            bool ok = Run(out string report);
            Debug.Log("[TenCandles] " + report);
            EditorUtility.DisplayDialog("Balance Simulator", ok ? "All checks passed. Full report in the Console." : "Some checks FAILED. Full report in the Console.", "OK");
        }

        public static void RunFromCommandLine()
        {
            bool ok = Run(out string report);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/balance_report.txt", report);
            Debug.Log("[TenCandles] " + report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            var towers = AssetDatabase.FindAssets("t:TowerData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<TowerData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(t => t != null)
                .Select(t => (t.displayName, t.baseCost));
            return Evaluate(towers, out report);
        }

        // Pure: no Unity editor APIs, so it can also run from a plain .NET harness.
        public static bool Evaluate(IEnumerable<(string name, float baseCost)> towers, out string report)
        {
            // The editor may run in a comma-decimal locale.
            var previousCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try { return EvaluateInvariant(towers, out report); }
            finally { CultureInfo.CurrentCulture = previousCulture; }
        }

        static bool EvaluateInvariant(IEnumerable<(string name, float baseCost)> towers, out string report)
        {
            var culture = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            bool allOk = true;

            void Check(string name, bool ok, string detail)
            {
                allOk &= ok;
                sb.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {name}: {detail}");
            }

            var rows = new List<Row>();
            for (int d = 0; d < Balance.DecadesPerRun; d++)
                for (int y = 1; y <= Balance.YearsPerDecade; y++)
                    rows.Add(Compute(d, y));
            Row At(int d, int y) => rows[d * Balance.YearsPerDecade + y - 1];

            sb.AppendLine("Ten Candles balance simulator (spec §17)");
            sb.AppendLine();
            sb.AppendLine("Tier Year Age Count  UnitHP   WaveHP  Dur s  ReqDPS  Rwd/kill  Income");
            foreach (var r in rows)
                sb.AppendLine(string.Format(culture, "{0,4} {1,4} {2,3} {3,5} {4,7:0.0} {5,8:0} {6,6:0.0} {7,7:0.0} {8,9:0.00} {9,7:0.0}",
                    r.Tier, r.Year, r.Age, r.Count, r.UnitHp, r.WaveHp, r.Duration, r.RequiredDps, r.Reward, r.Income));
            sb.AppendLine();

            // 1. §8 table within ±2 %. The spec prints rounded values, so a cell also passes when the
            //    computed value rounds to the printed one (within half its printed step).
            int cells = 0, failedCells = 0;
            float worst = 0f;
            string worstCell = "";
            var failures = new StringBuilder();
            var roundingOnly = new List<string>();
            foreach (var f in Fixture)
            {
                Row r = Compute((int)f[0], (int)f[1]);
                float[] got = { r.Tier, r.Year, r.Age, r.Count, r.UnitHp, r.WaveHp, r.Duration, r.RequiredDps, r.Reward, r.Income };
                for (int c = 0; c < got.Length; c++)
                {
                    cells++;
                    float diff = Math.Abs(got[c] - f[c]);
                    float rel = f[c] != 0f ? diff / Math.Abs(f[c]) : diff;
                    bool ok = rel <= TableTolerance || diff <= PrintedStep[c] * 0.5f + 1e-4f;
                    if (ok && rel > TableTolerance) roundingOnly.Add($"tier {f[0]} year {f[1]} {Columns[c]} ({got[c].ToString("0.###", culture)} prints as {f[c].ToString(culture)})");
                    if (rel > worst) { worst = rel; worstCell = $"tier {f[0]} year {f[1]} {Columns[c]} ({got[c].ToString("0.###", culture)} vs {f[c].ToString(culture)})"; }
                    if (!ok)
                    {
                        failedCells++;
                        failures.AppendLine($"       tier {f[0]} year {f[1]} {Columns[c]}: {got[c].ToString("0.###", culture)} vs {f[c].ToString(culture)} ({rel:P1})");
                    }
                }
            }
            Check("§8 table within ±2 %", failedCells == 0, $"{cells - failedCells}/{cells} cells match; largest relative gap {worst:P2} at {worstCell}");
            sb.Append(failures);
            foreach (string cell in roundingOnly) sb.AppendLine($"       over 2 % only because §8 prints it rounded: {cell}");

            float enemies = rows.Sum(r => r.Count);
            float waveSeconds = rows.Sum(r => r.Duration);
            float runSeconds = waveSeconds + rows.Count * Balance.IntermissionLength;
            float income = rows.Sum(r => r.Income);
            bool totalsOk = Near(enemies, FixtureEnemies) && Near(waveSeconds, FixtureWaveSeconds) && Near(runSeconds, FixtureRunSeconds) && Near(income, FixtureIncome);
            Check("§8 run totals within ±2 %", totalsOk,
                string.Format(culture, "{0:0} enemies (860), {1:0} s of waves (942), {2:0} s with intermissions (1302), {3:0} s kill income (3464)", enemies, waveSeconds, runSeconds, income));

            // 2. HP continuity across decade boundaries.
            var cont = new List<string>();
            bool contOk = true;
            for (int d = 0; d < Balance.DecadesPerRun - 1; d++)
            {
                float a = At(d, Balance.YearsPerDecade).UnitHp, b = At(d + 1, 1).UnitHp;
                float rel = Math.Abs(a - b) / a;
                contOk &= rel <= ContinuityTolerance;
                cont.Add(string.Format(culture, "{0:0.0}→{1:0.0} ({2:P1})", a, b, rel));
            }
            Check("HP continuity Hp(d,10) ≈ Hp(d+1,1) within 5 %", contOk, string.Join(", ", cont));

            // 3. Sawtooth: required DPS dips at every boundary, then climbs past the previous peak.
            var saw = new List<string>();
            bool sawOk = true;
            for (int d = 0; d < Balance.DecadesPerRun - 1; d++)
            {
                float peak = At(d, Balance.YearsPerDecade).RequiredDps;
                float dip = At(d + 1, 1).RequiredDps;
                float nextPeak = Enumerable.Range(1, Balance.YearsPerDecade).Max(y => At(d + 1, y).RequiredDps);
                sawOk &= dip < peak && nextPeak > peak;
                saw.Add(string.Format(culture, "{0:0}→{1:0}→{2:0}", peak, dip, nextPeak));
            }
            Check("Sawtooth (peak → dip → new peak)", sawOk, string.Join(", ", saw));

            // 4. No single upgrade step above the smallest stage wallet.
            float cap = Balance.TierCandleCap.Min() * Balance.SecondsPerCandle;
            float topStep = Balance.LevelCostMultiplier[Balance.MaxTowerLevel];
            var towerList = towers.ToList();
            var offenders = towerList.Where(t => t.baseCost * topStep > cap).Select(t => $"{t.name} L{Balance.MaxTowerLevel} = {t.baseCost * topStep:0} s").ToList();
            string priciest = towerList.Count == 0 ? "no TowerData assets found"
                : towerList.OrderByDescending(t => t.baseCost).Select(t => $"most expensive: {t.name} L{Balance.MaxTowerLevel} = {(t.baseCost * topStep).ToString("0", culture)} s").First();
            Check($"Tower upgrade step ≤ {cap:0} s", offenders.Count == 0, offenders.Count == 0 ? $"{towerList.Count} towers checked, {priciest}" : string.Join(", ", offenders));

            // 5. Run length.
            float minutes = runSeconds / 60f;
            Check("Run duration 19-25 min", minutes >= MinRunMinutes && minutes <= MaxRunMinutes, string.Format(culture, "{0:0.0} min", minutes));

            // 6. Tower budget: kill income over total drain.
            float drain = runSeconds * Balance.DrainRatePerSecond;
            float ratio = income / drain;
            Check("Kill income / drain 2.5-3.5×", ratio >= MinIncomeRatio && ratio <= MaxIncomeRatio, string.Format(culture, "{0:0} s / {1:0} s = {2:0.00}×", income, drain, ratio));

            // 7. Multi-lane invariant (§7): capacity ≥ 2 × active lanes at every age, using the tier's full lane allowance.
            var laneFailures = new List<string>();
            int tightestSlack = int.MaxValue;
            string tightest = "";
            for (int age = 1; age <= Balance.TotalYears; age++)
            {
                int tier = WaveGenerator.TierOfAge(age);
                int capacity = LifetimeManager.TowerCapacityFor(age);
                int needed = 2 * Balance.TierLaneAllowance[tier];
                if (capacity < needed) laneFailures.Add($"age {age}: {capacity} towers < 2 × {Balance.TierLaneAllowance[tier]} lanes");
                if (capacity - needed < tightestSlack)
                {
                    tightestSlack = capacity - needed;
                    tightest = $"tightest at age {age}: {capacity} towers for {Balance.TierLaneAllowance[tier]} lanes";
                }
            }
            Check("Tower capacity ≥ 2 × active lanes", laneFailures.Count == 0, laneFailures.Count == 0 ? $"ages 1-{Balance.TotalYears} checked, {tightest}" : string.Join(", ", laneFailures));

            sb.AppendLine();
            sb.AppendLine(allOk ? "RESULT: all checks passed" : "RESULT: some checks FAILED");
            report = sb.ToString();
            return allOk;
        }

        static bool Near(float got, float expected) => Math.Abs(got - expected) <= Math.Abs(expected) * TableTolerance;
    }
}
