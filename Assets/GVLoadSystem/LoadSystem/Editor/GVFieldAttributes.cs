using System;
using System.Reflection;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Excludes this field from JSON serialization in GroupValues CUSTOM entries.
    /// The field keeps its C# default value when loaded — it is never written to
    /// or read from the save file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class DontSaveAttribute : Attribute { }
    /// <summary>
    /// Excludes this field from being seing in Group Values.
    /// The field keeps its C# default value when loaded - it can be written
    /// or read from the save file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public class GVHideAttribute : Attribute { }

    /// <summary>
    /// Makes this field read-only in the GroupValues inspector and prevents
    /// GVEntryReference.Set from modifying it.
    /// The value IS serialized and loaded normally — it is only protected from
    /// manual edits.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class GVReadOnlyAttribute : Attribute { }

    /// <summary>
    /// Serializes this field under a different key name in the JSON.
    /// Use when renaming a C# field to preserve existing save data:
    ///   Old: public float speed;       → JSON key: "speed"
    ///   New: public float moveSpeed;   → JSON key: "speed" (via [SaveAs("speed")])
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class SaveAsAttribute : Attribute
    {
        public readonly string JsonKey;
        public SaveAsAttribute(string jsonKey) => JsonKey = jsonKey;
    }

    /// <summary>
    /// Clamps this field to [min, max] when saving and loading.
    /// Works on float, int, double, long, short and byte fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class GVRangeAttribute : Attribute
    {
        public readonly double Min;
        public readonly double Max;
        public GVRangeAttribute(double min, double max) { Min = min; Max = max; }
    }

    /// <summary>
    /// Clamps this field to a minimum value when saving and loading.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class GVMinAttribute : Attribute
    {
        public readonly double Min;
        public GVMinAttribute(double min) => Min = min;
    }

    /// <summary>
    /// Clamps this field to a maximum value when saving and loading.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class GVMaxAttribute : Attribute
    {
        public readonly double Max;
        public GVMaxAttribute(double max) => Max = max;
    }

    /// <summary>
    /// This field can only be written once — subsequent Set calls are ignored
    /// if the field already has a non-default value.
    /// Useful for unique IDs or first-run values that must never change.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class WriteOnceAttribute : Attribute { }

    /// <summary>
    /// This field can only be written N times total across its lifetime.
    /// The write count is tracked in a companion field named "__{fieldName}WriteCount".
    /// If no companion field exists, the limit is not enforced.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class WriteNAttribute : Attribute
    {
        public readonly int MaxWrites;
        public WriteNAttribute(int maxWrites) => MaxWrites = maxWrites;
    }

    /// <summary>
    /// Static helpers for applying GV field attributes during serialization.
    /// Used by GroupValuesWrapper and GVEntryReference path navigation.
    /// </summary>
    internal static class GVFieldAttributeHelper
    {
        // ── Attribute checks ──────────────────────────────────────────────

        public static bool IsDontSave(System.Reflection.FieldInfo fi)
            => fi.GetCustomAttributes(typeof(DontSaveAttribute), true).Length > 0;
        public static bool IsHide(System.Reflection.FieldInfo fi)
            => fi.GetCustomAttribute<GVHideAttribute>() != null;

        public static bool IsReadOnly(System.Reflection.FieldInfo fi)
            => fi.GetCustomAttributes(typeof(GVReadOnlyAttribute), true).Length > 0;

        public static bool IsWriteOnce(System.Reflection.FieldInfo fi)
            => fi.GetCustomAttributes(typeof(WriteOnceAttribute), true).Length > 0;

        public static int GetMaxWrites(System.Reflection.FieldInfo fi)
        {
            var attr = fi.GetCustomAttributes(typeof(WriteNAttribute), true);
            return attr.Length > 0 ? ((WriteNAttribute)attr[0]).MaxWrites : -1;
        }

        public static string GetJsonKey(System.Reflection.FieldInfo fi)
        {
            var attr = fi.GetCustomAttributes(typeof(SaveAsAttribute), true);
            return attr.Length > 0 ? ((SaveAsAttribute)attr[0]).JsonKey : fi.Name;
        }

        // ── Write guard ───────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this field can be written given the current instance state.
        /// Checks WriteOnce (value must be default) and WriteN (count must be under limit).
        /// </summary>
        public static bool CanWrite(System.Reflection.FieldInfo fi, object instance)
            => CanWrite(fi, instance, countAsWrite: false);

        /// <summary>Internal overload — countAsWrite=true increments counter after check.</summary>
        public static bool CanWrite(System.Reflection.FieldInfo fi, object instance,
                                    bool countAsWrite)
        {
            if (instance == null) return true;

            // WriteOnce — only writable if current value equals type default
            if (IsWriteOnce(fi))
            {
                object current = fi.GetValue(instance);
                object defValue = fi.FieldType.IsValueType
                    ? Activator.CreateInstance(fi.FieldType)
                    : null;
                bool isDefault = current == null
                    ? defValue == null
                    : current.Equals(defValue);
                if (!isDefault) return false;
            }

            // WriteN — only writable if write count is under the limit
            int maxWrites = GetMaxWrites(fi);
            if (maxWrites >= 0)
            {
                string counterName = $"__{fi.Name}WriteCount";
                var counterField = fi.DeclaringType?.GetField(counterName,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (counterField != null && counterField.FieldType == typeof(int))
                {
                    int count = (int)counterField.GetValue(instance);
                    if (count >= maxWrites) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Increments the write counter for a WriteN field if it exists.
        /// Call this after a successful write.
        /// </summary>
        public static void IncrementWriteCount(System.Reflection.FieldInfo fi, object instance)
        {
            if (instance == null) return;
            int maxWrites = GetMaxWrites(fi);
            if (maxWrites < 0) return;

            string counterName = $"__{fi.Name}WriteCount";
            var counterField = fi.DeclaringType?.GetField(counterName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (counterField != null && counterField.FieldType == typeof(int))
            {
                int count = (int)counterField.GetValue(instance);
                counterField.SetValue(instance, count + 1);
            }
        }

        /// <summary>Returns current write count for a WriteN field. -1 if not applicable.</summary>
        public static int GetWriteCount(System.Reflection.FieldInfo fi, object instance)
        {
            if (instance == null) return -1;
            int maxWrites = GetMaxWrites(fi);
            if (maxWrites < 0) return -1;

            string counterName = $"__{fi.Name}WriteCount";
            var counterField = fi.DeclaringType?.GetField(counterName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (counterField != null && counterField.FieldType == typeof(int))
                return (int)counterField.GetValue(instance);

            return 0;
        }

        /// <summary>Returns remaining writes for a WriteN field. -1 if not applicable.</summary>
        public static int GetRemainingWrites(System.Reflection.FieldInfo fi, object instance)
        {
            int maxWrites = GetMaxWrites(fi);
            if (maxWrites < 0) return -1;
            int current = GetWriteCount(fi, instance);
            return Mathf.Max(0, maxWrites - current);
        }

        /// <summary>Resets the write counter for a WriteN field to 0.</summary>
        public static void ResetWriteCount(System.Reflection.FieldInfo fi, object instance)
        {
            if (instance == null) return;
            string counterName = $"__{fi.Name}WriteCount";
            var counterField = fi.DeclaringType?.GetField(counterName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (counterField != null && counterField.FieldType == typeof(int))
                counterField.SetValue(instance, 0);
        }

        // ── Range label ───────────────────────────────────────────────────

        /// <summary>
        /// Returns a short label describing the range constraint on this field.
        /// e.g. "Min:3", "Max:10", "Rng:1-10", or "" if no constraint.
        /// </summary>
        public static string GetRangeLabel(System.Reflection.FieldInfo fi)
        {
            var range = fi.GetCustomAttributes(typeof(GVRangeAttribute), true);
            if (range.Length > 0)
            {
                var r = (GVRangeAttribute)range[0];
                return $"Rng:{FormatNum(r.Min)}-{FormatNum(r.Max)}";
            }

            var minAttr = fi.GetCustomAttributes(typeof(GVMinAttribute), true);
            var maxAttr = fi.GetCustomAttributes(typeof(GVMaxAttribute), true);

            bool hasMin = minAttr.Length > 0;
            bool hasMax = maxAttr.Length > 0;

            if (hasMin && hasMax)
            {
                double mn = ((GVMinAttribute)minAttr[0]).Min;
                double mx = ((GVMaxAttribute)maxAttr[0]).Max;
                return $"Rng:{FormatNum(mn)}-{FormatNum(mx)}";
            }
            if (hasMin) return $"Min:{FormatNum(((GVMinAttribute)minAttr[0]).Min)}";
            if (hasMax) return $"Max:{FormatNum(((GVMaxAttribute)maxAttr[0]).Max)}";

            return "";
        }

        static string FormatNum(double v)
        {
            // Show as int if whole number, otherwise 1 decimal
            return v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("F1");
        }

        // ── Clamping ──────────────────────────────────────────────────────

        /// <summary>
        /// Clamps a value according to GVRange, GVMin, GVMax attributes.
        /// Returns the original value if no clamping attribute is present.
        /// </summary>
        public static object ClampValue(System.Reflection.FieldInfo fi, object value)
        {
            if (value == null) return value;

            double min = double.MinValue;
            double max = double.MaxValue;
            bool hasClamp = false;

            var range = fi.GetCustomAttributes(typeof(GVRangeAttribute), true);
            if (range.Length > 0)
            {
                var r = (GVRangeAttribute)range[0];
                min = r.Min;
                max = r.Max;
                hasClamp = true;
            }

            var minAttr = fi.GetCustomAttributes(typeof(GVMinAttribute), true);
            if (minAttr.Length > 0)
            {
                min = Math.Max(min, ((GVMinAttribute)minAttr[0]).Min);
                hasClamp = true;
            }

            var maxAttr = fi.GetCustomAttributes(typeof(GVMaxAttribute), true);
            if (maxAttr.Length > 0)
            {
                max = Math.Min(max, ((GVMaxAttribute)maxAttr[0]).Max);
                hasClamp = true;
            }

            if (!hasClamp) return value;

            try
            {
                double d = Convert.ToDouble(value);
                double clamped = Math.Max(min, Math.Min(max, d));
                return Convert.ChangeType(clamped, value.GetType());
            }
            catch { return value; }
        }
    }
}