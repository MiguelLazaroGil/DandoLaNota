using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using GVLoadSystem.GVEditor;
using GVLoadSystem.Encryption;



#if UNITY_EDITOR
using UnityEditor;
namespace GVLoadSystem.Core
{
    [FilePath("ProjectSettings/SimpleGroupValues.asset",
              FilePathAttribute.Location.ProjectFolder)]
    public class SimpleGroupValues : ScriptableSingleton<SimpleGroupValues>
    {
        static SimpleGroupValues Inst => instance;
#else
namespace GVLoadSystem.Core
{
public class SimpleGroupValues : ScriptableObject
{
    static SimpleGroupValues _inst;
    public static SimpleGroupValues instance
    {
        get
        {
            if (_inst != null) return _inst;
            _inst = Resources.Load<SimpleGroupValues>("LoadSystem/SimpleGroupValues");
            if (_inst == null)
                Debug.LogWarning("[SimpleGroupValues] No asset found at " +
                    "Resources/LoadSystem/SimpleGroupValues.");
            return _inst;
        }
    }
    static SimpleGroupValues Inst => instance;
#endif

        // ── Data ──────────────────────────────────────────────────────────
        public List<GVEntry> entries = new();

        [NonSerialized] Dictionary<string, GVEntry> _cache;

        void RebuildCache()
        {
            _cache = new Dictionary<string, GVEntry>(StringComparer.Ordinal);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.name)) continue;
                if (!_cache.ContainsKey(e.name))
                    _cache[e.name] = e;
            }
        }

        void EnsureCache() { if (_cache == null) RebuildCache(); }

        // ── Static API ────────────────────────────────────────────────────

        /// <summary>Sets a value. Creates the key if it doesn't exist.</summary>
        public static void Set<T>(string key, T value)
        {
            var inst = Inst;
            if (inst == null) return;
            inst.EnsureCache();

            if (inst._cache.TryGetValue(key, out var entry))
            {
                WriteValue(entry, value);
            }
            else
            {
                var vt = InferType<T>();
                var newEntry = new GVEntry
                {
                    name = key,
                    type = vt,
                    value = GVValueFactory.Create(vt),
                };
                WriteValue(newEntry, value);
                inst.entries.Add(newEntry);
                inst._cache[key] = newEntry;
            }

#if UNITY_EDITOR
            SaveInternal();
#endif
        }

        /// <summary>Gets a value. Throws KeyNotFoundException if the key doesn't exist.</summary>
        public static T Get<T>(string key)
        {
            var inst = Inst;
            if (inst == null)
                throw new InvalidOperationException("[SimpleGroupValues] No instance available.");
            inst.EnsureCache();
            if (!inst._cache.TryGetValue(key, out var entry))
                throw new KeyNotFoundException(
                    $"[SimpleGroupValues] Key '{key}' not found. " +
                    $"Call Set(\"{key}\", value) first.");
            return CastValue<T>(entry.value.GetValue(), key);
        }

        /// <summary>Gets a value, returning defaultValue if the key doesn't exist.</summary>
        public static T GetOrDefault<T>(string key, T defaultValue = default)
        {
            var inst = Inst;
            if (inst == null) return defaultValue;
            inst.EnsureCache();
            if (!inst._cache.TryGetValue(key, out var entry)) return defaultValue;
            try { return CastValue<T>(entry.value.GetValue(), key); }
            catch { return defaultValue; }
        }

        /// <summary>Returns true if the key exists.</summary>
        public static bool HasKey(string key)
        {
            var inst = Inst;
            if (inst == null) return false;
            inst.EnsureCache();
            return inst._cache.ContainsKey(key);
        }

        /// <summary>Deletes a key. Does nothing if it doesn't exist.</summary>
        public static void Delete(string key)
        {
            var inst = Inst;
            if (inst == null) return;
            inst.EnsureCache();
            if (!inst._cache.TryGetValue(key, out var entry)) return;
            inst.entries.Remove(entry);
            inst._cache.Remove(key);
#if UNITY_EDITOR
            SaveInternal();
#endif
        }

        /// <summary>Deletes all keys.</summary>
        public static void DeleteAll()
        {
            var inst = Inst;
            if (inst == null) return;
            inst.entries.Clear();
            inst._cache?.Clear();
#if UNITY_EDITOR
            SaveInternal();
#endif
        }

        /// <summary>
        /// Saves to persistentDataPath with the encryption from ProjectSettings.
        /// </summary>
        public static void SaveInternal()
        {
#if UNITY_EDITOR
            instance.Save(true);
#endif
        }

        public static void SaveToFile()
        {
            var inst = Inst;
            if (inst == null) return;

            string json = GroupValuesJsonHandler.SerializeEntries(inst.entries);
            string path = JsonPath;

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

#if UNITY_EDITOR
            var settings = GroupValuesProjectSettings.instance;
            if (settings != null && settings.encryptionMethod != EncryptionMethod.None)
                JsonEncrypter.EncryptToFile(path, json, settings.passwordSalt,
                                            settings.encryptionMethod);
            else
#endif
                File.WriteAllText(path, json);

#if LOG_LOADSYSTEM
        Debug.Log($"[SimpleGroupValues] Saved to: {path}");
#endif
        }

        /// <summary>Loads from persistentDataPath.</summary>
        public static void LoadFromFile()
        {
            var inst = Inst;
            if (inst == null) return;

            if (!File.Exists(JsonPath))
            {
#if LOG_LOADSYSTEM
            Debug.Log("[SimpleGroupValues] No save file found — using defaults.");
#endif
                return;
            }

            try
            {
                string json;
#if UNITY_EDITOR
                var settings = GroupValuesProjectSettings.instance;
                if (settings != null && settings.encryptionMethod != EncryptionMethod.None)
                    json = JsonEncrypter.DecryptFromFile(JsonPath, settings.passwordSalt,
                                                         settings.encryptionMethod);
                else
#endif
                    json = File.ReadAllText(JsonPath);

                GroupValuesJsonHandler.DeserializeEntries(json, inst.entries);
                inst.RebuildCache();
#if LOG_LOADSYSTEM
            Debug.Log($"[SimpleGroupValues] Loaded from: {JsonPath}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleGroupValues] Failed to load: {ex.Message}");
            }
        }

        // ── Path ──────────────────────────────────────────────────────────
        static string JsonPath =>
            Path.Combine(Application.persistentDataPath, ".SimpleGroupValues.json");

        // ── Internal helpers ──────────────────────────────────────────────
        static void WriteValue<T>(GVEntry entry, T v)
        {
            if (entry.type == VALUE_TYPE.CUSTOM)
            { entry.value.SetValue(JsonUtility.ToJson(v)); return; }
            if (v is Vector2 or Vector3)
            { entry.value.SetValue(v); return; }
            object boxed = (typeof(T) == typeof(int) && v is float f)
                ? (object)Mathf.RoundToInt(f)
                : Convert.ChangeType(v, typeof(T));
            entry.value.SetValue(boxed);
        }

        static T CastValue<T>(object rawValue, string key)
        {
            if (rawValue is string json &&
                !typeof(T).IsPrimitive && typeof(T) != typeof(string))
                return JsonUtility.FromJson<T>(json);
            try
            {
                if (rawValue is T t) return t;
                return (T)Convert.ChangeType(rawValue, typeof(T));
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"[SimpleGroupValues] Cannot cast '{rawValue?.GetType().Name}' " +
                    $"to '{typeof(T).Name}' for key '{key}'.", ex);
            }
        }

        static VALUE_TYPE InferType<T>()
        {
            var t = typeof(T);
            if (t == typeof(bool)) return VALUE_TYPE.BOOL;
            if (t == typeof(float)) return VALUE_TYPE.FLOAT;
            if (t == typeof(double)) return VALUE_TYPE.DOUBLE;
            if (t == typeof(short)) return VALUE_TYPE.SHORT;
            if (t == typeof(int)) return VALUE_TYPE.INT;
            if (t == typeof(long)) return VALUE_TYPE.LONG;
            if (t == typeof(Vector2)) return VALUE_TYPE.VECTOR2;
            if (t == typeof(Vector3)) return VALUE_TYPE.VECTOR3;
            if (t == typeof(char)) return VALUE_TYPE.CHAR;
            if (t == typeof(string)) return VALUE_TYPE.STRING;
            if (t == typeof(byte)) return VALUE_TYPE.BYTE;
            return VALUE_TYPE.CUSTOM;
        }
    }
}