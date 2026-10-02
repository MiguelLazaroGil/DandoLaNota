using System;
using System.Collections.Generic;
using System.Linq;
using GVLoadSystem.Core;
using UnityEngine;

namespace GVLoadSystem
{

    /// <summary>
    /// Statistical and query utilities for GroupValues.
    /// All methods have two overloads: whole GV or a specific field.
    /// Supports scalar numeric types (int, float, double, long) and Vector2/Vector3.
    /// </summary>
    public static class GroupValuesUtility
    {
        // ── GetAll ────────────────────────────────────────────────────────

        /// <summary>Returns all values of type T across all fields.</summary>
        public static List<T> GetAll<T>(GroupValues gv)
        {
            var values = new List<T>();
            if (gv == null) return values;

            foreach (var field in gv.fields)
                foreach (var entry in field.entries)
                    if (TryCast<T>(entry.value?.GetValue(), out var v))
                        values.Add(v);

            return values;
        }

        /// <summary>Returns all values of type T from a specific field.</summary>
        public static List<T> GetAll<T>(GroupValues gv, string fieldName)
        {
            var values = new List<T>();
            if (gv == null) return values;

            var field = gv.fields.Find(f => f.fieldName == fieldName);
            if (field == null) return values;

            foreach (var entry in field.entries)
                if (TryCast<T>(entry.value?.GetValue(), out var v))
                    values.Add(v);

            return values;
        }

        // ── Count ─────────────────────────────────────────────────────────

        public static int CountAll<T>(GroupValues gv) => GetAll<T>(gv).Count;
        public static int CountAll<T>(GroupValues gv, string field) => GetAll<T>(gv, field).Count;

        // ── Sum ───────────────────────────────────────────────────────────

        /// <summary>Sum of all numeric or vector values of type T.</summary>
        public static T SumAll<T>(GroupValues gv) => Sum(GetAll<T>(gv));
        public static T SumAll<T>(GroupValues gv, string field) => Sum(GetAll<T>(gv, field));

        // ── Max / Min ─────────────────────────────────────────────────────

        /// <summary>Maximum value. Works on IComparable types and vectors (by magnitude).</summary>
        public static T Max<T>(GroupValues gv) => MaxOf(GetAll<T>(gv));
        public static T Max<T>(GroupValues gv, string field) => MaxOf(GetAll<T>(gv, field));

        /// <summary>Minimum value. Works on IComparable types and vectors (by magnitude).</summary>
        public static T Min<T>(GroupValues gv) => MinOf(GetAll<T>(gv));
        public static T Min<T>(GroupValues gv, string field) => MinOf(GetAll<T>(gv, field));

        // ── Average ───────────────────────────────────────────────────────

        /// <summary>Arithmetic mean. Works on scalars and vectors.</summary>
        public static T Average<T>(GroupValues gv) => Avg(GetAll<T>(gv));
        public static T Average<T>(GroupValues gv, string field) => Avg(GetAll<T>(gv, field));

        // ── Variance / StdDev ─────────────────────────────────────────────

        /// <summary>
        /// Population variance. Works on scalar numeric types.
        /// Returns 0 for vectors (use component-wise variance manually).
        /// </summary>
        public static double Variance<T>(GroupValues gv) => Var(GetAll<T>(gv));
        public static double Variance<T>(GroupValues gv, string field) => Var(GetAll<T>(gv, field));

        /// <summary>Standard deviation (square root of variance).</summary>
        public static double StdDev<T>(GroupValues gv) => Math.Sqrt(Variance<T>(gv));
        public static double StdDev<T>(GroupValues gv, string field) => Math.Sqrt(Variance<T>(gv, field));

        // ── Percentages ───────────────────────────────────────────────────

        /// <summary>
        /// Returns (key, percentage) pairs for all numeric entries.
        /// Percentage is relative to the total sum of all numeric values in the GV/field.
        /// </summary>
        public static List<(string key, float pct)> CalculatePercentages(GroupValues gv)
            => NamedPercentages(gv, null);

        public static List<(string key, float pct)> CalculatePercentages(GroupValues gv, string field)
            => NamedPercentages(gv, field);

        /// <summary>
        /// Converts a (key, pct) list to a human-readable string.
        /// Example: "speed: 45.2%  damage: 30.1%  health: 24.7%"
        /// </summary>
        public static string FormatPercentages(List<(string key, float pct)> percentages,
                                               string separator = "  ")
        {
            if (percentages == null || percentages.Count == 0) return "(no data)";
            return string.Join(separator,
                percentages.Select(p => $"{p.key}: {p.pct:F1}%"));
        }

        // Overload: format directly from GV
        public static string FormatPercentages(GroupValues gv, string separator = "  ")
            => FormatPercentages(CalculatePercentages(gv), separator);

        public static string FormatPercentages(GroupValues gv, string field,
                                               string separator = "  ")
            => FormatPercentages(CalculatePercentages(gv, field), separator);

        // ── Most used words ───────────────────────────────────────────────

        /// <summary>
        /// Returns (word, count) pairs sorted by frequency descending.
        /// </summary>
        public static List<(string word, int count)> MostUsedWords(GroupValues gv)
            => WordFrequency(GetAll<string>(gv));

        public static List<(string word, int count)> MostUsedWords(GroupValues gv, string field)
            => WordFrequency(GetAll<string>(gv, field));

        /// <summary>Formats word frequency as "word(n), word(n), ..."</summary>
        public static string FormatMostUsedWords(GroupValues gv, int topN = 10)
        {
            var words = MostUsedWords(gv).Take(topN);
            return string.Join(", ", words.Select(w => $"{w.word}({w.count})"));
        }

        public static string FormatMostUsedWords(GroupValues gv, string field, int topN = 10)
        {
            var words = MostUsedWords(gv, field).Take(topN);
            return string.Join(", ", words.Select(w => $"{w.word}({w.count})"));
        }

        // ── Type distribution ────────────────────────────────────────────

        /// <summary>
        /// Returns the percentage of entries per VALUE_TYPE across the whole GV.
        /// Example: FLOAT:42.9%  INT:28.6%  BOOL:14.3%
        /// </summary>
        public static List<(VALUE_TYPE type, float pct)> TypeDistribution(GroupValues gv)
            => CalcTypeDistribution(gv, null);

        public static List<(VALUE_TYPE type, float pct)> TypeDistribution(GroupValues gv, string field)
            => CalcTypeDistribution(gv, field);

        /// <summary>Formats TypeDistribution as "FLOAT:42.9%  INT:28.6%  BOOL:14.3%"</summary>
        public static string FormatTypeDistribution(GroupValues gv, string separator = "  ")
            => FormatTypeDist(TypeDistribution(gv), separator);

        public static string FormatTypeDistribution(GroupValues gv, string field,
                                                    string separator = "  ")
            => FormatTypeDist(TypeDistribution(gv, field), separator);

        static string FormatTypeDist(List<(VALUE_TYPE type, float pct)> dist, string sep)
        {
            if (dist == null || dist.Count == 0) return "(no data)";
            return string.Join(sep, dist.Select(d => $"{d.type}:{d.pct:F1}%"));
        }

        static List<(VALUE_TYPE type, float pct)> CalcTypeDistribution(GroupValues gv,
                                                                        string fieldFilter)
        {
            var counts = new Dictionary<VALUE_TYPE, int>();

            var fields = fieldFilter == null
                ? gv.fields
                : gv.fields.Where(f => f.fieldName == fieldFilter).ToList();

            int total = 0;
            foreach (var field in fields)
                foreach (var entry in field.entries)
                {
                    if (!counts.ContainsKey(entry.type)) counts[entry.type] = 0;
                    counts[entry.type]++;
                    total++;
                }

            if (total == 0) return new List<(VALUE_TYPE, float)>();

            return counts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => (kv.Key, (float)kv.Value / total * 100f))
                .ToList();
        }

        // ── Normalize ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns values normalized to [0,1] range based on min and max.
        /// </summary>
        public static List<float> Normalize<T>(GroupValues gv)
            => NormalizeList(GetAll<T>(gv).Select(ToFloat).ToList());

        public static List<float> Normalize<T>(GroupValues gv, string field)
            => NormalizeList(GetAll<T>(gv, field).Select(ToFloat).ToList());

        // ── Median ────────────────────────────────────────────────────────

        /// <summary>Median value of a numeric type.</summary>
        public static double Median<T>(GroupValues gv)
            => MedianOf(GetAll<T>(gv).Select(ToDouble).ToList());

        public static double Median<T>(GroupValues gv, string field)
            => MedianOf(GetAll<T>(gv, field).Select(ToDouble).ToList());

        // ════════════════════════════════════════════════════════════════
        // Internal implementations
        // ════════════════════════════════════════════════════════════════

        static T Sum<T>(List<T> list)
        {
            if (list.Count == 0) return default;
            if (list is List<Vector2> v2l) { var s = Vector2.zero; foreach (var v in v2l) s += v; return (T)(object)s; }
            if (list is List<Vector3> v3l) { var s = Vector3.zero; foreach (var v in v3l) s += v; return (T)(object)s; }
            if (list is List<Vector4> v4l) { var s = Vector4.zero; foreach (var v in v4l) s += v; return (T)(object)s; }
            double sum = 0;
            foreach (var item in list) sum += ToDouble(item);
            return FromDouble<T>(sum);
        }

        static T MaxOf<T>(List<T> list)
        {
            if (list.Count == 0) return default;
            if (list is List<Vector2> v2) return (T)(object)v2.OrderByDescending(v => v.magnitude).First();
            if (list is List<Vector3> v3) return (T)(object)v3.OrderByDescending(v => v.magnitude).First();
            if (list is List<Vector4> v4) return (T)(object)v4.OrderByDescending(v => v.magnitude).First();
            if (list[0] is IComparable) return list.Max();
            return list[0];
        }

        static T MinOf<T>(List<T> list)
        {
            if (list.Count == 0) return default;
            if (list is List<Vector2> v2) return (T)(object)v2.OrderBy(v => v.magnitude).First();
            if (list is List<Vector3> v3) return (T)(object)v3.OrderBy(v => v.magnitude).First();
            if (list is List<Vector4> v4) return (T)(object)v4.OrderBy(v => v.magnitude).First();
            if (list[0] is IComparable) return list.Min();
            return list[0];
        }

        static T Avg<T>(List<T> list)
        {
            if (list.Count == 0) return default;
            if (list is List<Vector2> v2) { var s = Vector2.zero; foreach (var v in v2) s += v; return (T)(object)(s / list.Count); }
            if (list is List<Vector3> v3) { var s = Vector3.zero; foreach (var v in v3) s += v; return (T)(object)(s / list.Count); }
            if (list is List<Vector4> v4) { var s = Vector4.zero; foreach (var v in v4) s += v; return (T)(object)(s / list.Count); }
            double sum = list.Sum(ToDouble);
            return FromDouble<T>(sum / list.Count);
        }

        static double Var<T>(List<T> list)
        {
            if (list.Count < 2) return 0;
            double mean = list.Average(ToDouble);
            return list.Average(x => Math.Pow(ToDouble(x) - mean, 2));
        }

        static List<(string key, float pct)> NamedPercentages(GroupValues gv, string fieldFilter)
        {
            var result = new List<(string, float)>();
            var pairs = new List<(string key, double val)>();

            var fields = fieldFilter == null
                ? gv.fields
                : gv.fields.Where(f => f.fieldName == fieldFilter).ToList();

            foreach (var field in fields)
                foreach (var entry in field.entries)
                {
                    object raw = entry.value?.GetValue();
                    if (raw == null) continue;
                    try { pairs.Add((entry.name, ToDouble(raw))); } catch { }
                }

            double total = pairs.Sum(p => p.val);
            if (Math.Abs(total) < 1e-10) return result;

            foreach (var (key, val) in pairs)
                result.Add((key, (float)(val / total * 100.0)));

            return result;
        }

        static List<float> Percentages(List<float> values)
        {
            float total = values.Sum();
            if (Mathf.Approximately(total, 0)) return new List<float>();
            return values.Select(v => v / total * 100f).ToList();
        }

        static List<float> Percentages(List<double> values)
        {
            double total = values.Sum();
            if (Math.Abs(total) < 1e-10) return new List<float>();
            return values.Select(v => (float)(v / total * 100.0)).ToList();
        }

        static List<(string word, int count)> WordFrequency(List<string> texts)
        {
            var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var text in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;
                foreach (var word in text.Split(new[] { ' ', '\n', '\r', '\t', ',', '.', '!', '?' },
                                                StringSplitOptions.RemoveEmptyEntries))
                {
                    var w = word.ToLower();
                    freq[w] = freq.TryGetValue(w, out int c) ? c + 1 : 1;
                }
            }
            return freq.OrderByDescending(kv => kv.Value)
                       .Select(kv => (kv.Key, kv.Value)).ToList();
        }

        static List<float> NormalizeList(List<float> values)
        {
            if (values.Count == 0) return values;
            float min = values.Min();
            float max = values.Max();
            float range = max - min;
            if (Mathf.Approximately(range, 0)) return values.Select(_ => 0f).ToList();
            return values.Select(v => (v - min) / range).ToList();
        }

        static double MedianOf(List<double> values)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }

        // ── Type conversion helpers ───────────────────────────────────────

        static bool TryCast<T>(object raw, out T result)
        {
            if (raw is T t) { result = t; return true; }
            try
            {
                result = (T)Convert.ChangeType(raw, typeof(T));
                return true;
            }
            catch { result = default; return false; }
        }

        static double ToDouble<T>(T value)
        {
            try { return Convert.ToDouble(value); }
            catch { return 0; }
        }

        static float ToFloat<T>(T value)
        {
            try { return Convert.ToSingle(value); }
            catch { return 0f; }
        }

        static T FromDouble<T>(double value)
        {
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch { return default; }
        }
    }
}