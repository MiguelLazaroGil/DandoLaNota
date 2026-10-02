using System.Collections.Generic;
using System.Diagnostics;
using GVLoadSystem.Core;
using GVUtils.Attributes;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GVLoadSystem.Examples
{

    /// <summary>
    /// Benchmarks GroupValues Get/Set performance.
    /// Attach to any GameObject with a GroupValues assigned.
    /// Press the BenchMark button in the Inspector.
    /// </summary>
    public class GroupValuesBenchmark : MonoBehaviour
    {
        [SerializeField] GroupValues groupValues;
        [SerializeField] int iterations = 1000;

        [Button("BenchMark")]
        public void RunBenchmark()
        {
            if (groupValues == null)
            {
                Debug.LogError("[Benchmark] No GroupValues assigned.");
                return;
            }

            // Collect all entry keys upfront so random access is fair
            var keys = new List<string>();
            foreach (var field in groupValues.fields)
                foreach (var entry in field.entries)
                    if (!string.IsNullOrEmpty(entry.name))
                        keys.Add(entry.name);

            if (keys.Count == 0)
            {
                Debug.LogError("[Benchmark] GroupValues has no entries.");
                return;
            }

            // Ensure cache is built before measuring — we benchmark access, not first build
            groupValues.RebuildCache();

            var rng = new System.Random(42); // fixed seed for reproducibility

            // ── GET benchmark ─────────────────────────────────────────────
            long getTotalTicks = 0;
            long getBestTicks = long.MaxValue;
            var sw = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                string key = keys[rng.Next(keys.Count)];
                var entry = GetEntryByKey(groupValues, key);
                if (entry == null) continue;

                sw.Restart();
                _ = groupValues.GetValue<object>(key);
                sw.Stop();

                getTotalTicks += sw.ElapsedTicks;
                if (sw.ElapsedTicks < getBestTicks)
                    getBestTicks = sw.ElapsedTicks;
            }

            double getTotalMs = TicksToMs(getTotalTicks);
            double getBestUs = TicksToUs(getBestTicks);
            double getAvgUs = TicksToUs(getTotalTicks / iterations);

            // ── SET benchmark ─────────────────────────────────────────────
            long setTotalTicks = 0;
            long setBestTicks = long.MaxValue;

            rng = new System.Random(42); // same seed

            for (int i = 0; i < iterations; i++)
            {
                string key = keys[rng.Next(keys.Count)];
                var entry = GetEntryByKey(groupValues, key);
                if (entry?.value == null) continue;

                object dummy = entry.value.GetValue(); // same type, same value

                sw.Restart();
                groupValues.SetValue(key, dummy);
                sw.Stop();

                setTotalTicks += sw.ElapsedTicks;
                if (sw.ElapsedTicks < setBestTicks)
                    setBestTicks = sw.ElapsedTicks;
            }

            double setTotalMs = TicksToMs(setTotalTicks);
            double setBestUs = TicksToUs(setBestTicks);
            double setAvgUs = TicksToUs(setTotalTicks / iterations);

            // ── Report ────────────────────────────────────────────────────
#if LOG_LOADSYSTEM
        Debug.Log($"[Benchmark] GroupValues: '{groupValues.name}' | " +
            $"Entries: {keys.Count} | Iterations: {iterations}\n" +
            $"─────────────────────────────────────\n" +
            $"GET  | Total: {getTotalMs:F3} ms | " +
            $"Avg: {getAvgUs:F3} µs | " +
            $"Best: {getBestUs:F3} µs\n" +
            $"SET  | Total: {setTotalMs:F3} ms | " +
            $"Avg: {setAvgUs:F3} µs | " +
            $"Best: {setBestUs:F3} µs");
#endif
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static GVEntry GetEntryByKey(GroupValues gv, string key)
        {
            foreach (var field in gv.fields)
                foreach (var entry in field.entries)
                    if (entry.name == key) return entry;
            return null;
        }

        static double TicksToMs(long ticks)
            => (double)ticks / Stopwatch.Frequency * 1000.0;

        static double TicksToUs(long ticks)
            => (double)ticks / Stopwatch.Frequency * 1_000_000.0;
    }
}