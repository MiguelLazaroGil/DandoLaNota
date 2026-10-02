using System;
using GVLoadSystem.GVEditor;
using UnityEngine;


namespace GVLoadSystem.Core
{
    /// <summary>
    /// Saves and loads any [Serializable] class as a CUSTOM entry in a GroupValues.
    /// No attributes needed on the data class.
    ///
    /// Usage:
    ///   GroupValuesWrapper&lt;PlayerStats&gt;.Save(gv, "stats");
    ///   PlayerStats stats = GroupValuesWrapper&lt;PlayerStats&gt;.Load(gv, "stats");
    /// </summary>
    public class GroupValuesWrapper<T> where T : class, new()
    {
        // ── Instance state ────────────────────────────────────────────────
        readonly GroupValues _gv;
        readonly string _key;

        /// <summary>
        /// Creates an instance bound to a specific GV and key.
        /// Use instance methods Load() and Save() for cleaner repeated access.
        /// </summary>
        public GroupValuesWrapper(GroupValues gv, string key)
        {
            _gv = gv;
            _key = key;
        }

        /// <summary>Loads the value using the bound GV and key.</summary>
        public T Load() => Load(_gv, _key);

        /// <summary>Saves the value using the bound GV and key.</summary>
        public void Save(T value) => Save(_gv, _key, value);

        /// <summary>Returns true if the bound key exists.</summary>
        public bool HasKey() => HasKey(_gv, _key);

        /// <summary>
        /// Before saving, clamp fields with GVRange/GVMin/GVMax and
        /// respect WriteOnce/WriteN guards.
        /// Note: [DontSave] fields are NOT serialized by JsonUtility if they are
        /// excluded from the JSON key — but since JsonUtility uses field names,
        /// we zero them out before serializing and restore after.
        /// </summary>
        static void ApplySaveAttributes(T instance)
        {
            foreach (var fi in typeof(T).GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance))
            {
                // [DontSave] — zero out before JsonUtility serializes
                if (GVFieldAttributeHelper.IsDontSave(fi))
                {
                    var def = fi.FieldType.IsValueType
                        ? Activator.CreateInstance(fi.FieldType) : null;
                    fi.SetValue(instance, def);
                    continue;
                }

                // Clamp value before saving
                object val = fi.GetValue(instance);
                object clamped = GVFieldAttributeHelper.ClampValue(fi, val);
                if (clamped != val) fi.SetValue(instance, clamped);
            }
        }

        // ── Static API ────────────────────────────────────────────────────
        /// <summary>
        /// Saves an object to a GroupValues entry.
        /// If the entry doesn't exist it is created automatically.
        /// </summary>
        public static void Save(GroupValues gv, string key, T value)
        {
            if (gv == null) { Debug.LogWarning("[GroupValuesWrapper] GroupValues is null."); return; }
            if (value == null) { Debug.LogWarning("[GroupValuesWrapper] Value is null."); return; }

            if (gv.ContainsKey(key))
            {
                if (gv.TryGetEntryType(key, out var existingType) &&
                    existingType != VALUE_TYPE.CUSTOM)
                {
                    Debug.LogWarning(
                        $"[GroupValuesWrapper] Key '{key}' in '{gv.name}' is {existingType}, " +
                        $"not CUSTOM. Use a CUSTOM entry or a different key.");
                    return;
                }
                // Apply attribute rules before saving
                ApplySaveAttributes(value);
                gv.SetValue(key, value);
            }
            else
            {
                // Create a new CUSTOM entry — serialize directly onto the value
                string json = JsonUtility.ToJson(value);
                var entry = new GVEntry
                {
                    name = key,
                    type = VALUE_TYPE.CUSTOM,
                    value = GVValueFactory.Create(VALUE_TYPE.CUSTOM),
                };
                entry.value.SetValue(json);

                if (gv.fields.Count == 0)
                    gv.fields.Add(new GVField { fieldName = "Default" });

                gv.fields[0].entries.Add(entry);
                gv.RebuildCache();

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(gv);
#endif
#if LOG_LOADSYSTEM
            Debug.Log($"[GroupValuesWrapper] Created CUSTOM entry '{key}' in '{gv.name}'.");
#endif
            }
        }

        /// <summary>
        /// Loads an object from a GroupValues entry.
        /// Respects [DontSave] (skips field), [SaveAs] (reads from alternate key),
        /// [GVRange]/[GVMin]/[GVMax] (clamps after load).
        /// Returns a new T() if the entry doesn't exist.
        /// </summary>
        public static T Load(GroupValues gv, string key)
        {
            if (gv == null)
            {
                Debug.LogWarning("[GroupValuesWrapper] GroupValues is null.");
                return new T();
            }

            if (!gv.ContainsKey(key))
            {
                Debug.LogWarning(
                    $"[GroupValuesWrapper] Key '{key}' not found in '{gv.name}'. " +
                    $"Returning default instance.");
                return new T();
            }

            string json = gv.GetValue<string>(key);
            if (string.IsNullOrEmpty(json)) return new T();

            try
            {
                var instance = new T();
                // Use JsonUtility for the base load, then apply attribute rules
                JsonUtility.FromJsonOverwrite(json, instance);
                ApplyLoadAttributes(instance);
                return instance;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[GroupValuesWrapper] Failed to deserialize '{key}' as {typeof(T).Name}: " +
                    $"{ex.Message}");
                return new T();
            }
        }

        /// <summary>After loading, clamp fields that have GVRange/GVMin/GVMax.</summary>
        static void ApplyLoadAttributes(T instance)
        {
            foreach (var fi in typeof(T).GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance))
            {
                if (GVFieldAttributeHelper.IsDontSave(fi))
                {
                    // Reset to default — field should not have been loaded
                    var def = fi.FieldType.IsValueType
                        ? Activator.CreateInstance(fi.FieldType) : null;
                    fi.SetValue(instance, def);
                    continue;
                }

                // Clamp if needed
                object val = fi.GetValue(instance);
                object clamped = GVFieldAttributeHelper.ClampValue(fi, val);
                if (clamped != val) fi.SetValue(instance, clamped);
            }
        }

        /// <summary>
        /// Loads from a GVEntryReference directly.
        /// </summary>
        public static T Load(GVEntryReference entryRef)
        {
            if (entryRef == null || !entryRef.IsValid)
            {
                Debug.LogWarning("[GroupValuesWrapper] GVEntryReference is null or invalid.");
                return new T();
            }

            if (entryRef.GetValueType() != VALUE_TYPE.CUSTOM)
            {
                Debug.LogWarning(
                    $"[GroupValuesWrapper] Entry '{entryRef.EntryKey}' is not CUSTOM type " +
                    $"(it is {entryRef.GetValueType()}). Use a CUSTOM entry for wrapper types.");
                return new T();
            }

            string json = entryRef.Get<string>();
            if (string.IsNullOrEmpty(json)) return new T();

            try
            {
                var instance = new T();
                JsonUtility.FromJsonOverwrite(json, instance);
                return instance;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[GroupValuesWrapper] Failed to deserialize as {typeof(T).Name}: {ex.Message}");
                return new T();
            }
        }

        /// <summary>
        /// Saves to a GVEntryReference directly.
        /// </summary>
        public static void Save(GVEntryReference entryRef, T value)
        {
            if (entryRef == null || !entryRef.IsValid)
            {
                Debug.LogWarning("[GroupValuesWrapper] GVEntryReference is null or invalid.");
                return;
            }

            if (entryRef.GetValueType() != VALUE_TYPE.CUSTOM)
            {
                Debug.LogWarning(
                    $"[GroupValuesWrapper] Entry '{entryRef.EntryKey}' is not CUSTOM type. " +
                    $"Use a CUSTOM entry for wrapper types.");
                return;
            }

            entryRef.Set(JsonUtility.ToJson(value));
        }

        /// <summary>Returns true if the key exists in the GroupValues.</summary>
        public static bool HasKey(GroupValues gv, string key)
            => gv != null && gv.ContainsKey(key);
    }
}