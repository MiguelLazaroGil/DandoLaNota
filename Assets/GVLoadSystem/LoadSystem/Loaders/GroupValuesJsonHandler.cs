using System;
using System.Collections.Generic;
using System.IO;
using GVLoadSystem.Core;
using UnityEngine;
using static GVLoadSystem.Core.ALoader;

namespace GVLoadSystem
{
    /// <summary>
    /// Single responsibility: convert GroupValues ↔ JSON string and handle file I/O.
    /// ALoader and JsonEncrypter both depend on this — neither knows how JSON is structured.
    /// </summary>
    public static class GroupValuesJsonHandler
    {
        // ── Serialize ─────────────────────────────────────────────────────

        /// <summary>Converts GroupValues to a JSON string.</summary>
        public static string Serialize(GroupValues values)
        {
            //Debug.Log($"[Serialize] SO version: {values.version}");
            var sgs = new SerializableGroupValues();
            sgs.CopyFrom(values);
            //Debug.Log($"[Serialize] version before ToJson: {sgs.version}");
            return ToJson(sgs);
        }

        /// <summary>Converts a pre-built SerializableGroupValues to a JSON string.</summary>
        public static string Serialize(SerializableGroupValues sgs)
            => ToJson(sgs);

        // ── Deserialize ───────────────────────────────────────────────────

        /// <summary>
        /// Applies a JSON string to an existing GroupValues instance.
        /// The values object must already exist and have its fields populated
        /// so Unity can resolve [SerializeReference] polymorphic types.
        /// </summary>
        public static void Deserialize(string json, GroupValues target)
        {
            // Pre-seed so Unity has typed GVValue instances to overwrite.

            var sgs = new SerializableGroupValues();
            //Debug.Log($"[Serialize] version from so: {target.version}");
            sgs.CopyFrom(target);


#if UNITY_EDITOR
            // EditorJsonUtility resolves the references/RefIds block that Unity
            // writes for [SerializeReference] fields. JsonUtility alone cannot.
            UnityEditor.EditorJsonUtility.FromJsonOverwrite(json, sgs);
#else
        // In builds Unity writes a simpler format without the rid block,
        // which JsonUtility handles correctly when instances are pre-seeded.
        JsonUtility.FromJsonOverwrite(json, sgs);
#endif
            //Debug.Log($"[Serialize] version from ToJson: {sgs.version}");
            if (sgs.version != target.version)
            {
                // Debug.Log($"[Serialize]Time to migrate, json version is{sgs.version} and so version is {target.version}");
                // Debug.Log($"[Serialize]json has {sgs.fields.Count} fields and so has {target.fields.Count} fields");
                if (sgs.fields == null) sgs.fields = new List<GVField>();

                // Build lookup of existing template fields and entries
                var existingFields = new Dictionary<string, GVField>();
                foreach (var f in sgs.fields)
                    existingFields[f.fieldName] = f;

                var newFields = new List<GVField>();

                foreach (var gvField in target.fields)
                {
                    var existingField = existingFields.TryGetValue(gvField.fieldName, out var ef) ? ef : null;
                    var mergedEntries = new List<GVEntry>();

                    // Build lookup of existing entries in this field
                    var existingEntries = new Dictionary<string, GVEntry>();
                    if (existingField != null)
                        foreach (var e in existingField.entries)
                            existingEntries[e.name] = e;

                    foreach (var gvEntry in gvField.entries)
                    {
                        if (existingEntries.TryGetValue(gvEntry.name, out var existing) &&
                            existing.type == gvEntry.type)
                        {
                            // Entry exists with same type — keep existing value
                            mergedEntries.Add(existing.Clone());
                        }
                        else
                        {
                            // New entry or type changed — use GV value as default
                            mergedEntries.Add(gvEntry.Clone());
                        }
                    }

                    newFields.Add(new GVField { fieldName = gvField.fieldName, entries = mergedEntries });
                }

                sgs.fields = newFields;
            }
            sgs.ApplyTo(target);
            // Debug.Log($"[Serialize]json now has {sgs.fields.Count} fields and so now has {target.fields.Count} fields");
        }

        // ── File I/O ──────────────────────────────────────────────────────

        /// <summary>Serializes GroupValues and writes it to a file.</summary>
        public static void SaveToFile(string path, GroupValues values)
        {
            EnsureDirectory(path);
            EnsureWritable(path);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.MakeEditable(path);
#endif
            File.WriteAllText(path, Serialize(values));
            HideFile(path);
#if LOG_LOADSYSTEM
        Debug.Log($"[JsonHandler] Saved: {path}");
#endif
        }

        /// <summary>Serializes a pre-built wrapper and writes it to a file.</summary>
        public static void SaveToFile(string path, SerializableGroupValues sgs)
        {
            EnsureDirectory(path);
            EnsureWritable(path);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.MakeEditable(path);
#endif
            File.WriteAllText(path, Serialize(sgs));
            HideFile(path);
#if LOG_LOADSYSTEM
        Debug.Log($"[JsonHandler] Saved: {path}");
#endif
        }

        /// <summary>
        /// Reads a JSON file and applies it to the target GroupValues.
        /// Creates a default file if it doesn't exist yet.
        /// </summary>
        public static void LoadFromFile(string path, GroupValues target)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[JsonHandler] File not found, creating default: {path}");
                CreateDefault(path, target);
                return;
            }

            string json = File.ReadAllText(path);
            Deserialize(json, target);
#if LOG_LOADSYSTEM
        Debug.Log($"[JsonHandler] Loaded: {path}");
#endif
        }

        /// <summary>Creates a JSON file with the current default values of the target.</summary>
        public static void CreateDefault(string path, GroupValues values)
        {
            EnsureDirectory(path);

            if (File.Exists(path))
            {
#if LOG_LOADSYSTEM
            Debug.Log($"[JsonHandler] File already exists: {path}");
#endif
                return;
            }

            SaveToFile(path, values);  // HideFile is called inside SaveToFile
#if LOG_LOADSYSTEM
        Debug.Log($"[JsonHandler] Default created: {path}");
#endif
        }

        // ── Internal ──────────────────────────────────────────────────────

        static string ToJson(SerializableGroupValues sgs) => ToJsonObj(sgs);
        static string ToJson(SimpleEntriesWrapper w) => ToJsonObj(w);

        static string ToJsonObj(object obj)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorJsonUtility.ToJson(obj, true);
#else
        return JsonUtility.ToJson(obj, true);
#endif
        }

        // ── SimpleGroupValues support ────────────────────────────────────
        // Serializes a flat list of entries (no fields) to JSON.
        public static string SerializeEntries(System.Collections.Generic.List<GVEntry> entries)
        {
            var wrapper = new SimpleEntriesWrapper { entries = entries };
            return ToJson(wrapper);
        }

        // Deserializes a flat JSON into an existing entries list.
        public static void DeserializeEntries(string json,
            System.Collections.Generic.List<GVEntry> target)
        {
            var wrapper = new SimpleEntriesWrapper();
            // Pre-seed so [SerializeReference] can resolve types
            wrapper.entries = new System.Collections.Generic.List<GVEntry>(target);
#if UNITY_EDITOR
            UnityEditor.EditorJsonUtility.FromJsonOverwrite(json, wrapper);
#else
        JsonUtility.FromJsonOverwrite(json, wrapper);
#endif
            target.Clear();
            foreach (var e in wrapper.entries)
                target.Add(e);
        }

        [System.Serializable]
        class SimpleEntriesWrapper
        {
            public System.Collections.Generic.List<GVEntry> entries = new();
        }

        static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

#if UNITY_EDITOR_WIN
            // On Windows, dot-prefix files are not hidden at OS level.
            // Set the Hidden attribute so they don't appear in Explorer either.
            if (File.Exists(filePath))
                File.SetAttributes(filePath,
                    File.GetAttributes(filePath) | FileAttributes.Hidden);
#endif
        }

        /// <summary>
        /// Applies the Hidden file attribute after writing on Windows.
        /// Called after every write operation to keep the file hidden
        /// even when it is created for the first time.
        /// </summary>
        static void HideFile(string filePath)
        {
#if UNITY_EDITOR_WIN
            if (File.Exists(filePath))
                File.SetAttributes(filePath,
                    File.GetAttributes(filePath) | FileAttributes.Hidden);
#endif
        }

        /// <summary>
        /// Removes ReadOnly attribute if present — Unity can mark Assets/ files
        /// as readonly after a build. Called before any write operation.
        /// </summary>
        static void EnsureWritable(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                var attr = File.GetAttributes(filePath);
                // Remove both ReadOnly and Hidden before writing
                var newAttr = attr & ~FileAttributes.ReadOnly & ~FileAttributes.Hidden;
                if (attr != newAttr)
                    File.SetAttributes(filePath, newAttr);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JsonHandler] Could not set file attributes on '{filePath}': {ex.Message}");
            }
        }
    }
}