// GroupValues.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GVLoadSystem.Core
{
    public enum VALUE_TYPE
    {
        BOOL, FLOAT, DOUBLE, SHORT, INT, LONG,
        VECTOR2, VECTOR3, CHAR, STRING, BYTE, CUSTOM,
    }

    #region GROUP VALUES
    [CreateAssetMenu(menuName = "LoadSystem/GroupValues")]
    public partial class GroupValues : ScriptableObject
    {
        public List<GVField> fields = new();

        /// <summary>Semantic version of this GroupValues asset.</summary>
        public GVVersion version = new GVVersion(1, 0, 0);

        // ── Cache ─────────────────────────────────────────────────────────
        [NonSerialized] Dictionary<string, GVEntry> _cache;

        public void RebuildCache()
        {
            _cache = new Dictionary<string, GVEntry>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                foreach (var entry in field.entries)
                {
                    if (string.IsNullOrEmpty(entry.name)) continue;
                    if (_cache.ContainsKey(entry.name))
                        Debug.LogWarning(
                            $"[GroupValues] Duplicate key '{entry.name}' in '{name}'. " +
                            $"Only the first occurrence is reachable by key.");
                    else
                        _cache[entry.name] = entry;
                }
            }
        }

        void EnsureCache()
        {
            if (_cache == null) RebuildCache();
        }

        // ── Key validation ────────────────────────────────────────────────
        /// <summary>Returns the VALUE_TYPE of an entry by key. O(1) via cache.</summary>
        public bool TryGetEntryType(string key, out VALUE_TYPE type)
        {
            EnsureCache();
            if (_cache.TryGetValue(key, out var entry))
            {
                type = entry.type;
                return true;
            }
            type = VALUE_TYPE.STRING;
            return false;
        }

        public bool ContainsKey(string key)
        {
            EnsureCache();
            return _cache.ContainsKey(key);
        }

        public bool IsKeyValid(string key, GVEntry entryBeingRenamed = null)
        {
            if (string.IsNullOrEmpty(key)) return false;
            EnsureCache();
            if (!_cache.TryGetValue(key, out var existing)) return true;
            return existing == entryBeingRenamed;
        }

        // ── Structured mutations ──────────────────────────────────────────
        public bool TryAddEntry(string fieldName, GVEntry entry)
        {
            EnsureCache();
            if (_cache.ContainsKey(entry.name))
            {
                Debug.LogWarning($"[GroupValues] Key '{entry.name}' already exists. Entry not added.");
                return false;
            }
            var field = fields.Find(f => f.fieldName == fieldName);
            if (field == null)
            {
                Debug.LogError($"[GroupValues] Field '{fieldName}' not found.");
                return false;
            }
            field.entries.Add(entry);
            _cache[entry.name] = entry;
            return true;
        }

        public bool TryRemoveEntry(string key)
        {
            EnsureCache();
            if (!_cache.TryGetValue(key, out var entry))
            {
                Debug.LogWarning($"[GroupValues] Key '{key}' not found. Nothing removed.");
                return false;
            }
            foreach (var field in fields)
            {
                if (field.entries.Remove(entry))
                {
                    _cache.Remove(key);
                    return true;
                }
            }
            return false;
        }

        public bool TryRenameEntry(string oldName, string newName)
        {
            EnsureCache();
            if (!_cache.TryGetValue(oldName, out var entry))
            {
                Debug.LogWarning($"[GroupValues] Key '{oldName}' not found.");
                return false;
            }
            if (_cache.ContainsKey(newName))
            {
                Debug.LogWarning($"[GroupValues] Key '{newName}' already exists. Rename aborted.");
                return false;
            }
            _cache.Remove(oldName);
            entry.name = newName;
            _cache[newName] = entry;
            return true;
        }

        // ── FindEntry ─────────────────────────────────────────────────────
        GVEntry FindEntry(string name)
        {
            EnsureCache();
            return _cache.TryGetValue(name, out var e) ? e : null;
        }

        GVEntry FindEntry(string fieldName, string entryName)
        {
            var f = fields.Find(f => f.fieldName == fieldName);
            return f?.entries.Find(e => e.name == entryName);
        }

        // ── GetValue ──────────────────────────────────────────────────────
        public T GetValue<T>(string fieldName, string entryName)
        {
            var entry = FindEntry(fieldName, entryName);
            if (entry == null) return default;
            return CastValue<T>(entry.value.GetValue(), entryName);
        }

        public T GetValue<T>(string name)
        {
            var entry = FindEntry(name);
            if (entry == null)
                throw new KeyNotFoundException(
                    $"[GroupValues] No entry found with key '{name}'.");
            return CastValue<T>(entry.value.GetValue(), name);
        }

        static T CastValue<T>(object rawValue, string key)
        {
            if (rawValue is string json &&
                !typeof(T).IsPrimitive &&
                typeof(T) != typeof(string) &&
                typeof(T) != typeof(object))
                return JsonUtility.FromJson<T>(json);
            try
            {
                if (rawValue is T tValue) return tValue;
                return (T)Convert.ChangeType(rawValue, typeof(T));
            }
            catch (Exception ex)
            {
                string wanted = typeof(T).Name;
                string actual = rawValue != null ? rawValue.GetType().Name : "null";
                throw new InvalidCastException(
                    $"[GroupValues] Cast error: tried to convert '{actual}' " +
                    $"to '{wanted}' for key '{key}'.", ex);
            }
        }

        // ── SetValue ──────────────────────────────────────────────────────
        public void SetValue<T>(string fieldName, string entryName, T newValue)
        {
            var entry = FindEntry(fieldName, entryName);
            if (entry == null)
            {
                Debug.LogError($"[GroupValues] Entry '{entryName}' not found in field '{fieldName}'.");
                return;
            }
            WriteValue(entry, newValue);
        }

        public void SetValue<T>(string name, T v)
        {
            var entry = FindEntry(name);
            if (entry == null)
            {
                Debug.LogError($"[GroupValues] No entry found with key '{name}'.");
                throw new KeyNotFoundException($"[GroupValues] No entry found with key '{name}'.");
            }
            WriteValue(entry, v);
        }

        static void WriteValue<T>(GVEntry entry, T v)
        {
            if (entry.type == VALUE_TYPE.CUSTOM)
            {
                entry.value.SetValue(JsonUtility.ToJson(v));
                return;
            }
            if (v is Vector2 or Vector3)
            {
                entry.value.SetValue(v);
                return;
            }
            object boxed = (typeof(T) == typeof(int) && v is float f)
                ? (object)Mathf.RoundToInt(f)
                : Convert.ChangeType(v, typeof(T));
            entry.value.SetValue(boxed);
        }

        // ── SetEntryValue ─────────────────────────────────────────────────
        public void SetEntryValue(GVEntry newEntry)
        {
            var entry = FindEntry(newEntry.name);
            if (entry != null)
                entry.value = newEntry.value;
            else
                Debug.LogWarning($"[GroupValues] Entry '{newEntry.name}' not found.");
        }

        public void SetEntryValue(int fieldIndex, int entryIndex, GVValue newValue)
        {
            if (fieldIndex < 0 || fieldIndex >= fields.Count ||
                entryIndex < 0 || entryIndex >= fields[fieldIndex].entries.Count)
            {
                Debug.LogWarning("[GroupValues] Index out of range in SetEntryValue.");
                return;
            }
            fields[fieldIndex].entries[entryIndex].value = newValue;
            // Only value changed, key unchanged — cache remains valid
        }

        // ── Utilities ─────────────────────────────────────────────────────
        public GroupValues Clone()
        {
            var clone = CreateInstance<GroupValues>();
            clone.version = version.Clone();
            clone.fields = new List<GVField>();
            foreach (var field in fields)
                clone.fields.Add(field.Clone());
            return clone;
        }

        public void CopyFrom(GroupValues other)
        {
            version = other.version;
            fields.Clear();
            foreach (var field in other.fields)
                fields.Add(field.Clone());
            RebuildCache();
        }

        public bool IsTheSame(GroupValues other)
        {
            if (other == null || fields.Count != other.fields.Count) return false;
            foreach (var field in fields)
            {
                var otherField = other.fields.Find(f => f.fieldName == field.fieldName);
                if (otherField == null || field.entries.Count != otherField.entries.Count)
                    return false;
                foreach (var e in field.entries)
                {
                    var oe = otherField.entries.Find(x => x.name == e.name);
                    if (oe == null || !e.Equals(oe)) return false;
                }
            }
            return true;
        }

        public void ResetToDefaults()
        {
            foreach (var field in fields)
                foreach (var entry in field.entries)
                    entry.value = GVValueFactory.Create(entry.type);
            // Only values changed, keys unchanged — cache remains valid
        }
    }
    #endregion

    #region INDIVIDUALELEMENT
    #region SETTING VALUE

    [Serializable]
    public abstract class GVValue
    {
        public abstract object GetValue();
        public abstract void SetValue(object value);
        public abstract VALUE_TYPE GetValueType();
        public abstract GVValue Clone();
    }

    [Serializable]
    public class GVValue<T> : GVValue
    {
        public T value;

        public override object GetValue() => value;

        public override void SetValue(object val)
        {
            if (val is float f && typeof(T) == typeof(int))
                value = (T)(object)Mathf.RoundToInt(f);
            else
                value = (T)Convert.ChangeType(val, typeof(T));
        }

        public override VALUE_TYPE GetValueType()
            => GVValueFactory.GetEnum(typeof(T));

        public override GVValue Clone()
        {
            var clone = GVValueFactory.Create(GetValueType());
            clone.SetValue(value);
            return clone;
        }

        public override bool Equals(object obj)
            => obj is GVValue<T> other &&
               EqualityComparer<T>.Default.Equals(value, other.value);

        public override int GetHashCode() => base.GetHashCode();
    }

    [Serializable] public class BoolGVValue : GVValue<bool> { }
    [Serializable] public class IntGVValue : GVValue<int> { }
    [Serializable] public class FloatGVValue : GVValue<float> { }
    [Serializable] public class DoubleGVValue : GVValue<double> { }
    [Serializable] public class LongGVValue : GVValue<long> { }
    [Serializable] public class ShortGVValue : GVValue<short> { }
    [Serializable] public class ByteGVValue : GVValue<byte> { }
    [Serializable] public class CharGVValue : GVValue<char> { }
    [Serializable] public class StringGVValue : GVValue<string> { }
    [Serializable] public class Vector2GVValue : GVValue<Vector2> { }
    [Serializable] public class Vector3GVValue : GVValue<Vector3> { }

    #endregion

    #region FACTORY

    public static class GVValueFactory
    {
        public static GVValue Create(VALUE_TYPE t) => t switch
        {
            VALUE_TYPE.BOOL => new BoolGVValue(),
            VALUE_TYPE.INT => new IntGVValue(),
            VALUE_TYPE.FLOAT => new FloatGVValue(),
            VALUE_TYPE.DOUBLE => new DoubleGVValue(),
            VALUE_TYPE.LONG => new LongGVValue(),
            VALUE_TYPE.SHORT => new ShortGVValue(),
            VALUE_TYPE.BYTE => new ByteGVValue(),
            VALUE_TYPE.CHAR => new CharGVValue(),
            VALUE_TYPE.STRING => new StringGVValue(),
            VALUE_TYPE.VECTOR2 => new Vector2GVValue(),
            VALUE_TYPE.VECTOR3 => new Vector3GVValue(),
            VALUE_TYPE.CUSTOM => new StringGVValue(),
            _ => throw new NotSupportedException($"Unsupported type: {t}")
        };

        public static GVValue CreateFromString(VALUE_TYPE type, string value)
        {
            var sv = Create(type);
            sv.SetValue(Parse(type, value));
            return sv;
        }

        static object Parse(VALUE_TYPE type, string value) => type switch
        {
            VALUE_TYPE.BOOL => bool.Parse(value),
            VALUE_TYPE.INT => int.Parse(value),
            VALUE_TYPE.FLOAT => float.Parse(value, Inv),
            VALUE_TYPE.DOUBLE => double.Parse(value, Inv),
            VALUE_TYPE.LONG => long.Parse(value),
            VALUE_TYPE.SHORT => short.Parse(value),
            VALUE_TYPE.BYTE => byte.Parse(value),
            VALUE_TYPE.CHAR => char.Parse(value),
            VALUE_TYPE.STRING => value,
            VALUE_TYPE.VECTOR2 => ParseVector2(value),
            VALUE_TYPE.VECTOR3 => ParseVector3(value),
            VALUE_TYPE.CUSTOM => value,
            _ => throw new NotSupportedException($"Unsupported type: {type}")
        };

        static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        static Vector2 ParseVector2(string value)
        {
            value = value.Trim().Trim('(', ')');
            var p = value.Split(',');
            return new Vector2(
                float.Parse(p[0].Trim(), Inv),
                float.Parse(p[1].Trim(), Inv));
        }

        static Vector3 ParseVector3(string value)
        {
            value = value.Trim().Trim('(', ')');
            var p = value.Split(',');
            return new Vector3(
                float.Parse(p[0].Trim(), Inv),
                float.Parse(p[1].Trim(), Inv),
                float.Parse(p[2].Trim(), Inv));
        }

        public static VALUE_TYPE GetEnum(Type t)
        {
            if (t == typeof(bool)) return VALUE_TYPE.BOOL;
            if (t == typeof(int)) return VALUE_TYPE.INT;
            if (t == typeof(float)) return VALUE_TYPE.FLOAT;
            if (t == typeof(double)) return VALUE_TYPE.DOUBLE;
            if (t == typeof(long)) return VALUE_TYPE.LONG;
            if (t == typeof(short)) return VALUE_TYPE.SHORT;
            if (t == typeof(byte)) return VALUE_TYPE.BYTE;
            if (t == typeof(char)) return VALUE_TYPE.CHAR;
            if (t == typeof(string)) return VALUE_TYPE.STRING;
            if (t == typeof(Vector2)) return VALUE_TYPE.VECTOR2;
            if (t == typeof(Vector3)) return VALUE_TYPE.VECTOR3;
            throw new NotSupportedException($"Unsupported type: {t}");
        }
    }

    #endregion

    #region SETTING ENTRY

    [Serializable]
    public class GVEntry
    {
        public string name = "MyVariable";
        public VALUE_TYPE type;
        public string customTypeName = "";

        [SerializeReference]
        public GVValue value;

        public GVEntry Clone() => new()
        {
            name = name,
            type = type,
            customTypeName = customTypeName,
            value = value?.Clone()
        };

        public void ConvertStringToValue(string valueString)
            => value = GVValueFactory.CreateFromString(type, valueString);

        public override bool Equals(object obj)
            => obj is GVEntry other &&
               name == other.name &&
               type == other.type &&
               Equals(value, other.value);

        public override int GetHashCode() => base.GetHashCode();
    }

    #endregion

    #region SETTING FIELD

    [Serializable]
    public class GVField
    {
        public string fieldName;
        public List<GVEntry> entries = new();

        public GVField Clone()
        {
            var f = new GVField { fieldName = fieldName };
            foreach (var e in entries)
                f.entries.Add(e?.Clone());
            return f;
        }
    }

    #endregion
    #endregion
    #region GVVersion
    /// <summary>
    /// Semantic version for a GroupValues asset.
    /// Format: major.minor.patch-label  (e.g. 1.2.3-beta)
    ///
    /// Bump rules (applied as suggestions in the editor):
    ///   patch  — entries added/removed within existing fields
    ///   minor  — fields added/removed (resets patch)
    ///   major  — manual only (full rework)
    ///   label  — free string ("alpha", "beta", "rc1", "")
    /// </summary>
    [Serializable]
    public class GVVersion : IEquatable<GVVersion>
    {
        public int major, minor, patch;

        public string label = "";

        public GVVersion() { }

        public GVVersion(int major, int minor, int patch, string label = "")
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
            this.label = label ?? "";
        }
        public GVVersion Clone()
        {
            GVVersion clone = new GVVersion(major, minor, patch, (string)label.Clone());
            return clone;
        }
        // ── Equality ──────────────────────────────────────────────────────
        public bool Equals(GVVersion other)
        {
            if (other == null) return false;
            return major == other.major &&
                   minor == other.minor &&
                   patch == other.patch &&
                   string.Equals(label, other.label,
                                  StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as GVVersion);

        public override int GetHashCode() =>
            HashCode.Combine(major, minor, patch,
                             label?.ToLowerInvariant() ?? "");

        public static bool operator ==(GVVersion a, GVVersion b) =>
            a is null ? b is null : a.Equals(b);
        public static bool operator !=(GVVersion a, GVVersion b) => !(a == b);

        // ── Comparison (for rollback detection) ──────────────────────────
        /// Returns true if this version is newer than other.
        public bool IsNewerThan(GVVersion other)
        {
            if (other == null) return true;
            if (major != other.major) return major > other.major;
            if (minor != other.minor) return minor > other.minor;
            return patch > other.patch;
            // label is not compared for ordering, only for equality
        }

        // ── Bump helpers ──────────────────────────────────────────────────
        public GVVersion BumpPatch() => new(major, minor, patch + 1, label);
        public GVVersion BumpMinor() => new(major, minor + 1, 0, label);
        public GVVersion BumpMajor() => new(major + 1, 0, 0, label);
        public GVVersion WithLabel(string l) => new(major, minor, patch, l);

        public override string ToString() =>
            string.IsNullOrEmpty(label)
                ? $"{major}.{minor}.{patch}"
                : $"{major}.{minor}.{patch}-{label}";

        // ── Diff helpers (used by editor to suggest bump) ─────────────────
        /// <summary>
        /// Compares two GroupValues and returns the suggested bumped version.
        /// Returns null if no structural change is detected.
        /// </summary>
        public static GVVersion SuggestBump(GroupValues original, GroupValues modified,
                                             GVVersion current)
        {
            if (current == null) current = new GVVersion(1, 0, 0);

            int origFields = original.fields.Count;
            int modFields = modified.fields.Count;
            int origEntries = CountAllEntries(original);
            int modEntries = CountAllEntries(modified);

            // Field count changed → minor bump
            if (origFields != modFields)
                return current.BumpMinor();

            // Entry count changed within existing fields → patch bump
            if (origEntries != modEntries)
                return current.BumpPatch();

            // Type change in any entry → patch bump
            if (HasTypeChange(original, modified))
                return current.BumpPatch();

            return null; // no structural change
        }

        static int CountAllEntries(GroupValues gv)
        {
            int count = 0;
            foreach (var f in gv.fields) count += f.entries.Count;
            return count;
        }

        static bool HasTypeChange(GroupValues a, GroupValues b)
        {
            for (int fi = 0; fi < Mathf.Min(a.fields.Count, b.fields.Count); fi++)
            {
                var fa = a.fields[fi];
                var fb = b.fields[fi];
                for (int ei = 0; ei < Mathf.Min(fa.entries.Count, fb.entries.Count); ei++)
                {
                    if (fa.entries[ei].name == fb.entries[ei].name &&
                        fa.entries[ei].type != fb.entries[ei].type)
                        return true;
                }
            }
            return false;
        }
    }
}
    #endregion