using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;
using GVLoadSystem.GVEditor;
using GVLoadSystem.Encryption;
using GVUtils.Attributes;





#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

namespace GVLoadSystem.Core
{

    [Serializable]
    public class ALoader
    {
#if UNITY_EDITOR
        protected string resourcePath;
#endif

        [Header("SO Path")]
        [SerializeField] protected string soPath = "Assets/Resources/LoadSystem/SavedFiles/";
        [SerializeField] protected string baseName = "Game";

        protected string soName => baseName + ".asset";
        protected string jsonFileName => "." + baseName + ".json";
        protected string backupFileName => "." + baseName + ".backup.json";

        [ReadOnly][SerializeField] EncryptionMethod encryptionMethod = EncryptionMethod.None;
        [ReadOnly][SerializeField] string password = "";
        [ReadOnly][SerializeField] bool useBackups = false;
        [ReadOnly][SerializeField] string saveSubfolder = "";

        [SerializeField]
        [ExposedScriptableObject]

        protected GroupValues values;

        // ── Settings ──────────────────────────────────────────────────────
        public void SetEncryptionSettings(EncryptionMethod eM, string passw)
        {
            encryptionMethod = eM;
            password = passw;
        }

        public void SetBackupSettings(bool enabled) => useBackups = enabled;
        public void SetSaveSubfolder(string subfolder) => saveSubfolder = subfolder;
        public void ChangeAssetName(string newName) => baseName = newName;
        public string GetCurrentName() => baseName;

        // ── JSON paths ────────────────────────────────────────────────────
        string GetJsonPath()
        {
#if UNITY_EDITOR
            return Path.Combine(soPath, jsonFileName);
#else
        return Path.Combine(Application.persistentDataPath, saveSubfolder, jsonFileName);
#endif
        }

        string GetBackupPath()
        {
#if UNITY_EDITOR
            return Path.Combine(soPath, backupFileName);
#else
        return Path.Combine(Application.persistentDataPath, saveSubfolder, backupFileName);
#endif
        }

        // ── Encryption helpers ────────────────────────────────────────────
        public string GetEffectivePassword()
        {
#if UNITY_EDITOR
            return password;
#else
        byte[] combined = DeviceKeyProvider.GetCombinedKey(password);
        return Convert.ToBase64String(combined);
#endif
        }

        // Un único WriteToPath que recibe combinedKey
        void WriteToPath(string path, string json, string combinedKey)
        {
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);

            if (encryptionMethod != EncryptionMethod.None)
                JsonEncrypter.EncryptToFile(path, json, encryptionMethod, combinedKey);
            else
                File.WriteAllText(path, json);
        }

        // Un único ReadFromPath que recibe combinedKey
        string ReadFromPath(string path, string combinedKey)
        {
            if (encryptionMethod != EncryptionMethod.None)
                return JsonEncrypter.DecryptFromFile(path, encryptionMethod, combinedKey);
            return File.ReadAllText(path);
        }
        #region LOAD
        // ─────────────────────────────────────────────────────────────────
        public GroupValues LoadValues()
        {
            if (values == null)
            {

#if UNITY_EDITOR
                string soFullPath = Path.Combine(soPath, soName);
                if (!File.Exists(soFullPath))
                    Debug.LogWarning("File does not exist in: " + soFullPath);
                values = AssetDatabase.LoadAssetAtPath<GroupValues>(soFullPath);
                if (values == null)
                    Debug.LogError("No values found in: " + soFullPath);
#else
            string resourceName = Path.GetFileNameWithoutExtension(soName);
            values = Resources.Load<GroupValues>(
                         Path.Combine(GetPathFromResources(soPath), resourceName));
            if (values == null)
            {
                Debug.LogError("[Loader] Could not load SO base in: " + resourceName);
                return null;
            }
#endif
            }
            //        Debug.Log($"[ALoader] LoadValues START baseName='{baseName}' instance={GetHashCode()}");
            bool acquired = GetLock().Wait(0);
            if (!acquired)
            {
                Debug.LogWarning($"[ALoader] '{baseName}' LoadValues waiting — file access busy.");
                GetLock().Wait();
            }

            try
            {
                string jsonPath = GetJsonPath();
                string backupPath = GetBackupPath();
                bool mainExists = File.Exists(jsonPath);
                bool backupExists = useBackups && File.Exists(backupPath);
                string combinedKey = encryptionMethod != EncryptionMethod.None
                    ? DeriveCombinedKeyString() : null;

                if (!mainExists && !backupExists)
                {

                    // First run — create both files from SO defaults
                    string json = GroupValuesJsonHandler.Serialize(values);
                    WriteToPath(jsonPath, json, combinedKey);
                    if (useBackups) WriteToPath(backupPath, json, combinedKey);
                    GroupValuesJsonHandler.Deserialize(json, values);
                }
                else
                {
                    bool mainLoaded = false;

                    if (mainExists)
                    {
                        try
                        {
                            string json = ReadFromPath(jsonPath, combinedKey);
                            GroupValuesJsonHandler.Deserialize(json, values);
                            mainLoaded = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[ALoader] Main file corrupted for '{baseName}': {ex.Message}. Trying backup.");
                        }
                    }

                    if (!mainLoaded)
                    {
                        if (backupExists)
                        {
                            try
                            {
                                string json = ReadFromPath(backupPath, combinedKey);
                                GroupValuesJsonHandler.Deserialize(json, values);
                                // Restore main from backup
                                WriteToPath(jsonPath, json, combinedKey);
                                Debug.LogWarning($"[ALoader] '{baseName}' restored from backup.");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[ALoader] Backup also corrupted for '{baseName}': {ex.Message}. Using SO defaults.");
                                string json = GroupValuesJsonHandler.Serialize(values);
                                WriteToPath(jsonPath, json, combinedKey);
                                if (useBackups) WriteToPath(backupPath, json, combinedKey);
                                GroupValuesJsonHandler.Deserialize(json, values);
                            }
                        }
                        else
                        {
                            // No valid file — create from defaults
                            string json = GroupValuesJsonHandler.Serialize(values);
                            WriteToPath(jsonPath, json, combinedKey);
                            if (useBackups) WriteToPath(backupPath, json, combinedKey);
                            GroupValuesJsonHandler.Deserialize(json, values);
                        }
                    }

                }
#if UNITY_EDITOR
                EditorUtility.SetDirty(values);
#endif
                return values.Clone();
            }
            finally { GetLock().Release(); }



        }

        public void SetGroupValues(GroupValues gv) => values = gv.Clone();
        public void RemoveLoadedValues() => values = null;

        public static string GetPathFromResources(string fullPath)
        {
            string keyword = "Resources/";
            int index = fullPath.IndexOf(keyword);
            if (index >= 0)
                return fullPath.Substring(index + keyword.Length);
            Debug.LogWarning("Path doesn't contain 'Resources/'");
            return fullPath;
        }
        #endregion

        #region SAVE
        // ─────────────────────────────────────────────────────────────────
        [ContextMenu("Save Data")]
        public void SaveValues()
        {

            bool acquired = GetLock().Wait(0);
            if (!acquired)
            {
                Debug.LogWarning($"[ALoader] '{baseName}' SaveValues waiting — file access busy.");
                GetLock().Wait();
            }

            try
            {
                string combinedKey = encryptionMethod != EncryptionMethod.None
                     ? DeriveCombinedKeyString() : null;
                string path = GetJsonPath();
                string json = GroupValuesJsonHandler.Serialize(values);
                if (useBackups) WriteToPath(GetBackupPath(), json, combinedKey);
                WriteToPath(path, json, combinedKey);
            }
            finally { GetLock().Release(); }
        }

        // Shortcut used by templates
        public void SaveValues(SerializableGroupValues sgs)
        {
            string path = GetJsonPath();
            string json = GroupValuesJsonHandler.Serialize(sgs);
            string combinedKey = encryptionMethod != EncryptionMethod.None
                     ? DeriveCombinedKeyString() : null;

            if (useBackups)
                WriteToPath(GetBackupPath(), json, combinedKey);

            WriteToPath(path, json, combinedKey);
        }

        public void SaveValues(GroupValues valuesToSave = null)
        {
            if (values == null && valuesToSave == null)
            {
                Debug.LogWarning("[Loader] No values to save.");
                return;
            }

            if (valuesToSave != null)
            {
                if (values != null && values.IsTheSame(valuesToSave))
                {
#if LOG_LOADSYSTEM
                Debug.Log("[Loader] Data is the same. No changes made.");
#endif
                    return;
                }
                values = valuesToSave.Clone();
            }

            SaveValues();
        }

        // public void ResetDefaultValues()
        // {
        //     values.ResetToDefaults();
        //     string json = GroupValuesJsonHandler.Serialize(values);
        //     string combinedKey = encryptionMethod != EncryptionMethod.None
        //              ? DeriveCombinedKeyString() : null;
        //     if (useBackups) WriteToPath(GetBackupPath(), json, combinedKey);
        //     WriteToPath(GetJsonPath(), json, combinedKey);
        // }

#if UNITY_EDITOR
        public void SaveValuesInPersistentData()
        {
            if (values == null) throw new BuildFailedException("[Loader] No values to save");
            string path = Path.Combine(Application.persistentDataPath, jsonFileName);
            if (File.Exists(path)) return;

            string json = GroupValuesJsonHandler.Serialize(values);
            string combinedKey = encryptionMethod != EncryptionMethod.None
                     ? DeriveCombinedKeyString() : null;
            WriteToPath(path, json, combinedKey);
        }

        public void SaveValuesInPersistentData(SerializableGroupValues sgs)
        {
            if (sgs == null) throw new BuildFailedException("[Loader] No values to save");
            string path = Path.Combine(Application.persistentDataPath, jsonFileName);
            if (File.Exists(path)) return;

            string json = GroupValuesJsonHandler.Serialize(sgs);
            string combinedKey = encryptionMethod != EncryptionMethod.None
                     ? DeriveCombinedKeyString() : null;
            WriteToPath(path, json, combinedKey);
        }
#endif
        #endregion

        #region GET / SET VALUES
        // ─────────────────────────────────────────────────────────────────
        internal void SetValue<T>(string key, T value)
        {
            if (values == null) return;
            values.SetValue(key, value);
#if UNITY_EDITOR
            EditorUtility.SetDirty(values);
#endif
        }

        internal T GetValue<T>(string key)
        {
            if (values == null) return default;
            return values.GetValue<T>(key);
        }

        [ContextMenu("Reset To Defaults")]
        public void ResetToDefaults()
        {
            if (values == null) return;
            values.ResetToDefaults();
        }
        #endregion

        #region EDITOR UTILS
#if UNITY_EDITOR
        public bool AutoResolveFromResources()
        {
            string[] guids = AssetDatabase.FindAssets($"t:GroupValues {baseName}");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                int resIndex = path.IndexOf("Resources/");
                if (resIndex < 0) continue;

                soPath = Path.GetDirectoryName(path).Replace("\\", "/") + "/";
                string insideResources = path.Substring(resIndex + "Resources/".Length);
                resourcePath = Path.ChangeExtension(insideResources, null);

#if LOG_LOADSYSTEM
            Debug.Log($"[ALoader] Auto-solved — SO: {soPath} | Resources: {resourcePath}");
#endif
                return true;
            }

            Debug.LogError($"[ALoader] Could not find '{baseName}' inside Resources.");
            return false;
        }
#endif
        #endregion

        #region ASYNC
        // ─────────────────────────────────────────────────────────────────
        bool isSaving;
        static readonly Dictionary<string, SemaphoreSlim> s_locks = new();

        static readonly object s_lockCreation = new object();

        SemaphoreSlim GetLock()
        {
            lock (s_lockCreation)
            {
                if (!s_locks.TryGetValue(baseName, out var sem))
                {
                    sem = new SemaphoreSlim(1, 1);
                    s_locks[baseName] = sem;
                }
                return sem;
            }
        }

        public async Task SaveValuesAsync(GroupValues valuesToSave = null)
        {
            if (isSaving) return;
            isSaving = true;
            //Debug.Log($"[ALoader] SaveValuesAsync START baseName='{baseName}' instance={GetHashCode()}");
            bool acquired = GetLock().Wait(0);
            if (!acquired)
            {
                Debug.LogWarning($"[ALoader] '{baseName}' SaveValuesAsync waiting — file access busy.");
                await GetLock().WaitAsync();
            }

            try
            {

                if (values == null && valuesToSave == null) return;
                var source = valuesToSave ?? values;

                string json = GroupValuesJsonHandler.Serialize(source.Clone());
                string path = GetJsonPath();
                string combinedKey = encryptionMethod != EncryptionMethod.None
                         ? DeriveCombinedKeyString() : null;

                if (encryptionMethod != EncryptionMethod.None)
                {
                    if (useBackups)
                    {
                        string bp = GetBackupPath();
                        await Task.Run(() =>
                            JsonEncrypter.EncryptToFile(bp, json, encryptionMethod, combinedKey));
                    }
                    await Task.Run(() =>
                            JsonEncrypter.EncryptToFile(path, json, encryptionMethod, combinedKey));
                }
                else
                {
                    if (useBackups)
                    {
                        string bp = GetBackupPath();
                        await Task.Run(() => File.WriteAllText(bp, json));
                    }
                    await Task.Run(() => File.WriteAllText(path, json));
                }
            }
            finally { isSaving = false; GetLock().Release(); }
        }

        public async Task<GroupValues> LoadValuesAsync()
        {
            if (values == null)
            {
                Debug.LogError("[ALoader] No base values assigned.");
                return null;
            }

            bool acquired = GetLock().Wait(0);
            if (!acquired)
            {
                Debug.LogWarning($"[ALoader] '{baseName}' LoadValuesAsync waiting — file access busy.");
                await GetLock().WaitAsync();
            }

            try
            {
                // Resolve everything that touches Unity APIs or device state
                // here on the main thread, before any Task.Run
                string jsonPath = GetJsonPath();
                string backupPath = GetBackupPath();
                string combinedKey = encryptionMethod != EncryptionMethod.None
                             ? DeriveCombinedKeyString() : null;
               // Debug.Log($"[Async key] '{combinedKey?.Substring(0, 8)}'");


                bool mainExists = File.Exists(jsonPath);
                bool backupExists = useBackups && File.Exists(backupPath);

                if (!mainExists && !backupExists)
                {
                    string json = GroupValuesJsonHandler.Serialize(values);
                    await Task.Run(() => WriteToPath(jsonPath, json, combinedKey));
                    if (useBackups) await Task.Run(() => WriteToPath(backupPath, json, combinedKey));
                    GroupValuesJsonHandler.Deserialize(json, values);
                    return values.Clone();
                }

                bool mainLoaded = false;
                string loadedJson = null;

                if (mainExists)
                {
                    try
                    {
                        if (encryptionMethod != EncryptionMethod.None)
                            loadedJson = await Task.Run(() =>
                               JsonEncrypter.DecryptFromFile(jsonPath, encryptionMethod, combinedKey));
                        else
                            loadedJson = await Task.Run(() => File.ReadAllText(jsonPath));

                        mainLoaded = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[ALoader] Main file corrupted for '{baseName}': {ex.Message}. Trying backup.");
                    }
                }

                if (!mainLoaded)
                {
                    if (backupExists)
                    {
                        try
                        {
                            if (encryptionMethod != EncryptionMethod.None)
                                loadedJson = await Task.Run(() =>
                                    JsonEncrypter.DecryptFromFile(backupPath, encryptionMethod, combinedKey));
                            else
                                loadedJson = await Task.Run(() => File.ReadAllText(backupPath));

                            await Task.Run(() => WriteToPath(jsonPath, loadedJson, combinedKey));
                            Debug.LogWarning($"[ALoader] '{baseName}' restored from backup.");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning(
                                $"[ALoader] Backup also corrupted for '{baseName}': {ex.Message}. Using SO defaults.");
                            string json = GroupValuesJsonHandler.Serialize(values);
                            await Task.Run(() => WriteToPath(jsonPath, json, combinedKey));
                            if (useBackups) await Task.Run(() => WriteToPath(backupPath, json, combinedKey));
                            GroupValuesJsonHandler.Deserialize(json, values);
                            return values.Clone();
                        }
                    }
                    else
                    {
                        string json = GroupValuesJsonHandler.Serialize(values);
                        await Task.Run(() => WriteToPath(jsonPath, json, combinedKey));
                        if (useBackups) await Task.Run(() => WriteToPath(backupPath, json, combinedKey));
                        GroupValuesJsonHandler.Deserialize(json, values);
                        return values.Clone();
                    }
                }
                GroupValuesJsonHandler.Deserialize(loadedJson, values);
                return values.Clone();
            }
            finally { GetLock().Release(); }


        }
        string DeriveCombinedKeyString()
        {
#if UNITY_EDITOR
            return password;
#else
    byte[] combined = DeviceKeyProvider.GetCombinedKey(password);
    return Convert.ToBase64String(combined);
#endif
        }
        #endregion
        // Kept for backward compat — GroupValuesWindow uses this directly
        public void CreateJsonFile(string fullPath)
            => GroupValuesJsonHandler.CreateDefault(fullPath, values);

        // ── Migration ─────────────────────────────────────────────────────
        public void MigrateIfNeeded()
        {
            if (values == null)
            {
                Debug.LogWarning("[ALoader] MigrateIfNeeded called but values is null.");
                return;
            }

            string path = GetJsonPath();
            string combinedKey = encryptionMethod != EncryptionMethod.None
                                 ? DeriveCombinedKeyString() : null;

            if (!System.IO.File.Exists(path))
            {
                bool acquired = GetLock().Wait(0);
                if (!acquired)
                {
                    Debug.LogWarning($"[ALoader] '{baseName}' MigrateIfNeeded waiting — file access busy.");
                    GetLock().Wait();
                }
                try
                {
                    string json = GroupValuesJsonHandler.Serialize(values);
                    WriteToPath(path, json, combinedKey);
                    if (useBackups) WriteToPath(GetBackupPath(), json, combinedKey);
                }
                finally { GetLock().Release(); }

#if LOG_LOADSYSTEM
        Debug.Log($"[ALoader] Created default JSON for '{values.name}'.");
#endif
                return;
            }

            string migrateJson;
            bool acquired2 = GetLock().Wait(0);
            if (!acquired2)
            {
                Debug.LogWarning($"[ALoader] '{baseName}' MigrateIfNeeded waiting — file access busy.");
                GetLock().Wait();
            }

            try
            {
                migrateJson = ReadFromPath(path, combinedKey);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[ALoader] Could not read JSON for '{values.name}': {ex.Message}" +
                    $"Skipping migration.");
                return;
            }
            finally { GetLock().Release(); }

            // Parsing y comparación de versiones — sin acceso a fichero, fuera del lock
            var savedData = new SerializableGroupValues();
            savedData.CopyFrom(values);

            try
            {
#if UNITY_EDITOR
                UnityEditor.EditorJsonUtility.FromJsonOverwrite(migrateJson, savedData);
#else
        UnityEngine.JsonUtility.FromJsonOverwrite(migrateJson, savedData);
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[ALoader] Could not parse JSON for '{values.name}': {ex.Message}. " +
                    $"Skipping migration.");
                return;
            }

            var soVersion = values.version ?? new GVVersion(1, 0, 0);
            var jsonVersion = savedData.version ?? new GVVersion(0, 0, 0);

#if LOG_LOADSYSTEM
    Debug.Log($"[Migration] SO version: {soVersion} | JSON version: {jsonVersion} | equal: {soVersion == jsonVersion}");
#endif

            if (soVersion == jsonVersion)
            {
#if LOG_LOADSYSTEM
        Debug.Log($"[ALoader] '{values.name}' is up to date (v{soVersion}). No migration needed.");
#endif
                return;
            }

#if LOG_LOADSYSTEM
    Debug.Log($"[ALoader] Migrating '{values.name}' from v{jsonVersion} to v{soVersion}.");
#endif

            var savedLookup = new Dictionary<string, GVEntry>(StringComparer.Ordinal);
            foreach (var field in savedData.fields)
                foreach (var entry in field.entries)
                    if (!string.IsNullOrEmpty(entry.name))
                        savedLookup[entry.name] = entry;

            foreach (var field in values.fields)
            {
                foreach (var entry in field.entries)
                {
                    if (!savedLookup.TryGetValue(entry.name, out var savedEntry)) continue;
                    if (savedEntry.type != entry.type) continue;
                    try { entry.value?.SetValue(savedEntry.value?.GetValue()); }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[ALoader] Could not restore value for '{entry.name}': {ex.Message}");
                    }
                }
            }

            values.RebuildCache();
            SaveValues(); // SaveValues ya adquiere el semáforo internamente
        }

        #region WRAPPER

        // ---------------------------------------------------------------------------------------
        // SERIALIZABLE WRAPPER
        // ---------------------------------------------------------------------------------------
        /// <summary>
        /// JSON-serializable wrapper for GroupValues.
        /// Lives inside ALoader.cs — extracted here for clarity.
        /// </summary>
        [Serializable]
        public class SerializableGroupValues
        {
            public List<GVField> fields = new();
            public GVVersion version = new GVVersion(1, 0, 0);

            public void CopyFrom(GroupValues settings)
            {
                fields.Clear();
                foreach (var f in settings.fields)
                    fields.Add(f.Clone());
                version = settings.version != null
                    ? new GVVersion(settings.version.major, settings.version.minor,
                                    settings.version.patch, settings.version.label)
                    : new GVVersion(1, 0, 0);
            }

            public void CopyFrom(List<GVField> otherFields)
            {
                fields.Clear();
                foreach (var f in otherFields)
                    fields.Add(f.Clone());
                // version stays as-is when copying only fields
            }

            public void ApplyTo(GroupValues target)
            {
                target.fields.Clear();
                foreach (var f in fields)
                    target.fields.Add(f.Clone());
                // Note: version is NOT applied to target here —
                // the SO version is the source of truth
            }
        }

        #region XML_SERIALIZATION
        public class SerializableGroupValuesXML
        {
            public List<SerializableField> fields = new();
            public SerializableGroupValuesXML() { }
            public SerializableGroupValuesXML(List<SerializableField> fs)
            {
                fields = fs;
            }


            public void CopyFrom(GroupValues settings)
            {
                fields.Clear();

                var inv = System.Globalization.CultureInfo.InvariantCulture;

                foreach (var f in settings.fields)
                {
                    var entries = new System.Collections.Generic.List<SerializableEntry>();

                    foreach (var e in f.Clone().entries)
                    {
                        string valueStr = "";
                        if (e.value != null)
                        {
                            object raw = e.value.GetValue();

                            if (raw is UnityEngine.Vector2 v2)
                                valueStr = string.Format(inv, "{0},{1}", v2.x, v2.y);
                            else if (raw is UnityEngine.Vector3 v3)
                                valueStr = string.Format(inv, "{0},{1},{2}", v3.x, v3.y, v3.z);
                            else
                                valueStr = string.Format(inv, "{0}", raw);
                        }

                        entries.Add(new SerializableEntry(
                            e.name,
                            e.type,
                            valueStr,
                            e.type == VALUE_TYPE.CUSTOM ? (e.customTypeName ?? "") : ""
                        ));
                    }

                    fields.Add(new SerializableField(f.fieldName, entries));
                }
            }

            public void CopyFrom(List<GVField> otherFields)
            {
                fields.Clear();

                foreach (var f in otherFields)
                {
                    GVField fieldClone = f.Clone();
                    List<SerializableEntry> entriesField = new();

                    foreach (var e in fieldClone.entries)
                    {
                        GVEntry entryClone = e.Clone();
                        SerializableEntry entry = new SerializableEntry(entryClone.name, entryClone.type, entryClone.value.ToString());
                        entriesField.Add(entry);

                    }
                    SerializableField fieldToAdd = new SerializableField(fieldClone.fieldName, entriesField);

                    fields.Add(fieldToAdd);
                }
            }
            public SerializableGroupValuesXML Clone()
            {
                var flds = new List<SerializableField>();
                foreach (var field in fields)
                {
                    flds.Add(field.Clone());
                }
                return new SerializableGroupValuesXML(flds);
            }

            public void ApplyTo(GroupValues target)
            {
                target.fields.Clear();

                foreach (var f in fields)
                {
                    var fieldEntries = new List<GVEntry>();

                    foreach (var e in f.entries)
                    {
                        var se = new GVEntry
                        {
                            name = e.key,
                            type = e.type,
                            customTypeName = e.customTypeName ?? "",
                        };
                        se.ConvertStringToValue(e.value);
                        fieldEntries.Add(se);
                    }

                    target.fields.Add(new GVField
                    {
                        fieldName = f.name,
                        entries = fieldEntries,
                    });
                }
            }
        }
        [Serializable]
        public class SerializableField
        {
            public string name;
            public List<SerializableEntry> entries = new();
            public SerializableField() { }
            public SerializableField(string n, List<SerializableEntry> ents)
            {
                name = n;
                entries = ents;
            }
            public SerializableField Clone()
            {
                var ents = new List<SerializableEntry>();
                foreach (var value in entries)
                {
                    ents.Add(value.Clone());
                }
                return new SerializableField(name, ents);
            }
        }

        [Serializable]
        public class SerializableEntry
        {
            public string key;
            public VALUE_TYPE type;
            public string value;
            public string customTypeName; // name of the [CustomSettingData] class, only used when type == CUSTOM

            public SerializableEntry() { }

            public SerializableEntry(string k, VALUE_TYPE t, string v, string customType = "")
            {
                key = k;
                type = t;
                value = v;
                customTypeName = customType;
            }

            public SerializableEntry Clone()
                => new SerializableEntry(key, type, (string)value.Clone(), customTypeName);
        }
        #endregion
        #endregion

        #region EDITOR
#if UNITY_EDITOR

        [CustomPropertyDrawer(typeof(ALoader))]
        public class ALoaderDrawer : PropertyDrawer
        {
            bool _wasNameFieldFocused;

            static readonly Color WarningBg = new Color(0.55f, 0.35f, 0.05f, 0.35f);
            static readonly Color WarningBdr = new Color(0.95f, 0.65f, 0.10f, 1.00f);

            // ── Height ────────────────────────────────────────────────────────
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                float line = EditorGUIUtility.singleLineHeight;
                float gap = EditorGUIUtility.standardVerticalSpacing;

                float h = 0f;

                // ── Load System Settings ───────────────────────────────────────
                h += line + gap;   // header
                h += line + gap;   // saveSubfolder
                h += line + gap;   // useBackups
                h += line + gap;   // encryptionMethod
                h += line + gap;   // password

                // ── Loader Settings ────────────────────────────────────────────
                h += line + gap;   // header
                h += line + gap;   // Name

                if (IsPathUnresolved(property))
                    h += 30f + gap; // Warning

                // values
                var valuesProp = property.FindPropertyRelative("values");
                h += EditorGUI.GetPropertyHeight(valuesProp, true) + gap;

                return h;
            }

            // ── Draw ──────────────────────────────────────────────────────────
            public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
            {
                float line = EditorGUIUtility.singleLineHeight;
                float gap = EditorGUIUtility.standardVerticalSpacing;
                float y = pos.y;

                EditorGUI.BeginProperty(pos, label, property);

                var baseNameProp = property.FindPropertyRelative("baseName");
                var encryptionProp = property.FindPropertyRelative("encryptionMethod");
                var passwordProp = property.FindPropertyRelative("password");
                var useBackupsProp = property.FindPropertyRelative("useBackups");
                var saveSubfolderProp = property.FindPropertyRelative("saveSubfolder");
                var valuesProp = property.FindPropertyRelative("values");

                var t = GVThemeManager.Current;

                // ══ Load System Settings ══════════════════════════════════════
                DrawSectionHeader(new Rect(pos.x, y, pos.width, line),
                                  "Load System Settings", t, pos);
                y += line + gap;

                EditorGUI.BeginDisabledGroup(true);

                EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, line),
                                        saveSubfolderProp, new GUIContent("Save Subfolder"));
                y += line + gap;

                EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, line),
                                        useBackupsProp, new GUIContent("Use Backups"));
                y += line + gap;

                EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, line),
                                        encryptionProp, new GUIContent("Encryption Method"));
                y += line + gap;

                EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, line),
                                        passwordProp, new GUIContent("Password Salt"));
                y += line + gap;

                EditorGUI.EndDisabledGroup();

                // ══ Loader Settings ═══════════════════════════════════════════
                DrawSectionHeader(new Rect(pos.x, y, pos.width, line),
                                  "Loader Settings", t, pos);
                y += line + gap;

                // ── Name ──────────────────────────────────────────────────────
                string controlName = "ALoaderBaseName_" + property.propertyPath;
                GUI.SetNextControlName(controlName);
                EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, line),
                                        baseNameProp, new GUIContent("Name"));
                y += line + gap;

                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
                bool pressedEnter = isFocused &&
                                    Event.current.type == EventType.KeyDown &&
                                    (Event.current.keyCode == KeyCode.Return ||
                                     Event.current.keyCode == KeyCode.KeypadEnter);
                bool lostFocus = _wasNameFieldFocused && !isFocused &&
                                    Event.current.type == EventType.Repaint;

                if (pressedEnter || lostFocus)
                {
                    property.serializedObject.ApplyModifiedProperties();
                    TryAutoResolve(property);
                    if (pressedEnter) Event.current.Use();
                }
                _wasNameFieldFocused = isFocused;

                // ── Warning ───────────────────────────────────────────────────
                if (IsPathUnresolved(property))
                {
                    DrawWarningBox(new Rect(pos.x, y, pos.width, 28f),
                        "Asset not found. Name must match a GroupValues asset inside a Resources folder.");
                    y += 30f + gap;
                }

                // ── GroupValues ───────────────────────────────────────────────
                EditorGUI.BeginChangeCheck();
                float valH = EditorGUI.GetPropertyHeight(valuesProp, true);
                EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, valH),
                                        valuesProp, new GUIContent("Group Values"), true);

                if (EditorGUI.EndChangeCheck() && valuesProp?.objectReferenceValue != null)
                {
                    var gv = valuesProp.objectReferenceValue as GroupValues;
                    if (gv != null && baseNameProp != null &&
                        string.IsNullOrEmpty(baseNameProp.stringValue))
                    {
                        baseNameProp.stringValue = gv.name;
                        property.serializedObject.ApplyModifiedProperties();
                        TryAutoResolve(property);
                    }
                    else if (gv != null && baseNameProp != null &&
                             baseNameProp.stringValue != gv.name)
                    {
                        baseNameProp.stringValue = gv.name;
                        property.serializedObject.ApplyModifiedProperties();
                        TryAutoResolve(property);
                    }
                }

                EditorGUI.EndProperty();
            }

            // ── Section header ────────────────────────────────────────────────
            static void DrawSectionHeader(Rect r, string title, GVTheme t, Rect fullPos)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(r, t.backgroundPanel);
                    EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), t.accent);
                    EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), t.separator);
                }

                bool isSystemHeader = title == "Load System Settings";
                float btnW = isSystemHeader ? 22f : 0f;

                var headerStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 10,
                };
                headerStyle.normal.textColor = t.accent;

                GUI.Label(new Rect(r.x + 6, r.y, r.width - btnW - 8f, r.height),
                          title, headerStyle);

                if (isSystemHeader)
                {
                    var btnStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 9,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                    };
                    btnStyle.normal.textColor = t.textPrimary;

                    GUI.backgroundColor = t.buttonNeutral;
                    if (GUI.Button(
                            new Rect(fullPos.xMax - btnW - 2f, r.y + 1f, btnW, r.height - 2f),
                            "⚙", btnStyle))
                        SettingsService.OpenProjectSettings("Project/Load System");
                    GUI.backgroundColor = Color.white;
                }
            }

            // ── Helpers ───────────────────────────────────────────────────────
            static bool IsPathUnresolved(SerializedProperty property)
            {
                var soPathProp = property.FindPropertyRelative("soPath");
                var baseNameProp = property.FindPropertyRelative("baseName");
                if (soPathProp == null || baseNameProp == null) return false;

                string soPath = soPathProp.stringValue;
                string name = baseNameProp.stringValue;

                if (string.IsNullOrEmpty(soPath) || string.IsNullOrEmpty(name)) return true;

                string assetPath = System.IO.Path.Combine(soPath, name + ".asset");
                return !System.IO.File.Exists(assetPath);
            }

            static void DrawWarningBox(Rect rect, string message)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rect, WarningBg);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), WarningBdr);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), WarningBdr);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), WarningBdr);
                    EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), WarningBdr);
                }
                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                    fontStyle = FontStyle.Bold,
                };
                style.normal.textColor = new Color(1.0f, 0.80f, 0.30f);
                GUI.Label(new Rect(rect.x + 6, rect.y + 4, rect.width - 10, rect.height - 4),
                          "⚠  " + message, style);
            }

            static void TryAutoResolve(SerializedProperty property)
            {
                var target = property.serializedObject.targetObject;

                var field = target.GetType().GetField(property.propertyPath,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                object loaderInstance = field?.GetValue(target);
                if (loaderInstance == null) return;

                var method = loaderInstance.GetType().GetMethod("AutoResolveFromResources",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                method?.Invoke(loaderInstance, null);
                property.serializedObject.Update();
            }
        }
#endif
        #endregion
        public interface IAsyncLoader
        {
            Task LoadDataAsync();
        }
    }
}